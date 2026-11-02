using AJAI_Server.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace AJAI.Server.Tests;
public sealed class ApiFactory(string environment = "Development", string? signingKey = null) : WebApplicationFactory<Program>
{
    private readonly string database = Path.Combine(Path.GetTempPath(), $"ajai-{Guid.NewGuid()}.db");
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = signingKey ?? "test-only-signing-key-never-use-in-production-123456",
            ["Jwt:Issuer"] = "AJAI.Server", ["Jwt:Audience"] = "AJAI.Clients",
            ["ConnectionStrings:DefaultConnection"] = $"Data Source={database};Pooling=False",
            ["Demo:Enabled"] = "true", ["Demo:AdminPassword"] = "Test-admin-password-42",
            ["Demo:CameraPassword"] = "Test-camera-password-42"
        }));
    }
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            foreach (var path in new[] { database, database + "-shm", database + "-wal" })
                if (File.Exists(path)) File.Delete(path);
    }
}
public class ApiTests
{
    [Theory]
    [InlineData("")]
    [InlineData("too-short")]
    public void Startup_rejects_missing_or_short_signing_keys(string key)
    {
        using var factory = new ApiFactory(signingKey: key);
        Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
    }
    [Fact]
    public async Task Production_does_not_seed_demo_accounts_or_expose_swagger()
    {
        using var factory = new ApiFactory(environment: "Production");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Equal(0, await db.Users.CountAsync());
        Assert.Equal(0, await db.Cameras.CountAsync());
    }
    private static async Task<string> Login(HttpClient client, bool camera = false)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = camera ? "camera1@example.test" : "admin@example.test",
            password = camera ? "Test-camera-password-42" : "Test-admin-password-42"
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString()!;
    }
    private static void Bearer(HttpClient client, string token) =>
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    [Fact]
    public async Task Fresh_database_starts_and_api_requires_login()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cameras")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsync("/dashboardHub/negotiate?negotiateVersion=1", null)).StatusCode);
    }
    [Fact]
    public async Task Login_rejects_invalid_input_and_wrong_credentials()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/login", new { email = "bad", password = "" })).StatusCode);
        foreach (var email in new[] { "admin@example.test", "missing@example.test" })
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong-password" })).StatusCode);
    }
    [Fact]
    public async Task Camera_can_only_create_its_own_valid_alerts()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        Bearer(client, await Login(client, true));
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/cameras")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/alerts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/api/cameras/toggle/1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/alerts", new { cameraEmail = "other@example.test", alertType = "no_helmet", base64Image = "aGVsbG8=" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/alerts", new { cameraEmail = "camera1@example.test", alertType = "no_helmet", base64Image = "not base64!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/alerts", new { cameraEmail = "camera1@example.test", alertType = "no_helmet", base64Image = "aGVsbG8=" })).StatusCode);
        Bearer(client, await Login(client));
        var alerts = await client.GetFromJsonAsync<JsonElement>("/api/alerts");
        Assert.Equal(1, alerts.GetArrayLength());
        Assert.Equal("no_helmet", alerts[0].GetProperty("alertType").GetString());
    }
    [Fact]
    public async Task Dashboard_lists_safe_camera_fields_and_can_toggle()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        Bearer(client, await Login(client));
        var cameras = await client.GetFromJsonAsync<JsonElement>("/api/cameras");
        Assert.Equal(1, cameras.GetArrayLength());
        Assert.False(cameras[0].TryGetProperty("password", out _));
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync("/api/cameras/toggle/1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync("/api/cameras/toggle/999", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/alerts", new { cameraEmail = "camera1@example.test", alertType = "no_helmet", base64Image = "aGVsbG8=" })).StatusCode);
    }
    [Fact]
    public async Task Tampered_token_is_rejected()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var token = await Login(client);
        var parts = token.Split('.');
        parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
        Bearer(client, string.Join('.', parts));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/cameras")).StatusCode);
    }
    [Fact]
    public async Task Login_is_rate_limited()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        for (var i = 0; i < 10; i++)
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = "wrong-password" })).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.PostAsJsonAsync("/api/auth/login", new { email = "missing@example.test", password = "wrong-password" })).StatusCode);
    }
    private static HubConnection Hub(ApiFactory factory, string token) => new HubConnectionBuilder()
        .WithUrl("http://localhost/dashboardHub", options =>
        {
            options.Transports = HttpTransportType.LongPolling;
            options.AccessTokenProvider = () => Task.FromResult<string?>(token);
            options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
        }).Build();
    [Fact]
    public async Task Hub_enforces_groups_and_delivers_alerts_and_commands()
    {
        using var factory = new ApiFactory(); using var client = factory.CreateClient();
        var adminToken = await Login(client); var cameraToken = await Login(client, true);
        await using var admin = Hub(factory, adminToken); await using var camera = Hub(factory, cameraToken);
        var alert = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        var status = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);
        admin.On<JsonElement>("ReceiveNewAlert", payload => alert.TrySetResult(payload));
        admin.On<JsonElement>("CameraStatusChanged", payload => status.TrySetResult(payload));
        camera.On<JsonElement>("ReceiveCameraCommand", payload => command.TrySetResult(payload));
        await admin.StartAsync(); await camera.StartAsync();
        await admin.InvokeAsync("JoinDashboard");
        await camera.InvokeAsync("JoinCameraGroup", "camera1@example.test");
        await Assert.ThrowsAsync<HubException>(() => camera.InvokeAsync("JoinDashboard"));
        await Assert.ThrowsAsync<HubException>(() => camera.InvokeAsync("JoinCameraGroup", "other@example.test"));
        await Assert.ThrowsAsync<HubException>(() => admin.InvokeAsync("JoinCameraGroup", "camera1@example.test"));
        Bearer(client, cameraToken);
        (await client.PostAsJsonAsync("/api/alerts", new { cameraEmail = "camera1@example.test", alertType = "no_helmet", base64Image = "aGVsbG8=" })).EnsureSuccessStatusCode();
        Assert.Equal("no_helmet", (await alert.Task.WaitAsync(TimeSpan.FromSeconds(10))).GetProperty("alertType").GetString());
        Bearer(client, adminToken);
        (await client.PostAsync("/api/cameras/toggle/1", null)).EnsureSuccessStatusCode();
        Assert.Equal("STOP_STREAM", (await command.Task.WaitAsync(TimeSpan.FromSeconds(10))).GetProperty("command").GetString());
        Assert.False((await status.Task.WaitAsync(TimeSpan.FromSeconds(10))).GetProperty("status").GetBoolean());
    }
}
