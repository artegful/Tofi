namespace Travelling.Middleware
{
    public class RequestLoggingMiddleware
    {
        private readonly RequestDelegate next;
        private readonly ILogger logger;

        public RequestLoggingMiddleware(RequestDelegate next, ILoggerFactory loggerFactory)
        {
            this.next = next;
            logger = loggerFactory.CreateLogger<RequestLoggingMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await next(context);
            }
            finally
            {
                LogLevel level = context?.Response?.StatusCode >= 400 ? LogLevel.Error : LogLevel.Information;

                logger.Log(level,                    
                    "Request {method} {url} ({query}) => {statusCode}",
                    context.Request?.Method,
                    context.Request?.Path.Value,
                    context.Request?.QueryString.ToString(),
                    context.Response?.StatusCode);
            }
        }
    }
}
