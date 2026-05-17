namespace _3dsGallery.DataLayer.DataBase
{
    using System;
    using System.Data.Entity;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Linq;

    public partial class GalleryContext : DbContext
    {
        public GalleryContext()
            : base("name=Gallery")
        {
        }

        public virtual DbSet<Gallery> Gallery { get; set; }
        public virtual DbSet<Picture> Picture { get; set; }
        public virtual DbSet<Style> Style { get; set; }
        public virtual DbSet<User> User { get; set; }
        public virtual DbSet<PictureComment> PictureComments { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Gallery>()
                .HasMany(e => e.Picture)
                .WithRequired(e => e.Gallery)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Picture>()
                .Property(e => e.type)
                .IsFixedLength()
                .IsUnicode(false);

            modelBuilder.Entity<Picture>()
                .HasMany(e => e.User)
                .WithMany(e => e.Picture)
                .Map(m => m.ToTable("Picture_User").MapLeftKey("pictureId").MapRightKey("userId"));

            modelBuilder.Entity<User>()
                .HasMany(e => e.Gallery)
                .WithRequired(e => e.User)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PictureComment>()
                .HasRequired(pc => pc.Picture)
                .WithMany(p => p.Comments)
                .HasForeignKey(pc => pc.IdPicture)
                .WillCascadeOnDelete(true); 

            modelBuilder.Entity<PictureComment>()
                .HasRequired(pc => pc.CreatedBy)
                .WithMany(u => u.Comments)
                .HasForeignKey(pc => pc.IdUser)
                .WillCascadeOnDelete(false);

        }
    }
}
