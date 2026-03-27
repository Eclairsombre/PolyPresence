using Microsoft.AspNetCore.Mvc;
using backend.Models;

using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]

    /**
     * IcsLinkController
     *
     * This controller handles CRUD operations for IcsLink entities.
     */
    [Route("api/[controller]")]
    public class IcsLinkController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public IcsLinkController(ApplicationDbContext context)
        {
            _context = context;
        }

        /*
         * GetAll
         *
         * This method retrieves all IcsLink entities from the database.
         */
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetAll()
        {
            var links = await _context.IcsLinks
                .Include(l => l.Specialization)
                .ToListAsync();

            return links.Select(l => new
            {
                l.Id,
                l.Year,
                l.Url,
                l.SpecializationId,
                SpecializationName = l.Specialization?.Name,
                SpecializationCode = l.Specialization?.Code
            }).ToList();
        }

        /*
        * Create
        *
        * This method creates a new IcsLink entity in the database.
        */
        [HttpPost]
        public async Task<ActionResult<IcsLink>> Create(IcsLink link)
        {
            if (link.SpecializationId == 0)
            {
                var defaultSpec = await _context.Specializations.FirstOrDefaultAsync(s => s.Code == "INFO");
                if (defaultSpec != null)
                    link.SpecializationId = defaultSpec.Id;
            }

            _context.IcsLinks.Add(link);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { id = link.Id }, link);
        }

        /*
        * Update
        *
        * This method updates an existing IcsLink entity in the database.
        */
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, IcsLink link)
        {
            if (id != link.Id) return BadRequest();

            var existing = await _context.IcsLinks.FindAsync(id);
            if (existing == null) return NotFound();

            existing.Year = link.Year;
            existing.Url = link.Url;
            if (link.SpecializationId != 0)
                existing.SpecializationId = link.SpecializationId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        /*
        * Delete
        *
        * This method deletes an existing IcsLink entity from the database.
        */
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var link = await _context.IcsLinks.FindAsync(id);
            if (link == null) return NotFound();
            _context.IcsLinks.Remove(link);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}