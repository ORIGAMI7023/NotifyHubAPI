using System.ComponentModel.DataAnnotations;

namespace NotifyHubAPI.Models
{
    public class EmailRequest
    {
        [Required(ErrorMessage = "收件人不能为空")]
        [EmailAddress(ErrorMessage = "收件人邮箱格式不正确")]
        public List<string> To { get; set; } = new();

        [EmailAddress(ErrorMessage = "抄送邮箱格式不正确")]
        public List<string>? Cc { get; set; }

        [EmailAddress(ErrorMessage = "密送邮箱格式不正确")]
        public List<string>? Bcc { get; set; }

        [Required(ErrorMessage = "邮件主题不能为空")]
        [StringLength(500, ErrorMessage = "主题长度不能超过500字符")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "邮件内容不能为空")]
        [StringLength(50000, ErrorMessage = "邮件内容长度不能超过50000字符")]
        public string Body { get; set; } = string.Empty;

        public EmailPriority Priority { get; set; } = EmailPriority.Normal;

        [Required(ErrorMessage = "邮件分类不能为空")]
        [StringLength(100, ErrorMessage = "分类长度不能超过100字符")]
        public string Category { get; set; } = string.Empty;

        public bool IsHtml { get; set; } = false;

        public Dictionary<string, string>? Attachments { get; set; }
    }

    public enum EmailPriority
    {
        Low = 0,
        Normal = 1,
        High = 2
    }
}