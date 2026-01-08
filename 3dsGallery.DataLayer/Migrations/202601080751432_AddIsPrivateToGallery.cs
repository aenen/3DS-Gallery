namespace _3dsGallery.DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIsPrivateToGallery : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Gallery", "isPrivate", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Gallery", "isPrivate");
        }
    }
}
