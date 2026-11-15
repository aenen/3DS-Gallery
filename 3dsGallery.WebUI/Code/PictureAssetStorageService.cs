using _3dsGallery.DataLayer.DataBase;
using System;
using System.Collections.Generic;
using System.IO;
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
            _uploadFolder = string.IsNullOrWhiteSpace(uploadFolder) ? "/3dsgallery" : uploadFolder;
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

            var originalPath = picture.path;
            var originalType = picture.type;
            var uploadedFileIds = new List<string>();
            try
            {
                picture.path = request.LegacyPath;
                picture.type = request.PictureType;

                uploadedFileIds.Add(UploadFile(request.OriginalBytes, GetRemoteOriginalPath(picture), request.OriginalContentType));

                if (request.PreviewBytes != null && request.PreviewBytes.Length > 0)
                {
                    var previewPath = GetRemotePreviewPath(picture);
                    if (!string.Equals(previewPath, GetRemoteOriginalPath(picture), StringComparison.OrdinalIgnoreCase))
                        uploadedFileIds.Add(UploadFile(request.PreviewBytes, previewPath, "image/jpeg"));
                }

                if (request.ThumbnailSmallBytes != null && request.ThumbnailSmallBytes.Length > 0)
                    uploadedFileIds.Add(UploadFile(request.ThumbnailSmallBytes, GetRemoteThumbnailSmallPath(picture), "image/jpeg"));

                if (request.ThumbnailMediumBytes != null && request.ThumbnailMediumBytes.Length > 0)
                    uploadedFileIds.Add(UploadFile(request.ThumbnailMediumBytes, GetRemoteThumbnailMediumPath(picture), "image/jpeg"));
            }
            catch
            {
                TryDeleteUploadedFiles(uploadedFileIds);
                picture.path = originalPath;
                picture.type = originalType;
                throw;
            }
        }

        public void DeleteAssets(Picture picture)
        {
            if (picture == null)
                throw new ArgumentNullException("picture");
            if (_imageKitClient == null)
                throw new InvalidOperationException("ImageKit is not configured. Set ImageKitPrivateKey and ImageKitUrlEndpoint before deleting remote picture assets.");

            var failures = new List<string>();
            foreach (var remotePath in GetRemotePaths(picture))
            {
                try
                {
                    var result = _imageKitClient.DeleteByPath(remotePath);
                    if (!result.Deleted && !result.NotFound)
                        failures.Add(remotePath);
                }
                catch
                {
                    failures.Add(remotePath);
                }
            }

            if (failures.Any())
                throw new InvalidOperationException("ImageKit deletion failed for one or more picture assets.");
        }

        public byte[] DownloadOriginalBytes(Picture picture)
        {
            if (picture == null)
                throw new ArgumentNullException("picture");

            if (_urlResolver.IsRemoteActive(picture))
                return _imageKitClient.Download(_urlResolver.GetRemoteOriginalUrl(picture));

            var localPath = _urlResolver.GetLocalOriginalPhysicalPath(picture);
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
                throw new InvalidOperationException("Original picture file was not found.");

            return File.ReadAllBytes(localPath);
        }

        private string UploadFile(byte[] bytes, string remotePath, string contentType)
        {
            var normalizedPath = NormalizeRemotePath(remotePath);
            var lastSlashIndex = normalizedPath.LastIndexOf('/');
            var folder = lastSlashIndex > 0 ? normalizedPath.Substring(0, lastSlashIndex) : _uploadFolder;
            var fileName = lastSlashIndex >= 0 ? normalizedPath.Substring(lastSlashIndex + 1) : normalizedPath;
            return _imageKitClient.Upload(bytes, fileName, folder, contentType).FileId;
        }

        private IEnumerable<string> GetRemotePaths(Picture picture)
        {
            return new[]
            {
                GetRemoteOriginalPath(picture),
                GetRemotePreviewPath(picture),
                GetRemoteThumbnailSmallPath(picture),
                GetRemoteThumbnailMediumPath(picture)
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        }

        private string GetRemoteOriginalPath(Picture picture)
        {
            return _urlResolver.GetRemoteOriginalPath(picture);
        }

        private string GetRemotePreviewPath(Picture picture)
        {
            return _urlResolver.GetRemotePreviewPath(picture);
        }

        private string GetRemoteThumbnailSmallPath(Picture picture)
        {
            return _urlResolver.GetRemoteThumbnailSmallPath(picture);
        }

        private string GetRemoteThumbnailMediumPath(Picture picture)
        {
            return _urlResolver.GetRemoteThumbnailMediumPath(picture);
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

        private string NormalizeRemotePath(string remotePath)
        {
            if (string.IsNullOrWhiteSpace(remotePath))
                return _uploadFolder;

            var normalized = remotePath.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("/"))
                normalized = "/" + normalized;
            return normalized;
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
                return "/3dsgallery";
            }
        }
    }
}
