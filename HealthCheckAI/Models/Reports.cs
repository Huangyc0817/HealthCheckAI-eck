
using System.ComponentModel.DataAnnotations;

namespace HealthCheckAI.Models
{
    public class Report
    {
        [Key]
        public int Id { get; set; }

        public int UserId { get; set; } // 關聯到 User.Id

        public string DiagnosisA { get; set; } = string.Empty;
        public string DiagnosisB { get; set; } = string.Empty;
        public string DiagnosisC { get; set; } = string.Empty;
        public string DiagnosisD { get; set; } = string.Empty;
        public string DiagnosisE { get; set; } = string.Empty;
        public string DiagnosisF { get; set; } = string.Empty;
    }
}
