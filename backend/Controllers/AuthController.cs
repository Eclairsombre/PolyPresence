using Microsoft.AspNetCore.Mvc;
using System.Xml;

public class AuthController : Controller
{
    private const string CasUrl = "https://cas.univ-lyon1.fr/cas/login";
    private const string ServiceUrl = "http://localhost:5173/";

    [HttpGet("login")]
    public IActionResult Login()
    {
        var redirectUrl = $"{CasUrl}?service={Uri.EscapeDataString(ServiceUrl)}";
        return Redirect(redirectUrl);
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback(string ticket)
    {
        var casServiceValidateUrl = "https://cas.univ-lyon1.fr/cas/serviceValidate";
        var validateUrl = $"{casServiceValidateUrl}?service={Uri.EscapeDataString(ServiceUrl)}&ticket={ticket}";

        using (var client = new HttpClient())
        {
            var response = await client.GetStringAsync(validateUrl);

            var user = ParseCasResponse(response);

            if (user != null)
            {
                HttpContext.Session.SetString("User", user);
                return Json(new
                {
                    success = true,
                    user = user,
                    rawResponse = response 
                });
            }
            else
            {
                return Json(new
                {
                    success = false,
                    message = "Authentification échouée",
                    rawResponse = response 
                });
            }
        }
    }

    private string ParseCasResponse(string response)
    {
        try
        {
            var xmlDoc = new XmlDocument();
            xmlDoc.LoadXml(response);

            // Créer un gestionnaire d'espace de noms
            XmlNamespaceManager nsManager = new XmlNamespaceManager(xmlDoc.NameTable);
            nsManager.AddNamespace("cas", "http://www.yale.edu/tp/cas");

            // Utiliser le gestionnaire lors de la sélection du nœud
            var userNode = xmlDoc.SelectSingleNode("//cas:user", nsManager);
            return userNode?.InnerText;
        }
        catch (Exception ex)
        {
            // Journaliser l'exception pour le débogage
            Console.WriteLine($"Erreur lors du parsing XML: {ex.Message}");
            return null;
        }
    }

    // Déconnexion de l'utilisateur
    [HttpGet("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Remove("User");
        var logoutUrl = $"https://cas.univ-lyon1.fr/cas/logout?service={Uri.EscapeDataString(ServiceUrl)}";
        return Redirect(logoutUrl);
    }

}