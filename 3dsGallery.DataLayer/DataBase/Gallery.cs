namespace _3dsGallery.DataLayer.DataBase
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("Gallery")]
    public partial class Gallery
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2214:DoNotCallOverridableMethodsInConstructors")]
        public Gallery()
        {
            Picture = new HashSet<Picture>();
        }

        public short id { get; set; }

        [Required]
        [StringLength(50)]
        public string name { get; set; }

        [StringLength(150)]
        public string info { get; set; }

        public short userId { get; set; }

        public byte? styleId { get; set; }

        public virtual User User { get; set; }

        public virtual Picture LastPicture { get; set; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2227:CollectionPropertiesShouldBeReadOnly")]
        public virtual ICollection<Picture> Picture { get; set; }

        public virtual Style Style { get; set; }
    }
}
