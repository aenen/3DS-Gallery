namespace _3dsGallery.DataLayer.Migrations
{
    using _3dsGallery.DataLayer.DataBase;
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddLastPicture : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Gallery", "LastPicture_id", c => c.Int());
            CreateIndex("dbo.Gallery", "LastPicture_id");
            AddForeignKey("dbo.Gallery", "LastPicture_id", "dbo.Picture", "id");
        }
        
        public override void Down()
        {
        }
    }
}
