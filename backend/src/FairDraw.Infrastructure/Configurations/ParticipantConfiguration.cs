using FairDraw.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FairDraw.Infrastructure.Configurations;

public class ParticipantConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> builder)
    {
        builder.ToTable("Participants");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.ClaimSecretHash).HasMaxLength(64);
        builder.Property(p => p.CreatedAt).IsRequired();

        builder.HasIndex(p => new { p.DrawId, p.SortOrder }).IsUnique();
        builder.HasIndex(p => new { p.DrawId, p.Name }).IsUnique();

        // A participant can have at most one commitment and at most one reveal (enforced by the
        // unique FK below, in addition to the service layer's explicit 409 on a second attempt).
        builder.HasOne(p => p.Commitment)
            .WithOne(c => c.Participant)
            .HasForeignKey<Commitment>(c => c.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.Reveal)
            .WithOne(r => r.Participant)
            .HasForeignKey<Reveal>(r => r.ParticipantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
