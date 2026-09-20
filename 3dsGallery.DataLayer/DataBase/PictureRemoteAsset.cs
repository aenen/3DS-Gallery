namespace _3dsGallery.DataLayer.DataBase
{
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;

    [Table("PictureRemoteAsset")]
    public partial class PictureRemoteAsset
    {
        [Key]
        [ForeignKey("Picture")]
        public int PictureId { get; set; }

        [StringLength(20)]
        public string StorageProvider { get; set; }

        [StringLength(20)]
        public string StorageMigrationStatus { get; set; }

        [StringLength(100)]
        public string OriginalRemoteFileId { get; set; }

        [StringLength(255)]
        public string OriginalRemotePath { get; set; }

        [StringLength(100)]
        public string PreviewRemoteFileId { get; set; }

        [StringLength(255)]
        public string PreviewRemotePath { get; set; }

        [StringLength(100)]
        public string ThumbnailSmallRemoteFileId { get; set; }

        [StringLength(255)]
        public string ThumbnailSmallRemotePath { get; set; }

        [StringLength(100)]
        public string ThumbnailMediumRemoteFileId { get; set; }

        [StringLength(255)]
        public string ThumbnailMediumRemotePath { get; set; }

        public virtual Picture Picture { get; set; }
    }
}
