using FairDraw.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairDraw.Infrastructure.Configurations;

public class ResultConfiguration : IEntityTypeConfiguration<Result>
{
    public void Configure(EntityTypeBuilder<Result> builder)
    {
        builder.ToTable("Results");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.ResultHash).IsRequired().HasMaxLength(64);
        builder.Property(r => r.CreatedAt).IsRequired();

        // A draw can only ever have one result.
        builder.HasIndex(r => r.DrawId).IsUnique();

        // Restrict (not Cascade) to avoid a second cascade path from Draw (Draw -> Option -> Result
        // in addition to the direct Draw -> Result path configured on Draw). The MVP never deletes
        // draws, but this keeps the model coherent if that changes later.
        builder.HasOne(r => r.WinnerOption)
            .WithMany()
            .HasForeignKey(r => r.WinnerOptionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
