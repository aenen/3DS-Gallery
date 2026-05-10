using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Models
{
    public class PictureModel
    {
        public int IdPicture { get; set; }
        public short IdGallery { get; set; }
        public string PictureDescription { get; set; }
        public string Path { get; set; }
        public string ColorThemeName { get; set; }
        public string ColorThemeClass { get; set; }
        public int LikeCount { get; set; }
        public bool Is3D { get; set; }
        public bool IsLikedByMe { get; set; }
        public string CreatedBy { get; set; }
        public DateTime? CreationDate { get; set; }
    }

    public class PictureDetailsModel
    {
        public PictureModel Pic { get; set; }

        public List<string> Comments { get; set; } = new List<string>
        {
            "test", "comment", "i eat ass", "67", @"According to all known laws
of aviation,
there is no way a bee
should be able to fly.
Its wings are too small to get
its fat little body off the ground.
The bee, of course, flies anyway
because bees don't care
what humans think is impossible.
Yellow, black. Yellow, black.
Yellow, black. Yellow, black.
Ooh, black and yellow!
Let's shake it up a little.
Barry! Breakfast is ready!
Ooming!
Hang on a second.
Hello?
- Barry?
- Adam?
- Oan you believe this is happening?
- I can't. I'll pick you up.
"
        };
    }
}