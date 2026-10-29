using FieldService.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace FieldService.Data;

public class FieldServiceContext : DbContext
{
    public FieldServiceContext(DbContextOptions<FieldServiceContext> options)
        : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();

    public DbSet<Technician> Technicians => Set<Technician>();

    public DbSet<AppUser> Users => Set<AppUser>();

    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();

    public DbSet<WorkOrderPart> WorkOrderParts => Set<WorkOrderPart>();

    public DbSet<WorkOrderPhoto> WorkOrderPhotos => Set<WorkOrderPhoto>();

    public DbSet<ImportedFile> ImportedFiles => Set<ImportedFile>();

    public DbSet<InvoiceBatch> InvoiceBatches => Set<InvoiceBatch>();

    public DbSet<SequenceCounter> SequenceCounters => Set<SequenceCounter>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Customer>(e =>
        {
            e.HasIndex(c => c.AccountCode).IsUnique();
            e.Property(c => c.AccountCode).HasMaxLength(20);
            e.Property(c => c.Name).HasMaxLength(200);
            e.Property(c => c.HourlyRate).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Technician>(e =>
        {
            e.Property(t => t.Name).HasMaxLength(100);
            e.Property(t => t.Region).HasMaxLength(50);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.UserName).IsUnique();
            e.Property(u => u.UserName).HasMaxLength(50);
            e.Property(u => u.Role).HasMaxLength(20);
        });

        modelBuilder.Entity<WorkOrder>(e =>
        {
            e.HasIndex(w => w.Number).IsUnique();
            e.HasIndex(w => w.Status);
            e.Property(w => w.Number).HasMaxLength(20);
            e.Property(w => w.Summary).HasMaxLength(200);
            e.Property(w => w.SiteAddress).HasMaxLength(300);
            e.Property(w => w.Source).HasMaxLength(20);
            e.Property(w => w.CustomerReference).HasMaxLength(50);
            e.Property(w => w.InvoiceBatch).HasMaxLength(30);
            e.Property(w => w.LaborHours).HasPrecision(6, 2);
            e.Ignore(w => w.IsOpen);
            e.HasMany(w => w.Parts).WithOne().HasForeignKey(p => p.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(w => w.Photos).WithOne().HasForeignKey(p => p.WorkOrderId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkOrderPart>(e =>
        {
            e.Property(p => p.PartNumber).HasMaxLength(30);
            e.Property(p => p.UnitPrice).HasPrecision(10, 2);
        });

        modelBuilder.Entity<InvoiceBatch>(e =>
        {
            e.HasIndex(b => b.BatchNumber).IsUnique();
            e.Property(b => b.TotalAmount).HasPrecision(12, 2);
        });

        modelBuilder.Entity<SequenceCounter>(e =>
        {
            e.HasKey(s => new { s.Name, s.Year });
            e.Property(s => s.Name).HasMaxLength(30);
        });
    }
}
