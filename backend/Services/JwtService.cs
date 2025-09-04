using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using backend.Models;

namespace backend.Services
{
    public interface IJwtService
    {
        Task<TokenResponse> GenerateTokensAsync(User user);
        Task<string> GenerateAccessTokenOnlyAsync(User user);
        Task<ClaimsPrincipal?> ValidateTokenAsync(string token);
        Task<TokenResponse?> RefreshTokenAsync(string refreshToken);
        Task RevokeTokenAsync(string token);
        Task RevokeAllUserTokensAsync(int userId);
        bool IsTokenRevoked(string token);
        int? GetUserIdFromRefreshToken(string refreshToken);
    }

    public class JwtService : IJwtService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<JwtService> _logger;
        private readonly HashSet<string> _revokedTokens = new HashSet<string>();
        // Les refresh tokens sont stockés en mémoire, ce qui signifie qu'ils seront perdus lors du redémarrage de l'application
        // Pour une solution de production, ils devraient être stockés dans une base de données persistante
        private static readonly Dictionary<string, (int UserId, DateTime Expiry)> _refreshTokens = new Dictionary<string, (int, DateTime)>();

        private readonly string _secretKey;
        private readonly string _issuer;
        private readonly string _audience;
        private readonly int _accessTokenExpiryMinutes;
        private readonly int _refreshTokenExpiryDays;

        public JwtService(IConfiguration configuration, ILogger<JwtService> logger)
        {
            _configuration = configuration;
            _logger = logger;

            _secretKey = Environment.GetEnvironmentVariable("JWT_SECRET_KEY") ??
                        _configuration["Jwt:SecretKey"] ??
                        throw new InvalidOperationException("JWT Secret Key not configured");
            _issuer = _configuration["Jwt:Issuer"] ?? "PolytechPresence";
            _audience = _configuration["Jwt:Audience"] ?? "PolytechPresenceAPI";
            _accessTokenExpiryMinutes = int.Parse(_configuration["Jwt:AccessTokenExpiryMinutes"] ?? "15");
            _refreshTokenExpiryDays = int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7");
        }

        public Task<TokenResponse> GenerateTokensAsync(User user)
        {
            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken();

            // Store refresh token with user ID and expiry
            var refreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays);
            _refreshTokens[refreshToken] = (user.Id, refreshTokenExpiry);

            _logger.LogInformation("Generated tokens for user {UserId} ({StudentNumber})", user.Id, user.StudentNumber);

