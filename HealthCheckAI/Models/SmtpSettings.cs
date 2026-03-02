namespace HealthCheckAI.Models
{
    public class SmtpSettings
    {
        public string Host { get; set; } = "";
        public int Port { get; set; } = 587;
        public bool UseSsl { get; set; } = false; // 587 通常用 StartTLS -> false
        public string User { get; set; } = "";
        public string Password { get; set; } = "";
        public string FromName { get; set; } = "HealthCheckAI";
        public string FromEmail { get; set; } = "";
    }
}