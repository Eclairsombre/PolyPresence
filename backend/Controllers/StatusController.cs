using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    /**
     * StatusController
     *
     * This controller handles the status check for the application.
     */
    [ApiController]
    [Route("api/[controller]")]
    public class StatusController : ControllerBase
    {
        [HttpGet]
        public IActionResult GetStatus()
        {
            return Ok(new { status = "ok" });
        }
    }
}
