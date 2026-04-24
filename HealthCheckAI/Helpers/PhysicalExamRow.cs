using System.Text.RegularExpressions;
using HealthCheckAI.Models;

namespace HealthCheckAI.Models
{
    public class PhysicalExamRow
    {
        public string Item { get; set; } = "";
        public string Result { get; set; } = "";
        public string Previous { get; set; } = "";
        public string Reference { get; set; } = "";
        public bool IsSection { get; set; } = false;
        public string? Extra1 { get; set; }
        public string? Extra2 { get; set; }
        public string? Extra3 { get; set; }
    }
}