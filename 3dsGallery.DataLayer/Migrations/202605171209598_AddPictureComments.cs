namespace _3dsGallery.DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPictureComments : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.pictureComments_tb",
                c => new
                    {
                        idComment = c.Int(nullable: false, identity: true),
                        idPicture = c.Int(nullable: false),
                        idUser = c.Short(nullable: false),
                        commentDesc = c.String(maxLength: 150),
                        creationDate = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.idComment)
                .ForeignKey("dbo.User", t => t.idUser)
                .ForeignKey("dbo.Picture", t => t.idPicture, cascadeDelete: true)
                .Index(t => t.idPicture)
                .Index(t => t.idUser);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.pictureComments_tb", "idPicture", "dbo.Picture");
            DropForeignKey("dbo.pictureComments_tb", "idUser", "dbo.User");
            DropIndex("dbo.pictureComments_tb", new[] { "idUser" });
            DropIndex("dbo.pictureComments_tb", new[] { "idPicture" });
            DropTable("dbo.pictureComments_tb");
        }
    }
}
