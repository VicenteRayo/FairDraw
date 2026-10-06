namespace FairDraw.Api.Errors;

/// <summary>
/// A well-known, client-facing failure with an explicit HTTP status code and a short machine-readable
/// error code (never a raw stack trace or internal detail). Caught by the global exception handler
/// in Program.cs and turned into `{ "error": code }` JSON.
/// </summary>
public class DomainException(string code, int statusCode) : Exception(code)
{
    public string Code { get; } = code;
    public int StatusCode { get; } = statusCode;
}

public static class ApiErrors
{
    public static DomainException Validation(string code) => new(code, StatusCodes.Status400BadRequest);
    public static DomainException Unauthorized(string code) => new(code, StatusCodes.Status401Unauthorized);
    public static DomainException Forbidden(string code) => new(code, StatusCodes.Status403Forbidden);
    public static DomainException NotFound(string code) => new(code, StatusCodes.Status404NotFound);
    public static DomainException Conflict(string code) => new(code, StatusCodes.Status409Conflict);
    public static DomainException Unprocessable(string code) => new(code, StatusCodes.Status422UnprocessableEntity);
}
