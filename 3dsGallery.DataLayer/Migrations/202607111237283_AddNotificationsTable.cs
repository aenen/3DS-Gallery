namespace _3dsGallery.DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddNotificationsTable : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.notifications_tb",
                c => new
                    {
                        idNotification = c.Int(nullable: false, identity: true),
                        idUser = c.Short(nullable: false),
                        type = c.String(maxLength: 50, unicode: false),
                        message = c.String(maxLength: 250),
                        relatedEntityId = c.Int(),
                        isRead = c.Boolean(nullable: false),
                        creationDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.idNotification)
                .ForeignKey("dbo.User", t => t.idUser)
                .Index(t => t.idUser);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.notifications_tb", "idUser", "dbo.User");
            DropIndex("dbo.notifications_tb", new[] { "idUser" });
            DropTable("dbo.notifications_tb");
        }
    }
}
