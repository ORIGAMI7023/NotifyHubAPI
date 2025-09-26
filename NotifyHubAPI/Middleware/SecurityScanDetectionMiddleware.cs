using Microsoft.Extensions.Caching.Memory;
using System.Text.RegularExpressions;

namespace NotifyHubAPI.Middleware
{
    /// <summary>
    /// 安全扫描检测中间件
    /// 检测并阻断常见的漏洞扫描和恶意请求
    /// </summary>
    public class SecurityScanDetectionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<SecurityScanDetectionMiddleware> _logger;
        private readonly IMemoryCache _cache;

        // 已知漏洞探测路径
        private readonly HashSet<string> _knownVulnPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            "/solr/admin/collections", "/websso/SAML2/SSO/vsphere.local", "/smartbi/vision/RMIServlet",
            "/suite-api", "/uapjs/jsinvoke", "/console/", "/admin/", "/manager/", "/phpmyadmin/",
            "/.env", "/wp-admin/", "/wp-login.php", "/.git/", "/phpinfo.php", "/test.php",
            "/debug", "/actuator/", "/druid/", "/nacos/", "/jenkins/", "/grafana/",
            "/kibana/", "/elasticsearch/", "/redis/", "/mongodb/", "/mysql/", "/mssql/",
            "/oracle/", "/postgres/", "/memcached/", "/zookeeper/", "/kafka/",
            "/weblogic/", "/jboss/", "/tomcat/", "/glassfish/", "/wildfly/",
            "/struts/", "/spring/", "/hibernate/", "/mybatis/", "/fastjson/"
        };

        // 恶意载荷模式
        private readonly Regex[] _maliciousPatterns = new[]
        {
            new Regex(@"\$\{jndi:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\$\{.*:.*\}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"<script.*?>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"javascript:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"union.*select", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"drop.*table", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\.\.[\\/]", RegexOptions.Compiled),
            new Regex(@"%2e%2e", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        public SecurityScanDetectionMiddleware(
            RequestDelegate next,
            ILogger<SecurityScanDetectionMiddleware> logger,
            IMemoryCache cache)
        {
            _next = next;
            _logger = logger;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = GetClientIpAddress(context);
            var path = context.Request.Path.Value ?? "";
            var userAgent = context.Request.Headers.UserAgent.FirstOrDefault() ?? "";
            var referer = context.Request.Headers.Referer.FirstOrDefault() ?? "";

            // 检查是否为已知漏洞扫描路径
            if (IsKnownVulnPath(path))
            {
                await HandleSecurityScan(context, clientIp, "已知漏洞路径", path);
                return;
            }

            // 检查恶意载荷
            if (ContainsMaliciousPayload(path, userAgent, referer))
            {
                await HandleSecurityScan(context, clientIp, "恶意载荷", path);
                return;
            }

            // 检查是否为扫描器User-Agent
            if (IsScannerUserAgent(userAgent))
            {
                await HandleSecurityScan(context, clientIp, "扫描器特征", userAgent);
                return;
            }

            await _next(context);
        }

        private bool IsKnownVulnPath(string path)
        {
            return _knownVulnPaths.Any(vulnPath =>
                path.StartsWith(vulnPath, StringComparison.OrdinalIgnoreCase));
        }

        private bool ContainsMaliciousPayload(params string[] inputs)
        {
            foreach (var input in inputs)
            {
                if (string.IsNullOrEmpty(input)) continue;

                foreach (var pattern in _maliciousPatterns)
                {
                    if (pattern.IsMatch(input))
                        return true;
                }
            }
            return false;
        }

        private static bool IsScannerUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return false;

            var scannerPatterns = new[]
            {
                "sqlmap", "nmap", "masscan", "nikto", "dirb", "gobuster", "dirbuster",
                "burp", "owasp", "metasploit", "nessus", "openvas", "acunetix",
                "w3af", "skipfish", "arachni", "nuclei", "httpx", "subfinder"
            };

            return scannerPatterns.Any(pattern =>
                userAgent.Contains(pattern, StringComparison.OrdinalIgnoreCase));
        }

        private async Task HandleSecurityScan(HttpContext context, string clientIp, string type, string detail)
        {
            // 增加IP封禁计数
            var banKey = $"security_ban_{clientIp}";
            var currentCount = _cache.Get<int>(banKey);
            _cache.Set(banKey, currentCount + 1, TimeSpan.FromHours(24));

            // 记录安全扫描日志（降级为Debug级别避免刷屏）
            _logger.LogDebug("安全扫描检测 - IP: {IP}, 类型: {Type}, 详情: {Detail}, 计数: {Count}",
                clientIp, type, SanitizeLogInput(detail), currentCount + 1);

            // 直接返回403，不返回详细信息
            context.Response.StatusCode = 403;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync("Forbidden");
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

        /// <summary>
        /// 清理日志输入，防止日志注入
        /// </summary>
        private static string SanitizeLogInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // 移除换行符和控制字符，限制长度
            return Regex.Replace(input, @"[\r\n\t]", "")
                       .Replace("${", "[FILTERED]")
                       .Take(100)
                       .Aggregate("", (acc, c) => acc + c);
        }
    }

    public static class SecurityScanDetectionMiddlewareExtensions
    {
        public static IApplicationBuilder UseSecurityScanDetection(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SecurityScanDetectionMiddleware>();
        }
    }
}