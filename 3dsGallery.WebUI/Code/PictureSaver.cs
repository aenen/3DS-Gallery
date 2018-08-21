using _3dsGallery.DataLayer.DataBase;
using _3dsGallery.DataLayer.Tools;
using _3dsGallery.WebUI.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Code
{
    public class PictureSaver
    {
        private readonly string picture_folder;

        public PictureSaver(string picture_folder)
        {
            this.picture_folder = picture_folder;
        }

        public Picture AnalyzeAndSave(Picture picture, AddPictureModel model, HttpPostedFileBase file)
        {
            // зберігаю зображення
            string picture_name = picture.id.ToString() + Path.GetExtension(file.FileName);
            string picture_folder_name = Path.Combine(picture_folder, picture_name);
            file.SaveAs(picture_folder_name);

            picture.path = Path.Combine("Picture", picture_name); // записую відносний шлях в обєкт бази даних

            // отримую всі зображення з файлу
            var images = MpoParser.GetImageSources(picture_folder_name);
            Image img_for_thumb;
            if (!images.Any()) // якщо 2D
            {
                img_for_thumb = Image.FromFile(picture_folder_name);
                picture.type = "2D";
            }
            else // якщо 3D
            {
                if (model.isAdvanced && model.isTo2d) // якщо юзер хоче зберегти 3D зображення в 2D
                {
                    img_for_thumb = images.ElementAt(model.leftOrRight);
                    picture_name = Path.ChangeExtension(picture_name, ".JPG");
                    picture.type = "2D";
                    System.IO.File.Delete(picture_folder_name); // видаляю непотрібний файл
                }
                else
                {
                    img_for_thumb = images.ElementAt(0); // беру перше зображення (з лівої камери)

                    // змінюю формат оригіналу на .mpo (на сервер заавжди приходить зображення формату JPG)
                    picture_name = Path.ChangeExtension(picture_name, ".MPO");
                    file.SaveAs(Path.Combine(picture_folder, picture_name));
                    picture.type = "3D";
                }
                img_for_thumb.Save(Path.ChangeExtension(picture_folder_name, ".JPG")); // зберігаю зображення, з якого буду робити прев'ю
                picture.path = Path.Combine("Picture", picture_name);
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

            return picture;

        }
    }
}