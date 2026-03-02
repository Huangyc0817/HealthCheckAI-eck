using HealthCheckAI.Models;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace HealthCheckAI.Services
{
    public interface IEmailService
    {
        Task SendOtpAsync(string toEmail, string otp);
    }

    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _smtp;

        public EmailService(IOptions<SmtpSettings> smtpOptions)
        {
            _smtp = smtpOptions.Value;
        }

        public async Task SendOtpAsync(string toEmail, string otp)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_smtp.FromName, _smtp.FromEmail));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = "HealthCheckAI 登入驗證碼";

            var body = $@"
您好，

您的登入驗證碼為：{otp}
有效時間：5 分鐘

若非本人操作，請忽略此信件。
";
            message.Body = new TextPart("plain") { Text = body };

            using var client = new SmtpClient();

            // 587: StartTLS；465: SSL
            var secureOption = _smtp.UseSsl ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;

            await client.ConnectAsync(_smtp.Host, _smtp.Port, secureOption);

            // 有些 SMTP 會要求移除 XOAUTH2
            client.AuthenticationMechanisms.Remove("XOAUTH2");

            await client.AuthenticateAsync(_smtp.User, _smtp.Password);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }
    }
}