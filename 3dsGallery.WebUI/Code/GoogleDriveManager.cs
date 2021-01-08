using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Code
{
    public class GoogleDriveManager
    {
        private DriveService service;

        public GoogleDriveManager()
        {
            var initializer = new ServiceAccountCredential.Initializer(ConfigurationManager.AppSettings["GoogleDriveServiceAccountEmail"].ToString())
            {
                Scopes = new string[] { DriveService.Scope.Drive }
            };
            var credential = new ServiceAccountCredential(initializer.FromPrivateKey(ConfigurationManager.AppSettings["GoogleDrivePrivateKey"].ToString()));

            service = new DriveService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = ConfigurationManager.AppSettings["GoogleDriveApplicationName"].ToString(),
            });

            service.HttpClient.Timeout = TimeSpan.FromMinutes(100);
        }

        public void Upload(string fileName, byte[] file)
        {
            var fileMetaData = new Google.Apis.Drive.v3.Data.File()
            {
                Name = fileName,
                MimeType = MimeMapping.GetMimeMapping(fileName),
                Parents = new List<string> { ConfigurationManager.AppSettings["GoogleDriveBackupFolderId"].ToString() }
            };

            using (var stream = new MemoryStream(file))
            {
                var request = service.Files.Create(fileMetaData, stream, fileMetaData.MimeType);
                request.Fields = "id";
                request.Upload();
            }
        }

        public void Upload(string filePath)
        {
            var fileMetaData = new Google.Apis.Drive.v3.Data.File()
            {
                Name = Path.GetFileName(filePath),
                MimeType = MimeMapping.GetMimeMapping(filePath),
                Parents = new List<string> { ConfigurationManager.AppSettings["GoogleDriveBackupFolderId"].ToString() }
            };

            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                var request = service.Files.Create(fileMetaData, stream, fileMetaData.MimeType);
                request.Fields = "id";
                request.Upload();
            }
        }
    }
}