using _3dsGallery.DataLayer.DataBase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace _3dsGallery.WebUI.Code
{
    public class PictureAssetUrlResolver
    {
        private readonly string _siteRootPath;
        private readonly string _deliveryEndpoint;
        private readonly string _uploadFolder;

        public PictureAssetUrlResolver(string siteRootPath)
        {
            _siteRootPath = siteRootPath;

            try
            {
                var configuration = ImageKitConfiguration.LoadFromConfiguration();
                _deliveryEndpoint = string.IsNullOrWhiteSpace(configuration.UrlEndpoint) ? null : configuration.UrlEndpoint.TrimEnd('/');
                _uploadFolder = NormalizeFolder(configuration.UploadFolder);
            }
            catch
            {
                _deliveryEndpoint = null;
                _uploadFolder = NormalizeFolder("/3dsgallery");
            }
        }

        public bool IsRemoteActive(Picture picture)
        {
            return picture != null && !string.IsNullOrWhiteSpace(_deliveryEndpoint) && !string.IsNullOrWhiteSpace(picture.path);
        }

        public string GetOpenUrl(UrlHelper urlHelper, int pictureId)
        {
            return urlHelper.Action("OpenOriginal", "Picture", new { id = pictureId });
        }

        public string GetPreviewUrl(UrlHelper urlHelper, int pictureId, string size)
        {
            return urlHelper.Action("Preview", "Picture", new { id = pictureId, size = size });
        }

        public string ResolvePreviewRedirectUrl(Picture picture, string size)
        {
            if (picture == null)
                return null;

            if (IsRemoteActive(picture))
                return BuildRemotePreviewUrl(picture, size);

            return GetLocalPreviewVirtualPath(picture, size);
        }

        public string ResolveOriginalRedirectUrl(Picture picture)
        {
            if (picture == null)
                return null;

            if (IsRemoteActive(picture))
                return GetRemoteOriginalUrl(picture);

            return GetLocalOriginalVirtualPath(picture);
        }

        public string GetRemoteOriginalUrl(Picture picture)
        {
            if (picture == null || string.IsNullOrWhiteSpace(picture.path) || string.IsNullOrWhiteSpace(_deliveryEndpoint))
                return null;

            return EnsureRawMpoUrl(BuildDeliveryUrl(GetRemoteOriginalPath(picture)));
        }

        public string GetRemoteOriginalPath(Picture picture)
        {
            return picture == null ? null : BuildRemoteStoredPath(picture.path);
        }

        public string GetRemotePreviewPath(Picture picture)
        {
            if (picture == null)
                return null;

            if (string.Equals(picture.type, "3D", StringComparison.OrdinalIgnoreCase))
                return BuildRemoteStoredPath(string.Format("Picture/{0}.JPG", picture.id));

            return BuildRemoteOriginalPath(picture);
        }

        public string GetRemoteThumbnailSmallPath(Picture picture)
        {
            return picture == null ? null : BuildRemoteStoredPath(string.Format("Picture/{0}-thumb_sm.JPG", picture.id));
        }

        public string GetRemoteThumbnailMediumPath(Picture picture)
        {
            return picture == null ? null : BuildRemoteStoredPath(string.Format("Picture/{0}-thumb_md.JPG", picture.id));
        }

        public string GetLocalOriginalPhysicalPath(Picture picture)
        {
            if (picture == null)
                return null;

            var virtualPath = GetLocalOriginalVirtualPath(picture);
            return MapVirtualPathToPhysical(virtualPath);
        }

        public static string EnsureRawMpoUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return url;

            var fragmentIndex = url.IndexOf('#');
            var fragment = fragmentIndex >= 0 ? url.Substring(fragmentIndex) : string.Empty;
            var withoutFragment = fragmentIndex >= 0 ? url.Substring(0, fragmentIndex) : url;

            var queryIndex = withoutFragment.IndexOf('?');
            var baseUrl = queryIndex >= 0 ? withoutFragment.Substring(0, queryIndex) : withoutFragment;
            var query = queryIndex >= 0 ? withoutFragment.Substring(queryIndex + 1) : string.Empty;

            if (!baseUrl.EndsWith(".mpo", StringComparison.OrdinalIgnoreCase))
                return url;

            var pairs = new List<KeyValuePair<string, string>>();
            if (!string.IsNullOrWhiteSpace(query))
            {
                foreach (var part in query.Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var equalsIndex = part.IndexOf('=');
                    if (equalsIndex >= 0)
                        pairs.Add(new KeyValuePair<string, string>(part.Substring(0, equalsIndex), part.Substring(equalsIndex + 1)));
                    else
                        pairs.Add(new KeyValuePair<string, string>(part, string.Empty));
                }
            }

            var transformations = new List<string>();
            var nonTransformPairs = new List<KeyValuePair<string, string>>();
            foreach (var pair in pairs)
            {
                if (string.Equals(pair.Key, "tr", StringComparison.OrdinalIgnoreCase))
                {
                    transformations.AddRange((pair.Value ?? string.Empty)
                        .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(x => x.Trim())
                        .Where(x => !string.IsNullOrWhiteSpace(x)));
                }
                else
                {
                    nonTransformPairs.Add(pair);
                }
            }

            if (!transformations.Any(x => string.Equals(x, "orig-true", StringComparison.OrdinalIgnoreCase)))
                transformations.Insert(0, "orig-true");

            transformations = transformations
                .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .ToList();

            var rebuilt = new List<string>();
            if (transformations.Any())
                rebuilt.Add("tr=" + string.Join(",", transformations));
            rebuilt.AddRange(nonTransformPairs.Select(x => string.IsNullOrWhiteSpace(x.Value) ? x.Key : x.Key + "=" + x.Value));

            return baseUrl + (rebuilt.Any() ? "?" + string.Join("&", rebuilt) : string.Empty) + fragment;
        }

        private string BuildRemotePreviewUrl(Picture picture, string size)
        {
            var remotePath = GetRemotePreviewPath(picture);
            return BuildDeliveryUrl(remotePath);
        }

        private string BuildDeliveryUrl(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(_deliveryEndpoint))
                return null;

            var normalizedPath = filePath.Replace('\\', '/');
            if (!normalizedPath.StartsWith("/"))
                normalizedPath = "/" + normalizedPath;
            return _deliveryEndpoint + normalizedPath;
        }

        private string BuildRemoteStoredPath(string relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            var normalizedRelativePath = relativePath.Replace('\\', '/').TrimStart('/');
            return _uploadFolder + "/" + normalizedRelativePath;
        }

        private string BuildRemoteOriginalPath(Picture picture)
        {
            return GetRemoteOriginalPath(picture);
        }

        private string GetLocalPreviewVirtualPath(Picture picture, string size)
        {
            if (picture == null)
                return null;

            var previewPath = string.Equals(picture.type, "3D", StringComparison.OrdinalIgnoreCase)
                ? string.Format("~/Picture/{0}.JPG", picture.id)
                : GetLocalOriginalVirtualPath(picture);

            if (string.Equals(size, PictureStorageConstants.PreviewSizeSmall, StringComparison.OrdinalIgnoreCase))
            {
                var thumbPath = string.Format("~/Picture/{0}-thumb_sm.JPG", picture.id);
                return FileExists(thumbPath) ? thumbPath : previewPath;
            }

            if (string.Equals(size, PictureStorageConstants.PreviewSizeMedium, StringComparison.OrdinalIgnoreCase))
            {
                var thumbPath = string.Format("~/Picture/{0}-thumb_md.JPG", picture.id);
                return FileExists(thumbPath) ? thumbPath : previewPath;
            }

            return previewPath;
        }

        private string GetLocalOriginalVirtualPath(Picture picture)
        {
            if (picture == null || string.IsNullOrWhiteSpace(picture.path))
                return null;

            return "~/" + picture.path.Replace('\\', '/').TrimStart('/');
        }

        private string MapVirtualPathToPhysical(string virtualPath)
        {
            if (string.IsNullOrWhiteSpace(virtualPath) || string.IsNullOrWhiteSpace(_siteRootPath))
                return null;

            var relative = virtualPath.Replace("~/", string.Empty).Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_siteRootPath, relative);
        }

        private bool FileExists(string virtualPath)
        {
            var physicalPath = MapVirtualPathToPhysical(virtualPath);
            return !string.IsNullOrWhiteSpace(physicalPath) && File.Exists(physicalPath);
        }

        private static string NormalizeFolder(string folderPath)
        {
            var normalized = string.IsNullOrWhiteSpace(folderPath) ? "/3dsgallery" : folderPath.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("/"))
                normalized = "/" + normalized;
            return normalized.TrimEnd('/');
        }
    }
}
