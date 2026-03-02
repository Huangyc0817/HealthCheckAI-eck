using System.ComponentModel.DataAnnotations;

namespace HealthCheckAI.Models
{
    public class MfaOtp
    {
        [Key]
        public int OtpId { get; set; }   // ✅ 這個就是主鍵

        [Required]
        public int UserId { get; set; }

        [Required]
        public string OtpHash { get; set; } = string.Empty;

        [Required]
        public DateTime ExpireAt { get; set; }

        public DateTime? UsedAt { get; set; }
        public int FailCount { get; set; } = 0;
        public DateTime? LockedUntil { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}