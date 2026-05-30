namespace _3dsGallery.DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class UserBio : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.User", "Bio", c => c.String(maxLength: 150));
        }
        
        public override void Down()
        {
            DropColumn("dbo.User", "Bio");
        }
    }
}
