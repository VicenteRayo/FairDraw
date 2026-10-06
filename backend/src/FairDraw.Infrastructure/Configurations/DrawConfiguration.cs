using FairDraw.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairDraw.Infrastructure.Configurations;

public class DrawConfiguration : IEntityTypeConfiguration<Draw>
{
    public void Configure(EntityTypeBuilder<Draw> builder)
    {
        builder.ToTable("Draws");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.PublicCode).IsRequired().HasMaxLength(8);
        builder.HasIndex(d => d.PublicCode).IsUnique();

        builder.Property(d => d.Name).IsRequired().HasMaxLength(200);

        builder.Property(d => d.State)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(d => d.ConfigurationHash).HasMaxLength(64);
        builder.Property(d => d.CreatorTokenHash).IsRequired().HasMaxLength(64);

        builder.Property(d => d.CreatedAt).IsRequired();

        builder.HasMany(d => d.Options)
            .WithOne(o => o.Draw)
            .HasForeignKey(o => o.DrawId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Participants)
            .WithOne(p => p.Draw)
            .HasForeignKey(p => p.DrawId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(d => d.Result)
            .WithOne(r => r.Draw)
            .HasForeignKey<Result>(r => r.DrawId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
