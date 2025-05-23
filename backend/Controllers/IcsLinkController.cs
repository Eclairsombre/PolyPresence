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
        public async Task<ActionResult<IEnumerable<IcsLink>>> GetAll()
        {
            return await _context.IcsLinks.ToListAsync();
        }

        /*
        * Create
        *
        * This method creates a new IcsLink entity in the database.
        */
        [HttpPost]
        public async Task<ActionResult<IcsLink>> Create(IcsLink link)
        {
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
            _context.Entry(link).State = EntityState.Modified;
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