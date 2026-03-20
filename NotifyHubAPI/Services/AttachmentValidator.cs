using NotifyHubAPI.Utilities;

namespace NotifyHubAPI.Services
{
    /// <summary>
    /// 附件验证结果
    /// </summary>
    public class AttachmentValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<ValidatedAttachment> ValidatedAttachments { get; set; } = new();
        public long TotalSize { get; set; }
        public int AttachmentCount { get; set; }
    }

    /// <summary>
    /// 已验证的附件
    /// </summary>
    public class ValidatedAttachment
    {
        public string FileName { get; set; } = string.Empty;
        public byte[] Content { get; set; } = Array.Empty<byte>();
        public string MimeType { get; set; } = string.Empty;
        public long Size { get; set; }
    }

    /// <summary>
    /// 附件验证服务接口
    /// </summary>
    public interface IAttachmentValidator
    {
        /// <summary>
        /// 验证附件
        /// </summary>
        /// <param name="attachments">附件字典（文件名 -> Base64 内容）</param>
        /// <returns>验证结果</returns>
        AttachmentValidationResult Validate(Dictionary<string, string> attachments);
    }

    /// <summary>
    /// 附件验证服务实现
    /// </summary>
    public class AttachmentValidator : IAttachmentValidator
    {
        private readonly AttachmentSettings _settings;
        private readonly ILogger<AttachmentValidator> _logger;

        public AttachmentValidator(
            IOptions<AttachmentSettings> settings,
            ILogger<AttachmentValidator> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public AttachmentValidationResult Validate(Dictionary<string, string> attachments)
        {
            var result = new AttachmentValidationResult
            {
                IsValid = true
            };

            // 如果没有附件，直接返回成功
            if (attachments == null || !attachments.Any())
            {
                return result;
            }

            // 1. 检查功能是否启用
            if (!_settings.EnableAttachments)
            {
                result.IsValid = false;
                result.Errors.Add("附件功能未启用");
                return result;
            }

            // 2. 检查附件数量
            if (attachments.Count > _settings.MaxAttachmentCount)
            {
                result.IsValid = false;
                result.Errors.Add($"附件数量超过限制（最多{_settings.MaxAttachmentCount}个），当前: {attachments.Count}");
            }

            var totalSize = 0L;
            var validatedList = new List<ValidatedAttachment>();

            // 3. 逐个验证附件
            foreach (var (fileName, base64Content) in attachments)
            {
                var validationResult = ValidateSingleAttachment(fileName, base64Content);

                if (!validationResult.IsValid)
                {
                    result.IsValid = false;
                    result.Errors.AddRange(validationResult.Errors);
                    continue;
                }

                totalSize += validationResult.ValidatedAttachment!.Size;
                validatedList.Add(validationResult.ValidatedAttachment);
            }

            // 4. 检查总大小
            if (totalSize > _settings.MaxTotalAttachmentSize)
            {
                result.IsValid = false;
                result.Errors.Add($"附件总大小超过限制（最大{_settings.MaxTotalAttachmentSize / 1024.0 / 1024:F2}MB），当前: {totalSize / 1024.0 / 1024:F2}MB");
            }

            result.ValidatedAttachments = validatedList;
            result.TotalSize = totalSize;
            result.AttachmentCount = validatedList.Count;

            // 记录验证日志
            _logger.LogInformation("附件验证完成，数量: {Count}, 总大小: {Size:F2}MB, 是否成功: {IsValid}",
                result.AttachmentCount, result.TotalSize / 1024.0 / 1024, result.IsValid);

            return result;
        }

        /// <summary>
        /// 验证单个附件
        /// </summary>
        private SingleAttachmentValidationResult ValidateSingleAttachment(string fileName, string base64Content)
        {
            var result = new SingleAttachmentValidationResult { IsValid = true };

            // 1. 检查文件名
            if (string.IsNullOrWhiteSpace(fileName))
            {
                result.IsValid = false;
                result.Errors.Add("文件名不能为空");
                return result;
            }

            // 2. 检查文件名长度
            if (fileName.Length > 255)
            {
                result.IsValid = false;
                result.Errors.Add($"文件名过长（最多255字符）: {fileName.Substring(0, 50)}...");
                return result;
            }

            // 3. 检查文件扩展名是否被禁止
            if (!MimeTypeHelper.IsAllowedFileType(fileName, _settings.BlockedExtensions))
            {
                var extension = Path.GetExtension(fileName).ToLowerInvariant();
                result.IsValid = false;
                result.Errors.Add($"不允许的文件类型: {fileName} ({extension})");
                return result;
            }

            // 4. 检查 Base64 内容是否为空
            if (string.IsNullOrWhiteSpace(base64Content))
            {
                result.IsValid = false;
                result.Errors.Add($"附件内容为空: {fileName}");
                return result;
            }

            // 5. 解码 Base64 并检查大小
            byte[] fileBytes;
            try
            {
                fileBytes = Convert.FromBase64String(base64Content);
            }
            catch (FormatException ex)
            {
                result.IsValid = false;
                result.Errors.Add($"附件 {fileName} 的 Base64 编码无效: {ex.Message}");
                return result;
            }

            // 6. 检查单个文件大小
            if (fileBytes.Length > _settings.MaxAttachmentSize)
            {
                result.IsValid = false;
                result.Errors.Add($"附件 {fileName} 大小超过限制（最大{_settings.MaxAttachmentSize / 1024.0 / 1024:F2}MB），当前: {fileBytes.Length / 1024.0 / 1024:F2}MB");
                return result;
            }

            // 7. 检查文件是否为空
            if (fileBytes.Length == 0)
            {
                result.IsValid = false;
                result.Errors.Add($"附件解码后内容为空: {fileName}");
                return result;
            }

            // 8. 获取 MIME 类型
            var mimeType = MimeTypeHelper.GetMimeType(fileName);

            result.IsValid = true;
            result.ValidatedAttachment = new ValidatedAttachment
            {
                FileName = fileName,
                Content = fileBytes,
                MimeType = mimeType,
                Size = fileBytes.Length
            };

            return result;
        }

        /// <summary>
        /// 单个附件验证结果
        /// </summary>
        private class SingleAttachmentValidationResult
        {
            public bool IsValid { get; set; }
            public List<string> Errors { get; set; } = new();
            public ValidatedAttachment? ValidatedAttachment { get; set; }
        }
    }
}
