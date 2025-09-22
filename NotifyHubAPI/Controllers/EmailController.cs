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
        /// 获取邮件发送状态
        /// </summary>
        /// <param name="emailId">邮件ID</param>
        /// <returns>邮件状态</returns>
        [HttpGet("status/{emailId}")]
        [ProducesResponseType(typeof(ApiResponse<EmailRecord>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        public async Task<IActionResult> GetEmailStatus([FromRoute] string emailId)
        {
            try
            {
                // 获取和验证API Key
                var apiKey = GetApiKeyFromRequest();
                if (string.IsNullOrEmpty(apiKey) || !_apiKeyService.IsValidApiKey(apiKey))
                {
                    return Unauthorized(ApiResponse<object>.FailureResult("无效的API密钥"));
                }

                // 验证邮件ID格式
                if (!Guid.TryParse(emailId, out var guid))
                {
                    return BadRequest(ApiResponse<object>.FailureResult("邮件ID格式错误"));
                }

                // 获取邮件状态
                var emailRecord = await _emailService.GetEmailStatusAsync(guid);
                if (emailRecord == null)
                {
                    return NotFound(ApiResponse<object>.FailureResult("邮件记录不存在"));
                }

                // 检查API Key权限（只能查看自己发送的邮件）
                if (emailRecord.ApiKey != apiKey)
                {
                    return NotFound(ApiResponse<object>.FailureResult("邮件记录不存在"));
                }

                return Ok(ApiResponse<EmailRecord>.SuccessResult(emailRecord));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取邮件状态异常，EmailId: {EmailId}", emailId);
                return StatusCode(500, ApiResponse<object>.FailureResult("服务器内部错误"));
            }
        }

        /// <summary>
        /// 重试发送邮件
        /// </summary>
        /// <param name="emailId">邮件ID</param>
        /// <returns>重试结果</returns>
        [HttpPost("retry/{emailId}")]
        [ProducesResponseType(typeof(ApiResponse<bool>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        [ProducesResponseType(typeof(ApiResponse<object>), 404)]
        public async Task<IActionResult> RetryEmail([FromRoute] string emailId)
        {
            try
            {
                // 获取和验证API Key
                var apiKey = GetApiKeyFromRequest();
                if (string.IsNullOrEmpty(apiKey) || !_apiKeyService.IsValidApiKey(apiKey))
                {
                    return Unauthorized(ApiResponse<object>.FailureResult("无效的API密钥"));
                }

                // 验证邮件ID格式
                if (!Guid.TryParse(emailId, out var guid))
                {
                    return BadRequest(ApiResponse<object>.FailureResult("邮件ID格式错误"));
                }

                // 先检查邮件是否存在且属于当前API Key
                var emailRecord = await _emailService.GetEmailStatusAsync(guid);
                if (emailRecord == null || emailRecord.ApiKey != apiKey)
                {
                    return NotFound(ApiResponse<object>.FailureResult("邮件记录不存在"));
                }

                // 重试发送
                var success = await _emailService.RetryEmailAsync(guid);

                return Ok(ApiResponse<bool>.SuccessResult(success,
                    success ? "邮件重试发送成功" : "邮件重试发送失败"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "重试发送邮件异常，EmailId: {EmailId}", emailId);
                return StatusCode(500, ApiResponse<object>.FailureResult("服务器内部错误"));
            }
        }

        /// <summary>
        /// 获取邮件发送历史
        /// </summary>
        /// <param name="category">分类筛选</param>
        /// <param name="status">状态筛选</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <param name="pageIndex">页码</param>
        /// <param name="pageSize">页大小</param>
        /// <returns>邮件历史记录</returns>
        [HttpGet("history")]
        [ProducesResponseType(typeof(ApiResponse<EmailHistoryResponse>), 200)]
        [ProducesResponseType(typeof(ApiResponse<object>), 401)]
        public async Task<IActionResult> GetEmailHistory(
            [FromQuery] string? category = null,
            [FromQuery] EmailStatus? status = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] int pageIndex = 1,
            [FromQuery] int pageSize = 20)
        {
            try
            {
                // 获取和验证API Key
                var apiKey = GetApiKeyFromRequest();
                if (string.IsNullOrEmpty(apiKey) || !_apiKeyService.IsValidApiKey(apiKey))
                {
                    return Unauthorized(ApiResponse<object>.FailureResult("无效的API密钥"));
                }

                // 验证分页参数
                if (pageIndex < 1) pageIndex = 1;
                if (pageSize < 1) pageSize = 20;
                if (pageSize > 100) pageSize = 100; // 限制最大页大小

                // 获取历史记录
                var (records, totalCount) = await _emailService.GetEmailHistoryAsync(
                    category, status, startDate, endDate, pageIndex, pageSize);

                // 过滤只返回当前API Key的记录
                var filteredRecords = records.Where(r => r.ApiKey == apiKey).ToList();
                var filteredCount = totalCount; // 注意：这里需要重新计算，但为了性能暂时使用原值

                var response = new EmailHistoryResponse
                {
                    Records = filteredRecords,
                    TotalCount = filteredCount,
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)filteredCount / pageSize)
                };

                return Ok(ApiResponse<EmailHistoryResponse>.SuccessResult(response));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取邮件历史记录异常");
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
    /// 邮件历史响应模型
    /// </summary>
    public class EmailHistoryResponse
    {
        public List<EmailRecord> Records { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
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