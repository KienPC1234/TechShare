using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Threading.Tasks;
using DotNetEnv;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace TechShare.Classes
{
    public enum EmailStatus
    {
        Success,
        Failure,
        SmtpConnectionError,
        AuthenticationError,
        SendError
    }

    public class MailToolBox
    {
        private readonly ILogger<MailToolBox> _logger;

        public MailToolBox(ILogger<MailToolBox> logger)
        {
            _logger = logger;
        }

        public static void EnsureEnvFileAndLoad()
        {
            string envPath = Path.Combine(AppContext.BaseDirectory, ".env");

            if (!File.Exists(envPath))
            {
                string defaultContent =
@"SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USER=your_email@gmail.com
SMTP_PASS=your_app_password
SMTP_SSL=false
API_KEY=your_api_key
ENCRYPTION_AES_KEY=your_aes_key
ENCRYPTION_AES_IV=your_aes_iv
RECAPTCHA_SITE_KEY=Your_RECAPTCHA_Site_Key
RECAPTCHA_SECRET_KEY=Your_RECAPTCHA_Secret_Key
";

                File.WriteAllText(envPath, defaultContent);
                Console.WriteLine("Fill env in: {0}", envPath);
            }

            Env.Load(envPath);
            Console.WriteLine("Env file loaded in {0}", envPath);
        }

        public EmailStatus SendEmail(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogError("Địa chỉ email nhận rỗng hoặc không hợp lệ.");
                return EmailStatus.Failure;
            }

            string smtpHost = Env.GetString("SMTP_HOST") ?? "smtp.gmail.com";
            int smtpPort = Env.GetInt("SMTP_PORT", 587);
            string smtpUser = Env.GetString("SMTP_USER");
            string smtpPass = Env.GetString("SMTP_PASS");
            bool useSsl = Env.GetBool("SMTP_SSL", false);

            if (string.IsNullOrWhiteSpace(smtpHost) || string.IsNullOrWhiteSpace(smtpUser) || string.IsNullOrWhiteSpace(smtpPass))
            {
                _logger.LogError("Cấu hình SMTP không đầy đủ: SMTP_HOST={Host}, SMTP_USER={User}, SMTP_PASS={Pass}", smtpHost, smtpUser, smtpPass != null ? "[đã đặt]" : "[null]");
                return EmailStatus.Failure;
            }

            // Chọn SecureSocketOptions phù hợp
            SecureSocketOptions socketOptions;
            if (smtpPort == 465)
                socketOptions = SecureSocketOptions.SslOnConnect;
            else if (smtpPort == 587)
                socketOptions = SecureSocketOptions.StartTls;
            else
                socketOptions = useSsl ? SecureSocketOptions.StartTls : SecureSocketOptions.None;

            // Kiểm tra kết nối mạng trước
            try
            {
                using var tcpClient = new TcpClient();
                tcpClient.Connect(smtpHost, smtpPort);
                _logger.LogInformation("Kết nối mạng tới {SmtpHost}:{SmtpPort} thành công.", smtpHost, smtpPort);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không thể kết nối mạng tới {SmtpHost}:{SmtpPort}.", smtpHost, smtpPort);
                return EmailStatus.SmtpConnectionError;
            }

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("TechShare", smtpUser));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = htmlBody };
            message.Body = bodyBuilder.ToMessageBody();

            try
            {
                using var smtpClient = new SmtpClient();

                _logger.LogInformation("Đang kết nối đến SMTP server {SmtpHost}:{SmtpPort} với chế độ {Mode}...", smtpHost, smtpPort, socketOptions);
                smtpClient.Connect(smtpHost, smtpPort, socketOptions);

                _logger.LogInformation("Đang xác thực với SMTP_USER={SmtpUser}...", smtpUser);
                smtpClient.Authenticate(smtpUser, smtpPass);

                _logger.LogInformation("Đang gửi email đến {ToEmail}...", toEmail);
                smtpClient.Send(message);
                smtpClient.Disconnect(true);

                _logger.LogInformation("Gửi email thành công đến {ToEmail}.", toEmail);
                return EmailStatus.Success;
            }
            catch (SmtpCommandException ex)
            {
                _logger.LogError(ex, "Lỗi lệnh SMTP khi gửi email đến {ToEmail}.", toEmail);
                return EmailStatus.SendError;
            }
            catch (SmtpProtocolException ex)
            {
                _logger.LogError(ex, "Lỗi giao thức SMTP khi gửi email đến {ToEmail}.", toEmail);
                return EmailStatus.SmtpConnectionError;
            }
            catch (MailKit.Security.AuthenticationException ex)
            {
                _logger.LogError(ex, "Lỗi xác thực khi gửi email đến {ToEmail}.", toEmail);
                return EmailStatus.AuthenticationError;
            }
            catch (SslHandshakeException ex)
            {
                _logger.LogError(ex, "Lỗi SSL/TLS khi kết nối SMTP đến {ToEmail}.", toEmail);
                return EmailStatus.SmtpConnectionError;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi không xác định khi gửi email đến {ToEmail}.", toEmail);
                return EmailStatus.Failure;
            }
        }
    }
}
