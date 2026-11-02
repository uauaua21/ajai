using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
namespace AJAI_Server.Hubs;
[Authorize]
public class DashboardHub : Hub
{
    [Authorize(Roles = "User")]
    public Task JoinDashboard() => Groups.AddToGroupAsync(Context.ConnectionId, "Admins");
    [Authorize(Roles = "Camera")]
    public Task JoinCameraGroup(string cameraEmail)
    {
        var identity = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.Equals(identity, cameraEmail, StringComparison.Ordinal))
            throw new HubException("A camera can only join its own group.");
        return Groups.AddToGroupAsync(Context.ConnectionId, $"Camera_{identity}");
    }
}
