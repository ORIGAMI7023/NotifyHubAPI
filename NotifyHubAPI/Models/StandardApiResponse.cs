namespace NotifyHubAPI.Models
{
    /// <summary>
    /// 标准化API响应模型
    /// 确保所有API端点返回一致的响应格式
    /// </summary>
    public class StandardApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public string RequestId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? ErrorCode { get; set; }
        public object? Details { get; set; } // 仅开发环境使用

        /// <summary>
        /// 创建成功响应
        /// </summary>
        public static StandardApiResponse<T> CreateSuccess(T data, string message = "操作成功")
        {
            return new StandardApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                RequestId = GenerateRequestId()
            };
        }

        /// <summary>
        /// 创建失败响应
        /// </summary>
        public static StandardApiResponse<T> CreateFailure(
            string message,
            ApiErrorCode errorCode = ApiErrorCode.Unknown,
            T? data = default,
            object? details = null)
        {
            return new StandardApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = data,
                ErrorCode = errorCode.ToString(),
                Details = details,
                RequestId = GenerateRequestId()
            };
        }

        /// <summary>
        /// 创建验证错误响应
        /// </summary>
        public static StandardApiResponse<T> CreateValidationError(
            string message,
            Dictionary<string, string[]>? validationErrors = null)
        {
            return new StandardApiResponse<T>
            {
                Success = false,
                Message = message,
                ErrorCode = ApiErrorCode.ValidationError.ToString(),
                Details = validationErrors,
                RequestId = GenerateRequestId()
            };
        }

        /// <summary>
        /// 创建授权失败响应
        /// </summary>
        public static StandardApiResponse<T> CreateUnauthorized(string message = "未授权访问")
        {
            return new StandardApiResponse<T>
            {
                Success = false,
                Message = message,
                ErrorCode = ApiErrorCode.Unauthorized.ToString(),
                RequestId = GenerateRequestId()
            };
        }

        /// <summary>
        /// 创建服务器内部错误响应
        /// </summary>
        public static StandardApiResponse<T> CreateServerError(
            string message = "服务器内部错误",
            object? details = null)
        {
            return new StandardApiResponse<T>
            {
                Success = false,
                Message = message,
                ErrorCode = ApiErrorCode.ServerError.ToString(),
                Details = details,
                RequestId = GenerateRequestId()
            };
        }

        /// <summary>
        /// 创建外部服务错误响应
        /// </summary>
        public static StandardApiResponse<T> CreateExternalServiceError(string message = "外部服务调用失败")
        {
            return new StandardApiResponse<T>
            {
                Success = false,
                Message = message,
                ErrorCode = ApiErrorCode.ExternalServiceError.ToString(),
                RequestId = GenerateRequestId()
            };
        }

        private static string GenerateRequestId()
        {
            return Guid.NewGuid().ToString("N")[..8];
        }
    }

    /// <summary>
    /// API错误代码枚举
    /// 提供结构化的错误分类
    /// </summary>
    public enum ApiErrorCode
    {
        // 通用错误
        Unknown = 1000,
        ValidationError = 1001,
        InvalidParameter = 1002,

        // 认证授权错误
        Unauthorized = 2001,
        InvalidApiKey = 2002,
        MissingApiKey = 2003,
        RateLimitExceeded = 2004,

        // 邮件服务错误
        EmailSendFailed = 3001,
        SmtpConnectionFailed = 3002,
        InvalidEmailFormat = 3003,
        EmailSizeExceeded = 3004,

        // 系统错误
        ServerError = 5001,
        DatabaseError = 5002,
        ExternalServiceError = 5003,
        ConfigurationError = 5004,
        ServiceUnavailable = 5005
    }

    /// <summary>
    /// 健康检查响应模型
    /// </summary>
    public class HealthCheckResponse
    {
        public string Status { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Version { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public Dictionary<string, object>? Components { get; set; }
    }
}