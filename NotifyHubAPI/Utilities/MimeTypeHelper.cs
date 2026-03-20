using System.Collections.Immutable;

namespace NotifyHubAPI.Utilities
{
    /// <summary>
    /// MIME 类型检测工具类
    /// 根据文件扩展名返回对应的 MIME 类型
    /// </summary>
    public static class MimeTypeHelper
    {
        private static readonly ImmutableDictionary<string, string> MimeTypes =
            new Dictionary<string, string>
            {
                // 图片
                { ".jpg", "image/jpeg" },
                { ".jpeg", "image/jpeg" },
                { ".png", "image/png" },
                { ".gif", "image/gif" },
                { ".bmp", "image/bmp" },
                { ".webp", "image/webp" },
                { ".svg", "image/svg+xml" },
                { ".ico", "image/x-icon" },
                { ".tiff", "image/tiff" },
                // 文档
                { ".pdf", "application/pdf" },
                { ".doc", "application/msword" },
                { ".docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" },
                { ".xls", "application/vnd.ms-excel" },
                { ".xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" },
                { ".ppt", "application/vnd.ms-powerpoint" },
                { ".pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation" },
                { ".odt", "application/vnd.oasis.opendocument.text" },
                { ".ods", "application/vnd.oasis.opendocument.spreadsheet" },
                { ".odp", "application/vnd.oasis.opendocument.presentation" },
                { ".rtf", "application/rtf" },
                // 压缩文件
                { ".zip", "application/zip" },
                { ".rar", "application/vnd.rar" },
                { ".7z", "application/x-7z-compressed" },
                { ".tar", "application/x-tar" },
                { ".gz", "application/gzip" },
                // 文本
                { ".txt", "text/plain" },
                { ".csv", "text/csv" },
                { ".xml", "text/xml" },
                { ".json", "application/json" },
                { ".md", "text/markdown" },
                // 音频
                { ".mp3", "audio/mpeg" },
                { ".wav", "audio/wav" },
                { ".ogg", "audio/ogg" },
                { ".m4a", "audio/mp4" },
                // 视频
                { ".mp4", "video/mp4" },
                { ".avi", "video/x-msvideo" },
                { ".mov", "video/quicktime" },
                { ".wmv", "video/x-ms-wmv" },
                { ".flv", "video/x-flv" },
                { ".webm", "video/webm" },
                // 其他
                { ".bin", "application/octet-stream" }
            }.ToImmutableDictionary();

        /// <summary>
        /// 根据文件名获取 MIME 类型
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <returns>MIME 类型字符串，未知类型返回 application/octet-stream</returns>
        public static string GetMimeType(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return "application/octet-stream";

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return MimeTypes.GetValueOrDefault(extension, "application/octet-stream");
        }

        /// <summary>
        /// 检查是否为允许的文件类型（不在禁止列表中）
        /// </summary>
        /// <param name="fileName">文件名</param>
        /// <param name="blockedExtensions">禁止的扩展名列表</param>
        /// <returns>true 表示允许，false 表示禁止</returns>
        public static bool IsAllowedFileType(string fileName, string[] blockedExtensions)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return false;

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            return !blockedExtensions.Contains(extension);
        }
    }
}
