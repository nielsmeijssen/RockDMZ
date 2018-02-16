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


        public ToolsContext(string nameOrConnectionString) : base(nameOrConnectionString)
        {
        }

        public DbSet<ServiceAccount> ServiceAccounts { get; set; }
        public DbSet<ApiDatatable> ApiDatatables { get; set; }

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