using Microsoft.AspNetCore.Mvc;
using backend.Models;

using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class IcsLinkController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public IcsLinkController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<IcsLink>>> GetAll()
        {
            return await _context.IcsLinks.ToListAsync();
        }

        [HttpPost]
        public async Task<ActionResult<IcsLink>> Create(IcsLink link)
        {
            _context.IcsLinks.Add(link);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { id = link.Id }, link);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, IcsLink link)
        {
            if (id != link.Id) return BadRequest();
            _context.Entry(link).State = EntityState.Modified;
            await _context.SaveChangesAsync();
            return NoContent();
        }

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