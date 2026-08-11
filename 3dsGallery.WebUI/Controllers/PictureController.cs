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

        // GET: Pictures/Details/5
        [Route("Pictures/{id}")]
        public ActionResult Details(int? id)
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var pic = db.Picture.Find(id);
            if (pic == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            if (pic.Gallery.IsPrivate && pic.Gallery.User.login != User.Identity.Name)
                return RedirectToAction("Index", "Home");

            var modelResult = new PictureDetailsModel
            {
                Pic = new PictureModel
                {
                    IdPicture = pic.id,
                    IdGallery = pic.galleryId,
                    PictureDescription = pic.description,
                    ColorThemeClass = pic.Gallery.Style.value,
                    ColorThemeName = pic.Gallery.Style.ValueEx ?? pic.Gallery.Style.value,
                    CreatedBy = pic.Gallery.User.login,
                    CreationDate = pic.CreationDate,
                    Is3D = pic.type == "3D",
                    IsLikedByMe = User.Identity.IsAuthenticated && pic.User.Any(x => x.login == User.Identity.Name),
                    Path = pic.path,
                    LikeCount = pic.User.Count,
                    CommentCount = pic.Comments.Count,
                    GalleryName = pic.Gallery.name
                },
                Comments = pic.Comments.Select(x => new PictureCommentModel
                {
                    CommentDesc = x.CommentDesc,
                    CreationDate = x.CreationDate,
                    Username = x.CreatedBy.login,
                    IdComment = x.IdComment
                }).ToList(),
                LikedByUsers = pic.User.Select(x => x.login).ToList()
            };

            return View(modelResult);
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

                picture = new PictureSaver(new CloudinaryService()).AnalyzeAndSave(picture, model, f);

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

            User user = db.User.First(x => x.login == User.Identity.Name);

            bool alreadyLiked = user.Picture.Any(x => x == item);

            if (alreadyLiked)
            {
                // Unlike
                item.User.Remove(user);

                // сповіщення іДі нахуй
                var notif = db.Notification
                    .FirstOrDefault(n =>
                        n.IdUser == item.Gallery.userId &&
                        n.Type == "Like" &&
                        n.RelatedEntityId == item.id &&
                        n.IdUserActor == user.id);

                if (notif != null)
                {
                    db.Notification.Remove(notif);
                }
            }
            else
            {
                // Like
                item.User.Add(user);

                // сповідення іДі
                if (user.id != item.Gallery.userId)
                {
                    var notification = new Notification
                    {
                        IdUser = item.Gallery.userId,
                        IdUserActor = user.id,
                        Type = "Like",
                        Message = $"{user.login} liked your picture from '" + item.Gallery.name + "'",
                        RelatedEntityId = item.id,
                        IsRead = false,
                        CreationDate = DateTime.Now
                    };

                    db.Notification.Add(notification);
                }
            }

            db.SaveChanges();

            return Json(item.User.Count);
        }

        [Authorize]
        [HttpPost]
        public ActionResult AddComment(CommentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            var user = db.User.First(x => x.login == User.Identity.Name);
            var picture = db.Picture.First(x => x.id == model.IdPicture);

            var comment = new PictureComment
            {
                CommentDesc = model.Text,
                CreationDate = DateTime.Now,
                CreatedBy = user
            };

            picture.Comments.Add(comment);

            db.SaveChanges();

            // +1 сповіщення
            if (user.id != picture.Gallery.userId)
            {
                var notification = new Notification
                {
                    IdUser = picture.Gallery.userId,
                    IdUserActor = user.id,
                    Type = "Comment",
                    Message = $"{user.login} commented on your picture from '" + picture.Gallery.name + "'",
                    RelatedEntityId = picture.id,
                    IsRead = false,
                    CreationDate = DateTime.Now
                };

                db.Notification.Add(notification);
                db.SaveChanges();
            }

            return Json("ok");
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

            // New Cloudinary paths have no extension; legacy paths have .JPG/.MPO
            string url = randomRow.path != null && !randomRow.path.Contains(".")
                ? new CloudinaryService().GetImageUrl(randomRow.path)
                : randomRow.path;
            return Json(url);
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
            if (total == 0)
                return new HttpStatusCodeResult(HttpStatusCode.NotFound);

            Random rand = new Random();
            int offset = rand.Next(0, total);

            var randomRow = db.Picture
                .Where(x => !x.Gallery.IsPrivate && x.type == "3D")
                .OrderBy(x => x.id)
                .Skip(offset)
                .FirstOrDefault();

            var bytes = new PictureSaver(new CloudinaryService()).GenerateSideBySideImage(randomRow.path);
            return File(bytes, "image/jpeg");
        }

        public ActionResult GenerateSideBySide(int? id)
        {
            Picture item = db.Picture.Find(id);
            if (item == null || item.type != "3D")
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var bytes = new PictureSaver(new CloudinaryService()).GenerateSideBySideImage(item.path);
            return File(bytes, "image/jpeg");
        }


        // POST: Picture/Delete/5
        [Authorize]
        [HttpPost]
        public ActionResult Delete(int id)
        {
            if (!IsItMine(id))
                return HttpNotFound();

            var cloudinary = new CloudinaryService();
            Picture picture = db.Picture.Include(X => X.User).FirstOrDefault(x => x.id == id);

            // Delete from Cloudinary (both main image and right-eye for 3D)
            if (!string.IsNullOrEmpty(picture.path))
            {
                cloudinary.Delete(picture.path);
                if (picture.type == "3D")
                    cloudinary.Delete(picture.path + "_r");
            }

            Gallery gallery = picture.Gallery;
            db.Picture.Remove(picture);
            gallery.LastPicture = gallery.Picture.LastOrDefault();
            db.Entry(gallery).State = EntityState.Modified;
            db.SaveChanges();
            return Json("ok");
        }

        [Authorize]
        [HttpPost]
        public ActionResult DeleteComment(int id)
        {
            var pictureComment = db.PictureComments
                .Include(c => c.Picture)
                .Include(c => c.CreatedBy)
                .FirstOrDefault(x => x.IdComment == id);

            if (pictureComment == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            if (pictureComment.CreatedBy.login != User.Identity.Name)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);


            var recipientId = pictureComment.Picture.Gallery.userId;   // picture owner
            var actorId = pictureComment.CreatedBy.id;           // commenter
            var notif = db.Notification.FirstOrDefault(n =>
                n.IdUser == recipientId &&
                n.Type == "Comment" &&
                n.RelatedEntityId == pictureComment.Picture.id &&
                n.IdUserActor == actorId);

            if (notif != null)
            {
                // сповіщення іДі нахуй
                db.Notification.Remove(notif);
            }

            // камєнт іДі нахуй
            db.PictureComments.Remove(pictureComment);

            db.SaveChanges();

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
                    LikeCount = pic.User.Count,
                    CommentCount = pic.Comments.Count
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
            PicturePageData pageData = new PageData(page, filter, is3ds, User.Identity.Name).GetPictruresByPage(gallery, user, user_likes);
            ViewBag.Page = page;
            ViewBag.Filter = filter;
            ViewBag.Pages = pageData.TotalPages;

            return PartialView(pageData.Pictures);
        }


        [HttpPost]
        public ActionResult GetPath(int id)
        {
            var pic = db.Picture.Find(id);
            string result = new CloudinaryService().GetImageUrl(pic.path);
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
