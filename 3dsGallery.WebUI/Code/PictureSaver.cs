using _3dsGallery.DataLayer.DataBase;
using _3dsGallery.DataLayer.Tools;
using _3dsGallery.WebUI.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Code
{
    public class PictureSaver
    {
        private readonly ICloudinaryService _cloudinary;

        public PictureSaver(ICloudinaryService cloudinary)
        {
            _cloudinary = cloudinary;
        }

        /// <summary>
        /// Downloads the left-eye and right-eye images for a 3D picture from Cloudinary
        /// (stored as public_id and public_id+"_r"), merges them side-by-side and returns
        /// the result as a JPEG byte array.
        /// </summary>
        public byte[] GenerateSideBySideImage(string publicId)
        {
            var leftBytes  = _cloudinary.Download(_cloudinary.GetImageUrl(publicId));
            var rightBytes = _cloudinary.Download(_cloudinary.GetImageUrl(publicId + "_r"));

            // Image.FromStream requires the underlying stream to remain open for the lifetime
            // of the Image; keep both MemoryStreams alive until MergeSideBySide completes.
            using (var ms1 = new MemoryStream(leftBytes))
            using (var ms2 = new MemoryStream(rightBytes))
            {
                var img1 = Image.FromStream(ms1);
                var img2 = Image.FromStream(ms2);
                return MergeSideBySide(img1, img2);
            }
        }

        public Picture AnalyzeAndSave(Picture picture, AddPictureModel model, HttpPostedFileBase file)
        {
            // Read uploaded file into memory so we can work with it without touching disk
            byte[] fileBytes;
            using (var ms = new MemoryStream())
            {
                file.InputStream.CopyTo(ms);
                fileBytes = ms.ToArray();
            }

            // Determine whether this is a 3D (MPO) file by attempting to parse its stereo frames
            var mpoImages = MpoParser.GetImageSources(fileBytes).ToList();
            bool is3D = mpoImages.Count >= 2;

            string publicId = $"Picture/{picture.id}";
            Image imgForDisplay;

            if (!is3D)
            {
                // 2D: upload the file as-is
                using (var ms = new MemoryStream(fileBytes))
                using (var tempImg = Image.FromStream(ms))
                    imgForDisplay = new Bitmap(tempImg);

                picture.type = "2D";
                _cloudinary.Upload(ImageToJpegBytes(imgForDisplay), publicId);
            }
            else
            {
                // 3D / MPO
                if (model.isAdvanced && model.isTo2d)
                {
                    // Save only one eye as a 2D image
                    imgForDisplay = mpoImages.ElementAt(model.leftOrRight);
                    picture.type  = "2D";
                    _cloudinary.Upload(ImageToJpegBytes(imgForDisplay), publicId);
                }
                else
                {
                    // Upload left eye as main, right eye as publicId+"_r"
                    imgForDisplay = mpoImages[0];
                    picture.type  = "3D";
                    _cloudinary.Upload(ImageToJpegBytes(mpoImages[0]), publicId);
                    _cloudinary.Upload(ImageToJpegBytes(mpoImages[1]), publicId + "_r");
                }
            }

            // Store the Cloudinary public_id in the path column
            picture.path = publicId;

            return picture;
        }

        // ── helpers ────────────────────────────────────────────────────────────

        private static byte[] MergeSideBySide(Image img1, Image img2)
        {
            int targetHeight = Math.Min(img1.Height, img2.Height);
            float scale1 = (float)targetHeight / img1.Height;
            float scale2 = (float)targetHeight / img2.Height;
            int width1 = (int)(img1.Width * scale1);
            int width2 = (int)(img2.Width * scale2);

            using (var merged = new Bitmap(width1 + width2, targetHeight))
            using (var g = Graphics.FromImage(merged))
            {
                g.DrawImage(img1, new Rectangle(0, 0, width1, targetHeight));
                g.DrawImage(img2, new Rectangle(width1, 0, width2, targetHeight));

                using (var ms = new MemoryStream())
                {
                    merged.Save(ms, ImageFormat.Jpeg);
                    return ms.ToArray();
                }
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