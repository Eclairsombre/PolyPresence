using System.Reflection;

namespace backend.Assets
{
    /// <summary>
    /// Classe utilitaire pour accéder aux ressources intégrées
    /// </summary>
    public static class LogoResources
    {
        // Nom de la ressource embarquée
        private const string EmbeddedResourcePath = "backend.Assets.polytech_lyon_logo.png";

        /// <summary>
        /// Obtient le logo Polytech Lyon comme tableau d'octets
        /// </summary>
        /// <returns>Tableau d'octets contenant l'image du logo</returns>
        public static byte[]? GetLogo()
        {
            // Ordre de priorité:
            // 1. Ressource embarquée
            // 2. Fichier sur disque (plusieurs chemins possibles)

            try
            {
                // 1. Essayer d'abord de charger depuis les ressources embarquées
                byte[]? embeddedLogo = GetLogoFromEmbeddedResource();
                if (embeddedLogo != null)
                {
                    Console.WriteLine("Logo chargé depuis les ressources embarquées");
                    return embeddedLogo;
                }

                // 2. Sinon, essayer de lire depuis un fichier sur le disque
                string[] possiblePaths = new[]
                {
                    "Assets/polytech_lyon_logo.png",
                    "Assets/polytech_Lyon_logo.png",
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "polytech_lyon_logo.png"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "polytech_Lyon_logo.png"),
                    "/app/Assets/polytech_lyon_logo.png",
                    "/app/Assets/polytech_Lyon_logo.png"
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        Console.WriteLine($"Logo chargé depuis le fichier: {path}");
                        return File.ReadAllBytes(path);
                    }
                }

                // Si aucun fichier n'est trouvé, logger l'erreur
                Console.WriteLine("Aucun logo trouvé. Chemins vérifiés: " + string.Join(", ", possiblePaths));

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement du logo: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Obtient le logo depuis les ressources embarquées
        /// </summary>
        private static byte[]? GetLogoFromEmbeddedResource()
        {
            try
            {
                // Obtenir l'assembly actuel
                Assembly assembly = typeof(LogoResources).Assembly;

                // Liste des ressources embarquées (pour debug)
                string[] resources = assembly.GetManifestResourceNames();
                Console.WriteLine("Ressources disponibles: " + string.Join(", ", resources));

                // Essayer de charger la ressource avec le nom exact
                using (Stream? stream = assembly.GetManifestResourceStream(EmbeddedResourcePath))
                {
                    if (stream != null)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            return ms.ToArray();
                        }
                    }
                }

                // Si le nom exact ne fonctionne pas, chercher une ressource contenant "polytech" et "logo"
                foreach (string resourceName in resources)
                {
                    if (resourceName.ToLower().Contains("polytech") && resourceName.ToLower().Contains("logo"))
                    {
                        using (Stream? stream = assembly.GetManifestResourceStream(resourceName))
                        {
                            if (stream != null)
                            {
                                Console.WriteLine($"Logo trouvé dans la ressource: {resourceName}");
                                using (MemoryStream ms = new MemoryStream())
                                {
                                    stream.CopyTo(ms);
                                    return ms.ToArray();
                                }
                            }
                        }
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du chargement des ressources embarquées: {ex.Message}");
                return null;
            }
        }
    }
}
