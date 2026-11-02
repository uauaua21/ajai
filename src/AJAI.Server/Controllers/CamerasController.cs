using AJAI_Server.Data;
using AJAI_Server.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AJAI_Server.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize(Roles = "User")]
    public class CamerasController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IHubContext<DashboardHub> _hubContext;

        public CamerasController(AppDbContext context, IHubContext<DashboardHub> hubContext)
        {
            _context = context;
            _hubContext = hubContext;
        }

        [HttpGet]
        public async Task<IActionResult> GetCameras()
        {
            return Ok(await _context.Cameras.AsNoTracking().Select(c => new { c.Id, c.Email, c.IsActive, c.LastPing }).ToListAsync());
        }

        [HttpPost("toggle/{id}")]
        public async Task<IActionResult> ToggleCamera(int id)
        {
            var camera = await _context.Cameras.FindAsync(id);
            if (camera == null) return NotFound();

            camera.IsActive = !camera.IsActive;
            await _context.SaveChangesAsync();

            await _hubContext.Clients.Group("Admins").SendAsync("CameraStatusChanged", new
            {
                cameraId = camera.Id,
                status = camera.IsActive
            });

            await _hubContext.Clients.Group($"Camera_{camera.Email}").SendAsync("ReceiveCameraCommand", new
            {
                command = camera.IsActive ? "START_STREAM" : "STOP_STREAM"
            });

            return Ok(new { Message = "Camera status updated", IsActive = camera.IsActive });
        }
    }
}
