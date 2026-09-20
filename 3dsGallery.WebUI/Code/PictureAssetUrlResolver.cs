using _3dsGallery.DataLayer.DataBase;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace _3dsGallery.WebUI.Code
{
    public class PictureAssetUrlResolver
    {
        private readonly string _siteRootPath;
        private readonly IImageKitClient _imageKitClient;

        public PictureAssetUrlResolver(string siteRootPath)
            : this(siteRootPath, TryCreateClient())
        {
        }

        public PictureAssetUrlResolver(string siteRootPath, IImageKitClient imageKitClient)
        {
            _siteRootPath = siteRootPath;
            _imageKitClient = imageKitClient;
        }

        public bool IsRemoteActive(Picture picture)
        {
            if (picture == null)
                return false;

            if (!string.Equals(picture.StorageProvider, PictureStorageConstants.StorageProviderImageKit, StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.IsNullOrWhiteSpace(picture.OriginalRemotePath))
                return false;

            if (string.Equals(picture.StorageMigrationStatus, PictureStorageConstants.MigrationStatusRemotePending, StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
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
            {
                if (_imageKitClient == null)
                    throw new InvalidOperationException("ImageKit is not configured. Set ImageKitPrivateKey and ImageKitUrlEndpoint before serving remote picture assets.");

                if (string.Equals(size, PictureStorageConstants.PreviewSizeSmall, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(picture.ThumbnailSmallRemotePath))
                    return _imageKitClient.BuildDeliveryUrl(picture.ThumbnailSmallRemotePath);
                if (string.Equals(size, PictureStorageConstants.PreviewSizeMedium, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(picture.ThumbnailMediumRemotePath))
                    return _imageKitClient.BuildDeliveryUrl(picture.ThumbnailMediumRemotePath);
                if (!string.IsNullOrWhiteSpace(picture.PreviewRemotePath))
                    return _imageKitClient.BuildDeliveryUrl(picture.PreviewRemotePath);
                return GetRemoteOriginalUrl(picture);
            }

            return GetLocalPreviewVirtualPath(picture, size);
        }

        public string ResolveOriginalRedirectUrl(Picture picture)
        {
            if (picture == null)
                return null;

            if (IsRemoteActive(picture))
            {
                if (_imageKitClient == null)
                    throw new InvalidOperationException("ImageKit is not configured. Set ImageKitPrivateKey and ImageKitUrlEndpoint before serving remote picture assets.");

                return GetRemoteOriginalUrl(picture);
            }

            return GetLocalOriginalVirtualPath(picture);
        }

        public string GetRemoteOriginalUrl(Picture picture)
        {
            if (picture == null || string.IsNullOrWhiteSpace(picture.OriginalRemotePath))
                return null;

            if (_imageKitClient == null)
                throw new InvalidOperationException("ImageKit is not configured. Set ImageKitPrivateKey and ImageKitUrlEndpoint before serving remote picture assets.");

            var url = _imageKitClient.BuildDeliveryUrl(picture.OriginalRemotePath);
            return EnsureRawMpoUrl(url);
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

        private static IImageKitClient TryCreateClient()
        {
            try
            {
                return new ImageKitClient();
            }
            catch
            {
                return null;
            }
        }
    }
}
