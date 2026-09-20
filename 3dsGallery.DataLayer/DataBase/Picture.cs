namespace _3dsGallery.DataLayer.DataBase
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Picture")]
    public partial class Picture
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Picture()
        {
            User = new HashSet<User>();
            Comments = new HashSet<PictureComment>();
        }

        public int id { get; set; }

        public short galleryId { get; set; }

        [StringLength(150)]
        public string description { get; set; }

        [StringLength(255)]
        public string path { get; set; }

        [StringLength(2)]
        public string type { get; set; }

        [Column("isBackupCopySaved")]
        public bool IsBackupCopySaved { get; set; }

        [Column("creationDate")]
        public DateTime? CreationDate { get; set; }

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

        public virtual Gallery Gallery { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<User> User { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<PictureComment> Comments { get; set; }
    }
}
