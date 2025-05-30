﻿using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Linq;
using LoginSystem.Security;
using LoginSystem.Data;
using Microsoft.EntityFrameworkCore;

namespace LoginSystem.Pages
{
    public class RegisterModel : PageModel
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<RegisterModel> _logger;
        private const string DefaultAvatar = "/images/default-avatar.png";
        private readonly ApplicationDbContext _context;
        private readonly IRecaptchaService _recaptcha;
        public RegisterModel(
            IMemoryCache cache,
            ILogger<RegisterModel> logger,
            IRecaptchaService recaptcha,
            ApplicationDbContext context)
        {
            _cache = cache;
            _logger = logger;
            _recaptcha = recaptcha;
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [BindProperty(Name = "g-recaptcha-response")]
        public string RecaptchaToken { get; set; }


        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập tên người dùng.")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên người dùng phải từ 3 đến 100 ký tự.")]
            public string Username { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
            [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên hiển thị phải từ 3 đến 100 ký tự.")]
            public string DisplayName { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập email.")]
            [EmailAddress(ErrorMessage = "Định dạng email không hợp lệ.")]
            [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Định dạng email không hợp lệ.")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu.")]
            [DataType(DataType.Password)]
            [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{6,}$", ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự, bao gồm chữ hoa, chữ thường và số.")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp.")]
            public string ConfirmPassword { get; set; }

            public string AvatarRequest { get; set; }
        }

        public IActionResult OnGet()
        {
            return Page();
        }


        public async Task<IActionResult> OnPostAsync()
        {
            // Kiểm tra reCAPTCHA
            if (!await _recaptcha.VerifyAsync(RecaptchaToken))
            {
                ModelState.AddModelError(string.Empty, "Xác thực reCAPTCHA thất bại");
                return Page();
            }

            // Kiểm tra ModelState
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(m => m.Value.Errors.Any())
                    .Select(m => new { Field = m.Key, Errors = m.Value.Errors.Select(e => e.ErrorMessage) });
                _logger.LogWarning("ModelState không hợp lệ khi đăng ký. Lỗi: {Errors}",
                    string.Join("; ", errors.Select(e => $"{e.Field}: {string.Join(", ", e.Errors)}")));
                return Page();
            }

            // Kiểm tra trùng lặp username và email trong cơ sở dữ liệu
            if (await _context.Users.AnyAsync(u => u.UserName == Input.Username))
            {
                ModelState.AddModelError("Input.Username", "Tên người dùng đã được sử dụng.");
                return Page();
            }

            if (await _context.Users.AnyAsync(u => u.Email == Input.Email))
            {
                ModelState.AddModelError("Input.Email", "Email đã được sử dụng.");
                return Page();
            }

            // Kiểm tra email trong cache
            var cacheKey = $"PendingUser_{Input.Email}";
            if (_cache.TryGetValue(cacheKey, out _))
            {
                _logger.LogWarning("Email {Email} đã có trong hàng đợi đăng ký", Input.Email);
                ModelState.AddModelError("Input.Email", "Email đang chờ xác thực. Vui lòng kiểm tra email của bạn.");
                return Page();
            }

            // Kiểm tra AvatarRequest
            var avatarUrl = Input.AvatarRequest == "No Avatar" ? DefaultAvatar : Input.AvatarRequest;
            if (Input.AvatarRequest != "No Avatar" && !Uri.IsWellFormedUriString(avatarUrl, UriKind.Relative))
            {
                _logger.LogWarning("AvatarRequest không hợp lệ: {AvatarRequest}", Input.AvatarRequest);
                ModelState.AddModelError("Input.AvatarRequest", "URL ảnh đại diện không hợp lệ.");
                return Page();
            }

            // Lưu dữ liệu tạm thời và tạo mã xác thực
            var userData = new
            {
                Input.Username,
                Input.Email,
                Input.DisplayName,
                Input.Password,
                AvatarUrl = avatarUrl
            };

            var verificationCode = GenerateVerificationCode();
            var cacheEntry = (Code: verificationCode, Expires: DateTime.UtcNow.AddMinutes(15));
            _cache.Set($"Verification_{Input.Email}", cacheEntry, TimeSpan.FromMinutes(15));
            _cache.Set(cacheKey, userData, TimeSpan.FromMinutes(15));

            _logger.LogInformation("Lưu dữ liệu đăng ký và mã xác thực cho email {Email}, mã: {Code}, hết hạn: {Expires}",
                Input.Email, verificationCode, cacheEntry.Expires);

            return RedirectToPage("/VerifyEmail", new { email = Input.Email });
        }

        private string GenerateVerificationCode()
        {
            using var rng = RandomNumberGenerator.Create();
            byte[] bytes = new byte[4];
            rng.GetBytes(bytes);
            var code = Convert.ToBase64String(bytes).Substring(0, 6).ToUpper();
            _logger.LogInformation("Tạo mã xác thực: {Code}", code);
            return code;
        }
    }
}