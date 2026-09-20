using _3dsGallery.DataLayer.DataBase;
using System;
using System.Collections.Generic;
using System.Linq;

namespace _3dsGallery.WebUI.Code
{
    public class PictureAssetUploadRequest
    {
        public string OriginalFileName { get; set; }
        public string OriginalContentType { get; set; }
        public byte[] OriginalBytes { get; set; }
        public string PreviewFileName { get; set; }
        public byte[] PreviewBytes { get; set; }
        public string ThumbnailSmallFileName { get; set; }
        public byte[] ThumbnailSmallBytes { get; set; }
        public string ThumbnailMediumFileName { get; set; }
        public byte[] ThumbnailMediumBytes { get; set; }
        public string LegacyPath { get; set; }
        public string PictureType { get; set; }
    }

    public interface IPictureAssetStorageService
    {
        void UploadAssets(Picture picture, PictureAssetUploadRequest request);
        void DeleteAssets(Picture picture);
        byte[] DownloadOriginalBytes(Picture picture);
    }

    public class PictureAssetStorageService : IPictureAssetStorageService
    {
        private readonly IImageKitClient _imageKitClient;
        private readonly PictureAssetUrlResolver _urlResolver;
        private readonly string _uploadFolder;

        public PictureAssetStorageService(string siteRootPath)
            : this(TryCreateClient(), new PictureAssetUrlResolver(siteRootPath), TryLoadUploadFolder())
        {
        }

        public PictureAssetStorageService(IImageKitClient imageKitClient, PictureAssetUrlResolver urlResolver, string uploadFolder)
        {
            _imageKitClient = imageKitClient;
            _urlResolver = urlResolver;
            _uploadFolder = uploadFolder;
        }

        public void UploadAssets(Picture picture, PictureAssetUploadRequest request)
        {
            if (picture == null)
                throw new ArgumentNullException("picture");
            if (request == null)
                throw new ArgumentNullException("request");
            if (request.OriginalBytes == null || request.OriginalBytes.Length == 0)
                throw new InvalidOperationException("The original picture asset is missing.");
            if (_imageKitClient == null)
                throw new InvalidOperationException("ImageKit is not configured. Set ImageKitPrivateKey and ImageKitUrlEndpoint before uploading pictures.");

            var originalStorageProvider = picture.StorageProvider;
            var originalStorageMigrationStatus = picture.StorageMigrationStatus;
            var originalPath = picture.path;
            var originalType = picture.type;
            var uploadedFileIds = new List<string>();
            try
            {
                picture.StorageProvider = PictureStorageConstants.StorageProviderImageKit;
                picture.StorageMigrationStatus = PictureStorageConstants.MigrationStatusRemotePending;
                picture.path = request.LegacyPath;
                picture.type = request.PictureType;

                var originalUpload = UploadFile(request.OriginalBytes, request.OriginalFileName, GetOriginalFolder(), request.OriginalContentType);
                ApplyOriginal(picture, originalUpload);
                uploadedFileIds.Add(originalUpload.FileId);

                if (request.PreviewBytes != null && request.PreviewBytes.Length > 0)
                {
                    if (ShouldReuseOriginal(request, originalUpload))
                    {
                        picture.PreviewRemoteFileId = picture.OriginalRemoteFileId;
                        picture.PreviewRemotePath = picture.OriginalRemotePath;
                    }
                    else
                    {
                        var previewUpload = UploadFile(request.PreviewBytes, request.PreviewFileName, GetPreviewFolder(), "image/jpeg");
                        picture.PreviewRemoteFileId = previewUpload.FileId;
                        picture.PreviewRemotePath = previewUpload.FilePath;
                        uploadedFileIds.Add(previewUpload.FileId);
                    }
                }

                if (request.ThumbnailSmallBytes != null && request.ThumbnailSmallBytes.Length > 0)
                {
                    var thumbSmallUpload = UploadFile(request.ThumbnailSmallBytes, request.ThumbnailSmallFileName, GetThumbnailSmallFolder(), "image/jpeg");
                    picture.ThumbnailSmallRemoteFileId = thumbSmallUpload.FileId;
                    picture.ThumbnailSmallRemotePath = thumbSmallUpload.FilePath;
                    uploadedFileIds.Add(thumbSmallUpload.FileId);
                }
                else
                {
                    picture.ThumbnailSmallRemoteFileId = null;
                    picture.ThumbnailSmallRemotePath = null;
                }

                if (request.ThumbnailMediumBytes != null && request.ThumbnailMediumBytes.Length > 0)
                {
                    var thumbMediumUpload = UploadFile(request.ThumbnailMediumBytes, request.ThumbnailMediumFileName, GetThumbnailMediumFolder(), "image/jpeg");
                    picture.ThumbnailMediumRemoteFileId = thumbMediumUpload.FileId;
                    picture.ThumbnailMediumRemotePath = thumbMediumUpload.FilePath;
                    uploadedFileIds.Add(thumbMediumUpload.FileId);
                }
                else
                {
                    picture.ThumbnailMediumRemoteFileId = null;
                    picture.ThumbnailMediumRemotePath = null;
                }

                picture.StorageMigrationStatus = PictureStorageConstants.MigrationStatusRemoteActive;
            }
            catch
            {
                TryDeleteUploadedFiles(uploadedFileIds);
                picture.StorageProvider = originalStorageProvider;
                picture.StorageMigrationStatus = originalStorageMigrationStatus;
                picture.path = originalPath;
                picture.type = originalType;
                picture.OriginalRemoteFileId = null;
                picture.OriginalRemotePath = null;
                picture.PreviewRemoteFileId = null;
                picture.PreviewRemotePath = null;
                picture.ThumbnailSmallRemoteFileId = null;
                picture.ThumbnailSmallRemotePath = null;
                picture.ThumbnailMediumRemoteFileId = null;
                picture.ThumbnailMediumRemotePath = null;
                throw;
            }
        }

