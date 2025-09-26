using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace NotifyHubAPI.Middleware
{
    /// <summary>
    /// 增强版安全中间件 - 路径黑名单检测和IP封禁
    /// </summary>
    public class EnhancedSecurityMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<EnhancedSecurityMiddleware> _logger;
        private readonly IMemoryCache _cache;

        // 已知漏洞探测路径黑名单
        private readonly HashSet<string> _blacklistedPaths = new(StringComparer.OrdinalIgnoreCase)
        {
            // Java/Spring 相关
            "/solr/admin/collections",
            "/actuator/health",
            "/actuator/env",
            "/actuator/beans",
            "/actuator/configprops",
            "/druid/index.html",
            "/druid/websession.json",
            
            // VMware 相关
            "/websso/SAML2/SSO/vsphere.local",
            "/ui/login",
            "/rest/appliance/access/ssh",
            
            // 商业软件漏洞
            "/smartbi/vision/RMIServlet",
            "/suite-api/",
            "/uapjs/jsinvoke/",
            "/webapi/entry.cgi",
            
            // 常见CMS和框架
            "/wp-admin/",
            "/wp-login.php",
            "/admin/",
            "/manager/text/list",
            "/console/",
            "/phpmyadmin/",
            
            // 配置文件探测
            "/.env",
            "/.git/config",
            "/web.config",
            "/config.php",
            "/phpinfo.php",
            "/info.php",
            
            // 中间件探测
            "/jenkins/login",
            "/grafana/login",
            "/kibana/",
            "/elasticsearch/_cluster/health",
            "/redis/info",
            "/nacos/v1/auth/login",
            
            // 容器和云服务
            "/docker/containers/json",
            "/kubernetes/api/v1",
            "/aws/credentials",
            "/azure/metadata/instance",
            
            // 数据库探测
            "/mysql/",
            "/mssql/",
            "/oracle/",
            "/postgres/",
            "/mongodb/",
            
            // 应用服务器
            "/weblogic/console",
            "/jboss/management",
            "/tomcat/manager",
            "/glassfish/common/index.jsf",
            "/wildfly/console"
        };

        // 恶意载荷模式
        private readonly Regex[] _maliciousPatterns = new[]
        {
            // Log4Shell 和 JNDI 注入
            new Regex(@"\$\{jndi:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\$\{.*:.*\}", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
            // XSS 载荷
            new Regex(@"<script.*?>", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"javascript:", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"on\w+\s*=", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
            // SQL注入
            new Regex(@"union.*select", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"drop.*table", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"insert.*into", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"delete.*from", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
            // 路径穿越
            new Regex(@"\.\.[\\/]", RegexOptions.Compiled),
            new Regex(@"%2e%2e", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"\.\.%2f", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            
            // 命令注入
            new Regex(@"[;&|`].*(?:cat|ls|ps|id|whoami|uname)", RegexOptions.IgnoreCase | RegexOptions.Compiled),
            new Regex(@"(?:wget|curl|nc|netcat).*http", RegexOptions.IgnoreCase | RegexOptions.Compiled)
        };

        // 恶意User-Agent模式
        private readonly string[] _maliciousUserAgents = new[]
        {
            "sqlmap", "nmap", "masscan", "nikto", "dirb", "gobuster", "dirbuster",
            "burp", "owasp", "metasploit", "nessus", "openvas", "acunetix",
            "w3af", "skipfish", "arachni", "nuclei", "httpx", "subfinder",
            "zgrab", "shodan", "censys", "fofa", "zoomeye"
        };

        // 封禁配置
        private readonly TimeSpan _blacklistBanDuration = TimeSpan.FromDays(1);    // 黑名单路径封禁1天
        private readonly TimeSpan _maliciousBanDuration = TimeSpan.FromHours(6);   // 恶意载荷封禁6小时
        private readonly TimeSpan _scannerBanDuration = TimeSpan.FromHours(2);     // 扫描器封禁2小时
        private readonly int _maxViolationsBeforeBan = 3;                          // 3次违规后封禁

        public EnhancedSecurityMiddleware(
            RequestDelegate next,
            ILogger<EnhancedSecurityMiddleware> logger,
            IMemoryCache cache)
        {
            _next = next;
            _logger = logger;
            _cache = cache;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var clientIp = GetTrustedClientIp(context);

            // 检查IP是否已被封禁
            if (IsIpBanned(clientIp))
            {
                await HandleBannedIp(context, clientIp);
                return;
            }

            var path = context.Request.Path.Value ?? "";
            var userAgent = context.Request.Headers.UserAgent.FirstOrDefault() ?? "";
            var queryString = context.Request.QueryString.Value ?? "";
            var referer = context.Request.Headers.Referer.FirstOrDefault() ?? "";

            // 1. 检查黑名单路径 - 立即封禁1天
            if (IsBlacklistedPath(path))
            {
                await BanIpAndRespond(context, clientIp, _blacklistBanDuration, "黑名单路径访问", path);
                return;
            }

            // 2. 检查恶意载荷 - 封禁6小时
            if (ContainsMaliciousPayload(path, queryString, userAgent, referer))
            {
                await BanIpAndRespond(context, clientIp, _maliciousBanDuration, "恶意载荷检测", path);
                return;
            }

            // 3. 检查扫描器User-Agent - 累计违规后封禁
            if (IsMaliciousUserAgent(userAgent))
            {
                if (await IncrementViolationAndCheckBan(clientIp, "scanner"))
                {
                    await BanIpAndRespond(context, clientIp, _scannerBanDuration, "扫描器检测", userAgent);
                    return;
                }
            }

            // 4. 检查异常行为模式
            if (await DetectAbnormalBehavior(context, clientIp))
            {
                if (await IncrementViolationAndCheckBan(clientIp, "abnormal"))
                {
                    await BanIpAndRespond(context, clientIp, _maliciousBanDuration, "异常行为模式", path);
                    return;
                }
            }

            await _next(context);
        }

        /// <summary>
        /// 获取可信的客户端IP - 不完全信任X-Forwarded-For
        /// </summary>
        private string GetTrustedClientIp(HttpContext context)
        {
            var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";

            // 只有在已知代理环境下才考虑X-Forwarded-For
            var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
            if (!string.IsNullOrEmpty(forwardedFor))
            {
                var firstIp = forwardedFor.Split(',')[0].Trim();
                // 基本验证IP格式
                if (IPAddress.TryParse(firstIp, out _))
                {
                    // 记录原始IP用于审计
                    _logger.LogDebug("使用X-Forwarded-For IP: {ForwardedIp}, 连接IP: {RemoteIp}", firstIp, remoteIp);
                    return firstIp;
                }
            }

            return remoteIp;
        }

        /// <summary>
        /// 检查路径是否在黑名单中
        /// </summary>
        private bool IsBlacklistedPath(string path)
        {
            return _blacklistedPaths.Any(blacklisted =>
                path.StartsWith(blacklisted, StringComparison.OrdinalIgnoreCase) ||
                path.Contains(blacklisted, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 检查是否包含恶意载荷
        /// </summary>
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

        /// <summary>
        /// 检查是否为恶意User-Agent
        /// </summary>
        private bool IsMaliciousUserAgent(string userAgent)
        {
            if (string.IsNullOrEmpty(userAgent)) return false;

            return _maliciousUserAgents.Any(malicious =>
                userAgent.Contains(malicious, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 检测异常行为模式
        /// </summary>
        private async Task<bool> DetectAbnormalBehavior(HttpContext context, string clientIp)
        {
            var path = context.Request.Path.Value ?? "";

            // 检查是否频繁访问不存在的路径
            if (context.Response.StatusCode == 404)
            {
                var key = $"404_count_{clientIp}";
                var count = _cache.Get<int>(key);
                _cache.Set(key, count + 1, TimeSpan.FromMinutes(10));

                // 10分钟内超过20个404
                if (count > 20)
                {
                    return true;
                }
            }

            // 检查是否在短时间内访问多个敏感路径
            var sensitivePatterns = new[] { "/admin", "/api", "/config", "/login", "/auth" };
            if (sensitivePatterns.Any(pattern => path.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
            {
                var key = $"sensitive_access_{clientIp}";
                var count = _cache.Get<int>(key);
                _cache.Set(key, count + 1, TimeSpan.FromMinutes(5));

                // 5分钟内访问超过10个敏感路径
                if (count > 10)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 增加违规计数并检查是否需要封禁
        /// </summary>
        private async Task<bool> IncrementViolationAndCheckBan(string clientIp, string violationType)
        {
            var key = $"violation_{violationType}_{clientIp}";
            var count = _cache.Get<int>(key);
            var newCount = count + 1;
            _cache.Set(key, newCount, TimeSpan.FromHours(1));

            _logger.LogWarning("IP违规计数增加: {IP}, 类型: {Type}, 次数: {Count}",
                clientIp, violationType, newCount);

            return newCount >= _maxViolationsBeforeBan;
        }

        /// <summary>
        /// 检查IP是否已被封禁
        /// </summary>
        private bool IsIpBanned(string clientIp)
        {
            var banKey = $"banned_{clientIp}";
            return _cache.TryGetValue(banKey, out _);
        }

        /// <summary>
        /// 封禁IP并返回响应
        /// </summary>
        private async Task BanIpAndRespond(HttpContext context, string clientIp, TimeSpan duration, string reason, string detail)
        {
            // 设置封禁
            var banKey = $"banned_{clientIp}";
            var banInfo = new
            {
                Reason = reason,
                Detail = detail,
                BannedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.Add(duration)
            };
            _cache.Set(banKey, banInfo, duration);

            // 记录封禁日志
            _logger.LogWarning("IP已被封禁: {IP}, 原因: {Reason}, 详情: {Detail}, 持续时间: {Duration}",
                clientIp, reason, SanitizeLogInput(detail), duration);

            // 返回403响应
            await WriteSecurityResponse(context, 403, "Access Denied");
        }

        /// <summary>
        /// 处理已封禁IP的请求
        /// </summary>
        private async Task HandleBannedIp(HttpContext context, string clientIp)
        {
            _logger.LogDebug("已封禁IP尝试访问: {IP}, 路径: {Path}", clientIp, context.Request.Path);
            await WriteSecurityResponse(context, 403, "Access Denied");
        }

        /// <summary>
        /// 写入安全响应
        /// </summary>
        private static async Task WriteSecurityResponse(HttpContext context, int statusCode, string message)
        {
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(message);
        }

        /// <summary>
        /// 清理日志输入，防止日志注入
        /// </summary>
        private static string SanitizeLogInput(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            return Regex.Replace(input, @"[\r\n\t]", "")
                       .Replace("${", "[FILTERED]")
                       .Substring(0, Math.Min(input.Length, 200));
        }
    }

    /// <summary>
    /// 增强安全中间件扩展方法
    /// </summary>
    public static class EnhancedSecurityMiddlewareExtensions
    {
        public static IApplicationBuilder UseEnhancedSecurity(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<EnhancedSecurityMiddleware>();
        }
    }
}