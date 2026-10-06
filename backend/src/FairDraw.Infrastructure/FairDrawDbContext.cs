using FairDraw.Domain;
using Microsoft.EntityFrameworkCore;

namespace FairDraw.Infrastructure;

public class FairDrawDbContext(DbContextOptions<FairDrawDbContext> options) : DbContext(options)
{
    public DbSet<Draw> Draws => Set<Draw>();
    public DbSet<Option> Options => Set<Option>();
    public DbSet<Participant> Participants => Set<Participant>();
    public DbSet<Commitment> Commitments => Set<Commitment>();
    public DbSet<Reveal> Reveals => Set<Reveal>();
    public DbSet<Result> Results => Set<Result>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FairDrawDbContext).Assembly);
    }
}
