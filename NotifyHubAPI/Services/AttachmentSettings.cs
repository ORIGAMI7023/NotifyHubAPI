namespace NotifyHubAPI.Services
{
    /// <summary>
    /// 附件配置设置
    /// </summary>
    public class AttachmentSettings
    {
        /// <summary>
        /// 单个附件最大大小（默认 5MB）
        /// </summary>
        public long MaxAttachmentSize { get; set; } = 5 * 1024 * 1024;

        /// <summary>
        /// 最多附件数量（默认 5 个）
        /// </summary>
        public int MaxAttachmentCount { get; set; } = 5;

        /// <summary>
        /// 附件总大小最大值（默认 15MB）
        /// </summary>
        public long MaxTotalAttachmentSize { get; set; } = 15 * 1024 * 1024;

        /// <summary>
        /// 是否启用附件功能（默认 true）
        /// </summary>
        public bool EnableAttachments { get; set; } = true;

        /// <summary>
        /// 禁止的文件扩展名列表
        /// </summary>
        public string[] BlockedExtensions { get; set; } = new[]
        {
            ".exe", ".bat", ".cmd", ".com", ".pif", ".scr", ".vbs", ".js", ".jar"
        };
    }
}
