namespace NotificationApi.Models
{
    public class ApiResponse<T>
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }
        public string? RequestId { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        public static ApiResponse<T> SuccessResult(T data, string message = "操作成功")
        {
            return new ApiResponse<T>
            {
                Success = true,
                Message = message,
                Data = data,
                RequestId = Guid.NewGuid().ToString("N")[..8]
            };
        }

        public static ApiResponse<T> FailureResult(string message, T? data = default)
        {
            return new ApiResponse<T>
            {
                Success = false,
                Message = message,
                Data = data,
                RequestId = Guid.NewGuid().ToString("N")[..8]
            };
        }
    }

    public class EmailSendResponse
    {
        public string EmailId { get; set; } = string.Empty;
        public EmailStatus Status { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}