using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FairDraw.Infrastructure;

/// <summary>
/// Design-time factory so `dotnet ef migrations add` works without needing the API project's
/// full DI setup or a reachable database. The connection string here is never used at runtime;
/// the API configures the real one from configuration in Program.cs.
/// </summary>
public class FairDrawDbContextFactory : IDesignTimeDbContextFactory<FairDrawDbContext>
{
    public FairDrawDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FairDrawDbContext>();
        optionsBuilder.UseNpgsql("Host=localhost;Database=fairdraw;Username=fairdraw;Password=design-time-only");
        return new FairDrawDbContext(optionsBuilder.Options);
    }
}
