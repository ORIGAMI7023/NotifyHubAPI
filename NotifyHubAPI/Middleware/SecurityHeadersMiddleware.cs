namespace NotifyHubAPI.Middleware
{
    /// <summary>
    /// 安全头中间件
    /// 添加安全相关的HTTP响应头
    /// </summary>
    public class SecurityHeadersMiddleware
    {
        private readonly RequestDelegate _next;

        public SecurityHeadersMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            // 移除服务器信息泄露
            context.Response.Headers.Remove("Server");
            context.Response.Headers.Remove("X-Powered-By");
            context.Response.Headers.Remove("X-AspNetMvc-Version");
            context.Response.Headers.Remove("X-AspNet-Version");

            // 安全地添加安全头（避免重复添加）
            AddHeaderIfNotExists(context.Response.Headers, "X-Content-Type-Options", "nosniff");
            AddHeaderIfNotExists(context.Response.Headers, "X-Frame-Options", "DENY");
            AddHeaderIfNotExists(context.Response.Headers, "X-XSS-Protection", "1; mode=block");
            AddHeaderIfNotExists(context.Response.Headers, "Referrer-Policy", "strict-origin-when-cross-origin");

            // 内容安全策略 - 针对API服务
            AddHeaderIfNotExists(context.Response.Headers, "Content-Security-Policy",
                "default-src 'none'; frame-ancestors 'none'; base-uri 'none'");

            // 严格传输安全 - 只在HTTPS且尚未存在时添加
            if (context.Request.IsHttps && !context.Response.Headers.ContainsKey("Strict-Transport-Security"))
            {
                context.Response.Headers.Add("Strict-Transport-Security",
                    "max-age=31536000; includeSubDomains");
            }

            await _next(context);
        }

        /// <summary>
        /// 安全地添加响应头，如果不存在则添加
        /// </summary>
        private static void AddHeaderIfNotExists(IHeaderDictionary headers, string key, string value)
        {
            if (!headers.ContainsKey(key))
            {
                headers.Add(key, value);
            }
        }
    }

    public static class SecurityHeadersMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityHeadersMiddleware>();
        }
    }
}