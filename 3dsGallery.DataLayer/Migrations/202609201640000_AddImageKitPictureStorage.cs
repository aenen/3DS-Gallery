namespace _3dsGallery.DataLayer.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddImageKitPictureStorage : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PictureRemoteAsset",
                c => new
                    {
                        PictureId = c.Int(nullable: false),
                        StorageProvider = c.String(maxLength: 20),
                        StorageMigrationStatus = c.String(maxLength: 20),
                        OriginalRemoteFileId = c.String(maxLength: 100),
                        OriginalRemotePath = c.String(maxLength: 255),
                        PreviewRemoteFileId = c.String(maxLength: 100),
                        PreviewRemotePath = c.String(maxLength: 255),
                        ThumbnailSmallRemoteFileId = c.String(maxLength: 100),
                        ThumbnailSmallRemotePath = c.String(maxLength: 255),
                        ThumbnailMediumRemoteFileId = c.String(maxLength: 100),
                        ThumbnailMediumRemotePath = c.String(maxLength: 255),
                    })
                .PrimaryKey(t => t.PictureId)
                .ForeignKey("dbo.Picture", t => t.PictureId, cascadeDelete: true)
                .Index(t => t.PictureId);
        }

        public override void Down()
        {
            DropForeignKey("dbo.PictureRemoteAsset", "PictureId", "dbo.Picture");
            DropIndex("dbo.PictureRemoteAsset", new[] { "PictureId" });
            DropTable("dbo.PictureRemoteAsset");
        }
    }
}
