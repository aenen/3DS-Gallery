namespace _3dsGallery.DataLayer.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddValueExColumn : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Style", "ValueEx", c => c.String(maxLength: 50));
            AlterColumn("dbo.Style", "value", c => c.String(nullable: false, maxLength: 50));
        }
        
        public override void Down()
        {
            AlterColumn("dbo.Style", "value", c => c.String(nullable: false, maxLength: 20));
            DropColumn("dbo.Style", "ValueEx");
        }
    }
}
