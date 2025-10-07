namespace backend.Services
{
    public interface ICookieEncryptionService
    {
        string Protect(string plainText);
        string Unprotect(string protectedText);
    }
}
