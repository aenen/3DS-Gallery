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

namespace _3dsGallery.WebUI.Controllers
{
    public class PictureController : Controller
    {
        private readonly GalleryContext db = new GalleryContext();
        private const int pictures3ds = 10;
        private const int picturesPc = 20;

        //GET: Pictures
        [Route("Pictures")]
        public ActionResult Index(int page = 1, string filter = "new")
        {
            ViewBag.Page = page;
            ViewBag.Filter = filter;
            
            var result = db.Picture.Include(p => p.Gallery).ToList();
            switch (filter)
            {
                case "new":
                    result = result.OrderByDescending(x => x.id).ToList();
                    break;
                case "old":
                    result = result.OrderBy(x => x.id).ToList();
                    break;
                case "best":
                    result = result.OrderByDescending(x => x.User.Count).ThenByDescending(x => x.id).ToList();
                    break;
                case "3d":
                    result = result.Where(x => x.type == "3D").OrderByDescending(x => x.id).ToList();
                    break;
                case "2d":
                    result = result.Where(x => x.type == "2D").OrderByDescending(x => x.id).ToList();
                    break;
                default:
                    break;
            }
            int count = result.Count;
            int show_items = (Request.UserAgent.Contains("Nintendo 3DS")) ? pictures3ds : picturesPc;
            int pages = count / show_items + ((count % show_items == 0) ? 0 : 1);
            ViewBag.Pages = pages;
            return View();
        }

        [Authorize]
        [Route("AddPicture")]
        public ActionResult AddPicture()
        {
            if (!Request.UserAgent.Contains("Nintendo 3DS"))
                return RedirectToAction("Not3ds", "User");

            var user = db.User.FirstOrDefault(x => x.login == User.Identity.Name);
            if (user == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            ViewBag.galleryId = new SelectList(user.Gallery, "id", "name");
            return View(new Picture());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("AddPicture")]
        public ActionResult AddPicture([Bind(Include = "id,description,path,galleryId")] Picture picture, HttpPostedFileBase file)
        {
            if (!Request.UserAgent.Contains("Nintendo 3DS"))
                return RedirectToAction("Not3ds", "User");

            var user = db.User.FirstOrDefault(x => x.login == User.Identity.Name);
            if (user == null || !IsItMineGallery(picture.galleryId))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            ViewBag.galleryId = new SelectList(user.Gallery, "id", "name");

            if (!ModelState.IsValid || file == null)
                return View(picture);

            if (file.ContentLength > 750 * 1000)
                return View(picture);

            string file_extention = Path.GetExtension(file.FileName).ToLower();
            if (file_extention != ".mpo" && file_extention != ".jpg")
                return View(picture);

            db.Picture.Add(picture);
            db.SaveChanges();

            // зберігаю зображення
            string picture_folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Picture");
            string picture_name = picture.id.ToString() + Path.GetExtension(file.FileName);
            string picture_folder_name = Path.Combine(picture_folder, picture_name);
            file.SaveAs(picture_folder_name);

            picture.path = Path.Combine("Picture", picture_name); // записую відносний шлях в обєкт бази даних

            // отримую всі зображення з файлу
            var images = MpoParser.GetImageSources(picture_folder_name);
            Image img_for_thumb;
            if (images.Count() == 0) // якщо 2D
            {
                img_for_thumb = Image.FromFile(picture_folder_name);
                picture.type = "2D";
            }
            else // якщо 3D
            {
                img_for_thumb = images.ElementAt(0); // беру перше зображення (з лівої камери)
                img_for_thumb.Save(Path.ChangeExtension(picture_folder_name, ".JPG")); // зберігаю зображення, з якого буду робити превю

                // змінюю формат оригіналу на .mpo (на сервер заавжди приходить зображення формату JPG)
                picture_name = Path.ChangeExtension(picture_name, ".MPO");
                file.SaveAs(Path.Combine(picture_folder, picture_name));
                picture.path = Path.Combine("Picture", picture_name);
                picture.type = "3D";
            }

            var original_length = PictureTools.getByteSize(img_for_thumb).LongLength;
            // створюю прев'ю
            var thumb_sm = PictureTools.MakeThumbnail(img_for_thumb, 155, 97);
            var thumb_sm_length = PictureTools.getByteSize(thumb_sm).LongLength;
            if (original_length > thumb_sm_length)
            {
                thumb_sm.Save($"{picture_folder}/{picture.id}-thumb_sm.JPG");
            }

            var thumb_md = PictureTools.MakeThumbnail(img_for_thumb, 280, 999);
            var thumb_md_length = PictureTools.getByteSize(thumb_md).LongLength;
            if (original_length > thumb_md_length)
            {
                thumb_md.Save($"{picture_folder}/{picture.id}-thumb_md.JPG");
                if (Path.GetExtension(picture.path) == ".MPO")
                    System.IO.File.Delete(picture_folder_name); // видаляю непотрібний файл
            }

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
            var result = db.Picture.Include(p => p.Gallery).ToList();
            var userdb = db.User.FirstOrDefault(x => x.login == user);

            if (gallery != null)
                result = result.Where(x => x.galleryId == gallery).ToList();
            if (user_likes && user != null)
                result = result.Where(x => x.User.Contains(userdb)).ToList();
            else if (user != null)
                result = result.Where(x => x.Gallery.User.login == user).ToList();

            switch (filter)
            {
                case "new":
                    result = result.OrderByDescending(x => x.id).ToList();
                    break;
                case "old":
                    result = result.OrderBy(x => x.id).ToList();
                    break;
                case "best":
                    result = result.OrderByDescending(x => x.User.Count).ThenByDescending(x => x.id).ToList();
                    break;
                case "3d":
                    result = result.Where(x => x.type == "3D").OrderByDescending(x => x.id).ToList();
                    break;
                case "2d":
                    result = result.Where(x => x.type == "2D").OrderByDescending(x => x.id).ToList();
                    break;
                default:
                    break;
            }

            int count = result.Count;
            int show_items = (Request.UserAgent.Contains("Nintendo 3DS")) ? pictures3ds : picturesPc;
            int pages = count / show_items + ((count % show_items == 0) ? 0 : 1);
            ViewBag.Pages = pages;
            ViewBag.Page = page;
            return PartialView(result.Skip((page - 1) * show_items).Take(show_items).ToList());
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
