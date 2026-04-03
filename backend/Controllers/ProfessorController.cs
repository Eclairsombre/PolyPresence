using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProfessorController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProfessorController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult GetProfessors()
        {
            var professors = _context.Professors.ToList();
            return Ok(professors);
        }

        [HttpGet("{id}")]
        public IActionResult GetProfessorById(int id)
        {
            var professor = _context.Professors.Find(id);
            if (professor == null)
            {
                return NotFound(new { error = true, message = "Professeur non trouvé." });
            }
            return Ok(professor);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProfessor(int id)
        {
            var professor = await _context.Professors.FindAsync(id);
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

            _context.Professors.Remove(professor);
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

            var existing = await _context.Professors.FirstOrDefaultAsync(
                p => p.Name == name && p.Firstname == firstname
            );

            if (existing != null)
            {
                return Conflict(new { error = true, message = "Ce professeur existe déjà." });
            }

            var newProfessor = new Professor
            {
                Name = name,
                Firstname = firstname,
                Email = email
            };

            _context.Professors.Add(newProfessor);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetProfessorById), new { id = newProfessor.Id }, newProfessor);
        }

        [HttpPut("{id}/email")]
        public async Task<IActionResult> UpdateProfessorEmail(int id, [FromBody] UpdateEmailModel model)
        {
            var professor = await _context.Professors.FindAsync(id);
            if (professor == null)
            {
                return NotFound(new { error = true, message = "Professeur non trouvé." });
            }
            if (string.IsNullOrWhiteSpace(model?.Email))
            {
                return BadRequest(new { error = true, message = "Email invalide." });
            }
            professor.Email = model.Email;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Email du professeur mis à jour avec succès." });
        }

        [HttpPost("find-or-create")]
        public async Task<IActionResult> FindOrCreateProfessor([FromBody] Professor input)
        {
            if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Firstname))
                return BadRequest("Nom et prénom requis");

            var existing = await _context.Professors.FirstOrDefaultAsync(p => p.Name == input.Name && p.Firstname == input.Firstname);
            if (existing != null)
            {
                return Ok(new { id = existing.Id });
            }

            var newProf = new Professor
            {
                Name = input.Name,
                Firstname = input.Firstname,
                Email = input.Email
            };
            _context.Professors.Add(newProf);
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