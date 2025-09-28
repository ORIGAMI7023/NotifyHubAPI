using Microsoft.AspNetCore.Mvc;
using NotifyHubAPI.Models;
using NotifyHubAPI.Services;
using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace NotifyHubAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly IApiKeyService _apiKeyService;
        private readonly ILogger<EmailController> _logger;

        public EmailController(
            IEmailService emailService,
            IApiKeyService apiKeyService,
            ILogger<EmailController> logger)
        {
            _emailService = emailService;
            _apiKeyService = apiKeyService;
            _logger = logger;
        }

        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="request">邮件发送请求</param>
        /// <returns>发送结果</returns>
        [HttpPost("send")]
        [ProducesResponseType(typeof(StandardApiResponse<EmailSendResponse>), 200)]
        [ProducesResponseType(typeof(StandardApiResponse<object>), 400)]
        [ProducesResponseType(typeof(StandardApiResponse<object>), 401)]
        [ProducesResponseType(typeof(StandardApiResponse<object>), 500)]
        public async Task<IActionResult> SendEmail([FromBody] EmailRequest request)
        {
            try
            {
                // 获取API Key（仅从安全的Header中）
                var apiKey = GetApiKeyFromSecureHeaders();
                if (string.IsNullOrEmpty(apiKey))
                {
                    return Unauthorized(StandardApiResponse<object>.CreateUnauthorized(
                        "缺少API密钥。请使用Authorization Header或X-API-Key Header提供密钥"));
                }

                // 验证API Key
                if (!_apiKeyService.IsValidApiKey(apiKey))
                {
                    return Unauthorized(StandardApiResponse<object>.CreateUnauthorized("无效的API密钥"));
                }

                // 自定义邮箱验证
                var emailValidationErrors = ValidateEmails(request);
                if (emailValidationErrors.Any())
                {
                    return BadRequest(StandardApiResponse<object>.CreateValidationError(
                        "邮箱格式错误",
                        new Dictionary<string, string[]>
                        {
                            ["emails"] = emailValidationErrors.ToArray()
                        }));
                }

                // 模型验证
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage).ToArray() ?? Array.Empty<string>()
                    );

                    return BadRequest(StandardApiResponse<object>.CreateValidationError(
                        "请求参数错误",
                        errors));
                }

                // 发送邮件
                var result = await _emailService.SendEmailAsync(request, apiKey);

                _logger.LogInformation("邮件发送请求完成，EmailId: {EmailId}, Status: {Status}",
                    result.EmailId, result.Status);

                return Ok(StandardApiResponse<EmailSendResponse>.CreateSuccess(
                    result,
                    "邮件发送请求已处理"));
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "邮件发送参数错误");
                return BadRequest(StandardApiResponse<object>.CreateFailure(
                    "请求参数错误",
                    ApiErrorCode.InvalidParameter));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("SMTP"))
            {
                _logger.LogError(ex, "SMTP配置错误");
                return StatusCode(500, StandardApiResponse<object>.CreateFailure(
                    "邮件服务配置错误",
                    ApiErrorCode.ConfigurationError));
            }
            catch (TimeoutException ex)
            {
                _logger.LogError(ex, "邮件发送超时");
                return StatusCode(500, StandardApiResponse<object>.CreateFailure(
                    "邮件发送超时，请稍后重试",
                    ApiErrorCode.ExternalServiceError));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮件发送接口异常");
                // 不在这里返回详细错误，让全局异常处理器处理
                throw;
            }
        }

        /// <summary>
        /// 健康检查
        /// </summary>
        /// <returns>服务状态</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(StandardApiResponse<HealthCheckResponse>), 200)]
        public IActionResult HealthCheck()
        {
            try
            {
                var response = new HealthCheckResponse
                {
                    Status = "Healthy",
                    Timestamp = DateTime.UtcNow,
                    Version = "1.0.1-Secure",
                    Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown",
                    Components = new Dictionary<string, object>
                    {
                        ["smtp"] = "Available",
                        ["apiKeys"] = _apiKeyService.GetAllApiKeys().Count + " keys loaded",
                        ["mode"] = "Stateless",
                        ["security"] = new
                        {
                            queryParamApiKey = "Disabled",
                            supportedMethods = new[]
                            {
                                "Authorization: Bearer {key}",
                                "X-API-Key: {key}"
                            }
                        }
                    }
                };

                return Ok(StandardApiResponse<HealthCheckResponse>.CreateSuccess(
                    response,
                    "服务运行正常"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "健康检查异常");
                return StatusCode(500, StandardApiResponse<HealthCheckResponse>.CreateServerError(
                    "健康检查失败"));
            }
        }

        /// <summary>
        /// 从安全的Header中获取API密钥
        /// 已移除Query参数支持
        /// </summary>
        /// <returns>API密钥</returns>
        private string? GetApiKeyFromSecureHeaders()
        {
            // 1. 从Authorization Header获取Bearer Token
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            // 2. 从X-API-Key Header获取
            var apiKeyHeader = Request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader.Trim();
            }

            // 🚫 已移除Query参数支持以提高安全性
            return null;
        }

        /// <summary>
        /// 验证邮箱地址格式
        /// </summary>
        /// <param name="request">邮件请求</param>
        /// <returns>验证错误列表</returns>
        private List<string> ValidateEmails(EmailRequest request)
        {
            var errors = new List<string>();
            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.Compiled);

            // 验证收件人
            if (request.To?.Any() == true)
            {
                foreach (var email in request.To.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    if (!emailRegex.IsMatch(email.Trim()))
                    {
                        errors.Add($"收件人邮箱格式不正确: {email}");
                    }
                }
            }
            else
            {
                errors.Add("收件人不能为空");
            }

            // 验证抄送
            if (request.Cc?.Any() == true)
            {
                foreach (var email in request.Cc.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    if (!emailRegex.IsMatch(email.Trim()))
                    {
                        errors.Add($"抄送邮箱格式不正确: {email}");
                    }
                }
            }

            // 验证密送
            if (request.Bcc?.Any() == true)
            {
                foreach (var email in request.Bcc.Where(e => !string.IsNullOrWhiteSpace(e)))
                {
                    if (!emailRegex.IsMatch(email.Trim()))
                    {
                        errors.Add($"密送邮箱格式不正确: {email}");
                    }
                }
            }

            // 检查邮件总数限制（防止滥用）
            var totalRecipients = (request.To?.Count ?? 0) +
                                (request.Cc?.Count ?? 0) +
                                (request.Bcc?.Count ?? 0);

            if (totalRecipients > 100) // 可配置的限制
            {
                errors.Add($"收件人总数不能超过100个，当前: {totalRecipients}");
            }

            return errors;
        }
    }
}