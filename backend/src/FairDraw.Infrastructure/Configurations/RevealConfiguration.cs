using FairDraw.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairDraw.Infrastructure.Configurations;

public class RevealConfiguration : IEntityTypeConfiguration<Reveal>
{
    public void Configure(EntityTypeBuilder<Reveal> builder)
    {
        builder.ToTable("Reveals");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.SecretHex).HasMaxLength(64);
        builder.Property(r => r.RevealedAt).IsRequired();

        // One reveal ROW per participant (may be overwritten while IsValid=false, see Reveal.cs).
        builder.HasIndex(r => r.ParticipantId).IsUnique();
        builder.HasIndex(r => r.DrawId);
    }
}
