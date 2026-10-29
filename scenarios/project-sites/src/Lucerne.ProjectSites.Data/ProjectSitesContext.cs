using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using Lucerne.ProjectSites.Data.Entities;

namespace Lucerne.ProjectSites.Data
{
    /// <summary>
    /// Database "ProjectSitesHub" on sql01 used by the dashboards add-in (schema dash) and the sync service
    /// (schema sync). The schema is created by db\01-schema.sql; EF does not create or migrate it.
    /// </summary>
    public class ProjectSitesContext : DbContext
    {
        static ProjectSitesContext()
        {
            Database.SetInitializer<ProjectSitesContext>(null);
        }

        public ProjectSitesContext()
            : base("name=ProjectSitesHub")
        {
        }

        public ProjectSitesContext(string nameOrConnectionString)
            : base(nameOrConnectionString)
        {
        }

        public DbSet<KpiSnapshot> KpiSnapshots { get; set; }
        public DbSet<ProjectSubscription> Subscriptions { get; set; }
        public DbSet<UserPreference> UserPreferences { get; set; }
        public DbSet<SyncWatermark> Watermarks { get; set; }
        public DbSet<SyncRunRecord> Runs { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
            modelBuilder.Entity<KpiSnapshot>().Property(k => k.HoursToDate).HasPrecision(12, 2);
            modelBuilder.Entity<KpiSnapshot>().Property(k => k.BudgetHours).HasPrecision(12, 2);
            modelBuilder.Entity<KpiSnapshot>().Property(k => k.BurnRatio).HasPrecision(6, 2);
            modelBuilder.Entity<KpiSnapshot>().Property(k => k.PercentComplete).HasPrecision(5, 2);
        }
    }
}
