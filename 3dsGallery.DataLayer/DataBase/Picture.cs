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
        }

        public int id { get; set; }

        [StringLength(150)]
        public string description { get; set; }

        [StringLength(50)]
        public string path { get; set; }

        [StringLength(2)]
        public string type { get; set; }

        public short galleryId { get; set; }

        public virtual Gallery Gallery { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<User> User { get; set; }
    }
}
