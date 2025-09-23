namespace NotifyHubAPI.Models
{
    /// <summary>
    /// 错误处理配置选项
    /// </summary>
    public class ErrorHandlingOptions
    {
        public const string SectionName = "ErrorHandling";

        /// <summary>
        /// 是否在生产环境显示详细错误信息（默认：false）
        /// </summary>
        public bool ShowDetailedErrors { get; set; } = false;

        /// <summary>
        /// 是否记录敏感信息到日志（默认：false）
        /// </summary>
        public bool LogSensitiveData { get; set; } = false;

        /// <summary>
        /// 最大收件人数量限制（默认：100）
        /// </summary>
        public int MaxRecipients { get; set; } = 100;

        /// <summary>
        /// 邮件内容最大长度（默认：50000）
        /// </summary>
        public int MaxEmailBodyLength { get; set; } = 50000;

        /// <summary>
        /// 邮件主题最大长度（默认：500）
        /// </summary>
        public int MaxSubjectLength { get; set; } = 500;

        /// <summary>
        /// API密钥掩码长度（默认：4）
        /// </summary>
        public int ApiKeyMaskLength { get; set; } = 4;

        /// <summary>
        /// 需要过滤的敏感信息关键词
        /// </summary>
        public List<string> SensitiveKeywords { get; set; } = new()
        {
            "password",
            "pwd",
            "token",
            "key",
            "secret",
            "connectionstring",
            "smtp",
            "auth"
        };
    }
}