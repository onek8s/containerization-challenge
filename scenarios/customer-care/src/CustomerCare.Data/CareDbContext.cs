using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using Tailspin.CustomerCare.Domain;

namespace Tailspin.CustomerCare.Data
{
    /// <summary>
    /// Entity Framework model over the CustomerCare database. The schema is owned by the scripts in the db folder;
    /// EF never creates or changes it.
    /// </summary>
    public class CareDbContext : DbContext
    {
        static CareDbContext()
        {
            Database.SetInitializer<CareDbContext>(null);
        }

        public CareDbContext()
            : base("name=CustomerCare")
        {
            Configuration.LazyLoadingEnabled = false;
            Configuration.ProxyCreationEnabled = false;
        }

        public DbSet<Customer> Customers { get; set; }
        public DbSet<Case> Cases { get; set; }
        public DbSet<CaseNote> CaseNotes { get; set; }
        public DbSet<CaseAttachment> CaseAttachments { get; set; }
        public DbSet<Queue> Queues { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<RoutingRule> RoutingRules { get; set; }
        public DbSet<SlaTarget> SlaTargets { get; set; }
        public DbSet<Holiday> Holidays { get; set; }
        public DbSet<KbArticle> KbArticles { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("care");
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            var customer = modelBuilder.Entity<Customer>().ToTable("Customer");
            customer.Property(c => c.AccountNumber).IsRequired().HasMaxLength(10).IsFixedLength().IsUnicode(false);
            customer.Property(c => c.Name).IsRequired().HasMaxLength(200);
            customer.Property(c => c.Email).HasMaxLength(254);
            customer.Property(c => c.Phone).HasMaxLength(30).IsUnicode(false);
            customer.Property(c => c.Street).HasMaxLength(200);
            customer.Property(c => c.PostalCode).HasMaxLength(5).IsFixedLength().IsUnicode(false);
            customer.Property(c => c.City).HasMaxLength(100);
            customer.Property(c => c.Segment).IsRequired().HasMaxLength(3).IsFixedLength().IsUnicode(false);
            customer.HasMany(c => c.Cases).WithRequired(c => c.Customer).HasForeignKey(c => c.CustomerId);

            var c2 = modelBuilder.Entity<Case>().ToTable("Case");
            c2.Property(c => c.CaseNumber).IsRequired().HasMaxLength(14).IsUnicode(false);
            c2.Property(c => c.CategoryCode).IsRequired().HasMaxLength(10).IsUnicode(false);
            c2.Property(c => c.Subject).IsRequired().HasMaxLength(200);
            c2.Property(c => c.Description).IsMaxLength();
            c2.Property(c => c.AssignedTo).HasMaxLength(100);
            c2.Property(c => c.CreatedBy).IsRequired().HasMaxLength(100);
            c2.Property(c => c.ExternalReference).HasMaxLength(50).IsUnicode(false);
            c2.Property(c => c.RowVersion).IsRowVersion();
            c2.HasRequired(c => c.Queue).WithMany().HasForeignKey(c => c.QueueId);
            c2.HasMany(c => c.Notes).WithRequired().HasForeignKey(n => n.CaseId);
            c2.HasMany(c => c.Attachments).WithRequired().HasForeignKey(a => a.CaseId);
            c2.Ignore(c => c.IsOpen);

            var note = modelBuilder.Entity<CaseNote>().ToTable("CaseNote");
            note.Property(n => n.Author).IsRequired().HasMaxLength(100);
            note.Property(n => n.Body).IsRequired().IsMaxLength();

            var att = modelBuilder.Entity<CaseAttachment>().ToTable("CaseAttachment");
            att.Property(a => a.FileName).IsRequired().HasMaxLength(255);
            att.Property(a => a.ContentType).IsRequired().HasMaxLength(100).IsUnicode(false);
            att.Property(a => a.Content).IsRequired().IsMaxLength();
            att.Property(a => a.UploadedBy).IsRequired().HasMaxLength(100);

            var queue = modelBuilder.Entity<Queue>().ToTable("Queue");
            queue.Property(q => q.Code).IsRequired().HasMaxLength(12).IsUnicode(false);
            queue.Property(q => q.Name).IsRequired().HasMaxLength(100);
            queue.Property(q => q.Email).HasMaxLength(254);

            var category = modelBuilder.Entity<Category>().ToTable("Category");
            category.HasKey(c => c.Code);
            category.Property(c => c.Code).HasMaxLength(10).IsUnicode(false).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);
            category.Property(c => c.Name).IsRequired().HasMaxLength(100);
            category.Property(c => c.QueueCode).HasMaxLength(12).IsUnicode(false);

            var rule = modelBuilder.Entity<RoutingRule>().ToTable("RoutingRule");
            rule.Property(r => r.CategoryCode).HasMaxLength(10).IsUnicode(false);
            rule.Property(r => r.PostalCodePrefix).HasMaxLength(5).IsUnicode(false);
            rule.Property(r => r.Segment).HasMaxLength(3).IsUnicode(false);
            rule.Property(r => r.QueueCode).IsRequired().HasMaxLength(12).IsUnicode(false);

            var sla = modelBuilder.Entity<SlaTarget>().ToTable("SlaTarget");
            sla.HasKey(s => s.Priority);
            sla.Property(s => s.Priority).HasDatabaseGeneratedOption(DatabaseGeneratedOption.None);

            var holiday = modelBuilder.Entity<Holiday>().ToTable("Holiday");
            holiday.HasKey(h => h.Date);
            holiday.Property(h => h.Date).HasColumnType("date");
            holiday.Property(h => h.Name).IsRequired().HasMaxLength(100);

            var kb = modelBuilder.Entity<KbArticle>().ToTable("KbArticle");
            kb.Property(k => k.Slug).IsRequired().HasMaxLength(80).IsUnicode(false);
            kb.Property(k => k.Title).IsRequired().HasMaxLength(200);
            kb.Property(k => k.Body).IsRequired().IsMaxLength();
            kb.Property(k => k.Tags).HasMaxLength(400);
            kb.Property(k => k.CategoryCode).HasMaxLength(10).IsUnicode(false);
            kb.Property(k => k.ModifiedBy).IsRequired().HasMaxLength(100);
        }
    }
}
