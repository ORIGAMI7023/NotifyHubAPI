using NotificationApi.Models;

namespace NotificationApi.Services
{
    public interface IEmailService
    {
        /// <summary>
        /// 发送邮件
        /// </summary>
        /// <param name="emailRequest">邮件请求</param>
        /// <param name="apiKey">API密钥</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>邮件发送响应</returns>
        Task<EmailSendResponse> SendEmailAsync(EmailRequest emailRequest, string apiKey, CancellationToken cancellationToken = default);

        /// <summary>
        /// 重试发送失败的邮件
        /// </summary>
        /// <param name="emailId">邮件ID</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <returns>是否重试成功</returns>
        Task<bool> RetryEmailAsync(Guid emailId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取邮件发送状态
        /// </summary>
        /// <param name="emailId">邮件ID</param>
        /// <returns>邮件记录</returns>
        Task<EmailRecord?> GetEmailStatusAsync(Guid emailId);

        /// <summary>
        /// 获取待重试的邮件列表
        /// </summary>
        /// <param name="maxRetryCount">最大重试次数</param>
        /// <param name="retryDelayMinutes">重试延迟分钟数</param>
        /// <returns>待重试的邮件列表</returns>
        Task<List<EmailRecord>> GetPendingRetryEmailsAsync(int maxRetryCount, int retryDelayMinutes);

        /// <summary>
        /// 获取邮件发送历史
        /// </summary>
        /// <param name="category">分类</param>
        /// <param name="status">状态</param>
        /// <param name="startDate">开始日期</param>
        /// <param name="endDate">结束日期</param>
        /// <param name="pageIndex">页码</param>
        /// <param name="pageSize">页大小</param>
        /// <returns>邮件历史记录</returns>
        Task<(List<EmailRecord> Records, int TotalCount)> GetEmailHistoryAsync(
            string? category = null,
            EmailStatus? status = null,
            DateTime? startDate = null,
            DateTime? endDate = null,
            int pageIndex = 1,
            int pageSize = 20);
    }
}