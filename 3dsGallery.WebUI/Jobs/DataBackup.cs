using _3dsGallery.DataLayer.DataBase;
using _3dsGallery.WebUI.Code;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Quartz;
using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;

namespace _3dsGallery.WebUI.Jobs
{
    public class DataBackup : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            // File backups are no longer needed: images are stored on Cloudinary.
            // Only the database backup script is retained.
            using (var db = new GalleryContext())
            {
                var googleDriveManager = new GoogleDriveManager();
                UploadDatabaseBackupScript(googleDriveManager);
            }
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