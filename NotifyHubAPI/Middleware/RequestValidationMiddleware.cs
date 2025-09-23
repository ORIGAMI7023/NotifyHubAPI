using System.Text;

namespace NotifyHubAPI.Middleware
{
    /// <summary>
    /// 请求验证中间件
    /// 验证请求大小、内容类型和恶意内容
    /// </summary>
    public class RequestValidationMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<RequestValidationMiddleware> _logger;
        private readonly long _maxRequestSize;
        private readonly HashSet<string> _allowedContentTypes;
        private readonly string[] _suspiciousPatterns;

        public RequestValidationMiddleware(
            RequestDelegate next,
            ILogger<RequestValidationMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;

            _maxRequestSize = configuration.GetValue<long>("Security:MaxRequestSizeBytes", 1024 * 1024); // 1MB default

            _allowedContentTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/json",
                "application/x-www-form-urlencoded",
                "text/plain"
            };

            // 可疑模式检测
            _suspiciousPatterns = new[]
            {
                "<script",
                "javascript:",
                "vbscript:",
                "onload=",
                "onerror=",
                "eval(",
                "expression(",
                "../",
                "..\\",
                "union select",
                "drop table",
                "insert into",
                "delete from",
                "update set",
                "create table"
            };
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 检查请求大小
            if (context.Request.ContentLength.HasValue &&
                context.Request.ContentLength.Value > _maxRequestSize)
            {
                _logger.LogWarning("请求大小超出限制: {Size} bytes, IP: {IP}",
                    context.Request.ContentLength.Value, GetClientIpAddress(context));

                await WriteErrorResponse(context, 413, "请求内容过大");
                return;
            }

            // 检查Content-Type（仅POST请求）
            if (context.Request.Method == "POST" &&
                !string.IsNullOrEmpty(context.Request.ContentType))
            {
                var contentType = context.Request.ContentType.Split(';')[0].Trim();
                if (!_allowedContentTypes.Contains(contentType))
                {
                    _logger.LogWarning("不支持的Content-Type: {ContentType}, IP: {IP}",
                        contentType, GetClientIpAddress(context));

                    await WriteErrorResponse(context, 415, "不支持的媒体类型");
                    return;
                }
            }

            // 检查查询字符串和用户代理
            if (await ContainsSuspiciousContent(context))
            {
                return; // 已在方法内处理响应
            }

            await _next(context);
        }

        private async Task<bool> ContainsSuspiciousContent(HttpContext context)
        {
            var suspicious = false;
            var suspiciousContent = new List<string>();

            // 检查查询字符串
            var queryString = context.Request.QueryString.Value;
            if (!string.IsNullOrEmpty(queryString))
            {
                foreach (var pattern in _suspiciousPatterns)
                {
                    if (queryString.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        suspicious = true;
                        suspiciousContent.Add($"QueryString: {pattern}");
                    }
                }
            }

            // 检查User-Agent
            var userAgent = context.Request.Headers.UserAgent.FirstOrDefault();
            if (!string.IsNullOrEmpty(userAgent))
            {
                // 检测常见的恶意爬虫或攻击工具
                var maliciousUserAgents = new[]
                {
                    "sqlmap", "nmap", "masscan", "nikto", "dirb", "gobuster",
                    "burpsuite", "owasp", "metasploit"
                };

                foreach (var malicious in maliciousUserAgents)
                {
                    if (userAgent.Contains(malicious, StringComparison.OrdinalIgnoreCase))
                    {
                        suspicious = true;
                        suspiciousContent.Add($"UserAgent: {malicious}");
                    }
                }
            }

            // 检查路径遍历
            var path = context.Request.Path.Value;
            if (!string.IsNullOrEmpty(path) && (path.Contains("../") || path.Contains("..\\") || path.Contains("%2e%2e")))
            {
                suspicious = true;
                suspiciousContent.Add("PathTraversal");
            }

            if (suspicious)
            {
                _logger.LogWarning("检测到可疑请求内容: {Content}, IP: {IP}, Path: {Path}",
                    string.Join(", ", suspiciousContent),
                    GetClientIpAddress(context),
                    context.Request.Path);

                await WriteErrorResponse(context, 400, "请求包含不允许的内容");
                return true;
            }

            return false;
        }

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

        private static async Task WriteErrorResponse(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = new
            {
                success = false,
                message = message,
                timestamp = DateTime.UtcNow,
                requestId = Guid.NewGuid().ToString("N")[..8]
            };

            var jsonResponse = System.Text.Json.JsonSerializer.Serialize(response, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }

    public static class RequestValidationMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestValidation(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RequestValidationMiddleware>();
        }
    }
}