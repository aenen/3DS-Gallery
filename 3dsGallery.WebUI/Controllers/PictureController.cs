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
            PicturePageData pageData = new PageData(page, filter, is3ds, User.Identity.Name).GetPictruresByPage();
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

            ViewBag.hasGalleries = user.Gallery.Any();
            ViewBag.galleryId = new SelectList(user.Gallery, "id", "name");
            return View(new AddPictureModel());
        }

        [Only3DS]
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("AddPicture")]
        public ActionResult AddPicture(AddPictureModel model, IEnumerable<HttpPostedFileBase> file, string action)
        {
            var user = db.User.FirstOrDefault(x => x.login == User.Identity.Name);
            if (user == null || !IsItMineGallery(model.galleryId))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            ViewBag.hasGalleries = user.Gallery.Any();
            ViewBag.galleryId = new SelectList(user.Gallery, "id", "name");

            var files = (file ?? Enumerable.Empty<HttpPostedFileBase>())
                .Where(f => f != null && f.ContentLength > 0)
                .ToList();

            if (!files.Any())
            {
                ModelState.AddModelError(string.Empty, "You must select at least one image.");
                return View(model);
            }

            if (files.Count > 5)
                ModelState.AddModelError(string.Empty, "You can upload a maximum of 5 files at once.");

            foreach (var f in files)
            {
                if (f.ContentLength > 750 * 1000)
                    ModelState.AddModelError(string.Empty, $"File '{f.FileName}' size must be less than 750 kilobytes.");

                string file_extention = Path.GetExtension(f.FileName).ToLower();
                if (file_extention != ".mpo" && file_extention != ".jpg")
                    ModelState.AddModelError(string.Empty, $"File '{f.FileName}' extension must be '.mpo' or '.jpg'.");
            }

            if (model.isAdvanced && model.isTo2d && model.leftOrRight < 0 && model.leftOrRight > 1)
                ModelState.AddModelError(string.Empty, "You must choose which of the images (left or right) should be saved in 2D.");

            if (!ModelState.IsValid)
                return View(model);

            Picture lastPicture = null;
            foreach (var f in files)
            {
                Picture picture = new Picture
                {
                    description = model.description,
                    galleryId = model.galleryId,
                    CreationDate = DateTime.Now
                };
                db.Picture.Add(picture);
                db.SaveChanges();

                picture = new PictureSaver(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Picture")).AnalyzeAndSave(picture, model, f);

                db.Entry(picture).State = EntityState.Modified;
                db.SaveChanges();
                lastPicture = picture;
            }

            if (lastPicture != null)
            {
                lastPicture.Gallery.LastPicture = lastPicture;
                db.Entry(lastPicture.Gallery).State = EntityState.Modified;
                db.SaveChanges();
            }

            if (action == "Upload & Add More")
                return RedirectToAction("AddPicture", "Gallery", new { id = model.galleryId });
            else
                return RedirectToAction("Details", "Gallery", new { id = model.galleryId });

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
            int total = db.Picture.Where(x => !x.Gallery.IsPrivate).Count();
            Random rand = new Random();
            int offset = rand.Next(0, total);

            var randomRow = db.Picture
                .Where(x => !x.Gallery.IsPrivate)
                .OrderBy(x => x.id)
                .Skip(offset)
                .FirstOrDefault();

            return Json(randomRow.path);
        }

        [HttpPost]
        public ActionResult GetTimeCapsule(int? existingId)
        {
            var result = new TimecapsuleModel();

            DateTime utcNow = DateTime.UtcNow;
            result.RefreshTimeInfo = TimeUntilTomorrow(utcNow);

            var timeCapsulesQuery = db.Picture
                .Where(x => !x.Gallery.IsPrivate
                    && x.CreationDate.HasValue
                    && x.CreationDate.Value.Month == utcNow.Month
                    && x.CreationDate.Value.Day == utcNow.Day
                    && x.CreationDate.Value.Year < utcNow.Year
                    && x.id != existingId)
                .OrderBy(x => x.id);

            int timeCapsulesCount = timeCapsulesQuery.Count();
            if (timeCapsulesCount == 0) 
                return Json(result);

            Random rand = new Random();
            int offset = rand.Next(0, timeCapsulesCount);
            var pictureTimecapsule = timeCapsulesQuery.Skip(offset).FirstOrDefault();

            result.TimecapsuleCount = timeCapsulesCount;
            result.IdPicture = pictureTimecapsule.id;
            result.YearsOld = utcNow.Year - pictureTimecapsule.CreationDate.Value.Year;
            result.GalleryName = pictureTimecapsule.Gallery.name;
            result.GalleryCssCode = pictureTimecapsule.Gallery.Style.value;
            result.GalleryCssCodeEx = pictureTimecapsule.Gallery.Style.ValueEx;
            result.IdGallery = pictureTimecapsule.galleryId;
            result.CreatedBy = pictureTimecapsule.Gallery.User.login;

            result.TestImgDate = pictureTimecapsule.CreationDate.Value.ToString();
            result.TestSrvDate = utcNow.ToString();

            if (!existingId.HasValue && result.TimecapsuleCount > 0)
                result.TimecapsuleCount--; // should show the remaining TC available

            return Json(result);
        }

        public ActionResult RandomGenerateSideBySide()
        {
            int total = db.Picture.Where(x => !x.Gallery.IsPrivate && x.type == "3D").Count();
            Random rand = new Random();
            int offset = rand.Next(0, total);

            var randomRow = db.Picture
                .Where(x => !x.Gallery.IsPrivate && x.type == "3D")
                .OrderBy(x => x.id)
                .Skip(offset)
                .FirstOrDefault();

            var bytes = new PictureSaver(AppDomain.CurrentDomain.BaseDirectory).GenerateSideBySideImage(randomRow.path);
            return File(bytes, "image/jpeg");
        }

        public ActionResult GenerateSideBySide(int? id)
        {
            Picture item = db.Picture.Find(id);
            if (item == null || item.type != "3D")
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var bytes = new PictureSaver(AppDomain.CurrentDomain.BaseDirectory).GenerateSideBySideImage(item.path);
            return File(bytes, "image/jpeg");
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
        //return RedirectToAction("Details", "Gallery", new { id = model.galleryId });

        [HttpPost]
        public ActionResult GetPictureElementById(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var picModel = db.Picture
                .Where(x => !x.Gallery.IsPrivate && x.id == id)
                .Take(1)
                .Select(pic => new PictureModel
            {
                IdPicture = pic.id,
                IdGallery = pic.galleryId,
                PictureDescription = pic.description,
                ColorThemeClass = pic.Gallery.Style.value,
                CreatedBy = pic.Gallery.User.login,
                CreationDate = pic.CreationDate,
                Is3D = pic.type == "3D",
                IsLikedByMe = User.Identity.IsAuthenticated && pic.User.Any(x => x.login == User.Identity.Name),
                Path = pic.path,
                LikeCount = pic.User.Count
            }).ToList();

            TempData["items"] = picModel;
            return RedirectToAction("GetPictureElements", "Picture");
        }

        public ActionResult GetPictureElements(IEnumerable<PictureModel> items)
        {
            // If called directly with items, use them
            if (items != null)
                return PartialView(items);

            // If redirected, pull from TempData
            var tempItems = TempData["items"] as IEnumerable<PictureModel>;
            if (tempItems == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            return PartialView(tempItems);
        }


        public ActionResult ShowPage(int? gallery, string user, int page = 1, string filter = "new", bool user_likes = false)
        {
            bool is3ds = Request.UserAgent.Contains("Nintendo 3DS");
            PicturePageData pageData = new PageData(page, filter, is3ds, User.Identity.Name).GetPictruresByPage(gallery,user,user_likes);
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

        private string TimeUntilTomorrow(DateTime now)
        {
            DateTime tomorrow = now.Date.AddDays(1);
            TimeSpan remaining = tomorrow - now;

            int hours = (int)remaining.TotalHours;
            int minutes = remaining.Minutes;

            if (hours > 0 && minutes > 0)
                return $"{hours}hr and {minutes}min";
            else if (hours > 0)
                return $"{hours}hr";
            else
                return $"{minutes}min";
        }

    }
}
