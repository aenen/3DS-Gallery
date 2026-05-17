namespace _3dsGallery.DataLayer.DataBase
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    [Table("pictureComments_tb")]
    public partial class PictureComment
    {
        public PictureComment()
        {
        }

        [Key]
        [Column("idComment")]
        public int IdComment { get; set; }
        
        [ForeignKey("Picture")]
        [Column("idPicture")]
        public int IdPicture { get; set; }

        [ForeignKey("CreatedBy")]
        [Column("idUser")]
        public short IdUser { get; set; }

        [Column("commentDesc")]
        [StringLength(150)]
        public string CommentDesc { get; set; }

        [Column("creationDate")]
        public DateTime CreationDate { get; set; }

        public virtual Picture Picture { get; set; }

        public virtual User CreatedBy { get; set; }
    }
}
