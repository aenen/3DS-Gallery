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
using System.Drawing;
using _3dsGallery.DataLayer.Tools;
using _3dsGallery.WebUI.Models;
using System.Data.Entity.Migrations;

namespace _3dsGallery.WebUI.Controllers
{
    [RoutePrefix("Gallery")]
    public class GalleryController : Controller
    {
        private readonly GalleryContext db = new GalleryContext();
        private const int gallery3ds = 5;
        private const int galleryPc = 12;
        private const int pictures3ds = 10;
        private const int picturesPc = 20;

        [Route("ShowPage")]
        public ActionResult ShowPage(string user, int page = 1, string filter = "updated")
        {
            var result = db.Gallery.Include(g => g.Style).Include(g => g.User).ToList();

            switch (filter)
            {
                case "updated":
                    result = result.Where(x=>x.LastPicture!=null).OrderByDescending(x => x.LastPicture.id).ToList();
                    break;
                case "new":
                    result = result.OrderByDescending(x => x.id).ToList();
                    break;
                case "old":
                    result = result.OrderBy(x => x.id).ToList();
                    break;
                case "best":
                    result = result.OrderByDescending(x => x.Picture.Sum(xx => xx.User.Count)).ToList();
                    break;
                case "big":
                    result = result.OrderByDescending(x => x.Picture.Count).ToList();
                    break;
                case "3d":
                    result = result.Where(x => x.Picture.Any(xx => xx.type == "3D")).ToList();
                    break;
                default:
                    break;
            }

            int count = result.Count;
            int show_items = (Request.UserAgent.Contains("Nintendo 3DS")) ? gallery3ds : galleryPc;
            int pages = count / show_items + ((count % show_items == 0) ? 0 : 1);

            ViewBag.Pages = pages;
            ViewBag.Page = page;

            return PartialView(result.Skip((page - 1) * show_items).Take(show_items).ToList());

        }

        // GET: Gallery
        public ActionResult Index(int page = 1, string filter = "updated")
        {
            ViewBag.Page = page;
            ViewBag.Filter = filter;
            return View();
        }

        // GET: Gallery/Details/5
        [Route("{id}")]
        public ActionResult Details(short? id, int page = 1, string filter = "new")
        {
            if (id == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Gallery gallery = db.Gallery.Find(id);
            if (gallery == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            ViewBag.Page = page;
            ViewBag.Filter = filter;

            var result = db.Gallery.FirstOrDefault(x=>x.id==id).Picture.ToList();
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

            return View(gallery);
        }

        [Authorize]
        [Route("{id}/AddPicture")]
        public ActionResult AddPicture(short? id)
        {
            if (!Request.UserAgent.Contains("Nintendo 3DS"))
                return RedirectToAction("Not3ds", "User");
            if (id == null || !IsItMine(id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Picture picture = new Picture { Gallery = db.Gallery.Find(id) };
            return View(picture);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("{id}/AddPicture")]
        public ActionResult AddPicture([Bind(Include = "id,description,path,galleryId")] Picture picture, short id, HttpPostedFileBase file)
        {
            if (!Request.UserAgent.Contains("Nintendo 3DS"))
                return RedirectToAction("Not3ds", "User");
            if (!IsItMine(id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            picture.Gallery = db.Gallery.Find(id);
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

            return RedirectToAction("Details", new { id = id });
        }

        // GET: Gallery/Create
        [Authorize]
        [Route("Create")]
        public ActionResult Create()
        {
            if (!Request.UserAgent.Contains("Nintendo 3DS"))
                return RedirectToAction("Not3ds", "User");

            ViewBag.styleId = new SelectList(db.Style, "id", "name");
            return View();
        }

        // POST: Gallery/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Create")]
        public ActionResult Create([Bind(Include = "id,name,info,styleId")] Gallery gallery)
        {
            if (!Request.UserAgent.Contains("Nintendo 3DS"))
                return RedirectToAction("Not3ds", "User");

            if (ModelState.IsValid)
            {
                gallery.User = db.User.FirstOrDefault(x => x.login == User.Identity.Name);
                db.Gallery.Add(gallery);
                db.SaveChanges();
                return RedirectToAction("MyProfile", "User");
            }

            ViewBag.styleId = new SelectList(db.Style, "id", "name", gallery.styleId);
            return View(gallery);
        }

        // GET: Gallery/Edit/5
        [Authorize]
        [Route("{id}/Edit")]
        public ActionResult Edit(short? id)
        {
            if (id == null || !IsItMine(id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Gallery gallery = db.Gallery.Find(id);
            if (gallery == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            ViewBag.styleId = new SelectList(db.Style, "id", "name", gallery.styleId);
            return View(gallery);
        }

        // POST: Gallery/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("{id}/Edit")]
        public ActionResult Edit([Bind(Include = "id,name,info,styleId")] Gallery gallery, short id)
        {
            if (!IsItMine(id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            if (ModelState.IsValid)
            {
                db.Entry(gallery).State = EntityState.Modified;
                db.Entry(gallery).Property(x => x.userId).IsModified = false;
                db.SaveChanges();
                return RedirectToAction("Details", new { id = id });
            }

            ViewBag.styleId = new SelectList(db.Style, "id", "name", gallery.styleId);
            return View(gallery);
        }

        public ActionResult GetElement(short id)
        {
            var gallery = db.Gallery.Find(id);
            if (gallery == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            return PartialView(gallery);
        }

        public ActionResult GetElements(IEnumerable<Gallery> items)
        {
            if (items == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            return PartialView(items);
        }

        // GET: Gallery/Delete/5
        [Authorize]
        [Route("{id}/Delete")]
        public ActionResult Delete(short? id)
        {
            if (id == null || !IsItMine(id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            Gallery gallery = db.Gallery.Find(id);
            if (gallery == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            return View(gallery);
        }

        // POST: Gallery/Delete/5
        [Authorize]
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Route("{id}/Delete")]
        public ActionResult DeleteConfirmed(short id)
        {
            if (!IsItMine(id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Picture");
            Gallery gallery = db.Gallery.FirstOrDefault(x => x.id == id);
            foreach (var item in gallery.Picture.ToList())
            {
                if (System.IO.File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.path)))
                    System.IO.File.Delete(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.path));
                if (System.IO.File.Exists(Path.Combine(path, $"{item.id}-thumb_sm.JPG")))
                    System.IO.File.Delete(Path.Combine(path, $"{item.id}-thumb_sm.JPG"));
                if (System.IO.File.Exists(Path.Combine(path, $"{item.id}-thumb_md.JPG")))
                    System.IO.File.Delete(Path.Combine(path, $"{item.id}-thumb_md.JPG"));
                if (System.IO.File.Exists(Path.Combine(path, $"{item.id}.JPG")))
                    System.IO.File.Delete(Path.Combine(path, $"{item.id}.JPG"));
                Picture picture = db.Picture.Include(X => X.User).FirstOrDefault(x => x.id == item.id);
                db.Picture.Remove(picture);
            }
            db.Gallery.Remove(gallery);
            db.SaveChanges();
            return RedirectToAction("MyProfile", "User");
        }

        bool IsItMine(short? id)
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
