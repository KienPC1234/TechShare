using LoginSystem.Models;
using Microsoft.AspNetCore.Identity;
using OtpNet;
using System.Threading.Tasks;

namespace LoginSystem.Classes
{
    public class CustomTOTPTokenProvider<TUser> : TotpSecurityStampBasedTokenProvider<TUser> where TUser : ApplicationUser
    {
        public override async Task<bool> ValidateAsync(string purpose, string token, UserManager<TUser> manager, TUser user)
        {
            if (string.IsNullOrEmpty(user.TwoFactorSecretKey))
            {
                return false;
            }

            try
            {
                var secretKeyBytes = Base32Encoding.ToBytes(user.TwoFactorSecretKey);
                var totp = new Totp(secretKeyBytes, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
                return totp.VerifyTotp(token, out _, new VerificationWindow(previous: 3, future: 3));
            }
            catch
            {
                return false;
            }
        }

        public override Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<TUser> manager, TUser user)
        {
            return Task.FromResult(!string.IsNullOrEmpty(user.TwoFactorSecretKey));
        }
    }
}