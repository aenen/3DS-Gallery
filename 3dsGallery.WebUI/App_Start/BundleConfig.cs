using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Optimization;

namespace _3dsGallery.WebUI
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js"));

            bundles.Add(new ScriptBundle("~/bundles/paginationFor3ds").Include(
                      "~/Scripts/paginationFor3ds.js",
                      "~/Scripts/initPaginationFor3ds.js"));

            bundles.Add(new ScriptBundle("~/bundles/paginationAjax").Include(
                      "~/Scripts/paginationAjax.js",
                      "~/Scripts/initPaginationAjax.js"));

            bundles.Add(new StyleBundle("~/Content/maincss").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/Site.css",
                      "~/Content/GalleryStyle.css"));

            bundles.Add(new StyleBundle("~/Content/desktopfonts").Include(
                      "~/Content/Fonts.css"));

            bundles.Add(new StyleBundle("~/Content/3dsfonts").Include(
                      "~/Content/Fonts3ds.css"));

            bundles.Add(new StyleBundle("~/Content/pagination").Include(
                      "~/Content/pagination.css"));

            BundleTable.EnableOptimizations = true;
        }
    }
}