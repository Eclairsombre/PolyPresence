using System.Collections.Concurrent;

namespace backend.Services
{
    /**
     * Service pour la gestion des tokens d'administration
     * Les tokens sont stockés en mémoire plutôt qu'en base de données
     */
    public class AdminTokenService
    {
        // Utilisation d'un dictionnaire thread-safe pour stocker les tokens
        // Clé: Token, Valeur: (ID utilisateur, Date d'expiration)
        private readonly ConcurrentDictionary<string, (int UserId, DateTime ExpirationTime)> _adminTokens = new();

        /**
         * Génère un nouveau token admin pour un utilisateur
         * @param userId ID de l'utilisateur admin
         * @param expirationHours Durée de validité du token en heures (par défaut 24h)
         * @return Le token généré
         */
        public string GenerateToken(int userId, int expirationHours = 24)
        {
            var token = Guid.NewGuid().ToString();
            var expiration = DateTime.UtcNow.AddHours(expirationHours);

            _adminTokens[token] = (userId, expiration);

            // Nettoyage des tokens expirés lors de la création d'un nouveau token
            CleanupExpiredTokens();

            return token;
        }

        /**
         * Valide un token admin et retourne l'ID de l'utilisateur associé
         * @param token Le token à valider
         * @return L'ID de l'utilisateur si le token est valide, null sinon
         */
        public int? ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token) || !_adminTokens.TryGetValue(token, out var tokenData))
            {
                return null;
            }

            var (userId, expiration) = tokenData;

            // Vérifier si le token a expiré
            if (expiration < DateTime.UtcNow)
            {
                // Supprimer le token expiré
                _adminTokens.TryRemove(token, out _);
                return null;
            }

            return userId;
        }

        /**
         * Révoque un token spécifique
         * @param token Le token à révoquer
         * @return true si le token a été révoqué, false sinon
         */
        public bool RevokeToken(string token)
        {
            return _adminTokens.TryRemove(token, out _);
        }

        /**
         * Révoque tous les tokens d'un utilisateur
         * @param userId ID de l'utilisateur
         */
        public void RevokeAllUserTokens(int userId)
        {
            var tokensToRemove = _adminTokens
                .Where(pair => pair.Value.UserId == userId)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var token in tokensToRemove)
            {
                _adminTokens.TryRemove(token, out _);
            }
        }

        /**
         * Nettoie les tokens expirés
         */
        private void CleanupExpiredTokens()
        {
            var now = DateTime.UtcNow;
            var expiredTokens = _adminTokens
                .Where(pair => pair.Value.ExpirationTime < now)
                .Select(pair => pair.Key)
                .ToList();

            foreach (var token in expiredTokens)
            {
                _adminTokens.TryRemove(token, out _);
            }
        }
    }
}
