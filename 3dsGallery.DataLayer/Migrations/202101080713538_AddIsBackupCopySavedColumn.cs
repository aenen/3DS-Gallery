namespace _3dsGallery.DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIsBackupCopySavedColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Picture", "isBackupCopySaved", c => c.Boolean(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Picture", "isBackupCopySaved");
        }
    }
}
