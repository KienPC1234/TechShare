using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using LoginSystem.Models;
using System.Threading.Tasks;
using System;
using OtpNet;
using QRCoder;
using System.Text;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using TechShare.Classes;
using Microsoft.Extensions.Hosting;
using MimeDetective;
using MimeDetective.Definitions;
using MimeDetective.Definitions.Licensing;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Cryptography;

namespace LoginSystem.Controllers
{
    [Route("api/auth")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly MailToolBox _mailToolBox;
        private readonly ILogger<AuthController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IMemoryCache _cache;
        private const int OTP_RATE_LIMIT_MINUTES = 15;
        private const int OTP_RATE_LIMIT_COUNT = 5;
        private const int TWO_FACTOR_TOKEN_VALIDITY_MINUTES = 20;
        private const int TOTP_MAX_ATTEMPTS = 5;
        private readonly long _fileSizeLimit = 5 * 1024 * 1024; // 5MB
        private static readonly string[] AllowedMimeTypes = {
            "image/png", "image/jpeg"
        };
        private readonly IContentInspector _inspector;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            MailToolBox mailToolBox,
            ILogger<AuthController> logger,
            IWebHostEnvironment environment,
            IMemoryCache cache)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _mailToolBox = mailToolBox;
            _logger = logger;
            _environment = environment;
            _cache = cache;
            _inspector = new ContentInspectorBuilder
            {
                Definitions = new ExhaustiveBuilder
                {
                    UsageType = UsageType.PersonalNonCommercial
                }.Build()
            }.Build();
        }

        [HttpPost("upload-avatar")]
        [RequestSizeLimit(5 * 1024 * 1024)]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { message = "File rỗng hoặc không tồn tại" });

            if (file.Length > _fileSizeLimit)
                return BadRequest(new { message = "File vượt quá giới hạn 5MB" });

            await using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var result = _inspector.Inspect(memoryStream);

            var mime = result.ByMimeType().FirstOrDefault()?.MimeType.ToLower() ?? "";
            if (!AllowedMimeTypes.Contains(mime))
                return BadRequest(new { message = "File không hợp lệ (chỉ cho phép PNG hoặc JPEG)." });

            var extension = mime switch
            {
                "image/png" => ".png",
                "image/jpeg" => ".jpg",
                _ => null
            };

            if (extension == null)
                return BadRequest(new { message = "Không xác định được phần mở rộng phù hợp." });

            var fileName = $"{Guid.NewGuid()}{extension}";
            var uploadPath = Path.Combine(_environment.WebRootPath, "Uploads");
            Directory.CreateDirectory(uploadPath);
            var filePath = Path.Combine(uploadPath, fileName);

            memoryStream.Position = 0;
            await using var fileStream = new FileStream(filePath, FileMode.Create);
            await memoryStream.CopyToAsync(fileStream);

            var fileUrl = $"/Uploads/{fileName}";
            bool success = true;
            return Ok(new
            {
                success,
                fileUrl
            });
        }

        [HttpPost("verify-password")]
        [Authorize]
        public async Task<IActionResult> VerifyPassword([FromBody] VerifyPasswordModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for password verification");
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for password verification");
                return Unauthorized(new { success = false, message = "Người dùng không hợp lệ." });
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                _logger.LogWarning("Invalid password for user {UserId}", user.Id);
                return BadRequest(new { success = false, message = "Mật khẩu không đúng." });
            }

            var sessionId = Guid.NewGuid().ToString();
            _cache.Set($"PasswordSession_{user.Id}", sessionId, TimeSpan.FromMinutes(10));

            _logger.LogInformation("Password verified successfully for user {UserId}, sessionId: {SessionId}", user.Id, sessionId);
            return Ok(new { success = true, sessionId });
        }

        [HttpPost("setup-totp")]
        [Authorize]
        public async Task<IActionResult> SetupTOTP([FromHeader(Name = "X-2FA-Session-Id")] string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogWarning("Missing 2FA session ID");
                return Unauthorized(new { success = false, message = "Phiên xác thực không hợp lệ." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for TOTP setup");
                return Unauthorized(new { success = false, message = "Người dùng không hợp lệ." });
            }

            var cachedSessionId = _cache.Get<string>($"PasswordSession_{user.Id}");
            if (cachedSessionId != sessionId)
            {
                _logger.LogWarning("Invalid 2FA session ID for user {UserId}", user.Id);
                return Unauthorized(new { success = false, message = "Phiên xác thực không hợp lệ." });
            }

            var secretKey = KeyGeneration.GenerateRandomKey(20);
            var base32Secret = Base32Encoding.ToString(secretKey).ToUpper();

            var issuer = "TechShare";
            var encodedEmail = Uri.EscapeDataString(user.Email);
            var totpUri = $"otpauth://totp/{issuer}:{encodedEmail}?secret={base32Secret}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";

            try
            {
                using var qrGenerator = new QRCodeGenerator();
                var qrCodeData = qrGenerator.CreateQrCode(totpUri, QRCodeGenerator.ECCLevel.Q);
                using var qrCode = new PngByteQRCode(qrCodeData);
                var qrCodeImage = qrCode.GetGraphic(20);
                var qrCodeBase64 = Convert.ToBase64String(qrCodeImage);
                var qrCodeUrl = $"data:image/png;base64,{qrCodeBase64}";

                _cache.Set($"TOTPSecret_{user.Id}_{sessionId}", base32Secret, TimeSpan.FromMinutes(10));

                _logger.LogInformation("TOTP QR code generated for user {UserId}, sessionId: {SessionId}", user.Id, sessionId);
                return Ok(new { success = true, qrCodeUrl, manualCode = base32Secret });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating TOTP QR code for user {UserId}", user.Id);
                return StatusCode(500, new { success = false, message = "Lỗi khi tạo mã QR TOTP." });
            }
        }

        [HttpPost("verify-totp")]
        [Authorize]
        public async Task<IActionResult> VerifyTOTP([FromBody] Verify2FAModel model, [FromHeader(Name = "X-2FA-Session-Id")] string sessionId)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for TOTP verification");
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for TOTP verification");
                return Unauthorized(new { success = false, message = "Người dùng không hợp lệ." });
            }

            var cacheKey = $"TOTPSecret_{user.Id}_{sessionId}";
            if (!_cache.TryGetValue(cacheKey, out string base32Secret))
            {
                _logger.LogWarning("TOTP temporary secret not found in cache for user {UserId}, sessionId: {SessionId}", user.Id, sessionId);
                return BadRequest(new { success = false, message = "Phiên cài đặt TOTP đã hết hạn." });
            }

            try
            {
                var totp = new Totp(Base32Encoding.ToBytes(base32Secret), step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
                if (totp.VerifyTotp(model.Code, out long timeStepMatched, new VerificationWindow(previous: 3, future: 3)))
                {
                    user.TwoFactorSecretKey = base32Secret;
                    user.TwoFactorMethod = "TOTP";
                    await _userManager.SetTwoFactorEnabledAsync(user, true);

                    var updateResult = await _userManager.UpdateAsync(user);
                    if (!updateResult.Succeeded)
                    {
                        _logger.LogError("Failed to update TOTP settings for user {UserId}: {Errors}", user.Id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                        return StatusCode(500, new { success = false, message = "Lỗi khi lưu cài đặt TOTP." });
                    }

                    _cache.Remove(cacheKey);
                    _cache.Remove($"PasswordSession_{user.Id}");

                    _logger.LogInformation("TOTP setup completed for user {UserId}, sessionId: {SessionId}", user.Id, sessionId);
                    return Ok(new { success = true, message = "Cài đặt TOTP thành công." });
                }
                else
                {
                    var attemptsKey = $"TOTPFailedAttempts_{user.Id}_{sessionId}";
                    var attempts = _cache.Get<int?>(attemptsKey) ?? 0;
                    attempts++;

                    if (attempts >= TOTP_MAX_ATTEMPTS)
                    {
                        _logger.LogWarning("TOTP verification attempts exceeded for user {UserId}, sessionId: {SessionId}", user.Id, sessionId);
                        _cache.Remove(attemptsKey);
                        return StatusCode(429, new { success = false, message = "Quá nhiều lần thử TOTP không hợp lệ. Vui lòng thử lại sau." });
                    }

                    _cache.Set(attemptsKey, attempts, TimeSpan.FromMinutes(10));
                    _logger.LogWarning("Invalid TOTP code for user {UserId}, code: {Code}, timeStep: {TimeStep}, attempt: {Attempt}, sessionId: {SessionId}", user.Id, model.Code, timeStepMatched, attempts, sessionId);
                    return BadRequest(new { success = false, message = "Mã TOTP không hợp lệ. Vui lòng kiểm tra lại thời gian trên thiết bị của bạn." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying TOTP for user {UserId}, sessionId: {SessionId}", user.Id, sessionId);
                return StatusCode(500, new { success = false, message = "Lỗi khi xác thực TOTP." });
            }
        }

        [HttpPost("verify-totp-login")]
        public async Task<IActionResult> VerifyTOTPLogin([FromBody] VerifyTOTPLoginModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for TOTP login verification");
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                _logger.LogWarning("User not found for TOTP login: {UserId}", model.UserId);
                return BadRequest(new { success = false, message = "Người dùng không tồn tại." });
            }

            if (string.IsNullOrEmpty(user.TwoFactorSecretKey))
            {
                _logger.LogWarning("TOTP secret key missing for user {UserId}", model.UserId);
                return BadRequest(new { success = false, message = "Cấu hình TOTP không hợp lệ." });
            }

            try
            {
                var totp = new Totp(Base32Encoding.ToBytes(user.TwoFactorSecretKey), step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
                if (totp.VerifyTotp(model.Code, out long timeStepMatched, new VerificationWindow(previous: 3, future: 3)))
                {
                    _logger.LogInformation("Manual TOTP validation successful for user {UserId}, code: {Code}, timeStep: {TimeStep}", user.Id, model.Code, timeStepMatched);

                    await _signInManager.SignInAsync(user, model.RememberMe);

                    user.LastLoginTime = DateTime.UtcNow;
                    await _userManager.UpdateAsync(user);
                    _logger.LogInformation("TOTP login successful for user {UserId}", user.Id);
                    return Ok(new { success = true, message = "Xác thực TOTP thành công." });
                }
                else
                {
                    var attemptsKey = $"TOTPLoginFailedAttempts_{user.Id}";
                    var attempts = _cache.Get<int?>(attemptsKey) ?? 0;
                    attempts++;

                    if (attempts >= TOTP_MAX_ATTEMPTS)
                    {
                        _logger.LogWarning("TOTP login attempts exceeded for user {UserId}", user.Id);
                        _cache.Remove(attemptsKey);
                        return StatusCode(429, new { success = false, message = "Quá nhiều lần thử TOTP không hợp lệ. Vui lòng thử lại sau." });
                    }

                    _cache.Set(attemptsKey, attempts, TimeSpan.FromMinutes(10));
                    _logger.LogWarning("Invalid TOTP code for user {UserId}, code: {Code}, timeStep: {TimeStep}, attempt: {Attempt}", user.Id, model.Code, timeStepMatched, attempts);
                    return BadRequest(new { success = false, message = "Mã TOTP không hợp lệ. Vui lòng kiểm tra lại thời gian trên thiết bị của bạn." });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TOTP login for user {UserId}", model.UserId);
                return StatusCode(500, new { success = false, message = "Lỗi khi xác thực TOTP." });
            }
        }

        [HttpPost("reset-totp")]
        [Authorize]
        public async Task<IActionResult> ResetTOTP([FromHeader(Name = "X-2FA-Session-Id")] string sessionId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                _logger.LogWarning("User not found for TOTP reset");
                return Unauthorized(new { success = false, message = "Người dùng không hợp lệ." });
            }

            var cachedSessionId = _cache.Get<string>($"PasswordSession_{user.Id}");
            if (cachedSessionId != sessionId)
            {
                _logger.LogWarning("Invalid 2FA session ID for user {UserId}", user.Id);
                return Unauthorized(new { success = false, message = "Phiên xác thực không hợp lệ." });
            }

            try
            {
                user.TwoFactorSecretKey = null;
                user.TwoFactorMethod = null;
                await _userManager.SetTwoFactorEnabledAsync(user, false);

                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    _logger.LogError("Failed to reset TOTP for user {UserId}: {Errors}", user.Id, string.Join(", ", updateResult.Errors.Select(e => e.Description)));
                    return StatusCode(500, new { success = false, message = "Lỗi khi hủy TOTP." });
                }

                _cache.Remove($"PasswordSession_{user.Id}");

                _logger.LogInformation("TOTP reset successful for user {UserId}, sessionId: {SessionId}", user.Id, sessionId);
                return Ok(new { success = true, message = "Đã hủy TOTP thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error resetting TOTP for user {UserId}", user.Id);
                return StatusCode(500, new { success = false, message = "Lỗi khi hủy TOTP." });
            }
        }

        [HttpPost("send-email-otp")]
        public async Task<IActionResult> SendEmailOTP([FromBody] SendOTPModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for sending email OTP");
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null || user.Email != model.Email)
            {
                _logger.LogWarning("User not found or email mismatch: {UserId}, {Email}", model.UserId, model.Email);
                return BadRequest(new { success = false, message = "Người dùng hoặc email không hợp lệ." });
            }

            if (!await _userManager.GetTwoFactorEnabledAsync(user) || user.TwoFactorMethod != "Email")
            {
                _logger.LogWarning("User {UserId} does not have Email 2FA enabled", model.UserId);
                return BadRequest(new { success = false, message = "Xác thực hai yếu tố qua email không được kích hoạt." });
            }

            if (!_environment.IsDevelopment())
            {
                var rateLimitKey = $"OTPRateLimit_{model.UserId}";
                var rateLimit = _cache.Get<(int Attempts, DateTime LastAttempt)>(rateLimitKey);
                int attempts = rateLimit.Attempts;
                DateTime? lastAttempt = rateLimit.LastAttempt;

                if (lastAttempt.HasValue && attempts >= OTP_RATE_LIMIT_COUNT && (DateTime.UtcNow - lastAttempt.Value).TotalMinutes < OTP_RATE_LIMIT_MINUTES)
                {
                    _logger.LogWarning("Rate limit exceeded for user {UserId}, attempts: {Attempts}", model.UserId, attempts);
                    return StatusCode(429, new { success = false, message = $"Vui lòng đợi {OTP_RATE_LIMIT_MINUTES} phút trước khi yêu cầu mã OTP mới." });
                }

                if (!lastAttempt.HasValue || (DateTime.UtcNow - lastAttempt.Value).TotalMinutes >= OTP_RATE_LIMIT_MINUTES)
                {
                    attempts = 0;
                }

                attempts++;
                _cache.Set(rateLimitKey, (attempts, DateTime.UtcNow), TimeSpan.FromMinutes(OTP_RATE_LIMIT_MINUTES));
            }

            try
            {
                var sessionId = Guid.NewGuid().ToString();
                var otpCode = GenerateVerificationCode();
                var cacheKey = $"TwoFactorEmail_{model.UserId}";
                _cache.Set(cacheKey, (Code: otpCode, Expires: DateTime.UtcNow.AddMinutes(TWO_FACTOR_TOKEN_VALIDITY_MINUTES)), TimeSpan.FromMinutes(TWO_FACTOR_TOKEN_VALIDITY_MINUTES));

                var emailResult = _mailToolBox.SendEmail(
                    model.Email,
                    "Xác thực OTP Đăng nhập",
                    GenerateOTPEmailHtml(model.Email, otpCode)
                );

                if (emailResult == EmailStatus.Success)
                {
                    _logger.LogInformation("Email OTP sent successfully to {Email}, code: {Code}, sessionId: {SessionId}", model.Email, otpCode, sessionId);
                    return Ok(new { success = true, message = "Mã OTP đã được gửi.", sessionId });
                }

                string errorMessage = emailResult switch
                {
                    EmailStatus.SmtpConnectionError => "Không thể kết nối đến máy chủ email.",
                    EmailStatus.AuthenticationError => "Lỗi xác thực email.",
                    EmailStatus.SendError => "Lỗi khi gửi email.",
                    _ => "Không thể gửi mã OTP."
                };
                _logger.LogWarning("Failed to send email OTP to {Email}: {Status}", model.Email, emailResult);
                return StatusCode(500, new { success = false, message = errorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email OTP for user {UserId}", model.UserId);
                return StatusCode(500, new { success = false, message = "Lỗi khi gửi mã OTP." });
            }
        }

        private string GenerateVerificationCode()
        {
            byte[] bytes = new byte[4];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }
            int value = BitConverter.ToInt32(bytes, 0) & int.MaxValue;

            int code = 100000 + (value % 900000);
            return code.ToString();
        }

        [HttpPost("verify-email-otp")]
        public async Task<IActionResult> VerifyEmailOTP([FromBody] VerifyOTPModel model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for email OTP verification");
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                _logger.LogWarning("User not found for email OTP verification: {UserId}", model.UserId);
                return BadRequest(new { success = false, message = "Người dùng không tồn tại." });
            }

            try
            {
                var cacheKey = $"EmailOTP_{model.UserId}_{model.SessionId}";
                if (!_cache.TryGetValue(cacheKey, out (string Token, string Email, DateTime Expires) cachedData))
                {
                    _logger.LogWarning("Email OTP not found in cache for user {UserId}, sessionId: {SessionId}", model.UserId, model.SessionId);
                    return BadRequest(new { success = false, message = "Mã OTP không hợp lệ hoặc đã hết hạn." });
                }

                if (cachedData.Token != model.Code || cachedData.Email != user.Email || DateTime.UtcNow > cachedData.Expires)
                {
                    _logger.LogWarning("Invalid email OTP for user {UserId}, code: {Code}, sessionId: {SessionId}, stored: {StoredToken}, expires: {Expires}",
                        user.Id, model.Code, model.SessionId, cachedData.Token, cachedData.Expires);
                    return BadRequest(new { success = false, message = "Mã OTP không hợp lệ hoặc đã hết hạn." });
                }

                _cache.Remove(cacheKey);
                await _signInManager.SignInAsync(user, model.RememberMe);
                user.LastLoginTime = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
                _logger.LogInformation("Email OTP login successful for user {UserId}, sessionId: {SessionId}", user.Id, model.SessionId);
                return Ok(new { success = true, message = "Xác thực OTP thành công." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying email OTP for user {UserId}, sessionId: {SessionId}", model.UserId, model.SessionId);
                return StatusCode(500, new { success = false, message = "Lỗi khi xác thực OTP." });
            }
        }

        [HttpPost("resend-email-verification")]
        public async Task<IActionResult> ResendEmailVerification([FromBody] ResendEmailVerificationRequest model)
        {
            if (!ModelState.IsValid)
            {
                _logger.LogWarning("Invalid model state for resending email verification");
                return BadRequest(new { success = false, message = "Dữ liệu không hợp lệ." });
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null || user.Email != model.Email)
            {
                _logger.LogWarning("User not found or email mismatch: {UserId}, {Email}", model.UserId, model.Email);
                return BadRequest(new { success = false, message = "Người dùng hoặc email không hợp lệ." });
            }

            if (!_environment.IsDevelopment())
            {
                var rateLimitKey = $"VerificationRateLimit_{model.UserId}";
                var rateLimit = _cache.Get<(int Attempts, DateTime LastAttempt)>(rateLimitKey);
                int attempts = rateLimit.Attempts;
                DateTime? lastAttempt = rateLimit.LastAttempt;

                if (lastAttempt.HasValue && attempts >= OTP_RATE_LIMIT_COUNT && (DateTime.UtcNow - lastAttempt.Value).TotalMinutes < OTP_RATE_LIMIT_MINUTES)
                {
                    _logger.LogWarning("Rate limit exceeded for email verification user {UserId}, attempts: {Attempts}", model.UserId, attempts);
                    return StatusCode(429, new { success = false, message = "Vui lòng đợi vài phút trước khi yêu cầu mã xác thực mới." });
                }

                if (!lastAttempt.HasValue || (DateTime.UtcNow - lastAttempt.Value).TotalMinutes >= OTP_RATE_LIMIT_MINUTES)
                {
                    attempts = 0;
                }

                attempts++;
                _cache.Set(rateLimitKey, (attempts, DateTime.UtcNow), TimeSpan.FromMinutes(OTP_RATE_LIMIT_MINUTES));
            }

            try
            {
                var sessionId = Guid.NewGuid().ToString();
                var cacheKey = $"EmailVerification_{model.UserId}_{sessionId}";

                // Check for existing valid token
                if (_cache.TryGetValue(cacheKey, out (string Token, string Email, DateTime Expires) cachedData) && DateTime.UtcNow < cachedData.Expires)
                {
                    _logger.LogInformation("Existing valid token found for user {UserId}, sessionId: {SessionId}, reusing token", user.Id, sessionId);
                    var emailResult = _mailToolBox.SendEmail(
                        model.Email,
                        "Xác Thực Email Mới",
                        GenerateVerificationEmailHtml(model.Email, cachedData.Token));

                    if (emailResult == EmailStatus.Success)
                    {
                        _logger.LogInformation("Reused email verification code sent successfully to {Email}, code: {Code}, sessionId: {SessionId}", model.Email, cachedData.Token, sessionId);
                        return Ok(new { success = true, message = "Mã xác thực đã được gửi lại.", sessionId });
                    }

                    string errorMessage = emailResult switch
                    {
                        EmailStatus.SmtpConnectionError => "Không thể kết nối đến máy chủ email.",
                        EmailStatus.AuthenticationError => "Lỗi xác thực email.",
                        EmailStatus.SendError => "Lỗi khi gửi email.",
                        _ => "Không thể gửi mã xác thực."
                    };
                    _logger.LogWarning("Failed to send reused email verification to {Email}: {Status}", model.Email, emailResult);
                    return StatusCode(500, new { success = false, message = errorMessage });
                }

                // Generate new token
                var verificationCode = GenerateVerificationCode();
                _cache.Set(cacheKey, (verificationCode, model.Email, DateTime.UtcNow.AddMinutes(TWO_FACTOR_TOKEN_VALIDITY_MINUTES)), TimeSpan.FromMinutes(TWO_FACTOR_TOKEN_VALIDITY_MINUTES));

                var newEmailResult = _mailToolBox.SendEmail(
                    model.Email,
                    "Xác Thực Email Mới",
                    GenerateVerificationEmailHtml(model.Email, verificationCode));

                if (newEmailResult == EmailStatus.Success)
                {
                    _logger.LogInformation("New email verification code sent successfully to {Email}, code: {Code}, sessionId: {SessionId}", model.Email, verificationCode, sessionId);
                    return Ok(new { success = true, message = "Mã xác thực đã được gửi lại.", sessionId });
                }

                string newErrorMessage = newEmailResult switch
                {
                    EmailStatus.SmtpConnectionError => "Không thể kết nối đến máy chủ email.",
                    EmailStatus.AuthenticationError => "Lỗi xác thực email.",
                    EmailStatus.SendError => "Lỗi khi gửi email.",
                    _ => "Không thể gửi mã xác thực."
                };
                _logger.LogWarning("Failed to send new email verification to {Email}: {Status}", model.Email, newEmailResult);
                return StatusCode(500, new { success = false, message = newErrorMessage });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending email verification for user {UserId}", model.UserId);
                return StatusCode(500, new { success = false, message = "Lỗi khi gửi mã xác thực." });
            }
        }

        private string GenerateOTPEmailHtml(string email, string code)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 20px auto; background: #ffffff; padding: 20px; border-radius: 8px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; padding: 20px 0; background: #007bff; color: white; border-radius: 8px 8px 0 0; }}
        .content {{ padding: 20px; text-align: center; }}
        .code {{ font-size: 24px; font-weight: bold; color: #007bff; margin: 20px 0; padding: 10px; background: #f8f9fa; border-radius: 4px; }}
        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TechShare</h1>
        </div>
        <div class='content'>
            <h2>Mã OTP đăng nhập</h2>
            <p>Vui lòng sử dụng mã sau để đăng nhập:</p>
            <div class='code'>{code}</div>
            <p>Mã này có hiệu lực trong {TWO_FACTOR_TOKEN_VALIDITY_MINUTES} phút.</p>
        </div>
        <div class='footer'>
            <p>Nếu bạn không yêu cầu, vui lòng bỏ qua email này.</p>
            <p>© 2025 TechShare. Mọi quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";
        }

        private string GenerateVerificationEmailHtml(string email, string code)
        {
            return $@"<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; }}
        .container {{ max-width: 600px; margin: 20px auto; background: #ffffff; padding: 20px; border-radius: 8px; box-shadow: 0 0 10px rgba(0,0,0,0.1); }}
        .header {{ text-align: center; padding: 20px 0; background: #007bff; color: white; border-radius: 8px 8px 0 0; }}
        .content {{ padding: 20px; text-align: center; }}
        .code {{ font-size: 24px; font-weight: bold; color: #007bff; margin: 20px 0; padding: 10px; background: #f8f9fa; border-radius: 4px; }}
        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>TechShare</h1>
        </div>
        <div class='content'>
            <h2>Xác thực Email mới</h2>
            <p>Vui lòng sử dụng mã sau để xác thực email mới của bạn:</p>
            <div class='code'>{code}</div>
            <p>Mã này có hiệu lực trong {TWO_FACTOR_TOKEN_VALIDITY_MINUTES} phút.</p>
        </div>
        <div class='footer'>
            <p>Nếu bạn không yêu cầu thay đổi email, vui lòng bỏ qua email này.</p>
            <p>© 2025 TechShare. Mọi quyền được bảo lưu.</p>
        </div>
    </div>
</body>
</html>";
        }
    }

    public class VerifyPasswordModel
    {
        [Required(ErrorMessage = "Mật khẩu là bắt buộc.")]
        [DataType(DataType.Password)]
        public string Password { get; set; }
    }

    public class Verify2FAModel
    {
        [Required(ErrorMessage = "Mã OTP là bắt buộc.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ được chứa số.")]
        public string Code { get; set; }
    }

    public class VerifyTOTPLoginModel
    {
        [Required(ErrorMessage = "UserId là bắt buộc.")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Mã TOTP là bắt buộc.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã TOTP phải có 6 chữ số.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã TOTP chỉ được chứa số.")]
        public string Code { get; set; }

        public bool RememberMe { get; set; }
    }

    public class SendOTPModel
    {
        [Required(ErrorMessage = "UserId là bắt buộc.")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; }
    }

    public class VerifyOTPModel
    {
        [Required(ErrorMessage = "UserId là bắt buộc.")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Mã OTP là bắt buộc.")]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Mã OTP phải có 6 chữ số.")]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Mã OTP chỉ được chứa số.")]
        public string Code { get; set; }

        public bool RememberMe { get; set; }

        [Required(ErrorMessage = "SessionId là bắt buộc.")]
        public string SessionId { get; set; }
    }

    public class ResendEmailVerificationRequest
    {
        [Required(ErrorMessage = "UserId là bắt buộc.")]
        public string UserId { get; set; }

        [Required(ErrorMessage = "Email là bắt buộc.")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; }
    }
}