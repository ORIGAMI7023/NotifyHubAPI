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
        [ProducesResponseType(typeof(ApiResponse<EmailSendResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 400)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        [ProducesResponseType(typeof(ApiResponse<object>), 500)]
        public async Task<IActionResult> SendEmail([FromBody] EmailRequest request)
        {
            try
            {
                // 获取API Key
                var apiKey = GetApiKeyFromRequest();
                if (string.IsNullOrEmpty(apiKey))
                {
                    return Unauthorized(ApiResponse<object>.FailureResult("缺少API密钥"));
                }

                // 验证API Key
                if (!_apiKeyService.IsValidApiKey(apiKey))
                {
                    return Unauthorized(ApiResponse<object>.FailureResult("无效的API密钥"));
                }

                // 自定义邮箱验证
                var emailValidationErrors = ValidateEmails(request);
                if (emailValidationErrors.Any())
                {
                    return BadRequest(ApiResponse<object>.FailureResult($"邮箱格式错误: {string.Join(", ", emailValidationErrors)}"));
                }

                // 模型验证
                if (!ModelState.IsValid)
                {
                    var errors = ModelState
                        .SelectMany(x => x.Value.Errors)
                        .Select(x => x.ErrorMessage);
                    return BadRequest(ApiResponse<object>.FailureResult($"请求参数错误: {string.Join(", ", errors)}"));
                }

                // 发送邮件
                var result = await _emailService.SendEmailAsync(request, apiKey);

                _logger.LogInformation("邮件发送请求完成，EmailId: {EmailId}, Status: {Status}",
                    result.EmailId, result.Status);

                return Ok(ApiResponse<EmailSendResponse>.SuccessResult(result, "邮件发送请求已处理"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮件发送接口异常");
                return StatusCode(500, ApiResponse<object>.FailureResult("服务器内部错误"));
            }
        }

        /// <summary>
        /// 健康检查
        /// </summary>
        /// <returns>服务状态</returns>
        [HttpGet("health")]
        [ProducesResponseType(typeof(ApiResponse<HealthCheckResponse>), 200)]
        public IActionResult HealthCheck()
        {
            var response = new HealthCheckResponse
            {
                Status = "Healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"
            };

            return Ok(ApiResponse<HealthCheckResponse>.SuccessResult(response, "服务运行正常"));
        }

        /// <summary>
        /// 从请求中获取API密钥
        /// </summary>
        /// <returns>API密钥</returns>
        private string? GetApiKeyFromRequest()
        {
            // 从Authorization Header获取Bearer Token
            var authHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
            {
                return authHeader.Substring("Bearer ".Length).Trim();
            }

            // 从X-API-Key Header获取
            var apiKeyHeader = Request.Headers["X-API-Key"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiKeyHeader))
            {
                return apiKeyHeader.Trim();
            }

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
            var emailRegex = new Regex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$");

            // 验证收件人
            if (request.To?.Any() == true)
            {
                foreach (var email in request.To)
                {
                    if (string.IsNullOrWhiteSpace(email) || !emailRegex.IsMatch(email))
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
                foreach (var email in request.Cc)
                {
                    if (!string.IsNullOrWhiteSpace(email) && !emailRegex.IsMatch(email))
                    {
                        errors.Add($"抄送邮箱格式不正确: {email}");
                    }
                }
            }

            // 验证密送
            if (request.Bcc?.Any() == true)
            {
                foreach (var email in request.Bcc)
                {
                    if (!string.IsNullOrWhiteSpace(email) && !emailRegex.IsMatch(email))
                    {
                        errors.Add($"密送邮箱格式不正确: {email}");
                    }
                }
            }

            return errors;
        }
    }

    /// <summary>
    /// 健康检查响应模型
    /// </summary>
    public class HealthCheckResponse
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Version { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
    }
}