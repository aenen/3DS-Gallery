using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace _3dsGallery.WebUI.Code
{
    public interface ICloudinaryService
    {
        /// <summary>Uploads image bytes with the given public_id and returns the confirmed public_id.</summary>
        string Upload(byte[] imageBytes, string publicId);

        /// <summary>Deletes a Cloudinary image resource by its public_id. No-ops if publicId is null/empty.</summary>
        void Delete(string publicId);

        /// <summary>Downloads raw bytes from a URL.</summary>
        byte[] Download(string url);

        /// <summary>Returns the canonical Cloudinary URL for a full-size image identified by publicId.</summary>
        string GetImageUrl(string publicId);

        /// <summary>
        /// Returns a Cloudinary transformation URL for a thumbnail.
        /// If height is 0 the transformation is width-only; otherwise width+height with crop=fill.
        /// </summary>
        string GetThumbnailUrl(string publicId, int width, int height = 0);
    }

    public class CloudinaryService : ICloudinaryService
    {
        private readonly string _cloudName;
        private readonly string _apiKey;
        private readonly string _apiSecret;

        public CloudinaryService()
        {
            _cloudName = ConfigurationManager.AppSettings["CloudinaryCloudName"];
            _apiKey    = ConfigurationManager.AppSettings["CloudinaryApiKey"];
            _apiSecret = ConfigurationManager.AppSettings["CloudinaryApiSecret"];
        }

        public string Upload(byte[] imageBytes, string publicId)
        {
            var timestamp    = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var paramsToSign = $"overwrite=true&public_id={publicId}&timestamp={timestamp}{_apiSecret}";
            var signature    = ComputeSha1(paramsToSign);

            var boundary = "---boundary" + Guid.NewGuid().ToString("N");
            var body = BuildMultipartBody(boundary,
                new[]
                {
                    ("api_key",   _apiKey),
                    ("timestamp", timestamp),
                    ("signature", signature),
                    ("public_id", publicId),
                    ("overwrite", "true"),
                },
                imageBytes, "file", "image.jpg");

            using (var client = new WebClient())
            {
                client.Headers.Add("Content-Type", $"multipart/form-data; boundary={boundary}");
                var url      = $"https://api.cloudinary.com/v1_1/{_cloudName}/image/upload";
                var response = Encoding.UTF8.GetString(client.UploadData(url, "POST", body));
                return ExtractJsonStringValue(response, "public_id") ?? publicId;
            }
        }

        public void Delete(string publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return;

            var timestamp    = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var paramsToSign = $"public_id={publicId}&timestamp={timestamp}{_apiSecret}";
            var signature    = ComputeSha1(paramsToSign);

            using (var client = new WebClient())
            {
                var url = $"https://api.cloudinary.com/v1_1/{_cloudName}/image/destroy";
                client.UploadValues(url, new NameValueCollection
                {
                    ["api_key"]   = _apiKey,
                    ["timestamp"] = timestamp,
                    ["signature"] = signature,
                    ["public_id"] = publicId,
                });
            }
        }

        public byte[] Download(string url)
        {
            using (var client = new WebClient())
                return client.DownloadData(url);
        }

        public string GetImageUrl(string publicId)
            => $"https://res.cloudinary.com/{_cloudName}/image/upload/{publicId}";

        public string GetThumbnailUrl(string publicId, int width, int height = 0)
        {
            var transform = height > 0
                ? $"w_{width},h_{height},c_fill"
                : $"w_{width}";
            return $"https://res.cloudinary.com/{_cloudName}/image/upload/{transform}/{publicId}";
        }

        // ── helpers ────────────────────────────────────────────────────────────

        private static string ComputeSha1(string input)
        {
            using (var sha = SHA1.Create())
            {
                var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
                var sb   = new StringBuilder(hash.Length * 2);
                foreach (var b in hash)
                    sb.AppendFormat("{0:x2}", b);
                return sb.ToString();
            }
        }

        private static byte[] BuildMultipartBody(string boundary,
            (string name, string value)[] fields,
            byte[] file, string fileFieldName, string fileName)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var (name, value) in fields)
                {
                    var part = Encoding.UTF8.GetBytes(
                        $"--{boundary}\r\nContent-Disposition: form-data; name=\"{name}\"\r\n\r\n{value}\r\n");
                    ms.Write(part, 0, part.Length);
                }

                var fileHeader = Encoding.UTF8.GetBytes(
                    $"--{boundary}\r\nContent-Disposition: form-data; name=\"{fileFieldName}\"; " +
                    $"filename=\"{fileName}\"\r\nContent-Type: image/jpeg\r\n\r\n");
                ms.Write(fileHeader, 0, fileHeader.Length);
                ms.Write(file, 0, file.Length);

                var footer = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
                ms.Write(footer, 0, footer.Length);

                return ms.ToArray();
            }
        }

        private static string ExtractJsonStringValue(string json, string key)
        {
            var search = $"\"{key}\":\"";
            var start  = json.IndexOf(search, StringComparison.Ordinal);
            if (start < 0) return null;
            start += search.Length;
            var end = json.IndexOf('"', start);
            return end < 0 ? null : json.Substring(start, end - start);
        }
    }
}
