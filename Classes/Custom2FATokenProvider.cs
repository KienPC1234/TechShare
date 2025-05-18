using Microsoft.AspNetCore.Identity;
using System;
using System.Threading.Tasks;
using System.Security.Cryptography;

namespace LoginSystem.Classes
{
    public class Custom2FATokenProvider<TUser> : IUserTwoFactorTokenProvider<TUser> where TUser : class
    {
        public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<TUser> manager, TUser user)
        {
            return Task.FromResult(true);
        }

        public Task<string> GenerateAsync(string purpose, UserManager<TUser> manager, TUser user)
        {
            int token = RandomNumberGenerator.GetInt32(100000, 1000000);
            return Task.FromResult(token.ToString());
        }

        public Task<bool> ValidateAsync(string purpose, string token, UserManager<TUser> manager, TUser user)
        {
            return Task.FromResult(token.Length == 6 && int.TryParse(token, out _));
        }
    }
}