            return Task.FromResult(new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresIn = _accessTokenExpiryMinutes * 60,
                TokenType = "Bearer"
            });
        }

        public Task<string> GenerateAccessTokenOnlyAsync(User user)
        {
            var accessToken = GenerateAccessToken(user);
            _logger.LogInformation("Generated access token only for user {UserId} ({StudentNumber})", user.Id, user.StudentNumber);
            return Task.FromResult(accessToken);
        }

        public Task<ClaimsPrincipal?> ValidateTokenAsync(string token)
        {
            try
            {
                if (IsTokenRevoked(token))
                {
                    _logger.LogWarning("Attempted to use revoked token");
                    return Task.FromResult<ClaimsPrincipal?>(null);
                }

                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.ASCII.GetBytes(_secretKey);

                var tokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = true,
                    ValidIssuer = _issuer,
                    ValidateAudience = true,
                    ValidAudience = _audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                var principal = tokenHandler.ValidateToken(token, tokenValidationParameters, out SecurityToken validatedToken);

                return Task.FromResult<ClaimsPrincipal?>(principal);
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogInformation("Token expired during validation");
                return Task.FromResult<ClaimsPrincipal?>(null);
            }
            catch (SecurityTokenException ex)
            {
                _logger.LogWarning("Token validation failed: {Message}", ex.Message);
                return Task.FromResult<ClaimsPrincipal?>(null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during token validation");
                return Task.FromResult<ClaimsPrincipal?>(null);
            }
        }

        public Task<TokenResponse?> RefreshTokenAsync(string refreshToken)
        {
            try
            {
                _logger.LogInformation("Attempting to refresh token: {TokenStart}...", 
                    refreshToken.Length > 10 ? refreshToken.Substring(0, 10) + "..." : refreshToken);
                
                if (string.IsNullOrWhiteSpace(refreshToken))
                {
                    _logger.LogWarning("RefreshTokenAsync: Refresh token is null or empty");
                    return Task.FromResult<TokenResponse?>(null);
                }
                
                if (!_refreshTokens.TryGetValue(refreshToken, out var tokenInfo))
                {
                    _logger.LogWarning("Invalid refresh token attempted. Token not found in storage");
                    _logger.LogInformation("Number of stored tokens: {TokenCount}", _refreshTokens.Count);
                    return Task.FromResult<TokenResponse?>(null);
                }

                if (tokenInfo.Expiry <= DateTime.UtcNow)
                {
                    _refreshTokens.Remove(refreshToken);
                    _logger.LogInformation("Expired refresh token removed");
                    return Task.FromResult<TokenResponse?>(null);
                }

                _logger.LogInformation("Refresh token validated for user {UserId}", tokenInfo.UserId);

                // 1. Générer un nouveau refresh token pour remplacer l'ancien
                var newRefreshToken = GenerateRefreshToken();
                var refreshTokenExpiry = DateTime.UtcNow.AddDays(_refreshTokenExpiryDays);

                // 2. Mettre à jour la collection de refresh tokens
                _refreshTokens[newRefreshToken] = (tokenInfo.UserId, refreshTokenExpiry);
                _refreshTokens.Remove(refreshToken);

                // 3. Retourner le userId via une autre méthode
                // Le contrôleur devra appeler GetUserIdFromRefreshToken après avoir reçu ce token
                return Task.FromResult<TokenResponse?>(new TokenResponse
                {
                    RefreshToken = newRefreshToken,
                    // L'AccessToken doit être généré par le contrôleur
                    AccessToken = "", // Temporaire, sera remplacé par le contrôleur
                    ExpiresIn = _accessTokenExpiryMinutes * 60,
                    TokenType = "Bearer"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during token refresh");
                return Task.FromResult<TokenResponse?>(null);
            }
        }

        public Task RevokeTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadJwtToken(token);
                var jti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;

                if (!string.IsNullOrEmpty(jti))
                {
                    _revokedTokens.Add(jti);
                    _logger.LogInformation("Token revoked with JTI: {Jti}", jti);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error revoking token");
            }
            return Task.CompletedTask;
        }

        public Task RevokeAllUserTokensAsync(int userId)
        {
            var tokensToRemove = _refreshTokens.Where(rt => rt.Value.UserId == userId).Select(rt => rt.Key).ToList();

            foreach (var token in tokensToRemove)
            {
                _refreshTokens.Remove(token);
            }

            _logger.LogInformation("Revoked all tokens for user {UserId}", userId);
            return Task.CompletedTask;
        }

        public bool IsTokenRevoked(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var jsonToken = tokenHandler.ReadJwtToken(token);
                var jti = jsonToken.Claims.FirstOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti)?.Value;

                return !string.IsNullOrEmpty(jti) && _revokedTokens.Contains(jti);
            }
            catch
            {
                return true; // If we can't read the token, consider it revoked
            }
        }

        private string GenerateAccessToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_secretKey);
            var jti = Guid.NewGuid().ToString();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim("sub", user.Id.ToString()),
                new Claim("studentNumber", user.StudentNumber),
                new Claim("role", user.IsAdmin ? "Admin" : "User"),
                new Claim("isDelegate", user.IsDelegate.ToString().ToLower()),
                new Claim("year", user.Year),
                new Claim("name", user.Name),
                new Claim("firstname", user.Firstname),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(_accessTokenExpiryMinutes),
                Issuer = _issuer,
                Audience = _audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private string GenerateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public int? GetUserIdFromRefreshToken(string refreshToken)
        {
            if (_refreshTokens.TryGetValue(refreshToken, out var tokenInfo))
            {
                if (tokenInfo.Expiry > DateTime.UtcNow)
                {
                    return tokenInfo.UserId;
                }
                else
                {
                    _refreshTokens.Remove(refreshToken);
                }
            }
            return null;
        }
    }
}
