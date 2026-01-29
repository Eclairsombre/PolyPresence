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
        private readonly ILogger<ProfessorController> _logger;

        public ProfessorController(ApplicationDbContext context, ILogger<ProfessorController> logger)
        {
            _context = context;
            _logger = logger;
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
    }
}