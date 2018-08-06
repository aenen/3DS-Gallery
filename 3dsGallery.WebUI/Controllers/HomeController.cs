using _3dsGallery.DataLayer.DataBase;
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
            return View(db);
        }
    }
}