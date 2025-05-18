using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace LoginSystem.Security
{
    public class CustomEmailTokenProvider<TUser> : IUserTwoFactorTokenProvider<TUser> where TUser : class
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<CustomEmailTokenProvider<TUser>> _logger;

        public CustomEmailTokenProvider(IMemoryCache cache, ILogger<CustomEmailTokenProvider<TUser>> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<TUser> manager, TUser user)
        {
            return Task.FromResult(true);
        }

        public Task<string> GenerateAsync(string purpose, UserManager<TUser> manager, TUser user)
        {
            // Not used, as OTP is generated in SendEmailOTPAsync
            throw new NotSupportedException("GenerateAsync is not supported for CustomEmailTokenProvider.");
        }

        public async Task<bool> ValidateAsync(string purpose, string token, UserManager<TUser> manager, TUser user)
        {

            // Get user ID dynamically
            var userId = await manager.GetUserIdAsync(user);
            var cacheKey = $"TwoFactorEmail_{userId}";
            if (!_cache.TryGetValue(cacheKey, out (string Code, DateTime Expires) cachedToken))
            {
                _logger.LogWarning("No OTP found in cache for user {UserId}, cacheKey: {CacheKey}", userId, cacheKey);
                return false;
            }

            _logger.LogInformation("Cache token found: Code={Code}, Expires={Expires}", cachedToken.Code, cachedToken.Expires);

            if (cachedToken.Expires < DateTime.UtcNow)
            {
                _logger.LogWarning("OTP expired for user {UserId}, expires: {Expires}", userId, cachedToken.Expires);
                _cache.Remove(cacheKey);
                return false;
            }

            if (cachedToken.Code != token)
            {
                _logger.LogWarning("Invalid OTP {Token} for user {UserId}, expected: {Expected}", token, userId, cachedToken.Code);
                return false;
            }

            _logger.LogInformation("OTP {Token} validated successfully for user {UserId}", token, userId);
            return true;
        }
    }
}