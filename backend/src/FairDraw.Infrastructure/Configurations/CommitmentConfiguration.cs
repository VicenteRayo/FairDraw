using FairDraw.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairDraw.Infrastructure.Configurations;

public class CommitmentConfiguration : IEntityTypeConfiguration<Commitment>
{
    public void Configure(EntityTypeBuilder<Commitment> builder)
    {
        builder.ToTable("Commitments");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CommitmentHash).IsRequired().HasMaxLength(64);
        builder.Property(c => c.CommittedAt).IsRequired();

        // One commitment per participant, ever (immutable once created).
        builder.HasIndex(c => c.ParticipantId).IsUnique();
        builder.HasIndex(c => c.DrawId);
    }
}
