using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using _3dsGallery.DataLayer.DataBase;
using System.IO;
using System.Data.Entity.Migrations;
using _3dsGallery.DataLayer.Tools;
using _3dsGallery.WebUI.Models;
using System.Drawing;
using _3dsGallery.WebUI.Code;

namespace _3dsGallery.WebUI.Controllers
{
    public class PictureController : Controller
    {
        private readonly GalleryContext db = new GalleryContext();

        //GET: Pictures
        [Route("Pictures")]
        public ActionResult Index(int page = 1, string filter = "new")
        {
            bool is3ds = Request.UserAgent.Contains("Nintendo 3DS");
            PicturePageData pageData = new PageData(page, filter, is3ds).GetPictruresByPage();
            ViewBag.Page = page;
            ViewBag.Filter = filter;
            ViewBag.Pages = pageData.TotalPages;

            return View(pageData.Pictures);
        }

        [Only3DS]
        [Authorize]
        [Route("AddPicture")]
        public ActionResult AddPicture()
        {
            var user = db.User.FirstOrDefault(x => x.login == User.Identity.Name);
            if (user == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            ViewBag.galleryId = new SelectList(user.Gallery, "id", "name");
            return View(new AddPictureModel());
        }

        [Only3DS]
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("AddPicture")]
        public ActionResult AddPicture(AddPictureModel model, HttpPostedFileBase file)
        {
            var user = db.User.FirstOrDefault(x => x.login == User.Identity.Name);
            if (user == null || !IsItMineGallery(model.galleryId))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            ViewBag.galleryId = new SelectList(user.Gallery, "id", "name");
            if (file == null)
            {
                ModelState.AddModelError(string.Empty, "You must select an image.");
                return View(model);
            }

            if (file.ContentLength > 750 * 1000)
                ModelState.AddModelError(string.Empty, "File size must be less than 750 kilobytes.");

            string file_extention = Path.GetExtension(file.FileName).ToLower();
            if (file_extention != ".mpo" && file_extention != ".jpg")
                ModelState.AddModelError(string.Empty, "File extention must be '.mpo' or '.jpg'.");

            if (model.isAdvanced && model.isTo2d && model.leftOrRight < 0 && model.leftOrRight > 1)
                ModelState.AddModelError(string.Empty, "You must choose which of the images (left or right) should be saved in 2D.");

            if (!ModelState.IsValid)
                return View(model);

            Picture picture = new Picture
            {
                description = model.description,
                galleryId = model.galleryId,
                CreationDate = DateTime.Now
            };
            db.Picture.Add(picture);
            db.SaveChanges();

            picture = new PictureSaver(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Picture")).AnalyzeAndSave(picture, model, file);

            picture.Gallery.LastPicture = picture;
            db.Entry(picture).State = EntityState.Modified;
            db.SaveChanges();

            return RedirectToAction("Details", "Gallery", new { id = picture.galleryId });
        }

        [Authorize]
        [HttpPost]
        public ActionResult Like(int? id)
        {
            Picture item = db.Picture.Find(id);
            if (item == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            User user = db.User.Where(x => x.login == User.Identity.Name).First();
            if (user.Picture.Any(x => x == item))
            {
                item.User.Remove(user);
            }
            else
            {
                item.User.Add(user);
            }
            db.SaveChanges();

            return Json(item.User.Count);
        }

        [HttpPost]
        public ActionResult Random()
        {
            int total = db.Picture.Count();
            Random rand = new Random();
            int offset = rand.Next(0, total);

            var randomRow = db.Picture
                .Skip(offset)
                .FirstOrDefault();

            return Json(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, randomRow.path));
        }

        // POST: Picture/Delete/5
        [Authorize]
        [HttpPost]
        public ActionResult Delete(int id)
        {
            if (!IsItMine(id))
                return HttpNotFound();

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Picture");
            Picture picture = db.Picture.Include(X => X.User).FirstOrDefault(x => x.id == id);
            if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, picture.path)))
                System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, picture.path));

            Gallery gallery = picture.Gallery;
            db.Picture.Remove(picture);
            gallery.LastPicture = gallery.Picture.LastOrDefault();
            db.Entry(gallery).State = EntityState.Modified;
            db.SaveChanges();
            if (System.IO.File.Exists(Path.Combine(path, $"{id}-thumb_sm.JPG")))
                System.IO.File.Delete(Path.Combine(path, $"{id}-thumb_sm.JPG"));
            if (System.IO.File.Exists(Path.Combine(path, $"{id}-thumb_md.JPG")))
                System.IO.File.Delete(Path.Combine(path, $"{id}-thumb_md.JPG"));
            if (System.IO.File.Exists(Path.Combine(path, $"{id}.JPG")))
                System.IO.File.Delete(Path.Combine(path, $"{id}.JPG"));
            return Json("ok");
        }


        [Authorize]
        [HttpPost]
        public ActionResult EditComment(int id, string comment)
        {
            if (!IsItMine(id))
                return HttpNotFound();

            Picture picture = db.Picture.FirstOrDefault(x => x.id == id);
            picture.description = comment;
            db.Picture.AddOrUpdate(picture);
            db.SaveChanges();
            return Json("ok");
        }

        public ActionResult GetElement(int id)
        {
            var picture = db.Picture.Find(id);
            if (picture == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            return PartialView(picture);
        }

        public ActionResult GetElements(IEnumerable<Picture> items)
        {
            if (items == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            return PartialView(items);
        }

        public ActionResult ShowPage(int? gallery, string user, int page = 1, string filter = "new", bool user_likes = false)
        {
            bool is3ds = Request.UserAgent.Contains("Nintendo 3DS");
            PicturePageData pageData = new PageData(page, filter, is3ds).GetPictruresByPage(gallery,user,user_likes);
            ViewBag.Page = page;
            ViewBag.Filter = filter;
            ViewBag.Pages = pageData.TotalPages;

            return PartialView(pageData.Pictures);
        }


        [HttpPost]
        public ActionResult GetPath(int id)
        {
            var pic = db.Picture.Find(id);
            string result = $"http://3dsgallery.azurewebsites.net/{pic.path.Replace('\\', '/')}";
            return Json(result);
        }

        bool IsItMine(int? id)
        {
            var user = new GalleryContext().User.Where(x => x.login == User.Identity.Name).FirstOrDefault();
            return user.Gallery.SelectMany(x => x.Picture).Any(x => x.id == id);
        }
        bool IsItMineGallery(int? id)
        {
            var user = new GalleryContext().User.Where(x => x.login == User.Identity.Name).FirstOrDefault();
            return user.Gallery.Any(x => x.id == id);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
