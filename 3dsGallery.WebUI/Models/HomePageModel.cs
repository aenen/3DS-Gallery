using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Models
{
    public class HomePageModel
    {
        public int TotalGalleryCount { get; set; }
        public int TotalImageCount { get; set; }
        public int Total3DImageCount { get; set; }

        public List<GalleryModel> GalleryList { get; set; }
        public List<PictureModel> PictureList { get; set; }
    }
}