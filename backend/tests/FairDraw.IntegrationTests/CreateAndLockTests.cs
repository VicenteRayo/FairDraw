using System.Net;
using FairDraw.Api.Dtos;

namespace FairDraw.IntegrationTests;

public class CreateAndLockTests(FairDrawApiFactory factory) : IClassFixture<FairDrawApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Create_WithValidData_Returns201WithCreatorTokenAndDraft()
    {
        var response = await _client.CreateDrawAsync("Viaje 2027", ["Roma", "Lisboa"], ["Vicente", "Juan"]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.ReadAsAsync<CreateDrawResponse>();

        Assert.Equal(8, body.DrawId.Length);
        Assert.Equal("Created", body.State);
        Assert.False(string.IsNullOrEmpty(body.CreatorToken));
        Assert.Equal(2, body.Draw.Options.Count);
        Assert.Equal(2, body.Draw.Participants.Count);
        Assert.Null(body.Draw.ConfigurationHash);
    }

    [Fact]
    public async Task Create_GeneratesDifferentUniquePublicCodes()
    {
        var codes = new HashSet<string>();
        for (var i = 0; i < 10; i++)
        {
            var response = await _client.CreateDrawAsync($"Draw {i}", ["A", "B"], ["X", "Y"]);
            var body = await response.ReadAsAsync<CreateDrawResponse>();
            codes.Add(body.DrawId);
        }

        Assert.Equal(10, codes.Count);
    }

    [Theory]
    [InlineData("", new[] { "A", "B" }, new[] { "X", "Y" })] // empty name
    [InlineData("Viaje", new[] { "A", "A" }, new[] { "X", "Y" })] // duplicate option
    [InlineData("Viaje", new[] { "A|B", "C" }, new[] { "X", "Y" })] // reserved character
    [InlineData("Viaje", new[] { "A", "B" }, new[] { "X", "X" })] // duplicate participant
    public async Task Create_WithInvalidData_Returns400(string name, string[] options, string[] participants)
    {
        var response = await _client.CreateDrawAsync(name, [.. options], [.. participants]);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lock_WithFewerThanTwoOptions_Returns400()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["OnlyOne"], ["Ana", "Bruno"])).ReadAsAsync<CreateDrawResponse>();

        var response = await _client.LockDrawAsync(create.DrawId, create.CreatorToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lock_WithFewerThanTwoParticipants_Returns400()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["OnlyOne"])).ReadAsAsync<CreateDrawResponse>();

        var response = await _client.LockDrawAsync(create.DrawId, create.CreatorToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lock_WithoutCreatorToken_Returns401()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();

        var response = await _client.LockDrawAsync(create.DrawId, "");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lock_WithWrongCreatorToken_Returns401()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();

        var response = await _client.LockDrawAsync(create.DrawId, "0000000000000000000000000000000000000000000000000000000000000000");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Lock_ComputesConfigurationHash_AndFreezesConfiguration()
    {
        var create = await (await _client.CreateDrawAsync("Cena", ["Pizza", "Sushi"], ["Ana", "Bruno", "Carla"])).ReadAsAsync<CreateDrawResponse>();

        var lockResponse = await _client.LockDrawAsync(create.DrawId, create.CreatorToken);
        Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);

        var draw = await lockResponse.ReadAsAsync<DrawDto>();
        Assert.Equal("Open", draw.State);
        Assert.False(string.IsNullOrEmpty(draw.ConfigurationHash));

        // Locked twice -> 409.
        var secondLock = await _client.LockDrawAsync(create.DrawId, create.CreatorToken);
        Assert.Equal(HttpStatusCode.Conflict, secondLock.StatusCode);
    }

    [Fact]
    public async Task Patch_AfterLock_Returns409()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();
        await _client.LockDrawAsync(create.DrawId, create.CreatorToken);

        var response = await _client.PatchDrawAsync(create.DrawId, create.CreatorToken, "Draw2", ["A", "C"], ["X", "Y"]);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Patch_InCreatedState_ReplacesConfiguration()
    {
        var create = await (await _client.CreateDrawAsync("Draw", ["A", "B"], ["X", "Y"])).ReadAsAsync<CreateDrawResponse>();

        var response = await _client.PatchDrawAsync(create.DrawId, create.CreatorToken, "Draw renamed", ["A", "B", "C"], ["X", "Y", "Z"]);

        Assert.True(response.StatusCode == HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var draw = await response.ReadAsAsync<DrawDto>();
        Assert.Equal("Draw renamed", draw.Name);
        Assert.Equal(3, draw.Options.Count);
        Assert.Equal(3, draw.Participants.Count);
    }

    [Fact]
    public async Task Get_UnknownCode_Returns404()
    {
        var response = await _client.GetDrawAsync("NOTFOUND");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
