using System.Net;

namespace NotifyHubAPI.Middleware
{
    /// <summary>
    /// 主机过滤中间件
    /// 限制允许访问的域名和IP，防止直接IP访问
    /// </summary>
    public class HostFilteringMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<HostFilteringMiddleware> _logger;
        private readonly HashSet<string> _allowedHosts;
        private readonly bool _allowLocalhostAccess;

        public HostFilteringMiddleware(
            RequestDelegate next,
            ILogger<HostFilteringMiddleware> logger,
            IConfiguration configuration)
        {
            _next = next;
            _logger = logger;

            // 从配置读取允许的主机
            var allowedHosts = configuration.GetSection("Security:AllowedHosts").Get<string[]>()
                ?? new[] { "notify.origami7023.cn", "localhost" };

            _allowedHosts = new HashSet<string>(allowedHosts, StringComparer.OrdinalIgnoreCase);

            // 是否允许本地访问（开发环境）
            _allowLocalhostAccess = configuration.GetValue<bool>("Security:AllowLocalhostAccess", true);
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var host = context.Request.Host.Host;
            var isAllowed = IsHostAllowed(host);

            if (!isAllowed)
            {
                _logger.LogWarning("拒绝非授权主机访问: {Host}, IP: {IP}, UserAgent: {UserAgent}",
                    context.Request.Host.Value,
                    GetClientIpAddress(context),
                    context.Request.Headers.UserAgent.FirstOrDefault());

                context.Response.StatusCode = 403;
                context.Response.ContentType = "application/json; charset=utf-8";

                await context.Response.WriteAsync(@"{
                    ""success"": false,
                    ""message"": ""访问被拒绝"",
                    ""errorCode"": ""HOST_NOT_ALLOWED"",
                    ""timestamp"": """ + DateTime.UtcNow.ToString("O") + @"""
                }");
                return;
            }

            await _next(context);
        }

        private bool IsHostAllowed(string host)
        {
            // 检查是否在允许列表中
            if (_allowedHosts.Contains(host))
                return true;

            // 开发环境允许localhost相关访问
            if (_allowLocalhostAccess && IsLocalhostAddress(host))
                return true;

            return false;
        }

        private static bool IsLocalhostAddress(string host)
        {
            // localhost 相关地址
            var localhostPatterns = new[]
            {
                "localhost",
                "127.0.0.1",
                "::1",
                "0.0.0.0"
            };

            return localhostPatterns.Any(pattern =>
                string.Equals(host, pattern, StringComparison.OrdinalIgnoreCase));
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
    }

    public static class HostFilteringMiddlewareExtensions
    {
        public static IApplicationBuilder UseCustomHostFiltering(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<HostFilteringMiddleware>();
        }
    }
}