namespace _3dsGallery.DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddNewPasswordProps : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.User", "PasswordSalt", c => c.Binary());
            AddColumn("dbo.User", "PasswordHash", c => c.Binary());
            AddColumn("dbo.User", "Iterations", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.User", "Iterations");
            DropColumn("dbo.User", "PasswordHash");
            DropColumn("dbo.User", "PasswordSalt");
        }
    }
}
