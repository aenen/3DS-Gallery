using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json.Linq;

namespace _3dsGallery.WebUI.Code
{
    public interface ICloudinaryService
    {
        /// <summary>Uploads image bytes with the given public_id and returns the confirmed public_id.</summary>
        string Upload(byte[] imageBytes, string publicId);

        /// <summary>Uploads a raw file with the given public_id and returns the confirmed public_id.</summary>
        string UploadRaw(byte[] fileBytes, string publicId, string fileName, string contentType);

        /// <summary>Deletes a Cloudinary image resource by its public_id. No-ops if publicId is null/empty.</summary>
        void Delete(string publicId);

        /// <summary>Deletes a Cloudinary raw resource by its public_id. No-ops if publicId is null/empty.</summary>
        void DeleteRaw(string publicId);

        /// <summary>Downloads raw bytes from a URL.</summary>
        byte[] Download(string url);

        /// <summary>Returns the canonical Cloudinary URL for a full-size image identified by publicId.</summary>
        string GetImageUrl(string publicId);

        /// <summary>Returns the canonical Cloudinary URL for a raw file identified by publicId and extension.</summary>
        string GetRawUrl(string publicId, string extension);

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
            => Upload("image", imageBytes, publicId, "image.jpg", "image/jpeg");

        public string UploadRaw(byte[] fileBytes, string publicId, string fileName, string contentType)
            => Upload("raw", fileBytes, publicId, fileName, contentType);

        public void Delete(string publicId)
            => Delete("image", publicId);

        public void DeleteRaw(string publicId)
            => Delete("raw", publicId);

        public byte[] Download(string url)
        {
            using (var client = new WebClient())
            {
                return client.DownloadData(url);
            }
        }

        public string GetImageUrl(string publicId)
            => $"https://res.cloudinary.com/{_cloudName}/image/upload/{ValidatePublicId(publicId)}";

        public string GetRawUrl(string publicId, string extension)
            => $"https://res.cloudinary.com/{_cloudName}/raw/upload/{ValidatePublicId(publicId)}.{extension.TrimStart('.')}";

        public string GetThumbnailUrl(string publicId, int width, int height = 0)
        {
            var transform = height > 0
                ? $"w_{width},h_{height},c_fill"
                : $"w_{width}";
            return $"https://res.cloudinary.com/{_cloudName}/image/upload/{transform}/{ValidatePublicId(publicId)}";
        }

        private string Upload(string resourceType, byte[] fileBytes, string publicId, string fileName, string contentType)
        {
            publicId = ValidatePublicId(publicId);
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
                fileBytes, "file", fileName, contentType);

            using (var client = new WebClient())
            {
                client.Headers.Add("Content-Type", $"multipart/form-data; boundary={boundary}");
                var url      = $"https://api.cloudinary.com/v1_1/{_cloudName}/{resourceType}/upload";
                var response = UploadData(client, url, body, publicId);
                var json     = JObject.Parse(response);
                return json.Value<string>("public_id") ?? publicId;
            }
        }

        private void Delete(string resourceType, string publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                return;

            publicId = ValidatePublicId(publicId);
            var timestamp    = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var paramsToSign = $"public_id={publicId}&timestamp={timestamp}{_apiSecret}";
            var signature    = ComputeSha1(paramsToSign);

            using (var client = new WebClient())
            {
                var url = $"https://api.cloudinary.com/v1_1/{_cloudName}/{resourceType}/destroy";
                try
                {
                    client.UploadValues(url, new NameValueCollection
                    {
                        ["api_key"]   = _apiKey,
                        ["timestamp"] = timestamp,
                        ["signature"] = signature,
                        ["public_id"] = publicId,
                    });
                }
                catch (WebException ex)
                {
                    throw CreateCloudinaryException("delete", publicId, ex);
                }
            }
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

        private static string ValidatePublicId(string publicId)
        {
            if (string.IsNullOrWhiteSpace(publicId))
                throw new ArgumentException("Cloudinary public_id is required.", nameof(publicId));

            if (publicId.Contains("&") || publicId.Contains("="))
                throw new ArgumentException("Cloudinary public_id contains unsupported characters.", nameof(publicId));

            return publicId;
        }

        private static string UploadData(WebClient client, string url, byte[] body, string publicId)
        {
            try
            {
                return Encoding.UTF8.GetString(client.UploadData(url, "POST", body));
            }
            catch (WebException ex)
            {
                throw CreateCloudinaryException("upload", publicId, ex);
            }
        }

        private static Exception CreateCloudinaryException(string operation, string publicId, WebException ex)
        {
            var responseBody = string.Empty;
            if (ex.Response != null)
            {
                using (var stream = ex.Response.GetResponseStream())
                using (var reader = stream != null ? new StreamReader(stream) : null)
                    responseBody = reader?.ReadToEnd() ?? string.Empty;
            }

            var message = string.IsNullOrWhiteSpace(responseBody)
                ? $"Cloudinary {operation} failed for '{publicId}'."
                : $"Cloudinary {operation} failed for '{publicId}': {responseBody}";
            return new InvalidOperationException(message, ex);
        }

        private static byte[] BuildMultipartBody(string boundary,
            (string name, string value)[] fields,
            byte[] file, string fileFieldName, string fileName, string contentType)
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
                    $"filename=\"{fileName}\"\r\nContent-Type: {contentType}\r\n\r\n");
                ms.Write(fileHeader, 0, fileHeader.Length);
                ms.Write(file, 0, file.Length);

                var footer = Encoding.UTF8.GetBytes($"\r\n--{boundary}--\r\n");
                ms.Write(footer, 0, footer.Length);

                return ms.ToArray();
            }
        }

    }
}
