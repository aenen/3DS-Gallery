using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Optimization;

namespace _3dsGallery.WebUI
{
    public class BundleConfig
    {
        // For more information on bundling, visit http://go.microsoft.com/fwlink/?LinkId=301862
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                        "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                        "~/Scripts/jquery.validate*"));

            // Use the development version of Modernizr to develop with and learn from. Then, when you're
            // ready for production, use the build tool at http://modernizr.com to pick only the tests you need.
            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                        "~/Scripts/modernizr-*"));

            bundles.Add(new ScriptBundle("~/bundles/bootstrap").Include(
                      "~/Scripts/bootstrap.js"));

            bundles.Add(new ScriptBundle("~/bundles/paginationFor3ds").Include(
                      "~/Scripts/paginationFor3ds.js"));

            bundles.Add(new ScriptBundle("~/bundles/paginationAjax").Include(
                      "~/Scripts/paginationAjax.js"));

            bundles.Add(new StyleBundle("~/Content/maincss").Include(
                      "~/Content/bootstrap.css",
                      "~/Content/Site.css",
                      "~/Content/GalleryStyle"));

            bundles.Add(new StyleBundle("~/Content/pagination").Include(
                      "~/Content/pagination.css"));

            BundleTable.EnableOptimizations = true;
        }
    }
}