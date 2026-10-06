using System.Net;
using FairDraw.Api.Dtos;
using FairDraw.Api.Services;
using FairDraw.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FairDraw.IntegrationTests;

/// <summary>
/// Uses a controllable clock (not a shared IClassFixture, since every test here needs its own
/// starting time) to jump forward past the 30-day retention window without waiting for real time
/// to pass, and checks both halves of the feature: the immediate 404 boundary (DrawQueryExtensions)
/// and the actual physical deletion (DrawExpirationPurger).
/// </summary>
public class DrawExpirationTests : IDisposable
{
    private readonly FairDrawApiFactory _baseFactory = new();
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DrawExpirationTests()
    {
        _factory = _baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(_clock);
            });
        });
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        _baseFactory.Dispose();
    }

    [Fact]
    public async Task Draw_StillAvailable_29DaysAfterCreation()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();

        _clock.Advance(TimeSpan.FromDays(29));

        var response = await _client.GetDrawAsync(create.DrawId);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Draw_Returns404_ExactlyAt30Days()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();

        _clock.Advance(TimeSpan.FromDays(30));

        var response = await _client.GetDrawAsync(create.DrawId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ExpiresAt_IsReturnedAndMatchesCreationPlus30Days()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();

        Assert.Equal(_clock.GetUtcNow().AddDays(30), create.Draw.ExpiresAt);
    }

    [Fact]
    public async Task CompletedDraw_AlsoExpires_RegardlessOfHavingFinished()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);

        // All commits must land before anyone can reveal (that's the whole point of the protocol),
        // so build+send every commitment first, then reveal only once everyone has committed.
        var plans = participants.Select(p => (p, DrawFlow.BuildCommitment(drawId, p.ParticipantId, 5))).ToList();
        foreach (var (p, c) in plans)
        {
            await _client.CommitAsync(drawId, p.ParticipantId, p.Secret, c.Commitment);
        }
        foreach (var (p, c) in plans)
        {
            await _client.RevealAsync(drawId, p.ParticipantId, p.Secret, c.Number, c.SecretHex);
        }

        var completed = await (await _client.GetDrawAsync(drawId)).ReadAsAsync<DrawDto>();
        Assert.Equal("Completed", completed.State);

        _clock.Advance(TimeSpan.FromDays(30));

        var afterExpiry = await _client.GetDrawAsync(drawId);
        Assert.Equal(HttpStatusCode.NotFound, afterExpiry.StatusCode);

        var verification = await _client.GetVerificationAsync(drawId);
        Assert.Equal(HttpStatusCode.NotFound, verification.StatusCode);
    }

    [Fact]
    public async Task AbandonedOpenDraw_AlsoExpires_EvenThoughNeverExecuted()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();
        await _client.LockDrawAsync(create.DrawId, create.CreatorToken);
        // Nobody ever claims/commits — the draw is simply abandoned in Open.

        _clock.Advance(TimeSpan.FromDays(30));

        var response = await _client.GetDrawAsync(create.DrawId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Claim_Returns404_OnExpiredDraw()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();
        await _client.LockDrawAsync(create.DrawId, create.CreatorToken);
        var participantId = create.Draw.Participants[0].Id;

        _clock.Advance(TimeSpan.FromDays(30));

        var response = await _client.ClaimAsync(create.DrawId, participantId);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Commit_Returns404_OnExpiredDraw()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (_, _, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 5);

        _clock.Advance(TimeSpan.FromDays(30));

        var response = await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, commitment);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Purger_ActuallyDeletesExpiredDrawAndAllRelatedRows()
    {
        var (drawId, _, participants) = await DrawFlow.CreateLockAndClaimAsync(_client, "Draw", ["A", "B"], ["Ana", "Bruno"]);
        var (number, secretHex, commitment) = DrawFlow.BuildCommitment(drawId, participants[0].ParticipantId, 5);
        await _client.CommitAsync(drawId, participants[0].ParticipantId, participants[0].Secret, commitment);

        _clock.Advance(TimeSpan.FromDays(30));

        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FairDrawDbContext>>();
        var purger = new DrawExpirationPurger(
            dbFactory,
            _clock,
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DrawExpirationPurger>>());

        var deletedCount = await purger.PurgeExpiredDrawsAsync(CancellationToken.None);

        Assert.Equal(1, deletedCount);

        await using var db = await dbFactory.CreateDbContextAsync();
        var deletedParticipantId = Guid.Parse(participants[0].ParticipantId);
        Assert.False(await db.Draws.AnyAsync(d => d.PublicCode == drawId));
        Assert.False(await db.Participants.AnyAsync(p => p.Id == deletedParticipantId));
        Assert.False(await db.Commitments.AnyAsync(c => c.CommitmentHash == commitment));
    }

    [Fact]
    public async Task Purger_DoesNotTouchDrawsWithinRetention()
    {
        await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"]);
        _clock.Advance(TimeSpan.FromDays(10));

        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<FairDrawDbContext>>();
        var purger = new DrawExpirationPurger(
            dbFactory,
            _clock,
            scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DrawExpirationPurger>>());

        var deletedCount = await purger.PurgeExpiredDrawsAsync(CancellationToken.None);

        Assert.Equal(0, deletedCount);
    }
}
