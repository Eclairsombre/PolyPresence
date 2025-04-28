using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json.Serialization;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<SessionController> _logger;

        public UserController(ApplicationDbContext context, ILogger<SessionController> logger)
        {
            _context = context;
            _logger = logger;
        }


        // GET: api/User
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers()
        {
            return await _context.Users.ToListAsync();
        }

        // GET: api/User/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            return user;
        }

        // GET: api/User/search/{studentNumber}
        [HttpGet("search/{studentNumber}")]
        public async Task<ActionResult<object>> SearchUserByNumber(string studentNumber)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.StudentNumber == studentNumber);

            if (user == null)
            {
                return NotFound(new { exists = false });
            }

            return new { exists = true, user };
        }

        // GET: api/User/IsUserAdmin/{username}
        [HttpGet("IsUserAdmin/{username}")]
        public IActionResult IsUserAdmin(string username)
        {
            var isAdmin = _context.Users.Any(u => u.StudentNumber == username && u.IsAdmin);
            return Ok(new { IsAdmin = isAdmin });
        }

        // POST: api/User
        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }

        // PUT: api/User/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest();
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!UserExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/User/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("year/{year}")]
        public async Task<ActionResult<IEnumerable<User>>> GetUserByYear(string year)
        {
            var users = await _context.Users
                .Where(s => s.IsAdmin == false)
                .Where(s => s.Year == year)
                .ToListAsync();

            if (users == null || users.Count == 0)
            {
                return NotFound(new { message = $"Aucun étudiant trouvé pour l'année {year}" });
            }

            return users;
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
    }
}