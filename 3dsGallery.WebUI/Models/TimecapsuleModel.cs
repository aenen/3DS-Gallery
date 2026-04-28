using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Models
{
    public class TimecapsuleModel
    {
        public int IdGallery { get; set; }
        public int IdPicture { get; set; }
        public int TimecapsuleCount { get; set; }
        public string RefreshTimeInfo { get; set; }
        public int YearsOld { get; set; }
        public string CreatedBy { get; set; }
        public string GalleryName { get; set; }
        public string GalleryCssCode { get; set; }
        public string GalleryCssCodeEx { get; set; }

        public string TestImgDate { get; set; }
        public string TestSrvDate { get; set; }
    }
}