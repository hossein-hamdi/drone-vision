using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Management.Infrastructure.Persistence;

public sealed class ManagementDbContextFactory : IDesignTimeDbContextFactory<ManagementDbContext>
{
    public ManagementDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? "Server=localhost;Database=DroneVision;Trusted_Connection=True;TrustServerCertificate=True;";
        return new(new DbContextOptionsBuilder<ManagementDbContext>().UseSqlServer(connection).Options);
    }
}
