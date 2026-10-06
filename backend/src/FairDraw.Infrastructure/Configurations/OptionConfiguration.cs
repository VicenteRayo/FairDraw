using FairDraw.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairDraw.Infrastructure.Configurations;

public class OptionConfiguration : IEntityTypeConfiguration<Option>
{
    public void Configure(EntityTypeBuilder<Option> builder)
    {
        builder.ToTable("Options");
        builder.HasKey(o => o.Id);

        builder.Property(o => o.Text).IsRequired().HasMaxLength(200);
        builder.Property(o => o.CreatedAt).IsRequired();

        // Unique per draw: no duplicate positions, no duplicate texts (also keeps canonical
        // configuration strings free of accidental collisions).
        builder.HasIndex(o => new { o.DrawId, o.SortOrder }).IsUnique();
        builder.HasIndex(o => new { o.DrawId, o.Text }).IsUnique();
    }
}
