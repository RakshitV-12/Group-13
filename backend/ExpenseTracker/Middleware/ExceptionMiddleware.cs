using System.Net;
using System.Text.Json;

namespace ExpenseTracker.Middleware
{
    public class ExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ExceptionMiddleware> _logger;

        public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing request: {Message}", ex.Message);
                context.Response.ContentType = "application/json";

                context.Response.StatusCode = ex switch
                {
                    UnauthorizedAccessException => (int)HttpStatusCode.Unauthorized,
                    KeyNotFoundException => (int)HttpStatusCode.NotFound,
                    ArgumentException or InvalidOperationException => (int)HttpStatusCode.BadRequest,
                    _ => (int)HttpStatusCode.InternalServerError
                };

                var payload = new
                {
                    statusCode = context.Response.StatusCode,
                    message = ex.Message
                };

                await context.Response.WriteAsync(JsonSerializer.Serialize(payload));
            }
        }
    }
}
