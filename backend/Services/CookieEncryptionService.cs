using Microsoft.AspNetCore.DataProtection;

namespace backend.Services
{
    public class CookieEncryptionService : ICookieEncryptionService
    {
        private readonly IDataProtector _protector;

        public CookieEncryptionService(IDataProtectionProvider provider)
        {
            _protector = provider.CreateProtector("PolyPresence.CookieProtector");
        }

        public string Protect(string plainText)
        {
            return _protector.Protect(plainText);
        }

        public string Unprotect(string protectedText)
        {
            try
            {
                return _protector.Unprotect(protectedText);
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
