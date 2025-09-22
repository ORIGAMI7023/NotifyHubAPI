using Microsoft.EntityFrameworkCore;
using NotifyHubAPI.Data;
using NotifyHubAPI.Models;

namespace NotifyHubAPI.Services
{
    /// <summary>
    /// 数据库初始化服务
    /// 负责数据库迁移和基础数据的创建
    /// </summary>
    public interface IDatabaseInitializationService
    {
        /// <summary>
        /// 初始化数据库
        /// </summary>
        /// <returns></returns>
        Task InitializeDatabaseAsync();

        /// <summary>
        /// 检查数据库连接状态
        /// </summary>
        /// <returns></returns>
        Task<bool> CheckDatabaseConnectionAsync();

        /// <summary>
        /// 获取数据库统计信息
        /// </summary>
        /// <returns></returns>
        Task<DatabaseStats> GetDatabaseStatsAsync();
    }

    public class DatabaseInitializationService : IDatabaseInitializationService
    {
        private readonly NotificationDbContext _context;
        private readonly ILogger<DatabaseInitializationService> _logger;
        private readonly IConfiguration _configuration;

        public DatabaseInitializationService(
            NotificationDbContext context,
            ILogger<DatabaseInitializationService> logger,
            IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        public async Task InitializeDatabaseAsync()
        {
            try
            {
                _logger.LogInformation("开始初始化数据库...");

                // 检查数据库是否可以连接
                var canConnect = await _context.Database.CanConnectAsync();
                if (!canConnect)
                {
                    _logger.LogError("无法连接到数据库");
                    throw new InvalidOperationException("数据库连接失败");
                }

                // 检查是否有待应用的迁移
                var pendingMigrations = await _context.Database.GetPendingMigrationsAsync();
                if (pendingMigrations.Any())
                {
                    _logger.LogInformation("发现 {Count} 个待应用的迁移: {Migrations}",
                        pendingMigrations.Count(), string.Join(", ", pendingMigrations));

                    // 应用迁移
                    await _context.Database.MigrateAsync();
                    _logger.LogInformation("数据库迁移已完成");
                }
                else
                {
                    _logger.LogInformation("数据库已是最新版本，无需迁移");
                }

                // 创建测试数据（仅在开发环境）
                if (_configuration.GetValue<bool>("DatabaseSettings:CreateTestData", false))
                {
                    await CreateTestDataAsync();
                }

                // 清理过期的失败邮件记录（超过30天）
                await CleanupOldRecordsAsync();

                _logger.LogInformation("数据库初始化完成");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "数据库初始化失败");
                throw;
            }
        }

        public async Task<bool> CheckDatabaseConnectionAsync()
        {
            try
            {
                return await _context.Database.CanConnectAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "检查数据库连接时发生错误");
                return false;
            }
        }

