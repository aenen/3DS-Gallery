using _3dsGallery.DataLayer.DataBase;
using _3dsGallery.WebUI.Code;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Quartz;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace _3dsGallery.WebUI.Jobs
{
    public class DataBackup : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            using (var db = new GalleryContext())
            {
                var numberOfPicturesToUpload = Convert.ToInt32(ConfigurationManager.AppSettings["NumberOfPicturesToUpload"].ToString());
                var googleDriveManager = new GoogleDriveManager();
                UploadDatabaseBackupScript(googleDriveManager);

                var pictureDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Picture");
                if (!Directory.Exists(pictureDirectory))
                    return;

                var pictureToProcessList = db.Picture
                    .Where(x => !x.IsBackupCopySaved && (x.StorageProvider == null || x.StorageProvider == PictureStorageConstants.StorageProviderLocal))
                    .OrderBy(x => x.CreationDate)
                    .Take(numberOfPicturesToUpload)
                    .ToList();

                foreach (var pictureToProcess in pictureToProcessList)
                {
                    var picturePathList = GetLocalAssetPaths(pictureDirectory, pictureToProcess.id);
                    if (picturePathList.Count == 0)
                        continue;

                    picturePathList.ForEach(x => googleDriveManager.Upload(x));
                    pictureToProcess.IsBackupCopySaved = true;
                    db.SaveChanges();
                }
            }
        }

        private static List<string> GetLocalAssetPaths(string pictureDirectory, int pictureId)
        {
            var regex = new Regex(string.Format("^{0}[.-]", pictureId));
            return Directory.GetFiles(pictureDirectory)
                .Where(x => regex.IsMatch(Path.GetFileName(x)))
                .ToList();
        }

        private void UploadDatabaseBackupScript(GoogleDriveManager googleDriveManager)
        {
            var connectionString = ConfigurationManager.ConnectionStrings["Gallery"].ToString();
            var server = new Server(new ServerConnection(new SqlConnection(connectionString)));
            var database = server.Databases[new SqlConnectionStringBuilder(connectionString).InitialCatalog];
            var options = new ScriptingOptions
            {
                ScriptData = true,
                ScriptSchema = true,
                ScriptDrops = false,
                Indexes = true,
                IncludeHeaders = true
            };

            byte[] bytes = null;
            using (var ms = new MemoryStream())
            {
                TextWriter tw = new StreamWriter(ms);

                foreach (Table table in database.Tables)
                    foreach (var statement in table.EnumScript(options))
                        tw.WriteLine(statement);

                tw.Flush();
                ms.Position = 0;
                bytes = ms.ToArray();
            }

            googleDriveManager.Upload($"#Backup#{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.sql", bytes);
        }
    }
}