        public void DeleteAssets(Picture picture)
        {
            if (picture == null)
                throw new ArgumentNullException("picture");
            if (!_urlResolver.IsRemoteActive(picture) && picture.StorageMigrationStatus != PictureStorageConstants.MigrationStatusDeletePending)
                return;
            if (_imageKitClient == null)
                throw new InvalidOperationException("ImageKit is not configured. Set ImageKitPrivateKey and ImageKitUrlEndpoint before deleting remote picture assets.");

            var fileIds = new[]
            {
                picture.OriginalRemoteFileId,
                picture.PreviewRemoteFileId,
                picture.ThumbnailSmallRemoteFileId,
                picture.ThumbnailMediumRemoteFileId
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

            var failures = new List<string>();
            foreach (var fileId in fileIds)
            {
                try
                {
                    var result = _imageKitClient.Delete(fileId);
                    if (!result.Deleted && !result.NotFound)
                        failures.Add(fileId);
                }
                catch
                {
                    failures.Add(fileId);
                }
            }

            if (failures.Any())
            {
                picture.StorageMigrationStatus = PictureStorageConstants.MigrationStatusDeletePending;
                throw new InvalidOperationException("ImageKit deletion failed for one or more picture assets.");
            }
        }

        public byte[] DownloadOriginalBytes(Picture picture)
        {
            if (picture == null)
                throw new ArgumentNullException("picture");

            if (_urlResolver.IsRemoteActive(picture))
                return _imageKitClient.Download(_urlResolver.GetRemoteOriginalUrl(picture));

            var localPath = _urlResolver.GetLocalOriginalPhysicalPath(picture);
            if (string.IsNullOrWhiteSpace(localPath) || !System.IO.File.Exists(localPath))
                throw new InvalidOperationException("Original picture file was not found.");

            return System.IO.File.ReadAllBytes(localPath);
        }

        private void ApplyOriginal(Picture picture, ImageKitStoredFile originalUpload)
        {
            picture.OriginalRemoteFileId = originalUpload.FileId;
            picture.OriginalRemotePath = originalUpload.FilePath;
        }

        private bool ShouldReuseOriginal(PictureAssetUploadRequest request, ImageKitStoredFile originalUpload)
        {
            return string.Equals(request.PictureType, "2D", StringComparison.OrdinalIgnoreCase)
                && request.PreviewBytes != null
                && request.OriginalBytes.SequenceEqual(request.PreviewBytes)
                && !string.IsNullOrWhiteSpace(originalUpload.FileId);
        }

        private ImageKitStoredFile UploadFile(byte[] bytes, string fileName, string folderPath, string contentType)
        {
            return _imageKitClient.Upload(bytes, fileName, folderPath, contentType);
        }

        private void TryDeleteUploadedFiles(IEnumerable<string> uploadedFileIds)
        {
            foreach (var fileId in uploadedFileIds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
            {
                try
                {
                    _imageKitClient.Delete(fileId);
                }
                catch
                {
                }
            }
        }

        private string GetOriginalFolder()
        {
            return _uploadFolder + "/originals";
        }

        private string GetPreviewFolder()
        {
            return _uploadFolder + "/previews";
        }

        private string GetThumbnailSmallFolder()
        {
            return _uploadFolder + "/thumbs/sm";
        }

        private string GetThumbnailMediumFolder()
        {
            return _uploadFolder + "/thumbs/md";
        }

        private static IImageKitClient TryCreateClient()
        {
            try
            {
                return new ImageKitClient();
            }
            catch
            {
                return null;
            }
        }

        private static string TryLoadUploadFolder()
        {
            try
            {
                return ImageKitConfiguration.LoadFromConfiguration().UploadFolder;
            }
            catch
            {
                return "/3dsgallery/pictures";
            }
        }
    }
}