        public async Task<DatabaseStats> GetDatabaseStatsAsync()
        {
            try
            {
                var stats = new DatabaseStats
                {
                    TotalEmails = await _context.EmailRecords.CountAsync(),
                    SentEmails = await _context.EmailRecords.CountAsync(e => e.Status == EmailStatus.Sent),
                    FailedEmails = await _context.EmailRecords.CountAsync(e => e.Status == EmailStatus.Failed),
                    PendingEmails = await _context.EmailRecords.CountAsync(e => e.Status == EmailStatus.Pending),
                    RetryingEmails = await _context.EmailRecords.CountAsync(e => e.Status == EmailStatus.Retrying),
                    Today = await _context.EmailRecords.CountAsync(e => e.CreatedAt.Date == DateTime.Today),
                    LastWeek = await _context.EmailRecords.CountAsync(e => e.CreatedAt >= DateTime.Today.AddDays(-7)),
                    LastMonth = await _context.EmailRecords.CountAsync(e => e.CreatedAt >= DateTime.Today.AddMonths(-1))
                };

                // 获取最常用的分类
                stats.TopCategories = await _context.EmailRecords
                    .GroupBy(e => e.Category)
                    .Select(g => new CategoryStats { Category = g.Key, Count = g.Count() })
                    .OrderByDescending(c => c.Count)
                    .Take(10)
                    .ToListAsync();

                // 获取最近的错误信息
                stats.RecentErrors = await _context.EmailRecords
                    .Where(e => e.Status == EmailStatus.Failed && !string.IsNullOrEmpty(e.ErrorMessage))
                    .OrderByDescending(e => e.CreatedAt)
                    .Take(5)
                    .Select(e => new ErrorStats
                    {
                        ErrorMessage = e.ErrorMessage,
                        Count = 1,
                        LastOccurred = e.CreatedAt
                    })
                    .ToListAsync();

                return stats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "获取数据库统计信息时发生错误");
                return new DatabaseStats();
            }
        }

        private async Task CreateTestDataAsync()
        {
            try
            {
                // 检查是否已有测试数据
                var hasData = await _context.EmailRecords.AnyAsync();
                if (hasData)
                {
                    _logger.LogInformation("数据库中已存在数据，跳过测试数据创建");
                    return;
                }

                _logger.LogInformation("创建测试数据...");

                var testEmails = new List<EmailRecord>
                {
                    new EmailRecord
                    {
                        ToAddresses = "test@example.com",
                        Subject = "系统启动通知",
                        Body = "NotifyHubAPI 系统已成功启动",
                        Category = "SYSTEM",
                        IsHtml = false,
                        Status = EmailStatus.Sent,
                        ApiKey = "default-api-key-2024",
                        CreatedAt = DateTime.UtcNow.AddHours(-2),
                        SentAt = DateTime.UtcNow.AddHours(-2).AddMinutes(1)
                    },
                    new EmailRecord
                    {
                        ToAddresses = "admin@example.com",
                        Subject = "测试邮件发送",
                        Body = "<h1>这是一封测试邮件</h1><p>用于验证系统功能</p>",
                        Category = "TEST",
                        IsHtml = true,
                        Status = EmailStatus.Failed,
                        ApiKey = "default-api-key-2024",
                        ErrorMessage = "SMTP连接超时",
                        CreatedAt = DateTime.UtcNow.AddHours(-1),
                        RetryCount = 1
                    },
                    new EmailRecord
                    {
                        ToAddresses = "user@example.com",
                        Subject = "欢迎使用邮件服务",
                        Body = "感谢您使用我们的邮件通知服务",
                        Category = "WELCOME",
                        IsHtml = false,
                        Status = EmailStatus.Pending,
                        ApiKey = "fms-secure-api-key-2024",
                        CreatedAt = DateTime.UtcNow.AddMinutes(-30)
                    }
                };

                _context.EmailRecords.AddRange(testEmails);
                await _context.SaveChangesAsync();

                _logger.LogInformation("测试数据创建完成，共创建 {Count} 条记录", testEmails.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "创建测试数据时发生错误");
            }
        }

        private async Task CleanupOldRecordsAsync()
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-30);
                var oldRecords = await _context.EmailRecords
                    .Where(e => e.Status == EmailStatus.Failed && e.CreatedAt < cutoffDate)
                    .CountAsync();

                if (oldRecords > 0)
                {
                    _logger.LogInformation("清理 {Count} 条超过30天的失败邮件记录", oldRecords);

                    await _context.EmailRecords
                        .Where(e => e.Status == EmailStatus.Failed && e.CreatedAt < cutoffDate)
                        .ExecuteDeleteAsync();

                    _logger.LogInformation("旧记录清理完成");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "清理旧记录时发生错误");
            }
        }
    }

    /// <summary>
    /// 数据库统计信息
    /// </summary>
    public class DatabaseStats
    {
        public int TotalEmails { get; set; }
        public int SentEmails { get; set; }
        public int FailedEmails { get; set; }
        public int PendingEmails { get; set; }
        public int RetryingEmails { get; set; }
        public int Today { get; set; }
        public int LastWeek { get; set; }
        public int LastMonth { get; set; }
        public List<CategoryStats> TopCategories { get; set; } = new();
        public List<ErrorStats> RecentErrors { get; set; } = new();
    }

    /// <summary>
    /// 分类统计
    /// </summary>
    public class CategoryStats
    {
        public string Category { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    /// <summary>
    /// 错误统计
    /// </summary>
    public class ErrorStats
    {
        public string ErrorMessage { get; set; } = string.Empty;
        public int Count { get; set; }
        public DateTime LastOccurred { get; set; }
    }
}