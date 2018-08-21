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
using _3dsGallery.WebUI.Code;

namespace _3dsGallery.WebUI.Controllers
{
    [RoutePrefix("Gallery")]
    public class GalleryController : Controller
    {
        private readonly GalleryContext db = new GalleryContext();
        
        [Route("ShowPage")]
        public ActionResult ShowPage(string user, int page = 1, string filter = "updated")
        {
            bool is3ds = Request.UserAgent.Contains("Nintendo 3DS");
            GalleryPageData pageData = new PageData(page, filter, is3ds).GetGalleriesByPage(user);            
            ViewBag.Page = page;
            ViewBag.Pages = pageData.TotalPages;
            ViewBag.Filter = filter;

            return PartialView(pageData.Galleries);
        }

        // GET: Gallery
        public ActionResult Index(int page = 1, string filter = "updated")
        {
            bool is3ds = Request.UserAgent.Contains("Nintendo 3DS");
            GalleryPageData pageData = new PageData(page, filter, is3ds).GetGalleriesByPage();
            ViewBag.Page = page;
            ViewBag.Pages = pageData.TotalPages;
            ViewBag.Filter = filter;

            return View(pageData.Galleries);
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

            bool is3ds = Request.UserAgent.Contains("Nintendo 3DS");
            PicturePageData pageData = new PageData(page, filter, is3ds).GetPictruresByPage(id);
            ViewBag.Page = page;
            ViewBag.Filter = filter;            
            ViewBag.Pages = pageData.TotalPages;

            var modelResult = new GalleryDetailsView
            {
                Gallery = gallery,
                PicturePageData = pageData.Pictures
            };
            return View(modelResult);
        }

        [Only3DS]
        [Authorize]
        [Route("{id}/AddPicture")]
        public ActionResult AddPicture(short? id)
        {
            if (id == null || !IsItMine(id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            AddPictureModel picture = new AddPictureModel { Gallery = db.Gallery.Find(id) };
            return View(picture);
        }

        [Only3DS]
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("{id}/AddPicture")]
        public ActionResult AddPicture(AddPictureModel model, short id, HttpPostedFileBase file)
        {
            if (!IsItMine(id))
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            model.Gallery = db.Gallery.Find(id);
            if (!ModelState.IsValid || file == null)
                return View(model);

            if (file.ContentLength > 750 * 1000)
                return View(model);

            string file_extention = Path.GetExtension(file.FileName).ToLower();
            if (file_extention != ".mpo" && file_extention != ".jpg")
                return View(model);
            
            Picture picture = new Picture
            {
                description = model.description,
                Gallery= model.Gallery
            };
            db.Picture.Add(picture);
            db.SaveChanges();

            picture = new PictureSaver(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Picture")).AnalyzeAndSave(picture, model, file);

            picture.Gallery.LastPicture = picture;
            db.Entry(picture).State = EntityState.Modified;
            db.SaveChanges();

            return RedirectToAction("Details", new { id = id });
        }

        // GET: Gallery/Create
        [Only3DS]
        [Authorize]
        [Route("Create")]
        public ActionResult Create()
        {
            ViewBag.styleId = new SelectList(db.Style, "id", "name");
            return View();
        }

        // POST: Gallery/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [Only3DS]
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Create")]
        public ActionResult Create([Bind(Include = "id,name,info,styleId")] Gallery gallery)
        {
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
