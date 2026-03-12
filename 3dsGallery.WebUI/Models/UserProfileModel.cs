using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Models
{
    public class UserProfileModel
    {
        public string UserName { get; set; }
        public string FavColorCss { get; set; }

        public int TotalGalleryCount { get; set; }
        public int TotalImageCount { get; set; }
        public int TotalLikesCount { get; set; }
        public int SocialCredits { get; set; }

        public bool IsItMe { get; set; }

        public List<GalleryModel> GalleryList { get; set; }
        public List<PictureModel> PictureList { get; set; }
        public List<PictureModel> LikeList { get; set; }
    }
}