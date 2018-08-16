using _3dsGallery.DataLayer.DataBase;
using _3dsGallery.WebUI.Code;
using _3dsGallery.WebUI.Models;
using Microsoft.AspNet.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace _3dsGallery.WebUI.Controllers
{
    public class UserController : Controller
    {
        readonly GalleryContext db = new GalleryContext();
        private const int gallery3ds = 5;
        private const int galleryPc = 12;
        private const int pictures3ds = 10;
        private const int picturesPc = 20;

        // GET: User
        [Route("Register")]
        public ActionResult Register()
        {
            if (Request.UserAgent.Contains("Nintendo 3DS"))
                return RedirectToAction("Not3ds");
            return View(new RegisterView());
        }

        [HttpPost]
        [Route("Register")]
        public ActionResult Register(RegisterView model)
        {
            if (Request.UserAgent.Contains("Nintendo 3DS"))
                return RedirectToAction("Not3ds");

            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Entered data is not right. Please try again.";
                return View(model);
            }
            if (!IsUsername(model.Login))
            {
                ViewBag.Error = "Login can only contains ENG/RUS characters, '_' symbol and digits.";
                return View(model);
            }
            if (db.User.Any(x => x.login == model.Login))
            {
                ViewBag.Error = "User with this login already exists. Please choose another login.";
                return View(model);
            }

            var salt = PasswordGenerator.GenerateSalt(16);
            var pass = PasswordGenerator.GenerateHash(model.Password, salt, 1000, 20);

            User user = new User
            {
                login = model.Login,
                PasswordSalt=salt,
                PasswordHash=pass,
                Iterations=1000
            };
            db.User.Add(user);
            db.SaveChanges();
            FormsAuthentication.RedirectFromLoginPage(model.Login, true);

            return View(model);
        }

        [Route("Login")]
        public ActionResult Login()
        {
            return View(new LoginView());
        }

        [HttpPost]
        [Route("Login")]
        public ActionResult Login(LoginView model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Error = "Entered data is not right. Please try again.";
                return View();
            }

            var user = db.User.FirstOrDefault(x => x.login == model.Login);
            var pass = PasswordGenerator.GenerateHash(model.Password, user.PasswordSalt, user.Iterations, 20);
            if (user.PasswordHash.SequenceEqual(pass))
            {
                FormsAuthentication.RedirectFromLoginPage(user.login, true);
            }

            ViewBag.Error = "Entered data is not right. Please try again.";
            return View();
        }

        [Authorize]
        [Route("Logout")]
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Index", "Home");
        }
        
        [Route("User/{login}")]
        public ActionResult UserProfile(string login)
        {
            var user = db.User.FirstOrDefault(x => x.login == login);
            if (user==null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            return View(user);
        }

        [Authorize]
        [Route("MyProfile")]
        public ActionResult MyProfile()
        {
            return RedirectToAction("UserProfile", new { login = User.Identity.Name });
        }

        [Route("User/{login}/Galleries")]
        public ActionResult Galleries(string login, int page = 1, string filter = "updated")
        {
            var user = db.User.FirstOrDefault(x => x.login == login);
            if (user == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            var result = user.Gallery.ToList();

            switch (filter)
            {
                case "updated":
                    result = result.Where(x => x.LastPicture != null).OrderByDescending(x => x.LastPicture.id).ToList();
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
            ViewBag.Filter = filter;
            ViewBag.Login = user.login;

            return View(result);
        }

        [Route("User/{login}/Pictures")]
        public ActionResult Pictures(string login, int page = 1, string filter = "new")
        {
            var user = db.User.Where(x => x.login == login).FirstOrDefault();
            if (user == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            
            ViewBag.Page = page;
            ViewBag.Filter = filter;

            var result = user.Gallery.SelectMany(x => x.Picture).ToList();
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

            ViewBag.Login = user.login;
            return View(result);
        }

        [Route("User/{login}/Likes")]
        public ActionResult Likes(string login, int page = 1, string filter = "new")
        {
            var user = db.User.FirstOrDefault(x => x.login == login);
            if (user == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            ViewBag.Page = page;
            ViewBag.Filter = filter;

            var result = user.Picture.ToList();
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

            ViewBag.Login = user.login;
            return View(result);
        }

        [Route("Not3ds")]
        public ActionResult Not3ds()
        {
            return View();
        }
        public static bool IsUsername(string username)
        {
            string pattern = "^[a-zA-Zа-яА-Я0-9_]{1,25}$";
            Regex regex = new Regex(pattern);
            return regex.IsMatch(username);
        }
    }
}