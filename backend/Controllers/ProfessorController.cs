using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    /// <summary>
    /// CRUD des professeurs. Depuis la fusion Professor/User, un professeur est un
    /// <see cref="User"/> portant le flag <c>IsProfessor</c>. Ce contrôleur reste une
    /// vue dédiée (mêmes routes /api/professor) afin de ne pas impacter le frontend.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ProfessorController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProfessorController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Projection exposée au frontend (mêmes champs qu'avant : id, name, firstname, email).
        private static object ToDto(User u) => new
        {
            id = u.Id,
            name = u.Name,
            firstname = u.Firstname,
            email = u.Email,
            notificationMode = u.NotificationMode,
            hasAccount = !string.IsNullOrEmpty(u.PasswordHash)
        };

        [HttpGet]
        public IActionResult GetProfessors()
        {
            var professors = _context.Users
                .Where(u => u.IsProfessor && !u.IsDeleted)
                .ToList()
                .Select(ToDto)
                .ToList();
            return Ok(professors);
        }

        [HttpGet("{id}")]
        public IActionResult GetProfessorById(int id)
        {
            var professor = _context.Users.FirstOrDefault(u => u.Id == id && u.IsProfessor && !u.IsDeleted);
            if (professor == null)
            {
                return NotFound(new { error = true, message = "Professeur non trouvé." });
            }
            return Ok(ToDto(professor));
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProfessor(int id)
        {
            var professor = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsProfessor);
            if (professor == null)
            {
                return NotFound(new { error = true, message = "Professeur non trouvé." });
            }

            var professorId = id.ToString();
            var sessions = await _context.Sessions
                .Where(s => s.ProfId == professorId || s.ProfId2 == professorId)
                .ToListAsync();

            foreach (var session in sessions)
            {
                if (session.ProfId == professorId)
                {
                    session.ProfId = null;
                }

                if (session.ProfId2 == professorId)
                {
                    session.ProfId2 = null;
                }
            }

            _context.Users.Remove(professor);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Professeur supprimé avec succès." });
        }

        [HttpPost]
        public async Task<IActionResult> CreateProfessor([FromBody] CreateProfessorModel model)
        {
            if (string.IsNullOrWhiteSpace(model?.Name) || string.IsNullOrWhiteSpace(model.Firstname))
            {
                return BadRequest(new { error = true, message = "Nom et prénom requis." });
            }

            var name = model.Name.Trim();
            var firstname = model.Firstname.Trim();
            var email = (model.Email ?? string.Empty).Trim();

            var existing = await _context.Users.FirstOrDefaultAsync(
                u => u.IsProfessor && !u.IsDeleted && u.Name == name && u.Firstname == firstname
            );

            if (existing != null)
            {
                return Conflict(new { error = true, message = "Ce professeur existe déjà." });
            }

            var newProfessor = new User
            {
                Name = name,
                Firstname = firstname,
                Email = email,
                Year = "PROF",
                IsProfessor = true
            };

            _context.Users.Add(newProfessor);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProfessorById), new { id = newProfessor.Id }, ToDto(newProfessor));
        }

        [HttpPut("{id}/email")]
        public async Task<IActionResult> UpdateProfessorEmail(int id, [FromBody] UpdateEmailModel model)
        {
            var professor = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsProfessor);
            if (professor == null)
            {
                return NotFound(new { error = true, message = "Professeur non trouvé." });
            }
            if (string.IsNullOrWhiteSpace(model?.Email))
            {
                return BadRequest(new { error = true, message = "Email invalide." });
            }
            professor.Email = model.Email.Trim();
            await _context.SaveChangesAsync();
            return Ok(new { message = "Email du professeur mis à jour avec succès." });
        }

        [HttpPost("find-or-create")]
        public async Task<IActionResult> FindOrCreateProfessor([FromBody] CreateProfessorModel input)
        {
            if (string.IsNullOrWhiteSpace(input?.Name) || string.IsNullOrWhiteSpace(input.Firstname))
                return BadRequest("Nom et prénom requis");

            var name = input.Name.Trim();
            var firstname = input.Firstname.Trim();

            var existing = await _context.Users.FirstOrDefaultAsync(
                u => u.IsProfessor && !u.IsDeleted && u.Name == name && u.Firstname == firstname);
            if (existing != null)
            {
                return Ok(new { id = existing.Id });
            }

            var newProf = new User
            {
                Name = name,
                Firstname = firstname,
                Email = (input.Email ?? string.Empty).Trim(),
                Year = "PROF",
                IsProfessor = true
            };
            _context.Users.Add(newProf);
            await _context.SaveChangesAsync();
            return Ok(new { id = newProf.Id });
        }

        public class UpdateEmailModel
        {
            public string Email { get; set; } = string.Empty;
        }

        public class CreateProfessorModel
        {
            public string Name { get; set; } = string.Empty;
            public string Firstname { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
        }
    }
}
