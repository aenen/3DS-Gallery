using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Models
{
    public class GalleryModel
    {
        public int IdGallery { get; set; }
        public string GalleryName { get; set; }
        //public string GalleryDescription { get; set; }
        public string ColorThemeClass { get; set; }
        public string CreatedBy { get; set; }
        public int PictureTotalCount { get; set; }
        public bool Is3D { get; set; }

        public List<GalleryPicturePreview> PicturePreviewList { get; set; }
    }

    public class GalleryPicturePreview
    {
        public int IdPicture { get; set; }
        public string Path { get; set; }
    }
}