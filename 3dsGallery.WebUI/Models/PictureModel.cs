using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Models
{
    public class PictureModel
    {
        public int IdPicture { get; set; }
        public string PictureDescription { get; set; }
        public string Path { get; set; }
        public string ColorThemeClass { get; set; }
        public int LikeCount { get; set; }
        public bool Is3D { get; set; }
        public bool IsLikedByMe { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreationDate { get; set; }
    }
}