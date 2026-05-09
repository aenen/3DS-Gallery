using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Code
{
    public static class MessageProvider
    {
        private static readonly Random _rand = new Random();

        // Constant messages grouped by category
        public static readonly Dictionary<string, string[]> Messages = new Dictionary<string, string[]>
        {
            ["common"] = new[]
            {
                "Share your pics straight from a Nintendo <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span> Internet Browser!",
                "Publish your lo-fi 0.3MP photos using a Nintendo <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span> Internet Browser",
                "Upload your images online using only Nintendo <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span> Internet Browser",
                "Developed for Nintendo <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span> Internet Browser",
                "No need to take out an SD card to transfer some pics. Just Nintendo <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span> Internet Browser... and a Wi-Fi connection",
                "Feel free to share what you draw in Colors3D, ArtAcademy, or any other apps",
                "Got some funny shots from Tomodachi Life or ACNL? Share them here!",
                "Commenting is coming (most likely) soon.",
                "A new juicy theme \"Watermelon Red\" is here. Use it for your <span class=\"kawaii\">GALLERY</span>!",
                "Browsing from your phone? Check the new home page section \"See 3D Without a <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span>\"!",
                "You may see a strange/unique message here. Means it's rare",
                "You can always access <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span> GALLERY</span> from your phone to save and share your uploads"
            },
            ["uncommon"] = new[]
            {
                "Press that button below. I know you want to.",
                "Private Galleries are now available to use",
                "Press the 3D icon in the top-right corner of an image to open it in side-by-side mode. You can \"parallel-view\" to see it im 3D. Even on a 2DS.",
                "You can now publish up to 5 pics at ones. Could be useful for bulky uploads.",
                "Use the context menu (...) on the image. Good for quickly download it, or to open gallery/profile.",
                "This new \"Upload & Add More\" button allows you to publish pics faster (kinda)",
                "Every image here is clickable! And if you press it using a <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span> - it might open in a glorious 3D",
                "Use a 3D indicator on the images and galleries to locate some stereoscopic goodness",
                "3D pictures are saved in its original format, and you can view them in 3D on a <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span>, VR, 3D Monitors",
                "Use the Filter feature on the top-right corner where there are pictures or galleries.",
                "<span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span> GALLERY</span> does not have any ads or tracking scripts that steals your data. That's cool, huh?",
                "Type up to 150 symbols for a PIC/<span class=\"kawaii\">GALLERY</span> description. You can also do it later from ur phone",
                "The total amount of likes you earned is shown next to your nickname. It doesn't affect anything tho.",
                "No need to enter your Email or any personal info while creating an account. Feel free to sign up if you haven't already.",
                "See a lovely pic? Give it a like! Go to your profile page to view it later",
                "Enjoying the colors of this section? It's called //style_name//, and you can use it for your <span class=\"kawaii\">GALLERY</span>!"
            },
            ["rare"] = new[]
            {
                "The messages you see here are random, and this one is Rare! People say only the chosen one can pull The Legendary message... Could it be you?",
                "Nintendo <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span> was released //2011-02-26// ago. Feeling old yet?",
                "EShop was closed //2023-03-27// ago",
                "I didn't feed my Nintendog for //2021-12-22//",
                "Shiggy was born //1952-11-16// ago",
                "3DS Gallery became public to use //2025-05-24// ago",
                "Hidden indie gem Celeste came out //2018-01-25// ago",
                "Masahiro Sakurai was born //1970-08-03// ago. He once said he only drinks Coke Zero since he dislikes water. Elixir of youth? Probably not lmao",
                "Hideo Kojima was born //1963-08-24// ago. His game Metal Gear Solid 3 came out on a <span class=\"kawaii\"><span class=\"text-3ds-red\">3</span><span class=\"text-3ds-dark\">DS</span></span> and it features an in-game Camera that u can use",
                "Satoru Iwata passed away //2015-07-11// ago. The console you're holding came out under his supervision",
                "You love Pokemon, huh? Then upload all 802 of them from USUM. Don't forget the shiny ones",
                "I bought lime, it was green lemon",
                "Sometimes cocojumbo means something more",
                "Funny rabbits with bloody eyes on balloons",
                "The universe was born 13.8 billion years ago",
                "Play Kid Icarus Uprising, you won't regret it. Trust.",
                "More updates are coming: more color themes, comments, profile personalization, QOL improvements",
                "This site was made using now old ASP.NET MVC tech, along with HTML, CSS, JS, JQuery, BootStrap, C#, EntityFramework. Hosted on a free Azure service"
            },
            ["legendary"] = new[]
            {
                "WOW! You got The Legendary message! Chances were 1 in 1000 (0.1%). It could be your lucky day! As a reward - email me a screenshot of this, and write your own message so i will include it to show here! Your Msg could be anything funny or random, just no ads/nsfw ofc."
            }
        };

        // Replace date markers with difference
        public static string ReplaceDateWithDiff(string input)
        {
            var match = System.Text.RegularExpressions.Regex.Match(input, @"\/\/(\d{4}-\d{2}-\d{2})\/\/");
            if (!match.Success) return input;

            var releaseDate = DateTime.Parse(match.Groups[1].Value);
            var today = DateTime.Today;

            var diffDays = (today - releaseDate).Days;

            int years = diffDays / 365;
            diffDays -= years * 365;

            int months = diffDays / 30;
            diffDays -= months * 30;

            var parts = new List<string>();
            if (years > 0) parts.Add($"{years}y");
            if (months > 0) parts.Add($"{months}m");
            if (diffDays > 0) parts.Add($"{diffDays}d");

            string result;
            if (parts.Count == 1)
            {
                result = parts[0];
            }
            else if (parts.Count == 2)
            {
                result = parts[0] + " and " + parts[1];
            }
            else if (parts.Count == 3)
            {
                result = parts[0] + ", " + parts[1] + " and " + parts[2];
            }
            else
            {
                result = "";
            }

            return System.Text.RegularExpressions.Regex.Replace(input, @"\/\/\d{4}-\d{2}-\d{2}\/\/", result);
        }


        // Weighted random category
        private static string GetRandomCategory()
        {
            var categories = new[]
            {
            new { Type = "common", Weight = 0.7 },
            new { Type = "uncommon", Weight = 0.2 },
            new { Type = "rare", Weight = 0.099 },
            new { Type = "legendary", Weight = 0.001 }
        };

            double randValue = _rand.NextDouble();
            double cumulative = 0;

            foreach (var cat in categories)
            {
                cumulative += cat.Weight;
                if (randValue < cumulative)
                    return cat.Type;
            }

            return "common"; // fallback
        }

        public static string GetRandomMessage()
        {
            var category = GetRandomCategory();
            var options = Messages[category];
            return options[_rand.Next(options.Length)];
        }
    }

}