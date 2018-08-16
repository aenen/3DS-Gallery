using _3dsGallery.DataLayer.DataBase;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;

namespace _3dsGallery.WebUI.Code
{
    public class PageData
    {
        private readonly GalleryContext db = new GalleryContext();
        private const int gallery3ds = 5;
        private const int galleryPc = 12;
        private const int pictures3ds = 10;
        private const int picturesPc = 20;

        public int Page { get; set; }
        public string Filter { get; set; }
        public bool Is3ds { get; set; }

        public PageData(int page, string filter, bool is3ds)
        {
            Page = page;
            Filter = filter;
            Is3ds = is3ds;
        }

        public PicturePageData GetPictruresByPage(int? gallery=null, string user=null, bool user_likes = false)
        {
            var picturesList = db.Picture.Include(p => p.Gallery).AsEnumerable();
            var userdb = db.User.FirstOrDefault(x => x.login == user);

            if (gallery != null)
                picturesList = picturesList.Where(x => x.galleryId == gallery);
            if (user_likes && user != null)
                picturesList = picturesList.Where(x => x.User.Contains(userdb));
            else if (user != null)
                picturesList = picturesList.Where(x => x.Gallery.User.login == user);

            switch (Filter)
            {
                case "new":
                    picturesList = picturesList.OrderByDescending(x => x.id);
                    break;
                case "old":
                    picturesList = picturesList.OrderBy(x => x.id);
                    break;
                case "best":
                    picturesList = picturesList.OrderByDescending(x => x.User.Count).ThenByDescending(x => x.id);
                    break;
                case "3d":
                    picturesList = picturesList.Where(x => x.type == "3D").OrderByDescending(x => x.id);
                    break;
                case "2d":
                    picturesList = picturesList.Where(x => x.type == "2D").OrderByDescending(x => x.id);
                    break;
                default:
                    break;
            }

            int count = picturesList.Count();
            int show_items = Is3ds ? pictures3ds : picturesPc;
            int pages = count / show_items + ((count % show_items == 0) ? 0 : 1);
            picturesList = picturesList.Skip((Page - 1) * show_items).Take(show_items).ToList();

            PicturePageData result = new PicturePageData
            {
                Pictures = picturesList,
                TotalPages = pages
            };
            return result;
        }

        public GalleryPageData GetGalleriesByPage(string user=null)
        {
            IEnumerable<Gallery> galleriesList = db.Gallery.AsEnumerable();
            if (user != null)
                galleriesList = galleriesList.Where(x => x.User.login == user);

            switch (Filter)
            {
                case "updated":
                    galleriesList = galleriesList.Where(x => x.LastPicture != null).OrderByDescending(x => x.LastPicture.id);
                    break;
                case "new":
                    galleriesList = galleriesList.OrderByDescending(x => x.id);
                    break;
                case "old":
                    galleriesList = galleriesList.OrderBy(x => x.id);
                    break;
                case "best":
                    galleriesList = galleriesList.OrderByDescending(x => x.Picture.Sum(xx => xx.User.Count));
                    break;
                case "big":
                    galleriesList = galleriesList.OrderByDescending(x => x.Picture.Count);
                    break;
                case "3d":
                    galleriesList = galleriesList.Where(x => x.Picture.Any(xx => xx.type == "3D"));
                    break;
                default:
                    break;
            }

            int count = galleriesList.Count();
            int show_items = Is3ds ? gallery3ds : galleryPc;
            int pages = count / show_items + ((count % show_items == 0) ? 0 : 1);
            galleriesList = galleriesList.Skip((Page - 1) * show_items).Take(show_items).ToList();

            GalleryPageData result = new GalleryPageData
            {
                Galleries = galleriesList,
                TotalPages = pages
            };

            return result;
        }
    }

    public class PicturePageData
    {
        public PicturePageData()
        {
            Pictures = new List<Picture>();
        }

        public int TotalPages { get; set; }
        public IEnumerable<Picture> Pictures { get; set; }
    }

    public class GalleryPageData
    {
        public GalleryPageData()
        {
            Galleries = new List<Gallery>();
        }

        public int TotalPages { get; set; }
        public IEnumerable<Gallery> Galleries { get; set; }
    }
}