using _3dsGallery.DataLayer.DataBase;
using _3dsGallery.DataLayer.Tools;
using _3dsGallery.WebUI.Models;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Code
{
    public class PictureSaver
    {
        private readonly string _siteRootPath;
        private readonly IPictureAssetStorageService _storageService;

        public PictureSaver(string siteRootPath)
            : this(siteRootPath, new PictureAssetStorageService(siteRootPath))
        {
        }

        public PictureSaver(string siteRootPath, IPictureAssetStorageService storageService)
        {
            _siteRootPath = siteRootPath;
            _storageService = storageService;
        }

        public byte[] GenerateSideBySideImage(Picture picture)
        {
            if (picture == null)
                throw new ArgumentNullException("picture");

            var sourceBytes = _storageService.DownloadOriginalBytes(picture);
            var images = MpoParser.GetImageSources(sourceBytes).ToList();
            try
            {
                if (images.Count < 2)
                    throw new InvalidOperationException("Need at least two images to merge.");

                return MergeSideBySide(images[0], images[1]);
            }
            finally
            {
                foreach (var image in images)
                    image.Dispose();
            }
        }

        public Picture AnalyzeAndSave(Picture picture, AddPictureModel model, HttpPostedFileBase file)
        {
            if (file == null)
                throw new ArgumentNullException("file");

            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                return AnalyzeAndSave(picture, model, file.FileName, ms.ToArray());
            }
        }

        public Picture AnalyzeAndSave(Picture picture, AddPictureModel model, string fileName, byte[] fileBytes)
        {
            if (picture == null)
                throw new ArgumentNullException("picture");
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("fileName");
            if (fileBytes == null || fileBytes.Length == 0)
                throw new ArgumentException("fileBytes");

            var mpoImages = MpoParser.GetImageSources(fileBytes).ToList();
            Image imageForPreview = null;
            Image previewImageToPersist = null;
            Image thumbSmall = null;
            Image thumbMedium = null;

            try
            {
                var request = new PictureAssetUploadRequest();
                var fileExtension = Path.GetExtension(fileName);
                if (string.IsNullOrWhiteSpace(fileExtension))
                    fileExtension = ".JPG";

                if (!mpoImages.Any())
                {
                    picture.type = "2D";
                    request.LegacyPath = string.Format("Picture/{0}.JPG", picture.id);
                    request.PictureType = picture.type;
                    request.OriginalFileName = picture.id + ".JPG";
                    request.OriginalContentType = "image/jpeg";
                    request.OriginalBytes = fileBytes;
                    request.PreviewFileName = request.OriginalFileName;
                    request.PreviewBytes = fileBytes;

                    using (var stream = new MemoryStream(fileBytes))
                    using (var original = Image.FromStream(stream))
                    {
                        imageForPreview = new Bitmap(original);
                        FillThumbnailAssets(request, picture.id, imageForPreview, fileBytes.LongLength, ref thumbSmall, ref thumbMedium);
                    }
                }
                else if (model != null && model.isAdvanced && model.isTo2d)
                {
                    var eyeIndex = model.leftOrRight;
                    if (eyeIndex < 0 || eyeIndex > 1)
                        throw new InvalidOperationException("You must choose which of the images (left or right) should be saved in 2D.");

                    previewImageToPersist = new Bitmap(mpoImages.ElementAt(eyeIndex));
                    picture.type = "2D";
                    request.LegacyPath = string.Format("Picture/{0}.JPG", picture.id);
                    request.PictureType = picture.type;
                    request.OriginalFileName = picture.id + ".JPG";
                    request.OriginalContentType = "image/jpeg";
                    request.OriginalBytes = ImageToJpegBytes(previewImageToPersist);
                    request.PreviewFileName = request.OriginalFileName;
                    request.PreviewBytes = request.OriginalBytes;

                    imageForPreview = new Bitmap(previewImageToPersist);
                    FillThumbnailAssets(request, picture.id, imageForPreview, request.OriginalBytes.LongLength, ref thumbSmall, ref thumbMedium);
                }
                else
                {
                    picture.type = "3D";
                    previewImageToPersist = new Bitmap(mpoImages[0]);
                    imageForPreview = new Bitmap(previewImageToPersist);

                    request.LegacyPath = string.Format("Picture/{0}.MPO", picture.id);
                    request.PictureType = picture.type;
                    request.OriginalFileName = picture.id + ".MPO";
                    request.OriginalContentType = "image/mpo";
                    request.OriginalBytes = fileBytes;
                    request.PreviewFileName = picture.id + ".JPG";
                    request.PreviewBytes = ImageToJpegBytes(previewImageToPersist);

                    FillThumbnailAssets(request, picture.id, imageForPreview, request.PreviewBytes.LongLength, ref thumbSmall, ref thumbMedium);
                }

                _storageService.UploadAssets(picture, request);
                return picture;
            }
            finally
            {
                foreach (var image in mpoImages)
                    image.Dispose();

                if (imageForPreview != null)
                    imageForPreview.Dispose();
                if (previewImageToPersist != null)
                    previewImageToPersist.Dispose();
                if (thumbSmall != null)
                    thumbSmall.Dispose();
                if (thumbMedium != null)
                    thumbMedium.Dispose();
            }
        }

        private static void FillThumbnailAssets(PictureAssetUploadRequest request, int pictureId, Image imageForPreview, long originalLength, ref Image thumbSmall, ref Image thumbMedium)
        {
            thumbSmall = PictureTools.MakeThumbnail(imageForPreview, 155, 97);
            var thumbSmallBytes = PictureTools.GetByteSize(thumbSmall);
            if (thumbSmallBytes.LongLength < originalLength)
            {
                request.ThumbnailSmallFileName = pictureId + "-thumb_sm.JPG";
                request.ThumbnailSmallBytes = thumbSmallBytes;
            }

            thumbMedium = PictureTools.MakeThumbnail(imageForPreview, 280, 999);
            var thumbMediumBytes = PictureTools.GetByteSize(thumbMedium);
            if (thumbMediumBytes.LongLength < originalLength)
            {
                request.ThumbnailMediumFileName = pictureId + "-thumb_md.JPG";
                request.ThumbnailMediumBytes = thumbMediumBytes;
            }
        }

        private static byte[] MergeSideBySide(Image leftImage, Image rightImage)
        {
            int targetHeight = Math.Min(leftImage.Height, rightImage.Height);
            float scaleLeft = (float)targetHeight / leftImage.Height;
            float scaleRight = (float)targetHeight / rightImage.Height;

            int leftWidth = (int)(leftImage.Width * scaleLeft);
            int rightWidth = (int)(rightImage.Width * scaleRight);

            using (var merged = new Bitmap(leftWidth + rightWidth, targetHeight))
            using (var graphics = Graphics.FromImage(merged))
            using (var ms = new MemoryStream())
            {
                graphics.DrawImage(leftImage, new Rectangle(0, 0, leftWidth, targetHeight));
                graphics.DrawImage(rightImage, new Rectangle(leftWidth, 0, rightWidth, targetHeight));
                merged.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }

        private static byte[] ImageToJpegBytes(Image image)
        {
            using (var ms = new MemoryStream())
            {
                image.Save(ms, ImageFormat.Jpeg);
                return ms.ToArray();
            }
        }
    }
}
