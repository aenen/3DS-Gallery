using _3dsGallery.DataLayer.DataBase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;

namespace _3dsGallery.WebUI.Controllers
{
    public class NotificationController : Controller
    {
        private GalleryContext db = new GalleryContext();

        [ChildActionOnly]
        public ActionResult Counter()
        {
            if (!User.Identity.IsAuthenticated) return Content("");

            var currentUser = db.User.FirstOrDefault(u => u.login == User.Identity.Name);
            int notifCount = db.Notification
                .Count(n => n.IdUser == currentUser.id && !n.IsRead);

            return PartialView("_NotificationCounter", notifCount > 99 ? ":D" : notifCount.ToString());
        }

        [Authorize]
        public ActionResult Index()
        {
            var currentUser = db.User.FirstOrDefault(u => u.login == User.Identity.Name);
            if (currentUser == null) return HttpNotFound();

            var notifications = db.Notification
                .Where(n => n.IdUser == currentUser.id)
                .OrderByDescending(n => n.CreationDate)
                .ToList();

            return View(notifications);
        }

        [Authorize]
        [HttpPost]
        public ActionResult MarkAllAsRead()
        {
            var currentUser = db.User.FirstOrDefault(u => u.login == User.Identity.Name);
            if (currentUser == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var unread = db.Notification.Where(n => n.IdUser == currentUser.id && !n.IsRead);
            foreach (var n in unread) n.IsRead = true;
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        [Authorize]
        [HttpPost]
        public ActionResult Purge()
        {
            var currentUser = db.User.FirstOrDefault(u => u.login == User.Identity.Name);
            if (currentUser == null) return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var all = db.Notification.Where(n => n.IdUser == currentUser.id);
            db.Notification.RemoveRange(all);
            db.SaveChanges();

            return RedirectToAction("Index");
        }

        [Authorize]
        public ActionResult Open(int id)
        {
            var currentUser = db.User.FirstOrDefault(u => u.login == User.Identity.Name);
            if (currentUser == null) return HttpNotFound();

            var notif = db.Notification.FirstOrDefault(n => n.IdNotification == id && n.IdUser == currentUser.id);
            if (notif == null) return HttpNotFound();

            notif.IsRead = true;
            db.SaveChanges();

            return RedirectToAction("Details", "Picture", new { id = notif.RelatedEntityId }); // поки тут пікча айді. з іншими сповіщеннями доведеться зарефакторити а поки норм
        }

    }
}