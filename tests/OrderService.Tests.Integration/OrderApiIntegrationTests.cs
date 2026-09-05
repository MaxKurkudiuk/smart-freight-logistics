using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Application.DTOs;
using OrderService.Domain.Enums;
using OrderService.Infrastructure.Data;

namespace OrderService.Tests.Integration;

public sealed class OrderApiIntegrationTests : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public OrderApiIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    public async ValueTask InitializeAsync()
    {
        // Ensure DB is clean before each test (EnsureDeleted + Migrate)
        await _factory.ResetDatabaseAsync();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task CleanDatabaseAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        try
        {
            // Fast clean: delete orders (cascades to StatusHistories)
            await db.Database.ExecuteSqlRawAsync("TRUNCATE TABLE \"Orders\" CASCADE");
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P01")
        {
            // Table doesn't exist yet (first run) — ensure migrated
            await db.Database.MigrateAsync();
        }
    }

    private static CreateOrderRequest ValidCreateRequest() => new()
    {
        CargoType = nameof(CargoType.General),
        Deadline = DateTime.UtcNow.AddDays(5),
        WeightKg = 120.5m,
        VolumeM3 = 2.3m,
        Origin = "Kyiv, UA",
        Destination = "Warsaw, PL",
        Description = "Integration test cargo",
        DeclaredValue = 5000
    };

    private HttpClient ClientWithToken(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task PostOrders_WithoutToken_ShouldReturn401()
    {
        var response = await _client.PostAsJsonAsync("/api/orders", ValidCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task PostOrders_WithClientToken_ShouldReturn201()
    {
        var clientId = Guid.NewGuid();
        var token = JwtHelper.GenerateToken(clientId, "client@example.com", "Test Client", "Client");
        var authed = ClientWithToken(token);

        var response = await authed.PostAsJsonAsync("/api/orders", ValidCreateRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var bodyObj = await response.Content.ReadFromJsonAsync<OrderResponse>();
        bodyObj.Should().NotBeNull();
        bodyObj!.ClientId.Should().Be(clientId);
        bodyObj.Status.Should().Be(OrderStatus.Created);
        response.Headers.Location.Should().NotBeNull();
    }

    [Fact]
    public async Task PostOrders_WithInvalidBody_ShouldReturn400()
    {
        var token = JwtHelper.ClientToken();
        var authed = ClientWithToken(token);
        var invalid = ValidCreateRequest() with { WeightKg = 0, Origin = "" };

        var response = await authed.PostAsJsonAsync("/api/orders", invalid);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostOrders_WithRpaBot_ShouldReturn403()
    {
        var token = JwtHelper.RpaBotToken();
        var authed = ClientWithToken(token);

        var response = await authed.PostAsJsonAsync("/api/orders", ValidCreateRequest());

        // RpaBot lacks ClientPolicy -> 403 Forbidden (Authorize Policy)
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetOrderById_ClientShouldSeeOwn_And404ForOthers()
    {
        var ownerId = Guid.NewGuid();
        var otherId = Guid.NewGuid();
        var ownerToken = JwtHelper.GenerateToken(ownerId, "owner@example.com", "Owner", "Client");
        var otherToken = JwtHelper.GenerateToken(otherId, "other@example.com", "Other", "Client");

        var ownerClient = ClientWithToken(ownerToken);
        var otherClient = ClientWithToken(otherToken);

        var createResp = await ownerClient.PostAsJsonAsync("/api/orders", ValidCreateRequest());
        createResp.EnsureSuccessStatusCode();
        var created = await createResp.Content.ReadFromJsonAsync<OrderResponse>();
        created.Should().NotBeNull();

        // Owner can fetch
        var getOwn = await ownerClient.GetAsync($"/api/orders/{created!.Id}");
        getOwn.StatusCode.Should().Be(HttpStatusCode.OK);
        var ownBody = await getOwn.Content.ReadFromJsonAsync<OrderResponse>();
        ownBody!.Id.Should().Be(created.Id);

        // Other Client gets 404 (hide existence)
        var getOther = await otherClient.GetAsync($"/api/orders/{created.Id}");
        getOther.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetOrders_List_ClientSeesOnlyOwn_ManagerSeesAll()
    {
        await CleanDatabaseAsync();

        var clientA = Guid.NewGuid();
        var clientB = Guid.NewGuid();
        var managerId = Guid.NewGuid();

        var tokenA = JwtHelper.GenerateToken(clientA, "a@example.com", "A", "Client");
        var tokenB = JwtHelper.GenerateToken(clientB, "b@example.com", "B", "Client");
        var managerToken = JwtHelper.GenerateToken(managerId, "manager@example.com", "Manager", "LogisticsManager");

        var clientAHttp = ClientWithToken(tokenA);
        var clientBHttp = ClientWithToken(tokenB);
        var managerHttp = ClientWithToken(managerToken);

        await clientAHttp.PostAsJsonAsync("/api/orders", ValidCreateRequest());
        await clientAHttp.PostAsJsonAsync("/api/orders", ValidCreateRequest() with { Origin = "Lviv, UA", Destination = "Odesa, UA" });
        await clientBHttp.PostAsJsonAsync("/api/orders", ValidCreateRequest() with { Origin = "Dnipro, UA", Destination = "Kharkiv, UA" });

        var listA = await clientAHttp.GetFromJsonAsync<List<OrderResponse>>("/api/orders");
        listA.Should().NotBeNull();
        listA!.Should().HaveCount(2);
        listA.All(o => o.ClientId == clientA).Should().BeTrue();

        var listB = await clientBHttp.GetFromJsonAsync<List<OrderResponse>>("/api/orders");
        listB.Should().HaveCount(1);

        var listMgr = await managerHttp.GetFromJsonAsync<List<OrderResponse>>("/api/orders");
        listMgr.Should().HaveCount(3);
    }

    [Fact]
    public async Task PutStatus_ValidTransition_CreatedToCancelled_ShouldReturn200()
    {
        var clientId = Guid.NewGuid();
        var token = JwtHelper.GenerateToken(clientId, "client@example.com", "Client", "Client");
        var authed = ClientWithToken(token);

        var createResp = await authed.PostAsJsonAsync("/api/orders", ValidCreateRequest());
        var created = await createResp.Content.ReadFromJsonAsync<OrderResponse>();

        var putResp = await authed.PutAsJsonAsync($"/api/orders/{created!.Id}/status",
            new UpdateStatusRequest { NewStatus = OrderStatus.Cancelled, Notes = "client cancel" });

        putResp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await putResp.Content.ReadFromJsonAsync<OrderResponse>();
        updated!.Status.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task PutStatus_InvalidTransition_CreatedToDelivered_ShouldReturn409()
    {
        var clientId = Guid.NewGuid();
        var token = JwtHelper.GenerateToken(clientId, "client@example.com", "Client", "Client");
        var authed = ClientWithToken(token);

        var createResp = await authed.PostAsJsonAsync("/api/orders", ValidCreateRequest());
        var created = await createResp.Content.ReadFromJsonAsync<OrderResponse>();

        var putResp = await authed.PutAsJsonAsync($"/api/orders/{created!.Id}/status",
            new UpdateStatusRequest { NewStatus = OrderStatus.Delivered });

        putResp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task PutStatus_Unauthorized_WhenNotOwner_ShouldReturn403()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var ownerToken = JwtHelper.GenerateToken(owner, "owner@example.com", "Owner", "Client");
        var otherToken = JwtHelper.GenerateToken(other, "other@example.com", "Other", "Client");

        var ownerClient = ClientWithToken(ownerToken);
        var otherClient = ClientWithToken(otherToken);

        var createResp = await ownerClient.PostAsJsonAsync("/api/orders", ValidCreateRequest());
        var created = await createResp.Content.ReadFromJsonAsync<OrderResponse>();

        var putResp = await otherClient.PutAsJsonAsync($"/api/orders/{created!.Id}/status",
            new UpdateStatusRequest { NewStatus = OrderStatus.Cancelled });

        putResp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task PutStatus_WithoutToken_ShouldReturn401()
    {
        var resp = await _client.PutAsJsonAsync($"/api/orders/{Guid.NewGuid()}/status",
            new UpdateStatusRequest { NewStatus = OrderStatus.Cancelled });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
