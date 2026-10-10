namespace Limita.API.Middlewares;

/// <summary>
/// Backward-compatible alias for ExceptionHandlingMiddleware.
/// </summary>
public class ExceptionMiddleware : ExceptionHandlingMiddleware
{
    public ExceptionMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
        : base(next, logger, environment)
    {
    }
}
