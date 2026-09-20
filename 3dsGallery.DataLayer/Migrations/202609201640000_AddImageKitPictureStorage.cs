namespace _3dsGallery.DataLayer.Migrations
{
    using System.Data.Entity.Migrations;

    public partial class AddImageKitPictureStorage : DbMigration
    {
        public override void Up()
        {
            AlterColumn("dbo.Picture", "path", c => c.String(maxLength: 255));
            AddColumn("dbo.Picture", "StorageProvider", c => c.String(maxLength: 20));
            AddColumn("dbo.Picture", "StorageMigrationStatus", c => c.String(maxLength: 20));
            AddColumn("dbo.Picture", "OriginalRemoteFileId", c => c.String(maxLength: 100));
            AddColumn("dbo.Picture", "OriginalRemotePath", c => c.String(maxLength: 255));
            AddColumn("dbo.Picture", "PreviewRemoteFileId", c => c.String(maxLength: 100));
            AddColumn("dbo.Picture", "PreviewRemotePath", c => c.String(maxLength: 255));
            AddColumn("dbo.Picture", "ThumbnailSmallRemoteFileId", c => c.String(maxLength: 100));
            AddColumn("dbo.Picture", "ThumbnailSmallRemotePath", c => c.String(maxLength: 255));
            AddColumn("dbo.Picture", "ThumbnailMediumRemoteFileId", c => c.String(maxLength: 100));
            AddColumn("dbo.Picture", "ThumbnailMediumRemotePath", c => c.String(maxLength: 255));
            Sql("UPDATE dbo.Picture SET StorageProvider = 'Local', StorageMigrationStatus = 'LocalOnly' WHERE StorageProvider IS NULL");
        }

        public override void Down()
        {
            DropColumn("dbo.Picture", "ThumbnailMediumRemotePath");
            DropColumn("dbo.Picture", "ThumbnailMediumRemoteFileId");
            DropColumn("dbo.Picture", "ThumbnailSmallRemotePath");
            DropColumn("dbo.Picture", "ThumbnailSmallRemoteFileId");
            DropColumn("dbo.Picture", "PreviewRemotePath");
            DropColumn("dbo.Picture", "PreviewRemoteFileId");
            DropColumn("dbo.Picture", "OriginalRemotePath");
            DropColumn("dbo.Picture", "OriginalRemoteFileId");
            DropColumn("dbo.Picture", "StorageMigrationStatus");
            DropColumn("dbo.Picture", "StorageProvider");
            AlterColumn("dbo.Picture", "path", c => c.String(maxLength: 50));
        }
    }
}
