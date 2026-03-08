using _3dsGallery.DataLayer.DataBase;
using _3dsGallery.WebUI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace _3dsGallery.WebUI.Controllers
{
    public class HomeController : Controller
    {
        GalleryContext db = new GalleryContext();

        // GET: Home
        public ActionResult Index()
        {
            int galleryCount = Request.UserAgent.Contains("Nintendo 3DS") ? 2 : 3;
            int pictureCount = Request.UserAgent.Contains("Nintendo 3DS") ? 3 : 12;

            var model = new HomePageModel();
            var homePageGalleryList = db.Gallery
                .Where(x => x.LastPicture != null && (!x.IsPrivate || (x.IsPrivate && x.User.login == User.Identity.Name)))
                .OrderByDescending(x => x.LastPicture.id)
                .Take(galleryCount)
                .ToList();
            var homePagePictureList = db.Picture
                .Where(x => !x.Gallery.IsPrivate || (x.Gallery.IsPrivate && x.Gallery.User.login == User.Identity.Name))
                .OrderByDescending(x => x.id)
                .Take(pictureCount)
                .ToList();

            model.GalleryList = homePageGalleryList.Select(gallery => new GalleryModel
            {
                IdGallery = gallery.id,
                GalleryName = gallery.name,
                ColorThemeClass = gallery.Style.value,
                CreatedBy = gallery.User.login,
                Is3D = gallery.Picture.Any(pic => pic.type == "3D"),
                PictureTotalCount = gallery.Picture.Count,
                PicturePreviewList = gallery.Picture.OrderByDescending(x => x.id).Take(2).Select(pic => new GalleryPicturePreview
                {
                    IdPicture = pic.id,
                    Path = pic.path
                }).ToList()
            }).ToList();

            model.PictureList = homePagePictureList.Select(pic => new PictureModel
            {
                IdPicture = pic.id,
                PictureDescription = pic.description,
                ColorThemeClass = pic.Gallery.Style.value,
                CreatedBy = pic.Gallery.User.login,
                CreationDate = pic.CreationDate,
                Is3D = pic.type == "3D",
                IsLikedByMe = User.Identity.IsAuthenticated && pic.User.Any(x => x.login == User.Identity.Name),
                Path = pic.path,
                LikeCount = pic.User.Count
            }).ToList();

            model.TotalGalleryCount = db.Gallery.Where(x=>!x.IsPrivate).Count();
            model.TotalImageCount = db.Picture.Where(x=>!x.Gallery.IsPrivate).Count();
            model.Total3DImageCount = db.Picture.Where(x=>!x.Gallery.IsPrivate && x.type == "3D").Count();

            return View(model);
        }
    }
}