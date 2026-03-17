using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FinanceHubFunctions.Data
{
    public class FinanceHubDbContextFactory : IDesignTimeDbContextFactory<FinanceHubDbContext>
    {
        public FinanceHubDbContext CreateDbContext(string[] args)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("ConnectionStrings__FinanceHubDb") ??
                Environment.GetEnvironmentVariable("FinanceHubDb") ??
                "Server=(localdb)\\MSSQLLocalDB;Database=FinanceHub;Trusted_Connection=True;";

            var optionsBuilder = new DbContextOptionsBuilder<FinanceHubDbContext>();
            optionsBuilder.UseSqlServer(connectionString);

            return new FinanceHubDbContext(optionsBuilder.Options);
        }
    }
}
