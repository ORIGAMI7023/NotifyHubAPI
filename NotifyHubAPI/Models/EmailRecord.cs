using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NotifyHubAPI.Models
{
    [Table("EmailRecords")]
    public class EmailRecord
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(1000)]
        public string ToAddresses { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? CcAddresses { get; set; }

        [StringLength(1000)]
        public string? BccAddresses { get; set; }

        [Required]
        [StringLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        public EmailPriority Priority { get; set; }

        [Required]
        [StringLength(100)]
        public string Category { get; set; } = string.Empty;

        public bool IsHtml { get; set; }

        public EmailStatus Status { get; set; } = EmailStatus.Pending;

        [StringLength(2000)]
        public string? ErrorMessage { get; set; }

        public int RetryCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? SentAt { get; set; }

        public DateTime? LastRetryAt { get; set; }

        [Required]
        [StringLength(100)]
        public string ApiKey { get; set; } = string.Empty;

        [StringLength(50)]
        public string? RequestId { get; set; }
    }

    public enum EmailStatus
    {
        Pending = 0,
        Sent = 1,
        Failed = 2,
        Retrying = 3,
        Cancelled = 4
    }
}