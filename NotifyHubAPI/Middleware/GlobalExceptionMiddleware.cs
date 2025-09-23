using System.Net;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace NotifyHubAPI.Middleware
{
    /// <summary>
    /// 全局异常处理中间件
    /// 统一处理所有未捕获的异常，避免敏感信息泄露
    /// </summary>
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        // JSON序列化选项
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger,
            IWebHostEnvironment environment)
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
                _logger.LogError(ex, "全局异常捕获 - 路径: {Path}, 方法: {Method}, IP: {IP}, RequestId: {RequestId}",
                    context.Request.Path,
                    context.Request.Method,
                    GetClientIpAddress(context),
                    GetRequestId(context));

                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            // 避免重复处理已经开始的响应
            if (context.Response.HasStarted)
            {
                _logger.LogWarning("响应已开始，无法处理异常");
                return;
            }

            var requestId = GetRequestId(context);
            var (statusCode, message, details) = GetErrorResponse(exception);

            context.Response.StatusCode = (int)statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = new
            {
                success = false,
                message = message,
                requestId = requestId,
                timestamp = DateTime.UtcNow,
                details = _environment.IsDevelopment() ? details : null // 仅开发环境显示详细信息
            };

            var jsonResponse = JsonSerializer.Serialize(response, _jsonOptions);
            await context.Response.WriteAsync(jsonResponse);
        }

        /// <summary>
        /// 根据异常类型返回适当的错误响应
        /// </summary>
        private (HttpStatusCode statusCode, string message, object? details) GetErrorResponse(Exception exception)
        {
            return exception switch
            {
                ArgumentException or ArgumentNullException =>
                    (HttpStatusCode.BadRequest, "请求参数错误", GetSafeExceptionDetails(exception)),

                UnauthorizedAccessException =>
                    (HttpStatusCode.Unauthorized, "未授权访问", null),

                NotImplementedException =>
                    (HttpStatusCode.NotImplemented, "功能暂未实现", null),

                TimeoutException =>
                    (HttpStatusCode.RequestTimeout, "请求超时，请稍后重试", null),

                InvalidOperationException when exception.Message.Contains("API密钥") =>
                    (HttpStatusCode.InternalServerError, "系统配置错误", null),

                HttpRequestException =>
                    (HttpStatusCode.BadGateway, "外部服务调用失败", null),

                TaskCanceledException =>
                    (HttpStatusCode.RequestTimeout, "请求已取消或超时", null),

                // SMTP相关异常
                _ when exception.GetType().Name.Contains("Smtp") =>
                    (HttpStatusCode.BadGateway, "邮件服务暂时不可用", null),

                // 默认情况
                _ => (HttpStatusCode.InternalServerError, "服务器内部错误", GetSafeExceptionDetails(exception))
            };
        }

        /// <summary>
        /// 获取安全的异常详细信息（过滤敏感信息）
        /// </summary>
        private object? GetSafeExceptionDetails(Exception exception)
        {
            if (!_environment.IsDevelopment())
                return null;

            // 过滤敏感信息的异常详情
            var safeMessage = FilterSensitiveInfo(exception.Message);

            return new
            {
                type = exception.GetType().Name,
                message = safeMessage,
                // 不包含StackTrace以避免路径泄露
            };
        }

        /// <summary>
        /// 过滤敏感信息
        /// </summary>
        private static string FilterSensitiveInfo(string message)
        {
            if (string.IsNullOrEmpty(message))
                return message;

            // 过滤常见的敏感信息模式
            var patterns = new[]
            {
                @"password[=:]\s*[^\s;,]+",  // 密码
                @"pwd[=:]\s*[^\s;,]+",      // 密码缩写
                @"token[=:]\s*[^\s;,]+",    // 令牌
                @"key[=:]\s*[^\s;,]+",      // 密钥
                @"secret[=:]\s*[^\s;,]+",   // 密钥
                @"connectionstring[=:]\s*[^\s;,]+", // 连接字符串
                @"[A-Za-z]:\\[^\\]*\\",     // Windows文件路径
                @"/[a-zA-Z0-9_\-./]+/",     // Linux文件路径
            };

            var filteredMessage = message;
            foreach (var pattern in patterns)
            {
                filteredMessage = System.Text.RegularExpressions.Regex.Replace(
                    filteredMessage, pattern, "[已过滤]",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return filteredMessage;
        }

        /// <summary>
        /// 获取客户端IP地址
        /// </summary>
        private static string GetClientIpAddress(HttpContext context)
        {
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
                return forwardedFor.Split(',')[0].Trim();

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
                return realIp;

            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        /// <summary>
        /// 获取请求ID
        /// </summary>
        private static string GetRequestId(HttpContext context)
        {
            return context.TraceIdentifier ?? Guid.NewGuid().ToString("N")[..8];
        }
    }

    /// <summary>
    /// 全局异常中间件扩展方法
    /// </summary>
    public static class GlobalExceptionMiddlewareExtensions
    {
        public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<GlobalExceptionMiddleware>();
        }
    }
}