using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Threading.Tasks;

public class AuthController : Controller
{
    private const string CasUrl = "https://cas.univ-lyon1.fr/cas/login";
    private const string ServiceUrl = "http://localhost:5173/callback";

    // Redirige l'utilisateur vers CAS pour l'authentification
    [HttpGet("login")]
    public IActionResult Login()
    {
        var redirectUrl = $"{CasUrl}?service={Uri.EscapeDataString(ServiceUrl)}";
        return Redirect(redirectUrl);
    }

    // Callback après la connexion CAS, où le ticket est récupéré
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
                // Sauvegarde l'utilisateur dans la session
                HttpContext.Session.SetString("User", user);
                return Json(new { success = true, user });
            }
            else
            {
                return Json(new { success = false, message = "Authentification échouée" });
            }
        }
    }

    // Parse la réponse XML de CAS pour extraire l'utilisateur
    private string ParseCasResponse(string response)
    {
        var xmlDoc = new System.Xml.XmlDocument();
        xmlDoc.LoadXml(response);
        var userNode = xmlDoc.SelectSingleNode("//cas:user");
        return userNode?.InnerText;
    }
}
