using _3dsGallery.DataLayer.DataBase;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Models
{
    public class RegisterView
    {
        [Required]
        [StringLength(25)]
        public string Login { get; set; }        

        [Required]
        [StringLength(50)]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Confirm Password")]
        [DataType(DataType.Password)]
        [StringLength(50)]
        [Compare("Password")]
        public string ConfirmPassword { get; set; }
    }

    public class LoginView
    {
        [Required]
        [StringLength(25)]
        public string Login { get; set; }

        [Required]
        [StringLength(50)]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public class GalleryDetailsView
    {
        public Gallery Gallery { get; set; }

        public IEnumerable<Picture> PicturePageData { get; set; }
    }

    public class AddPictureModel
    {
        [StringLength(150)]
        public string description { get; set; }

        //[StringLength(50)]
        //public string path { get; set; }

        public short galleryId { get; set; }

        public Gallery Gallery { get; set; }

        public bool isAdvanced { get; set; }

        public bool isTo2d { get; set; }

        public int leftOrRight { get; set; }
    }
}