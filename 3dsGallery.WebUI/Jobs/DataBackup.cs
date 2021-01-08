using _3dsGallery.DataLayer.DataBase;
using Quartz;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Web;

namespace _3dsGallery.WebUI.Jobs
{
    public class DataBackup : IJob
    {
        public void Execute(IJobExecutionContext context)
        {
            using (var db = new GalleryContext())
            {
                //var script = new StringBuilder();
                //var connectionString = ConfigurationManager.ConnectionStrings["Gallery"].ToString();

                //Server server = new Server(new ServerConnection(new SqlConnection(connectionString)));
                //Database database = server.Databases[SqlConnectionStringBuilder(connectionString).InitialCatalog];
                //ScriptingOptions options = new ScriptingOptions
                //{
                //    ScriptData = true,
                //    ScriptSchema = true,
                //    ScriptDrops = false,
                //    Indexes = true,
                //    IncludeHeaders = true
                //};

                //foreach (Table table in database.Tables)
                //{
                //    foreach (var statement in table.EnumScript(options))
                //    {
                //        script.Append(statement);
                //        script.Append(Environment.NewLine);
                //    }
                //}

                //File.WriteAllText(backupPath + databaseName + ".sql", script.ToString());
            }
        }
    }
}