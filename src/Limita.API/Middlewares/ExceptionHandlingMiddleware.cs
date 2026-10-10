using System.Net;
using System.Text.Json;
using FluentValidation;
using Limita.Application.Common;

namespace Limita.API.Middlewares;

/// <summary>
/// Global exception handling middleware that catches domain and system exceptions
/// and converts them into uniform, structured ApiResponse payloads with appropriate HTTP status codes.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        var (statusCode, message, errors) = exception switch
        {
            KeyNotFoundException notFoundEx => (
                (int)HttpStatusCode.NotFound,
                notFoundEx.Message.Length > 0 ? notFoundEx.Message : "Resource not found.",
                new List<string> { notFoundEx.Message }),

            ValidationException validationEx => (
                (int)HttpStatusCode.BadRequest,
                "One or more validation errors occurred.",
                validationEx.Errors.Select(e => e.ErrorMessage).ToList()),

            ArgumentException argEx => (
                (int)HttpStatusCode.BadRequest,
                argEx.Message,
                new List<string> { argEx.Message }),

            InvalidOperationException invOpEx => (
                (int)HttpStatusCode.BadRequest,
                invOpEx.Message,
                new List<string> { invOpEx.Message }),

            UnauthorizedAccessException unauthEx => (
                (int)HttpStatusCode.Forbidden,
                unauthEx.Message.Length > 0 ? unauthEx.Message : "You do not have permission to access this resource.",
                new List<string> { unauthEx.Message }),

            _ => (
                (int)HttpStatusCode.InternalServerError,
                _environment.IsDevelopment()
                    ? exception.Message
                    : "An internal server error occurred while processing your request.",
                _environment.IsDevelopment() && exception.StackTrace != null
                    ? new List<string> { exception.Message, exception.StackTrace }
                    : new List<string> { "Please contact support if the issue persists." })
        };

        context.Response.StatusCode = statusCode;

        var response = ApiResponse.Fail(message, errors);
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, jsonOptions));
    }
}
