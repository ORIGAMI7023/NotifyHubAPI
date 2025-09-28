using Microsoft.AspNetCore.Http;
using NotifyHubAPI.Services;
using System.Text.Json;
using System.Text.Encodings.Web;

namespace NotifyHubAPI.Middleware
{
    /// <summary>
    /// API密钥认证中间件 - 安全版本
    /// 仅支持安全的Header方式：Authorization Bearer Token 和 X-API-Key Header
    /// 已移除不安全的Query参数支持
    /// </summary>
    public class ApiKeyMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiKeyMiddleware> _logger;
        private readonly IServiceProvider _serviceProvider;

        // 不需要API Key验证的路径
        private readonly HashSet<string> _excludedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/",
            "/swagger",
            "/swagger/index.html",
            "/swagger/v1/swagger.json",
            "/health",
            "/api/email/health",
            "/info"
        };

        // JSON序列化选项 - 避免Unicode转义
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger, IServiceProvider serviceProvider)
        {
            _next = next;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 检查是否为排除的路径
            if (IsExcludedPath(context.Request.Path))
            {
                await _next(context);
                return;
            }

            // 获取API Key服务（使用作用域）
            using var scope = _serviceProvider.CreateScope();
            var apiKeyService = scope.ServiceProvider.GetRequiredService<IApiKeyService>();

            // 从请求中获取API Key（仅安全方式）
            var apiKey = GetApiKeyFromSecureHeaders(context);

            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("API请求缺少密钥，路径: {Path}, IP: {IP}, UserAgent: {UserAgent}",
                    context.Request.Path, GetClientIpAddress(context),
                    context.Request.Headers.UserAgent.FirstOrDefault());
                await WriteUnauthorizedResponse(context, "缺少API密钥。请使用Authorization Header或X-API-Key Header提供密钥");
                return;
            }

            // 验证API Key
            if (!apiKeyService.IsValidApiKey(apiKey))
            {
                _logger.LogWarning("无效的API密钥访问尝试，密钥: {ApiKey}, 路径: {Path}, IP: {IP}",
                    MaskApiKey(apiKey), context.Request.Path, GetClientIpAddress(context));
                await WriteUnauthorizedResponse(context, "无效的API密钥");
                return;
            }

            // 获取项目名称并添加到请求上下文
            var projectName = apiKeyService.GetProjectByApiKey(apiKey);
            if (!string.IsNullOrEmpty(projectName))
            {
                context.Items["ProjectName"] = projectName;
                context.Items["ApiKey"] = apiKey;
            }

            _logger.LogDebug("API密钥验证通过，项目: {ProjectName}, 路径: {Path}",
                projectName ?? "Unknown", context.Request.Path);

            // 继续处理请求
            await _next(context);
        }

        /// <summary>
        /// 检查路径是否在排除列表中
        /// </summary>
        private bool IsExcludedPath(PathString path)
        {
            // 完全匹配
            if (_excludedPaths.Contains(path.Value))
                return true;

            // Swagger相关路径
            if (path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
                return true;

            // 健康检查路径
            if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// 从安全的HTTP Header中获取API密钥
        /// 移除了不安全的Query参数支持
        /// </summary>
        private static string? GetApiKeyFromSecureHeaders(HttpContext context)
        {
            // 1. 从Authorization Header获取Bearer Token (推荐方式)
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            // 2. 从X-API-Key Header获取 (备选方式)
            var apiKeyHeader = context.Request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader.Trim();
            }

            // 🚫 已移除Query参数支持以提高安全性
            // 不再支持 ?apikey=xxx 方式

            return null;
        }

        /// <summary>
        /// 获取客户端IP地址
        /// </summary>
        private static string GetClientIpAddress(HttpContext context)
        {
            // 检查反向代理头
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                return forwardedFor.Split(',')[0].Trim();
            }

            var realIp = context.Request.Headers["X-Real-IP"].FirstOrDefault();
            if (!string.IsNullOrEmpty(realIp))
            {
                return realIp;
            }

            // 使用连接的远程IP
            return context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        }

        /// <summary>
        /// 掩码API密钥用于日志记录
        /// </summary>
        private static string MaskApiKey(string apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return "Empty";

            if (apiKey.Length < 8)
            {
                // 小于8位全隐藏
                return new string('*', apiKey.Length);
            }
            else if (apiKey.Length <= 20)
            {
                // 8-20位隐藏中间8位
                var start = apiKey.Substring(0, 4);
                var end = apiKey.Substring(apiKey.Length - 4);
                return start + new string('*', 8) + end;
            }
            else
            {
                // 大于20位隐藏前后8位中间的部分
                var start = apiKey.Substring(0, 8);
                var end = apiKey.Substring(apiKey.Length - 8);
                var middleLength = apiKey.Length - 16;
                return start + new string('*', middleLength) + end;
            }
        }

        /// <summary>
        /// 写入未授权响应
        /// </summary>
        private async Task WriteUnauthorizedResponse(HttpContext context, string message)
        {
            context.Response.StatusCode = 401;
            context.Response.ContentType = "application/json; charset=utf-8";

            var errorCode = message.Contains("缺少") ?
                ApiErrorCode.MissingApiKey :
                ApiErrorCode.InvalidApiKey;

            var response = StandardApiResponse<object>.CreateFailure(
                message,
                errorCode,
                details: new
                {
                    supportedMethods = new[]
                    {
                        "Authorization: Bearer {your-api-key}",
                        "X-API-Key: {your-api-key}"
                    },
                    securityNote = "Query参数方式已禁用以提高安全性"
                }
            );

            var jsonResponse = JsonSerializer.Serialize(response, _jsonOptions);
            await context.Response.WriteAsync(jsonResponse);
        }
    }

    /// <summary>
    /// API密钥中间件扩展方法
    /// </summary>
    public static class ApiKeyMiddlewareExtensions
    {
        /// <summary>
        /// 添加API密钥认证中间件
        /// </summary>
        public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<ApiKeyMiddleware>();
        }
    }
}