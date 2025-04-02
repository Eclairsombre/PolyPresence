using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetUser()
        {
            var user = HttpContext.Session.GetString("User");
            if (user != null)
            {
                return Ok(new { success = true, user });
            }
            return Ok(new { success = false, message = "Utilisateur non authentifié" });
        }
    }
}