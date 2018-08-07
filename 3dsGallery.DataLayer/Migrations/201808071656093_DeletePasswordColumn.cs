namespace _3dsGallery.DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class DeletePasswordColumn : DbMigration
    {
        public override void Up()
        {
            DropColumn("dbo.User", "password");
        }
        
        public override void Down()
        {
            AddColumn("dbo.User", "password", c => c.Int(nullable: false));
        }
    }
}
