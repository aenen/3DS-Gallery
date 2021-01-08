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

namespace _3dsGallery.WebUI.Jobs
{
    public class DataBackup : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            try
            {
                using (var db = new GalleryContext())
                {
                    var numberOfPicturesToUpload = Convert.ToInt32(ConfigurationManager.AppSettings["NumberOfPicturesToUpload"].ToString());
                    var pictureToProcessList = db.Picture.Where(x => !x.IsBackupCopySaved).OrderBy(x => x.CreationDate).Take(numberOfPicturesToUpload).ToList();
                    if (pictureToProcessList.Count == 0)
                        return;

                    var googleDriveManager = new GoogleDriveManager();

                    foreach (var pictureToProcess in pictureToProcessList)
                    {
                        var regex = new Regex($"^{pictureToProcess.id}[.-]");
                        var picturePathList = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Picture"))
                                             .Where(x => regex.IsMatch(Path.GetFileName(x)))
                                             .ToList();

                        picturePathList.ForEach(x => googleDriveManager.Upload(x));
                        pictureToProcess.IsBackupCopySaved = true;
                        db.SaveChanges();
                    }


                    UploadDatabaseBackupScript(googleDriveManager);
                }

            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DataBackupLogs.txt"), $"{ex.Message}\n{ex.InnerException?.Message??""}\n{ex.StackTrace}");
                throw;
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