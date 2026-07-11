
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace _3dsGallery.DataLayer.DataBase
{
    [Table("notifications_tb")]
    public partial class Notification
    {
        [Key]
        [Column("idNotification")]
        public int IdNotification { get; set; }

        [ForeignKey("User")]
        [Column("idUser")]
        public short IdUser { get; set; }

        [Column("idUserActor")]
        public short IdUserActor { get; set; }

        [Column("type")]
        [StringLength(50)]
        public string Type { get; set; }

        [Column("message")]
        [StringLength(250)]
        public string Message { get; set; }

        [Column("relatedEntityId")]
        public int? RelatedEntityId { get; set; }

        [Column("isRead")]
        public bool IsRead { get; set; }

        [Column("creationDate")]
        public DateTime CreationDate { get; set; }

        public virtual User User { get; set; }
    }
}
