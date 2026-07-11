namespace _3dsGallery.DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddUserActorToNotifications : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.notifications_tb", "idUserActor", c => c.Short(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.notifications_tb", "idUserActor");
        }
    }
}
