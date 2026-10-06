using FairDraw.Api.Dtos;
using FairDraw.Api.Services;

namespace FairDraw.Api.Endpoints;

public static class DrawsEndpoints
{
    public static void MapDrawsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/draws");

        group.MapPost("/", async (CreateDrawRequest req, DrawService svc, CancellationToken ct) =>
        {
            var (draw, creatorToken) = await svc.CreateAsync(req.Name, req.Options ?? [], req.Participants ?? [], ct);
            var dto = DrawViewMapper.ToDto(draw);
            return Results.Created($"/api/v1/draws/{draw.PublicCode}", new CreateDrawResponse(draw.PublicCode, draw.State.ToString(), creatorToken, dto));
        });

        group.MapGet("/{code}", async (string code, DrawService svc, CancellationToken ct) =>
        {
            var draw = await svc.FindByCodeAsync(code, ct);
            return draw is null
                ? Results.NotFound(new ErrorResponse("draw_not_found"))
                : Results.Ok(DrawViewMapper.ToDto(draw));
        });

        group.MapPatch("/{code}", async (string code, UpdateDrawRequest req, HttpRequest http, DrawService svc, CancellationToken ct) =>
        {
            var token = CredentialAuthenticator.ExtractBearerToken(http.Headers.Authorization);
            var draw = await svc.ReplaceConfigurationAsync(code, token ?? string.Empty, req.Name, req.Options ?? [], req.Participants ?? [], ct);
            return Results.Ok(DrawViewMapper.ToDto(draw));
        });

        group.MapPost("/{code}/lock", async (string code, HttpRequest http, DrawService svc, CancellationToken ct) =>
        {
            var token = CredentialAuthenticator.ExtractBearerToken(http.Headers.Authorization);
            var draw = await svc.LockAsync(code, token ?? string.Empty, ct);
            return Results.Ok(DrawViewMapper.ToDto(draw));
        });

        group.MapPost("/{code}/cancel", async (string code, HttpRequest http, DrawService svc, CancellationToken ct) =>
        {
            var token = CredentialAuthenticator.ExtractBearerToken(http.Headers.Authorization);
            var draw = await svc.CancelAsync(code, token ?? string.Empty, ct);
            return Results.Ok(DrawViewMapper.ToDto(draw));
        });

        group.MapGet("/{code}/verification", async (string code, VerificationService svc, CancellationToken ct) =>
        {
            var verification = await svc.BuildAsync(code, ct);
            return verification is null
                ? Results.NotFound(new ErrorResponse("draw_not_found"))
                : Results.Ok(verification);
        });

        group.MapPost("/{code}/participants/{participantId:guid}/claim", async (string code, Guid participantId, IdentityService svc, CancellationToken ct) =>
        {
            var (secret, claimedAt) = await svc.ClaimAsync(code, participantId, ct);
            return Results.Ok(new ClaimResponse(participantId.ToString(), secret, claimedAt));
        });

        group.MapPost("/{code}/participants/{participantId:guid}/commitments", async (string code, Guid participantId, CommitRequest req, HttpRequest http, CommitmentService svc, CancellationToken ct) =>
        {
            var token = CredentialAuthenticator.ExtractBearerToken(http.Headers.Authorization);
            var result = await svc.CommitAsync(code, participantId, token, req.Commitment ?? string.Empty, req.Number, req.Secret, ct);
            return Results.Created(
                $"/api/v1/draws/{code}/participants/{participantId}/commitments",
                new CommitResponse(participantId.ToString(), result.CommittedAt));
        });

        group.MapPost("/{code}/participants/{participantId:guid}/reveals", async (string code, Guid participantId, RevealRequest req, HttpRequest http, RevealService svc, CancellationToken ct) =>
        {
            var token = CredentialAuthenticator.ExtractBearerToken(http.Headers.Authorization);
            var result = await svc.RevealAsync(code, participantId, token, req.Number, req.Secret ?? string.Empty, ct);
            var response = new RevealResponse(participantId.ToString(), result.Valid, result.RevealedAt);
            return result.Valid
                ? Results.Ok(response)
                : Results.Json(response, statusCode: StatusCodes.Status422UnprocessableEntity);
        });
    }
}
