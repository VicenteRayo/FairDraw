using FairDraw.Api.Errors;
using FairDraw.Crypto;
using FairDraw.Domain;
using FairDraw.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace FairDraw.Api.Services;

/// <summary>
/// Draw lifecycle operations that are always driven by a single actor (the creator) and therefore
/// do not need the Serializable-transaction-with-retry treatment applied to commit/reveal (see
/// CommitmentService/RevealService) — there is no concurrent-writer race to guard against here.
/// </summary>
public class DrawService(IDbContextFactory<FairDrawDbContext> dbFactory, TimeProvider clock)
{
    public async Task<(Draw Draw, string CreatorToken)> CreateAsync(
        string name, List<string> options, List<string> participants, CancellationToken ct)
    {
        DrawValidation.ValidateDraft(name, options, participants);

        await using var db = await dbFactory.CreateDbContextAsync(ct);

        var now = clock.GetUtcNow();
        var creatorToken = SecretGenerator.GenerateHex(32);

        var draw = new Draw
        {
            Id = Guid.NewGuid(),
            PublicCode = await GenerateUniquePublicCodeAsync(db, ct),
            Name = name,
            State = DrawState.Created,
            CreatorTokenHash = CanonicalFormats.Sha256Hex(creatorToken),
            CreatedAt = now
        };

        draw.Options = options.Select((text, i) => new Option
        {
            Id = Guid.NewGuid(),
            DrawId = draw.Id,
            Text = text,
            SortOrder = i,
            CreatedAt = now
        }).ToList();

        draw.Participants = participants.Select((n, i) => new Participant
        {
            Id = Guid.NewGuid(),
            DrawId = draw.Id,
            Name = n,
            SortOrder = i,
            CreatedAt = now
        }).ToList();

        db.Draws.Add(draw);
        await db.SaveChangesAsync(ct);

        return (draw, creatorToken);
    }

    public async Task<Draw?> FindByCodeAsync(string code, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var draw = await LoadFullDrawAsync(db, code, ct);
        return draw is null || draw.IsExpired(clock.GetUtcNow()) ? null : draw;
    }

    public async Task<Draw> ReplaceConfigurationAsync(
        string code, string creatorToken, string name, List<string> options, List<string> participants, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var draw = (await LoadFullDrawAsync(db, code, ct)).RequireNotExpired(clock);

        CredentialAuthenticator.Verify(creatorToken, draw.CreatorTokenHash, "creator_token_required", "invalid_creator_token");

        if (draw.State != DrawState.Created)
        {
            throw ApiErrors.Conflict("draw_not_editable");
        }

        DrawValidation.ValidateDraft(name, options, participants);

        var now = clock.GetUtcNow();

        // Deliberately two round trips: delete the old rows and save, THEN add the new ones and
        // save again. Doing both in one SaveChanges — whether via RemoveRange + reassigning the
        // navigation, or via Clear() + Add() on the same tracked collection — confuses EF's
        // relationship fixup into treating some of the new entities as updates to the just-deleted
        // rows (observed as either a double-DELETE or a stray UPDATE against a nonexistent Id, both
        // surfacing as DbUpdateConcurrencyException). Splitting the operation avoids relying on that
        // same-SaveChanges fixup entirely. This runs only for the creator, before Lock, so the extra
        // round trip has no meaningful cost.
        draw.Name = name;
        db.Options.RemoveRange(draw.Options);
        db.Participants.RemoveRange(draw.Participants);
        await db.SaveChangesAsync(ct);

        for (var i = 0; i < options.Count; i++)
        {
            db.Options.Add(new Option { Id = Guid.NewGuid(), DrawId = draw.Id, Text = options[i], SortOrder = i, CreatedAt = now });
        }

        for (var i = 0; i < participants.Count; i++)
        {
            db.Participants.Add(new Participant { Id = Guid.NewGuid(), DrawId = draw.Id, Name = participants[i], SortOrder = i, CreatedAt = now });
        }

        await db.SaveChangesAsync(ct);
        return (await LoadFullDrawAsync(db, code, ct)).RequireNotExpired(clock);
    }

    public async Task<Draw> LockAsync(string code, string creatorToken, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var draw = (await LoadFullDrawAsync(db, code, ct)).RequireNotExpired(clock);

        CredentialAuthenticator.Verify(creatorToken, draw.CreatorTokenHash, "creator_token_required", "invalid_creator_token");

        if (draw.State != DrawState.Created)
        {
            throw ApiErrors.Conflict("draw_not_lockable");
        }

        var orderedOptions = draw.Options.OrderBy(o => o.SortOrder).Select(o => o.Text).ToList();
        var orderedParticipants = draw.Participants.OrderBy(p => p.SortOrder).Select(p => p.Name).ToList();

        DrawValidation.ValidateReadyToLock(orderedOptions, orderedParticipants);

        var canonical = CanonicalFormats.BuildConfigurationCanonical(draw.PublicCode, orderedOptions, orderedParticipants);
        draw.ConfigurationHash = CanonicalFormats.Sha256Hex(canonical);
        draw.State = DrawState.Open;
        draw.LockedAt = clock.GetUtcNow();

        await db.SaveChangesAsync(ct);
        return draw;
    }

    public async Task<Draw> CancelAsync(string code, string creatorToken, CancellationToken ct)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var draw = (await LoadFullDrawAsync(db, code, ct)).RequireNotExpired(clock);

        CredentialAuthenticator.Verify(creatorToken, draw.CreatorTokenHash, "creator_token_required", "invalid_creator_token");

        if (draw.State is DrawState.Completed or DrawState.Cancelled)
        {
            throw ApiErrors.Conflict("draw_not_cancellable");
        }

        draw.State = DrawState.Cancelled;
        draw.CancelledAt = clock.GetUtcNow();

        await db.SaveChangesAsync(ct);
        return draw;
    }

    private static Task<Draw?> LoadFullDrawAsync(FairDrawDbContext db, string code, CancellationToken ct) =>
        db.Draws
            .Include(d => d.Options)
            .Include(d => d.Participants).ThenInclude(p => p.Commitment)
            .Include(d => d.Participants).ThenInclude(p => p.Reveal)
            .Include(d => d.Result).ThenInclude(r => r!.WinnerOption)
            .FirstOrDefaultAsync(d => d.PublicCode == code, ct);

    private static async Task<string> GenerateUniquePublicCodeAsync(FairDrawDbContext db, CancellationToken ct)
    {
        for (var i = 0; i < 10; i++)
        {
            var code = PublicCodeGenerator.Generate();
            if (!await db.Draws.AnyAsync(d => d.PublicCode == code, ct))
            {
                return code;
            }
        }

        throw new InvalidOperationException("Could not generate a unique public draw code after 10 attempts.");
    }
}
