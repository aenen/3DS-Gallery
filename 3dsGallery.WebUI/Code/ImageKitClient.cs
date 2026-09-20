using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;

namespace _3dsGallery.WebUI.Code
{
    public class ImageKitConfiguration
    {
        public string PublicKey { get; set; }
        public string PrivateKey { get; set; }
        public string UrlEndpoint { get; set; }
        public string UploadFolder { get; set; }
        public int TimeoutSeconds { get; set; }
        public int MaxRetries { get; set; }

        public static ImageKitConfiguration LoadFromConfiguration()
        {
            var timeout = ReadIntSetting("ImageKitTimeoutSeconds", 30);
            var retries = ReadIntSetting("ImageKitMaxRetries", 3);

            return new ImageKitConfiguration
            {
                PublicKey = ReadSetting("ImageKitPublicKey"),
                PrivateKey = ReadSetting("ImageKitPrivateKey"),
                UrlEndpoint = ReadSetting("ImageKitUrlEndpoint"),
                UploadFolder = ReadSetting("ImageKitUploadFolder") ?? "/3dsgallery/pictures",
                TimeoutSeconds = timeout < 1 ? 30 : timeout,
                MaxRetries = retries < 1 ? 1 : retries
            };
        }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(PrivateKey) || string.IsNullOrWhiteSpace(UrlEndpoint))
                throw new InvalidOperationException("ImageKit is not configured. Set ImageKitPrivateKey and ImageKitUrlEndpoint in app settings or environment variables.");
        }

        private static string ReadSetting(string key)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
                return value;

            value = ConfigurationManager.AppSettings[key];
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        private static int ReadIntSetting(string key, int defaultValue)
        {
            var raw = ReadSetting(key);
            int value;
            return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : defaultValue;
        }
    }

    public class ImageKitStoredFile
    {
        public string FileId { get; set; }
        public string FilePath { get; set; }
        public string Url { get; set; }
        public long Size { get; set; }
    }

    public class ImageKitDeleteResult
    {
        public bool Deleted { get; set; }
        public bool NotFound { get; set; }
    }

    public interface IImageKitClient
    {
        ImageKitStoredFile Upload(byte[] fileBytes, string fileName, string folderPath, string contentType);
        ImageKitDeleteResult Delete(string fileId);
        ImageKitDeleteResult DeleteByPath(string filePath);
        byte[] Download(string absoluteUrl);
        string BuildDeliveryUrl(string filePath);
    }

    public class ImageKitClient : IImageKitClient
    {
        private readonly ImageKitConfiguration _configuration;

        public ImageKitClient()
            : this(ImageKitConfiguration.LoadFromConfiguration())
        {
        }

        public ImageKitClient(ImageKitConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException("configuration");

            _configuration = configuration;
            _configuration.Validate();
        }

        public ImageKitStoredFile Upload(byte[] fileBytes, string fileName, string folderPath, string contentType)
        {
            if (fileBytes == null || fileBytes.Length == 0)
                throw new ArgumentException("ImageKit upload requires file bytes.", "fileBytes");
            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("ImageKit upload requires fileName.", "fileName");

            var boundary = "----ImageKitBoundary" + Guid.NewGuid().ToString("N");
            var requestBody = BuildMultipartBody(boundary, fileBytes, fileName, folderPath, contentType);
            var responseText = ExecuteWithRetry("upload", fileName, delegate()
            {
                var request = CreateRequest("https://upload.imagekit.io/api/v1/files/upload", "POST", "multipart/form-data; boundary=" + boundary);
                using (var stream = request.GetRequestStream())
                {
                    stream.Write(requestBody, 0, requestBody.Length);
                }

                return ReadResponse(request);
            });

            var json = JObject.Parse(responseText);
            var storedFile = new ImageKitStoredFile
            {
                FileId = (string)json["fileId"],
                FilePath = (string)json["filePath"],
                Url = (string)json["url"],
                Size = (long?)json["size"] ?? 0L
            };

            if (string.IsNullOrWhiteSpace(storedFile.FileId) || string.IsNullOrWhiteSpace(storedFile.FilePath))
                throw new InvalidOperationException("ImageKit upload response did not include fileId and filePath.");

            return storedFile;
        }

        public ImageKitDeleteResult Delete(string fileId)
        {
            if (string.IsNullOrWhiteSpace(fileId))
                return new ImageKitDeleteResult { Deleted = true };

            try
            {
                ExecuteWithRetry("delete", fileId, delegate()
                {
                    var request = CreateRequest("https://api.imagekit.io/v1/files/" + Uri.EscapeDataString(fileId), "DELETE", null);
                    return ReadResponse(request);
                });

                return new ImageKitDeleteResult { Deleted = true };
            }
            catch (WebException ex)
            {
                var httpResponse = ex.Response as HttpWebResponse;
                if (httpResponse != null && httpResponse.StatusCode == HttpStatusCode.NotFound)
                    return new ImageKitDeleteResult { Deleted = false, NotFound = true };

                throw CreateOperationException("delete", fileId, ex);
            }
        }

        public ImageKitDeleteResult DeleteByPath(string filePath)
        {
            var fileId = FindFileIdByPath(filePath);
            if (string.IsNullOrWhiteSpace(fileId))
                return new ImageKitDeleteResult { Deleted = false, NotFound = true };

            return Delete(fileId);
        }

        public byte[] Download(string absoluteUrl)
        {
            if (string.IsNullOrWhiteSpace(absoluteUrl))
                throw new ArgumentException("ImageKit download requires a URL.", "absoluteUrl");

            return ExecuteWithRetry("download", absoluteUrl, delegate()
            {
                var request = (HttpWebRequest)WebRequest.Create(absoluteUrl);
                request.Method = "GET";
                request.Timeout = _configuration.TimeoutSeconds * 1000;
                request.ReadWriteTimeout = request.Timeout;
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var responseStream = response.GetResponseStream())
                using (var ms = new MemoryStream())
                {
                    if (responseStream == null)
                        throw new InvalidOperationException("ImageKit download response did not contain a body.");

                    responseStream.CopyTo(ms);
                    return ms.ToArray();
                }
            });
        }

        public string BuildDeliveryUrl(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            var endpoint = (_configuration.UrlEndpoint ?? string.Empty).TrimEnd('/');
            var normalizedPath = filePath.Replace('\\', '/');
            if (!normalizedPath.StartsWith("/"))
                normalizedPath = "/" + normalizedPath;

            return endpoint + normalizedPath;
        }

        private string FindFileIdByPath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return null;

            var normalizedPath = filePath.Replace('\\', '/');
            if (!normalizedPath.StartsWith("/"))
                normalizedPath = "/" + normalizedPath;

            var lastSlashIndex = normalizedPath.LastIndexOf('/');
            if (lastSlashIndex < 0 || lastSlashIndex == normalizedPath.Length - 1)
                return null;

            var folderPath = normalizedPath.Substring(0, lastSlashIndex);
            var fileName = normalizedPath.Substring(lastSlashIndex + 1);
            var requestUrl = string.Format(
                CultureInfo.InvariantCulture,
                "https://api.imagekit.io/v1/files?path={0}&name={1}",
                Uri.EscapeDataString(folderPath),
                Uri.EscapeDataString(fileName));

            var responseText = ExecuteWithRetry("search", normalizedPath, delegate()
            {
                var request = CreateRequest(requestUrl, "GET", null);
                return ReadResponse(request);
            });

            var token = JToken.Parse(responseText);
            var files = token as JArray;
            if (files == null)
                files = token["files"] as JArray;
            if (files == null)
                return null;

            foreach (var file in files.OfType<JObject>())
            {
                var candidatePath = ((string)file["filePath"] ?? string.Empty).Replace('\\', '/');
                if (string.Equals(candidatePath, normalizedPath, StringComparison.OrdinalIgnoreCase))
                    return (string)file["fileId"];
            }

            var first = files.OfType<JObject>().FirstOrDefault();
            return first == null ? null : (string)first["fileId"];
        }

        private HttpWebRequest CreateRequest(string url, string method, string contentType)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = method;
            request.Timeout = _configuration.TimeoutSeconds * 1000;
            request.ReadWriteTimeout = request.Timeout;
            request.Headers[HttpRequestHeader.Authorization] = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(_configuration.PrivateKey + ":"));
            if (!string.IsNullOrWhiteSpace(contentType))
                request.ContentType = contentType;
            return request;
        }

        private string ReadResponse(HttpWebRequest request)
        {
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = stream != null ? new StreamReader(stream) : null)
                {
                    return reader == null ? string.Empty : reader.ReadToEnd();
                }
            }
            catch (WebException ex)
            {
                throw CreateOperationException(request.Method == "DELETE" ? "delete" : "upload", request.RequestUri.AbsoluteUri, ex);
            }
        }

        private T ExecuteWithRetry<T>(string operation, string target, Func<T> action)
        {
            Exception lastError = null;
            for (var attempt = 1; attempt <= _configuration.MaxRetries; attempt++)
            {
                try
                {
                    return action();
                }
                catch (WebException ex)
                {
                    lastError = ex;
                    if (attempt >= _configuration.MaxRetries || !IsTransient(ex))
                        throw;

                    Thread.Sleep(GetDelayMilliseconds(ex, attempt));
                }
                catch (InvalidOperationException ex)
                {
                    lastError = ex;
                    if (attempt >= _configuration.MaxRetries)
                        throw;

                    Thread.Sleep(GetDelayMilliseconds(null, attempt));
                }
            }

            throw new InvalidOperationException("ImageKit " + operation + " failed for '" + target + "'.", lastError);
        }

        private static bool IsTransient(WebException ex)
        {
            var response = ex.Response as HttpWebResponse;
            if (response == null)
                return ex.Status == WebExceptionStatus.Timeout || ex.Status == WebExceptionStatus.ConnectFailure || ex.Status == WebExceptionStatus.NameResolutionFailure;

            var statusCode = (int)response.StatusCode;
            return statusCode == 408 || statusCode == 429 || statusCode >= 500;
        }

        private static int GetDelayMilliseconds(WebException ex, int attempt)
        {
            var response = ex == null ? null : ex.Response as HttpWebResponse;
            if (response != null)
            {
                var retryAfter = response.Headers["Retry-After"];
                int retryAfterSeconds;
                if (int.TryParse(retryAfter, NumberStyles.Integer, CultureInfo.InvariantCulture, out retryAfterSeconds) && retryAfterSeconds > 0)
                    return Math.Min(retryAfterSeconds * 1000, 10000);
            }

            return Math.Min(1000 * attempt * attempt, 10000);
        }

        private Exception CreateOperationException(string operation, string target, WebException ex)
        {
            var responseBody = string.Empty;
            var response = ex.Response as HttpWebResponse;
            if (response != null)
            {
                using (var stream = response.GetResponseStream())
                using (var reader = stream != null ? new StreamReader(stream) : null)
                {
                    responseBody = reader == null ? string.Empty : reader.ReadToEnd();
                }
            }

            var message = string.IsNullOrWhiteSpace(responseBody)
                ? string.Format(CultureInfo.InvariantCulture, "ImageKit {0} failed for '{1}'.", operation, target)
                : string.Format(CultureInfo.InvariantCulture, "ImageKit {0} failed for '{1}': {2}", operation, target, responseBody);
            return new InvalidOperationException(message, ex);
        }

        private static byte[] BuildMultipartBody(string boundary, byte[] fileBytes, string fileName, string folderPath, string contentType)
        {
            var fields = new NameValueCollection
            {
                { "fileName", fileName },
                { "useUniqueFileName", "false" },
                { "overwriteFile", "true" },
                { "overwriteAITags", "true" },
                { "overwriteTags", "true" },
                { "overwriteCustomMetadata", "true" }
            };

            if (!string.IsNullOrWhiteSpace(folderPath))
                fields.Add("folder", NormalizeFolder(folderPath));

            using (var ms = new MemoryStream())
            {
                foreach (string key in fields.Keys)
                {
                    WriteString(ms, "--" + boundary + "\r\n");
                    WriteString(ms, string.Format(CultureInfo.InvariantCulture, "Content-Disposition: form-data; name=\"{0}\"\r\n\r\n{1}\r\n", key, fields[key]));
                }

                WriteString(ms, "--" + boundary + "\r\n");
                WriteString(ms, string.Format(CultureInfo.InvariantCulture, "Content-Disposition: form-data; name=\"file\"; filename=\"{0}\"\r\n", fileName));
                WriteString(ms, string.Format(CultureInfo.InvariantCulture, "Content-Type: {0}\r\n\r\n", string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType));
                ms.Write(fileBytes, 0, fileBytes.Length);
                WriteString(ms, "\r\n--" + boundary + "--\r\n");
                return ms.ToArray();
            }
        }

        private static void WriteString(Stream stream, string value)
        {
            var buffer = Encoding.UTF8.GetBytes(value);
            stream.Write(buffer, 0, buffer.Length);
        }

        private static string NormalizeFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                return null;

            var normalized = folderPath.Replace('\\', '/').Trim();
            if (!normalized.StartsWith("/"))
                normalized = "/" + normalized;
            return normalized.TrimEnd('/');
        }
    }
}
