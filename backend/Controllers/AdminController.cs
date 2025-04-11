using Microsoft.AspNetCore.Mvc;
using backend.Data;
using System.Linq;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("IsUserAdmin/{username}")]
        public IActionResult IsUserAdmin(string username)
        {
            var isAdmin = _context.Admins.Any(a => a.Username == username);
            return Ok(new { IsAdmin = isAdmin });
        }
    }
}