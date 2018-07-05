namespace RockDMZ.Infrastructure
{
    using System;
    using System.Data;
    using System.Data.Entity;
    using System.Data.Entity.ModelConfiguration.Conventions;
    using System.Threading.Tasks;
    using RockDMZ.Domain;

    public class ToolsContext : DbContext
    {
        private DbContextTransaction _currentTransaction;

        public ToolsContext() : base("Server=.\\SQLEXPRESS;Database=RockDB;MultipleActiveResultSets=true;Integrated Security=SSPI;")
        {
            Database.SetInitializer<ToolsContext>(null);
        }

        public ToolsContext(string nameOrConnectionString) : base(nameOrConnectionString)
        {
            Database.SetInitializer<ToolsContext>(null);
        }

        public DbSet<ServiceAccount> ServiceAccounts { get; set; }
        public DbSet<ApiDatatable> ApiDatatables { get; set; }
        public DbSet<PriceExtensionProductFeed> PriceExtensionProductFeeds {get;set;}
        public DbSet<PriceExtensionProductPerformance> PriceExtensionProductPerformances { get; set; }
        public DbSet<AdWordsCampaignStructure> AdWordsCampaignStructures {get;set;}
        public DbSet<AdWordsCampaignTemplate> AdWordsCampaignTemplates {get;set;}
        public DbSet<AdWordsAdgroupTemplate> AdWordsAdgroupTemplates {get;set;}
        public DbSet<PriceExtensionProject> PriceExtensionProjects {get;set;}
        public DbSet<PromotionExtensionProject> PromotionExtensionProjects { get; set; }
        public DbSet<StoreData> StoreDatas { get; set; }
        public DbSet<BccSourceFeedItem> BccSourceFeedItems { get; set; }
        public DbSet<BccStoreStockItem> BccStoreStockItems { get; set; }
        public DbSet<AdWordsLocation> AdWordsLocations { get; set; }
        public DbSet<PointOfInterest> PointsOfInterest { get; set; }
        public DbSet<PointOfInterestAdWordsLocation> PointOfInterestAdWordsLocations { get; set; }
        public DbSet<AdWordsLocationProject> AdWordsLocationProjects { get; set; }
        public DbSet<AdWordsCustomTagLine> AdWordsCustomTagLines { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();

            //modelBuilder.Entity<CourseInstructor>().HasKey(ci => new { ci.CourseID, ci.InstructorID });

            //modelBuilder.Entity<Department>().MapToStoredProcedures();
        }

        public void BeginTransaction()
        {
            if (_currentTransaction != null)
            {
                return;
            }

            _currentTransaction = Database.BeginTransaction(IsolationLevel.ReadCommitted);
        }

        public async Task CommitTransactionAsync()
        {
            try
            {
                await SaveChangesAsync();

                _currentTransaction?.Commit();
            }
            catch
            {
                RollbackTransaction();
                throw;
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                }
            }
        }

        public void RollbackTransaction()
        {
            try
            {
                _currentTransaction?.Rollback();
            }
            finally
            {
                if (_currentTransaction != null)
                {
                    _currentTransaction.Dispose();
                    _currentTransaction = null;
                }
            }
        }

    }
}