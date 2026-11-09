namespace DocAgent.Api;

/// <summary>The error responses the endpoints share. All of them are ProblemDetails (RFC 9457).</summary>
internal static class ApiResults
{
    public static IResult RunNotFound(Guid runId) =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Run not found",
            detail: $"There is no run {runId}.");

    /// <summary>400 with the problems listed per field: the shape a form can show next to its inputs.</summary>
    public static IResult Invalid(IDictionary<string, string[]> errors) => TypedResults.ValidationProblem(errors);

    public static IResult Invalid(string field, string message) =>
        Invalid(new Dictionary<string, string[]> { [field] = [message] });

    /// <summary>For endpoints that read the form themselves: a broken or oversized body becomes a 400.</summary>
    public static async Task<IFormCollection?> TryReadFormAsync(HttpRequest request, CancellationToken ct)
    {
        try
        {
            return await request.ReadFormAsync(ct);
        }
        catch (Exception ex) when (ex is InvalidDataException or BadHttpRequestException)
        {
            return null;
        }
    }
}
