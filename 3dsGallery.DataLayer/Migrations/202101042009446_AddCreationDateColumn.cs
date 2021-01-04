namespace _3dsGallery.DataLayer.Migrations
{
    using _3dsGallery.DataLayer.Tools;
    using System;
    using System.Collections.Generic;
    using System.Data.Entity.Migrations;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;

    public partial class AddCreationDateColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Picture", "creationDate", c => c.DateTime());

            var regex = new Regex("^\\d{1,10}.(?i)(jpg|mpo)$");
            var pictures = Directory.GetFiles(Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Picture")))
                                 .Where(x => regex.IsMatch(Path.GetFileName(x)))
                                 .Select(x => new { ID = Path.GetFileNameWithoutExtension(x), CreationDate = File.GetCreationTime(x).ToString("yyyy'-'MM'-'dd HH:mm:ss.fff") })
                                 .DistinctBy(x => x.ID)
                                 .ToList();

            foreach (var picture in pictures)
                Sql($"UPDATE dbo.Picture SET creationDate = '{picture.CreationDate}' WHERE id = {picture.ID}");
        }
        
        public override void Down()
        {
            DropColumn("dbo.Picture", "creationDate");
        }
    }
}
