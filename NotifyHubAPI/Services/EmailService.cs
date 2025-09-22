using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using MimeKit.Text;

namespace NotifyHubAPI.Services
{
    public class EmailService : IEmailService
    {
        private readonly NotificationDbContext _context;
        private readonly SmtpSettings _smtpSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(
            NotificationDbContext context,
            IOptions<SmtpSettings> smtpSettings,
            ILogger<EmailService> logger)
        {
            _context = context;
            _smtpSettings = smtpSettings.Value;
            _logger = logger;
        }

        public async Task<EmailSendResponse> SendEmailAsync(EmailRequest emailRequest, string apiKey, CancellationToken cancellationToken = default)
        {
            var requestId = Guid.NewGuid().ToString("N")[..8];
            _logger.LogInformation("开始发送邮件，RequestId: {RequestId}, Category: {Category}", requestId, emailRequest.Category);

            // 创建邮件记录
            var emailRecord = new EmailRecord
            {
                ToAddresses = string.Join(";", emailRequest.To),
                CcAddresses = emailRequest.Cc?.Any() == true ? string.Join(";", emailRequest.Cc) : null,
                BccAddresses = emailRequest.Bcc?.Any() == true ? string.Join(";", emailRequest.Bcc) : null,
                Subject = emailRequest.Subject,
                Body = emailRequest.Body,
                Priority = emailRequest.Priority,
                Category = emailRequest.Category,
                IsHtml = emailRequest.IsHtml,
                ApiKey = apiKey,
                RequestId = requestId,
                Status = EmailStatus.Pending
            };

            try
            {
                // 保存到数据库
                _context.EmailRecords.Add(emailRecord);
                await _context.SaveChangesAsync(cancellationToken);

                // 发送邮件
                await SendEmailInternalAsync(emailRecord, cancellationToken);

                // 更新状态为已发送
                emailRecord.Status = EmailStatus.Sent;
                emailRecord.SentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("邮件发送成功，EmailId: {EmailId}, RequestId: {RequestId}", emailRecord.Id, requestId);

                return new EmailSendResponse
                {
                    EmailId = emailRecord.Id.ToString(),
                    Status = EmailStatus.Sent,
                    Message = "邮件发送成功"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮件发送失败，EmailId: {EmailId}, RequestId: {RequestId}", emailRecord.Id, requestId);

                // 更新状态为失败
                emailRecord.Status = EmailStatus.Failed;
                emailRecord.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync(cancellationToken);

                return new EmailSendResponse
                {
                    EmailId = emailRecord.Id.ToString(),
                    Status = EmailStatus.Failed,
                    Message = $"邮件发送失败: {ex.Message}"
                };
            }
        }

        public async Task<bool> RetryEmailAsync(Guid emailId, CancellationToken cancellationToken = default)
        {
            var emailRecord = await _context.EmailRecords.FindAsync(emailId);
            if (emailRecord == null)
            {
                _logger.LogWarning("未找到邮件记录，EmailId: {EmailId}", emailId);
                return false;
            }

            if (emailRecord.Status == EmailStatus.Sent)
            {
                _logger.LogInformation("邮件已发送，无需重试，EmailId: {EmailId}", emailId);
                return true;
            }

            try
            {
                _logger.LogInformation("开始重试发送邮件，EmailId: {EmailId}, 重试次数: {RetryCount}", emailId, emailRecord.RetryCount + 1);

                // 更新重试信息
                emailRecord.Status = EmailStatus.Retrying;
                emailRecord.RetryCount++;
                emailRecord.LastRetryAt = DateTime.UtcNow;
                emailRecord.ErrorMessage = null;

                await _context.SaveChangesAsync(cancellationToken);

                // 重新发送邮件
                await SendEmailInternalAsync(emailRecord, cancellationToken);

                // 更新状态为已发送
                emailRecord.Status = EmailStatus.Sent;
                emailRecord.SentAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation("邮件重试发送成功，EmailId: {EmailId}", emailId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "邮件重试发送失败，EmailId: {EmailId}", emailId);

                // 更新状态为失败
                emailRecord.Status = EmailStatus.Failed;
                emailRecord.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync(cancellationToken);

                return false;
            }
        }

        public async Task<EmailRecord?> GetEmailStatusAsync(Guid emailId)
        {
            return await _context.EmailRecords.FindAsync(emailId);
        }

        public async Task<List<EmailRecord>> GetPendingRetryEmailsAsync(int maxRetryCount, int retryDelayMinutes)
        {
            var cutoffTime = DateTime.UtcNow.AddMinutes(-retryDelayMinutes);

            return await _context.EmailRecords
                .Where(e => e.Status == EmailStatus.Failed &&
                           e.RetryCount < maxRetryCount &&
                           (e.LastRetryAt == null || e.LastRetryAt <= cutoffTime))
                .OrderBy(e => e.CreatedAt)
                .Take(50) // 限制一次处理的数量
                .ToListAsync();
        }

        public async Task<(List<EmailRecord> Records, int TotalCount)> GetEmailHistoryAsync(
            string? category = null,
            EmailStatus? status = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageIndex = 1,
            int pageSize = 20)
        {
            var query = _context.EmailRecords.AsQueryable();

            // 应用过滤条件
            if (!string.IsNullOrEmpty(category))
                query = query.Where(e => e.Category == category);

            if (status.HasValue)
                query = query.Where(e => e.Status == status.Value);

            if (startDate.HasValue)
                query = query.Where(e => e.CreatedAt >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(e => e.CreatedAt <= endDate.Value);

            // 获取总数
            var totalCount = await query.CountAsync();

            // 分页和排序
            var records = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (records, totalCount);
        }

        private async Task SendEmailInternalAsync(EmailRecord emailRecord, CancellationToken cancellationToken = default)
        {
            var message = new MimeMessage();

            // 设置发件人
            message.From.Add(new MailboxAddress(_smtpSettings.FromName, _smtpSettings.FromEmail));

            // 设置收件人
            foreach (var to in emailRecord.ToAddresses.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                message.To.Add(MailboxAddress.Parse(to.Trim()));
            }

            // 设置抄送
            if (!string.IsNullOrEmpty(emailRecord.CcAddresses))
            {
                foreach (var cc in emailRecord.CcAddresses.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.Cc.Add(MailboxAddress.Parse(cc.Trim()));
                }
            }

            // 设置密送
            if (!string.IsNullOrEmpty(emailRecord.BccAddresses))
            {
                foreach (var bcc in emailRecord.BccAddresses.Split(';', StringSplitOptions.RemoveEmptyEntries))
                {
                    message.Bcc.Add(MailboxAddress.Parse(bcc.Trim()));
                }
            }

            // 设置主题
            message.Subject = emailRecord.Subject;

            // 设置优先级
            message.Priority = emailRecord.Priority switch
            {
                EmailPriority.High => MessagePriority.Urgent,
                EmailPriority.Low => MessagePriority.NonUrgent,
                _ => MessagePriority.Normal
            };

            // 设置邮件正文
            var textFormat = emailRecord.IsHtml ? TextFormat.Html : TextFormat.Plain;
            message.Body = new TextPart(textFormat)
            {
                Text = emailRecord.Body
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

    public class SmtpSettings
    {
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; }
        public bool UseSsl { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
    }
}