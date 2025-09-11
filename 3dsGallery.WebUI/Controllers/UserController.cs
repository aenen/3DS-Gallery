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

        // GET: User
        [Only3DS]
        [Route("Register")]
        public ActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("MyProfile");
            }

            return View(new RegisterView());
        }

        [Only3DS]
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Register")]
        public ActionResult Register(RegisterView model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }
            if (!IsUsername(model.Login))
            {
                ModelState.AddModelError(string.Empty, "Login can only contains ENG characters, '_' symbol and digits.");
                return View(model);
            }
            if (db.User.Any(x => x.login == model.Login))
            {
                ModelState.AddModelError(string.Empty, "User with this login already exists. Please choose another login.");
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
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("MyProfile");
            }

            return View(new LoginView());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Route("Login")]
        public ActionResult Login(LoginView model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = db.User.FirstOrDefault(x => x.login == model.Login);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "The entered login or password is incorrect. Please try again.");
                return View(model);
            }
            var pass = PasswordGenerator.GenerateHash(model.Password, user.PasswordSalt, user.Iterations, 20);
            if (user.PasswordHash.SequenceEqual(pass))
            {
                FormsAuthentication.RedirectFromLoginPage(user.login, true);
            }

            ModelState.AddModelError(string.Empty, "The entered login or password is incorrect. Please try again.");
            return View(model);
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

            bool is3ds = Request.UserAgent.Contains("Nintendo 3DS");
            GalleryPageData pageData = new PageData(page, filter, is3ds).GetGalleriesByPage(login);
            ViewBag.Page = page;
            ViewBag.Pages = pageData.TotalPages;
            ViewBag.Filter = filter;
            ViewBag.Login = user.login;

            return View(pageData.Galleries);
        }

        [Route("User/{login}/Pictures")]
        public ActionResult Pictures(string login, int page = 1, string filter = "new")
        {
            var user = db.User.Where(x => x.login == login).FirstOrDefault();
            if (user == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            bool is3ds = Request.UserAgent.Contains("Nintendo 3DS");
            PicturePageData pageData = new PageData(page, filter, is3ds).GetPictruresByPage(user: login);
            ViewBag.Page = page;
            ViewBag.Filter = filter;
            ViewBag.Pages = pageData.TotalPages;
            ViewBag.Login = user.login;

            return View(pageData.Pictures);
        }

        [Route("User/{login}/Likes")]
        public ActionResult Likes(string login, int page = 1, string filter = "new")
        {
            var user = db.User.FirstOrDefault(x => x.login == login);
            if (user == null)
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);

            bool is3ds = Request.UserAgent.Contains("Nintendo 3DS");
            PicturePageData pageData = new PageData(page, filter, is3ds).GetPictruresByPage(user: login,user_likes: true);
            ViewBag.Page = page;
            ViewBag.Filter = filter;
            ViewBag.Pages = pageData.TotalPages;
            ViewBag.Login = user.login;

            return View(pageData.Pictures);
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