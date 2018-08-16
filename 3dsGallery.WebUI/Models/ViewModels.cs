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
}