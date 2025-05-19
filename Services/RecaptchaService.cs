using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.DependencyInjection;

namespace LoginSystem.Security
{
    /// <summary>
    /// Interface để mock/test dễ dàng
    /// </summary>
    public interface IRecaptchaService
    {
        /// <summary>Public site key (để render)</summary>
        string SiteKey { get; }

        /// <summary>HTML string để chèn vào form</summary>
        string RenderCaptcha();

        /// <summary>Verify token trả về từ client</summary>
        Task<bool> VerifyAsync(string token, CancellationToken cancellation = default);
    }

    /// <summary>
    /// Service chính, lấy key từ ENV VAR và tự tạo HttpClient
    /// </summary>
    public class RecaptchaService : IRecaptchaService, IDisposable
    {
        public string SiteKey { get; }
        private readonly string _secretKey;
        private readonly HttpClient _http;
        private bool _disposed;

        public RecaptchaService()
        {
            SiteKey = Environment.GetEnvironmentVariable("RECAPTCHA_SITE_KEY")
                      ?? throw new InvalidOperationException("Missing RECAPTCHA_SITE_KEY");
            _secretKey = Environment.GetEnvironmentVariable("RECAPTCHA_SECRET_KEY")
                       ?? throw new InvalidOperationException("Missing RECAPTCHA_SECRET_KEY");

            _http = new HttpClient
            {
                BaseAddress = new Uri("https://www.google.com/")
            };
        }

        public string RenderCaptcha()
            => $"<span class=\"recaptcha-wrapper animate__animated animate__zoomIn\"><div class=\"g-recaptcha\" data-sitekey=\"{SiteKey}\"></div></span>";

        public async Task<bool> VerifyAsync(string token, CancellationToken cancellation = default)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var form = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", _secretKey),
                new KeyValuePair<string, string>("response", token)
            });

            var resp = await _http.PostAsync("recaptcha/api/siteverify", form, cancellation);
            if (!resp.IsSuccessStatusCode) return false;

            var model = await resp.Content.ReadFromJsonAsync<RecaptchaResponse>(cancellation);
            return model?.Success == true;
        }

        private class RecaptchaResponse
        {
            public bool Success { get; set; }
            public string[] ErrorCodes { get; set; }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _http.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// HTML Helper để bạn chỉ cần gọi @Html.RenderCaptchaV2()
    /// </summary>
    public static class RecaptchaHtmlHelpers
    {
        public static IHtmlContent RenderCaptchaV2(this IHtmlHelper html)
        {
            var svc = html.ViewContext.HttpContext
                          .RequestServices
                          .GetRequiredService<IRecaptchaService>();
            return new HtmlString(svc.RenderCaptcha());
        }
    }
}