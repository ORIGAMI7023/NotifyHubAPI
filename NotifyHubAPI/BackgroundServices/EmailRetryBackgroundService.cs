using NotifyHubAPI.Services;

namespace NotifyHubAPI.BackgroundServices
{
    public class EmailRetryBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailRetryBackgroundService> _logger;
        private readonly IConfiguration _configuration;
        private readonly TimeSpan _checkInterval;
        private readonly int _maxRetryAttempts;
        private readonly int _retryDelayMinutes;

        public EmailRetryBackgroundService(
            IServiceProvider serviceProvider,
            ILogger<EmailRetryBackgroundService> logger,
            IConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;

            // 从配置读取重试设置
            _checkInterval = TimeSpan.FromMinutes(_configuration.GetValue<int>("RetrySettings:CheckIntervalMinutes", 5));
            _maxRetryAttempts = _configuration.GetValue<int>("RetrySettings:MaxRetryAttempts", 3);
            _retryDelayMinutes = _configuration.GetValue<int>("RetrySettings:RetryDelayMinutes", 5);

            _logger.LogInformation("邮件重试服务已初始化，检查间隔: {CheckInterval}, 最大重试次数: {MaxRetryAttempts}, 重试延迟: {RetryDelayMinutes}分钟",
                _checkInterval, _maxRetryAttempts, _retryDelayMinutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("邮件重试后台服务启动");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessFailedEmails(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理失败邮件时发生异常");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // 正常取消，退出循环
                    break;
                }
            }

            _logger.LogInformation("邮件重试后台服务已停止");
        }

        private async Task ProcessFailedEmails(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();
            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

            try
            {
                // 获取待重试的邮件
                var pendingEmails = await emailService.GetPendingRetryEmailsAsync(_maxRetryAttempts, _retryDelayMinutes);

                if (pendingEmails.Count == 0)
                {
                    _logger.LogDebug("没有待重试的邮件");
                    return;
                }

                _logger.LogInformation("发现 {Count} 封待重试邮件", pendingEmails.Count);

                var successCount = 0;
                var failedCount = 0;

                foreach (var email in pendingEmails)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        _logger.LogDebug("开始重试邮件 {EmailId}, 当前重试次数: {RetryCount}",
                            email.Id, email.RetryCount);

                        var success = await emailService.RetryEmailAsync(email.Id, cancellationToken);

                        if (success)
                        {
                            successCount++;
                            _logger.LogInformation("邮件重试成功 {EmailId}", email.Id);
                        }
                        else
                        {
                            failedCount++;
                            _logger.LogWarning("邮件重试失败 {EmailId}", email.Id);
                        }

                        // 在重试之间添加小延迟，避免对SMTP服务器造成压力
                        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        failedCount++;
                        _logger.LogError(ex, "重试邮件 {EmailId} 时发生异常", email.Id);
                    }
                }

                if (successCount > 0 || failedCount > 0)
                {
                    _logger.LogInformation("邮件重试批次完成，成功: {SuccessCount}, 失败: {FailedCount}",
                        successCount, failedCount);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取待重试邮件列表时发生异常");
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("正在停止邮件重试后台服务...");
            await base.StopAsync(cancellationToken);
        }
    }
}