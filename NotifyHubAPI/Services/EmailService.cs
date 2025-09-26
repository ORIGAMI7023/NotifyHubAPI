using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace NotifyHubAPI.Services
{
    public class SimpleEmailService : IEmailService
    {
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<SimpleEmailService> _logger;

        public SimpleEmailService(
            IOptions<SmtpSettings> smtpSettings,
            ILogger<SimpleEmailService> logger)
        {
            _smtpSettings = smtpSettings.Value;
            _logger = logger;
        }

        public async Task<EmailSendResponse> SendEmailAsync(EmailRequest emailRequest, string apiKey, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            var emailId = Guid.NewGuid();

            _logger.LogInformation("开始发送邮件，RequestId: {RequestId}, Category: {Category}", requestId, emailRequest.Category);

            try
            {
                // 直接发送邮件，不保存到数据库
                await SendEmailInternalAsync(emailRequest, cancellationToken);

                // 记录邮件发送成功日志
                LogEmailSent(emailId, emailRequest, apiKey, requestId, true, null);

                _logger.LogInformation("邮件发送成功，EmailId: {EmailId}, RequestId: {RequestId}", emailId, requestId);

                return new EmailSendResponse
                {
                    EmailId = emailId.ToString(),
                    Status = EmailStatus.Sent,
                    Message = "邮件发送成功"
                };
            }
            catch (Exception ex)
            {
                // 记录邮件发送失败日志（API密钥已验证通过）
                LogEmailSent(emailId, emailRequest, apiKey, requestId, false, ex.Message);

                _logger.LogError(ex, "邮件发送失败，EmailId: {EmailId}, RequestId: {RequestId}", emailId, requestId);

                return new EmailSendResponse
                {
                    EmailId = emailId.ToString(),
                    Status = EmailStatus.Failed,
                    Message = $"邮件发送失败: {ex.Message}"
                };
            }
        }

        /// <summary>
        /// 记录邮件发送结果的详细日志 - 直接写入独立文件
        /// </summary>
        private void LogEmailSent(Guid emailId, EmailRequest emailRequest, string apiKey, string requestId, bool isSuccess, string? errorMessage = null)
        {
            var projectName = GetProjectNameFromApiKey(apiKey);
            var recipientCount = (emailRequest.To?.Count ?? 0) +
                               (emailRequest.Cc?.Count ?? 0) +
                               (emailRequest.Bcc?.Count ?? 0);

            var status = isSuccess ? "成功" : "失败";
            var logLine = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] 邮件发送{status} | EmailId: {emailId} | RequestId: {requestId} | Project: {projectName} | " +
                         $"Category: {emailRequest.Category} | Subject: {emailRequest.Subject} | " +
                         $"To: {(emailRequest.To != null ? string.Join(", ", emailRequest.To) : "")} | " +
                         $"Cc: {(emailRequest.Cc != null ? string.Join(", ", emailRequest.Cc) : "")} | " +
                         $"Bcc: {(emailRequest.Bcc != null ? $"[{emailRequest.Bcc.Count} recipients]" : "")} | " +
                         $"Recipients: {recipientCount} | Priority: {emailRequest.Priority} | IsHtml: {emailRequest.IsHtml} | " +
                         $"BodyLength: {emailRequest.Body?.Length ?? 0} | From: {_smtpSettings.FromEmail} | " +
                         $"SentAt: {DateTime.UtcNow:O}";

            // 如果是失败状态，添加错误信息
            if (!isSuccess && !string.IsNullOrEmpty(errorMessage))
            {
                logLine += $" | Error: {errorMessage}";
            }

            // 直接写入独立的邮件日志文件
            try
            {
                var logDirectory = "logs";
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                var logFileName = Path.Combine(logDirectory, $"email-sent-{DateTime.Now:yyyyMMdd}.log");
                File.AppendAllText(logFileName, logLine + Environment.NewLine);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "写入邮件发送日志失败");
            }
        }

        /// <summary>
        /// 从API密钥获取项目名称（简化版）
        /// </summary>
        private string GetProjectNameFromApiKey(string apiKey)
        {
            // 简单的项目识别逻辑
            if (apiKey.Contains("DEFAULT"))
                return "DEFAULT";
            if (apiKey.Contains("FMS"))
                return "FMS_DATA_PROCESSOR";

            return "UNKNOWN";
        }

        public Task<bool> RetryEmailAsync(Guid emailId, CancellationToken cancellationToken = default)
        {
            _logger.LogWarning("简化版本不支持邮件重试功能");
            return Task.FromResult(false);
        }

        public Task<EmailRecord?> GetEmailStatusAsync(Guid emailId)
        {
            _logger.LogWarning("简化版本不支持邮件状态查询");
            return Task.FromResult<EmailRecord?>(null);
        }

        public Task<List<EmailRecord>> GetPendingRetryEmailsAsync(int maxRetryCount, int retryDelayMinutes)
        {
            return Task.FromResult(new List<EmailRecord>());
        }

        public Task<(List<EmailRecord> Records, int TotalCount)> GetEmailHistoryAsync(
            string? category = null,
            EmailStatus? status = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageIndex = 1,
            int pageSize = 20)
        {
            return Task.FromResult((new List<EmailRecord>(), 0));
        }

        private async Task SendEmailInternalAsync(EmailRequest emailRequest, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();

            // 设置发件人
            message.From.Add(new MailboxAddress(_smtpSettings.FromName, _smtpSettings.FromEmail));

            // 设置收件人
            foreach (var to in emailRequest.To)
            {
                message.To.Add(MailboxAddress.Parse(to.Trim()));
            }

            // 设置抄送
            if (emailRequest.Cc?.Any() == true)
            {
                foreach (var cc in emailRequest.Cc)
                {
                    message.Cc.Add(MailboxAddress.Parse(cc.Trim()));
                }
            }

            // 设置密送
            if (emailRequest.Bcc?.Any() == true)
            {
                foreach (var bcc in emailRequest.Bcc)
                {
                    message.Bcc.Add(MailboxAddress.Parse(bcc.Trim()));
                }
            }

            // 设置主题
            message.Subject = emailRequest.Subject;

            // 设置优先级
            message.Priority = emailRequest.Priority switch
            {
                EmailPriority.High => MessagePriority.Urgent,
                EmailPriority.Low => MessagePriority.NonUrgent,
                _ => MessagePriority.Normal
            };

            // 设置邮件正文
            var textFormat = emailRequest.IsHtml ? TextFormat.Html : TextFormat.Plain;
            message.Body = new TextPart(textFormat)
            {
                Text = emailRequest.Body
            };

            // 发送邮件
            using var smtpClient = new SmtpClient();

            try
            {
                // 连接到SMTP服务器
                await smtpClient.ConnectAsync(_smtpSettings.Host, _smtpSettings.Port,
                    _smtpSettings.UseSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None, cancellationToken);

                // 身份验证
                await smtpClient.AuthenticateAsync(_smtpSettings.Username, _smtpSettings.Password, cancellationToken);

                // 发送邮件
                await smtpClient.SendAsync(message, cancellationToken);

                _logger.LogInformation("SMTP邮件发送成功");
            }
            finally
            {
                if (smtpClient.IsConnected)
                {
                    await smtpClient.DisconnectAsync(true, cancellationToken);
                }
            }
        }
    }
}