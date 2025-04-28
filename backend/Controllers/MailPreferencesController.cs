using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.Data;
using backend.Models;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MailPreferencesController : ControllerBase
    {
        private readonly ILogger<SessionController> _logger;

        private readonly ApplicationDbContext _context;

        public MailPreferencesController(ApplicationDbContext context, ILogger<SessionController> logger)
        {
            _logger = logger;
            _context = context;
        }

        [HttpGet("{userId}")]
        public IActionResult GetPreferences(string userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == userId);
            if (user == null) return NotFound("Utilisateur non trouvé.");

            _logger.LogInformation($"Récupération des préférences de mail pour l'utilisateur {userId}");
            _logger.LogInformation($"Préférences de mail ID : {user.MailPreferencesId}");
            _logger.LogInformation($"Email : {user.Email}");

            MailPreferences? preferences;
            if (user.MailPreferencesId == null)
            {
                // Créer un nouvel objet MailPreferences si aucun n'existe
                preferences = new MailPreferences
                {
                    EmailTo = user.Email,
                    Days = new List<string>(),
                    Active = false
                };
                _context.MailPreferences.Add(preferences);
                user.MailPreferencesId = preferences.Id;
                _context.SaveChanges();
            }
            else
            {
                // Récupérer les préférences existantes
                preferences = _context.MailPreferences.FirstOrDefault(mp => mp.Id == user.MailPreferencesId);
                if (preferences == null)
                {
                    return NotFound("Préférences de mail non trouvées.");
                }
            }

            return Ok(preferences);
        }

        [HttpPut("{userId}")]
        public IActionResult UpdatePreferences(string userId, [FromBody] MailPreferences preferences)
        {
            var user = _context.Users.FirstOrDefault(u => u.StudentNumber == userId);
            if (user == null) return NotFound("Utilisateur non trouvé.");
            _logger.LogInformation($"Récupération des préférences de mail pour l'utilisateur {userId}");
            _logger.LogInformation($"Préférences de mail ID : {user.MailPreferencesId}");
            _logger.LogInformation($"Email : {user.Email}");
            
            MailPreferences? existingPreferences;
            if (user.MailPreferencesId == null)
            {
                // Créer un nouvel objet MailPreferences si aucun n'existe
                existingPreferences = new MailPreferences
                {
                    EmailTo = preferences.EmailTo,
                    Days = preferences.Days,
                    Active = preferences.Active
                };
                _context.MailPreferences.Add(existingPreferences);
                user.MailPreferencesId = existingPreferences.Id;
            }
            else
            {
                // Mettre à jour les préférences existantes
                existingPreferences = _context.MailPreferences.FirstOrDefault(mp => mp.Id == user.MailPreferencesId);
                if (existingPreferences == null) return NotFound("Préférences de mail non trouvées.");

                existingPreferences.EmailTo = preferences.EmailTo;
                existingPreferences.Days = preferences.Days;
                existingPreferences.Active = preferences.Active;
            }

            _context.SaveChanges();
            return NoContent();
        }
    }
}
