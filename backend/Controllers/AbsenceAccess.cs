using System.Security.Claims;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    /// <summary>
    /// Droits de l'application Absences : la scolarité (compte admin) lit et modifie,
    /// les profs lisent seulement. Les étudiants n'y ont pas accès.
    /// </summary>
    internal static class AbsenceAccess
    {
        public static async Task<User?> CurrentUserAsync(ControllerBase controller, ApplicationDbContext db)
        {
            if (controller.HttpContext?.Items["User"] is User fromMiddleware)
                return fromMiddleware;

            var claim = controller.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(claim, out var id))
                return null;

            return await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted);
        }

        /// <summary>Admin ou prof ; sinon la réponse d'erreur à renvoyer.</summary>
        public static async Task<(User? User, IActionResult? Error)> RequireReaderAsync(
            ControllerBase controller, ApplicationDbContext db)
        {
            var user = await CurrentUserAsync(controller, db);
            if (user == null)
                return (null, controller.Unauthorized(new { message = "Utilisateur non identifié." }));
            if (!user.IsAdmin && !user.IsProfessor)
                return (null, controller.StatusCode(403, new { message = "Accès réservé à la scolarité et aux enseignants." }));
            return (user, null);
        }

        /// <summary>Admin (scolarité) uniquement ; sinon la réponse d'erreur à renvoyer.</summary>
        public static async Task<(User? User, IActionResult? Error)> RequireAdminAsync(
            ControllerBase controller, ApplicationDbContext db)
        {
            var user = await CurrentUserAsync(controller, db);
            if (user == null)
                return (null, controller.Unauthorized(new { message = "Utilisateur non identifié." }));
            if (!user.IsAdmin)
                return (null, controller.StatusCode(403, new { message = "Action réservée à la scolarité." }));
            return (user, null);
        }
    }
}
