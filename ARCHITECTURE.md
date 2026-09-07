# Smart Freight Logistics — Architecture Documentation

> Generated from GitNexus knowledge graph (`smart-freight-logistics`, commit `features/init-tracking-service`, indexed `2026-09-07T20:07:00Z`) + manual source verification. The graph now reports `35` execution flows and `35` functional areas after Stage 5 (TrackingService + Redis Cache-Aside + Order Cache-Aside + YARP). This document supplements the graph with deterministic code reads, each cited as `file:line`.

---

## 1. Overview

Smart Freight Logistics is a **.NET 10 microservices** system for freight order management, tracking, and RPA customs integration. Architecture follows **YARP API Gateway + per-service PostgreSQL databases + Redis cache + shared Serilog building block**.

| Concern | Choice | Evidence |
|---------|--------|----------|
| Runtime | .NET 10 (`net10.0`) | `src/Gateways/YarpGateway/YarpGateway.csproj:4`, `src/Services/IdentityService/IdentityService.csproj:4` |
| Gateway | YARP Reverse Proxy 2.3.0 | `src/Gateways/YarpGateway/YarpGateway.csproj:10`, `src/Gateways/YarpGateway/Program.cs:9` |
| Logging | Serilog.AspNetCore 10.0.0 + Serilog.Sinks.Console 6.1.1 | `src/BuildingBlocks/Logging/BuildingBlocks.Logging.csproj:10` |
| Caching | StackExchange.Redis 2.8.37 + Microsoft.Extensions.Caching.StackExchangeRedis 9.0.8 | `src/BuildingBlocks/Caching/BuildingBlocks.Caching.csproj:10`, `src/BuildingBlocks/Caching/Extensions/CacheServiceCollectionExtensions.cs:10` |
| Identity DB | EF Core 10.0.11 + Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, PostgreSQL 16 | `src/Services/IdentityService/IdentityService.csproj:15`, `docker/docker-compose.yml:6` |
| Auth | Microsoft.AspNetCore.Authentication.JwtBearer 10.0.11 (JWT Policies) | `src/Services/IdentityService/IdentityService.csproj:11` |
| Broker | RabbitMQ 3-management-alpine + MassTransit 8.3.5 | `docker/docker-compose.yml:49`, `src/BuildingBlocks/EventBus/BuildingBlocks.EventBus.csproj:10` |
| Infra | Docker Compose (postgres, redis healthy, pgAdmin, rabbitmq healthy, integration-service :5003, tracking-service :5004) on `logistics-network` bridge | `docker/docker-compose.yml:140` |

**Current maturity:** Stages 1 (Foundation), 2 (Identity + JWT + YARP policies), 3 (OrderService Clean Architecture, CRUD, tests, seed), **4 (Event-Driven: Domain Events → Integration Events → MassTransit/RabbitMQ → IntegrationService RPA Bridge → RpaBot Customs)** and **5 (TrackingService + Redis Cache-Aside + Order Cache-Aside + YARP)** are implemented. Stages 6-7 (CQRS, Prod Readiness) remain planned per `docs/Smart Freight Logistics main plan.md:3`.

**Entry point:** All clients hit the gateway; gateway routes `/api/auth/{**catch-all}` → IdentityService (`:5001`), `/api/tracking/{**catch-all}` → TrackingService (`:5004`) (`YarpGateway/appsettings.json:43`), `/api/orders/{id}/status PUT` → `rpa-status-route` (`YarpGateway/appsettings.json:36`), and `/api/orders/{**catch-all}` → OrderService.API (`:5002`) — `src/Gateways/YarpGateway/appsettings.json:28`.

---

## 2. Codebase Stats (Knowledge Graph)

Source: `gitnexus://repo/smart-freight-logistics/context`, `gitnexus://repo/smart-freight-logistics/clusters`, `gitnexus://repo/smart-freight-logistics/processes`, `gitnexus://repos`, and `cypher MATCH (n) RETURN labels(n), n.name`.

| Metric | Value | Notes |
|--------|-------|-------|
| Indexed at | `2026-09-07T20:07:00Z` | Runner `gitnexus@1.6.11`, `node v24.16.0 win32` |
| Commit | `features/init-tracking-service` | `Stage 5 TrackingService + Redis Cache-Aside + Order Cache-Aside + YARP + 70 tests` |
| Files indexed | 134 | `status: matches all 134 covered file(s)` |
| Symbols | 891 | `nodes: 891` — includes `Order`, `CargoDetails`, `StatusHistory`, `OrderStatusTransitions`, `OrderCreatedDomainEvent`, `OrderCreatedIntegrationEvent`, `OrderCreatedConsumer`, `RpaClient`, `OrderStatusClient`, `IntegrationService`, `CacheKeys`, `RedisCacheService`, `ICacheService`, `TrackingEntry`, `GeoCoordinate`, `RedisTrackingRepository`, `TrackingAppService`, `TrackingController`, `TrackingService` + tests + `README` |
| Relationships | 1736 | `edges: 1736` |
| Processes (execution flows) | 35 | `flows: 35` — Order CRUD, Auth, YARP, `OrderCreatedDomainEvent → IntegrationEvent → Publish → Consume → Rpa → Customs`, `Tracking PUT/GET + Cache-Aside Order/Tracking`, `Gateway tracking-route` |
| Functional areas (Leiden clusters) | 35 | `clusters: 35` — `Community` nodes (Auth, Order, Gateway, Logging, EventBus, Integration, Caching, Tracking) |
| Solution projects | 19 | `SmartFreightLogistics.slnx:1` — 11 src (`+Caching`, `+Tracking.Domain/Infrastructure/Application/API`) + 5 tests (`OrderService Unit/Integration`, `IntegrationService Integration`, `TrackingService Unit/Integration`) + Gateway |

> **Why 0 processes/clusters was not an error before:** The indexed code was minimal — `Program.cs` only wired middleware, `OrderService` were `Class1.cs:1` placeholders. After Stage 3 (`Order` aggregate, `OrdersController`, `Jwt`, `YARP` policies, `Testcontainers`), the analyzer correctly populates `23` processes and `21` clusters. No re-index needed until next stage.

---

## 3. Functional Areas

Since `gitnexus://repo/smart-freight-logistics/clusters` is empty, areas are derived from solution folders (`SmartFreightLogistics.slnx:1`) and `docker/docker-compose.yml:1`.

### 3.1 BuildingBlocks.Logging — Cross-Cutting Observability
- **Path:** `src/BuildingBlocks/Logging/` (`BuildingBlocks.Logging.csproj:1`)
- **Symbols:** `SerilogExtensions` (`src/BuildingBlocks/Logging/SerilogExtensions.cs:7`), `CorrelationIdMiddleware` (`src/BuildingBlocks/Logging/CorrelationIdMiddleware.cs:7`)
- **Responsibility:** Shared Serilog configuration and correlation-ID propagation. Consumed by every Web host.
- **Key APIs:**
  - `SerilogExtensions.AddSharedLogging(WebApplicationBuilder)` — `Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).CreateLogger()` then `builder.Host.UseSerilog()` and `AddTransient<CorrelationIdMiddleware>()` — `SerilogExtensions.cs:9`
  - `SerilogExtensions.UseSharedLogging(WebApplication)` — `app.UseMiddleware<CorrelationIdMiddleware>()` — `SerilogExtensions.cs:23`
  - `CorrelationIdMiddleware.InvokeAsync` — reads `X-Correlation-ID` header or `Guid.NewGuid()`, echoes to `Response.Headers`, wraps `next` in `LogContext.PushProperty("CorrelationId", ...)` — `CorrelationIdMiddleware.cs:11`
- **Config:** `Serilog` section in every `appsettings.json` — console sink `outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}"`, `Enrich: [FromLogContext, WithMachineName, WithThreadId]` — `src/Services/IdentityService/appsettings.json:11`, `src/Gateways/YarpGateway/appsettings.json:2`, `src/Services/OrderService/OrderService.API/appsettings.json:2`
- **Dependents:** `YarpGateway.csproj:14`, `IdentityService.csproj:21`, `OrderService.API.csproj:14` all `ProjectReference` this block.

### 3.2 Gateways.YarpGateway — Edge Gateway
- **Path:** `src/Gateways/YarpGateway/` (`YarpGateway.csproj:1`)
- **Entrypoint:** `src/Gateways/YarpGateway/Program.cs:1`
- **Responsibility:** Single entry point, reverse-proxy routing, pipeline head for correlation IDs.
- **Routing (YARP `ReverseProxy`):** `src/Gateways/YarpGateway/appsettings.json:28`
  - `identity-route: ClusterId=identity-cluster, Match Path=/api/auth/{**catch-all}, Destination=http://localhost:5001` — `appsettings.json:30`
  - `rpa-status-route: ClusterId=order-cluster, Match Path=/api/orders/{id}/status Methods PUT, Destination=http://localhost:5002` (no `AuthorizationPolicy` → `OrderService` enforces `RpaBot→Customs`) — `appsettings.json:36`
  - `tracking-route: ClusterId=tracking-cluster, Match Path=/api/tracking/{**catch-all}, Destination=http://localhost:5004` (no `AuthorizationPolicy`, service enforces `ClientPolicy` on `PUT`) — `appsettings.json:43`
  - `order-route: ClusterId=order-cluster, Match Path=/api/orders/{**catch-all}, AuthorizationPolicy=ClientPolicy, Destination=http://localhost:5002` — `appsettings.json:49`
- **Pipeline:** `builder.AddSharedLogging()` → `AddReverseProxy().LoadFromConfig(ReverseProxy)` → `app.UseSharedLogging()` → `UseRouting()` → `MapReverseProxy()` — `Program.cs:6`
- **Tests:** `YarpGateway.http` `register/login/me/orders` + `tracking` `PUT 401/PUT 200/GET 200/health` via `:5000` — `YarpGateway.http:129`

### 3.3 Services.IdentityService — Authentication & Users
- **Path:** `src/Services/IdentityService/` (`IdentityService.csproj:1`)
- **Entrypoint:** `src/Services/IdentityService/Program.cs:1`
- **Entity:** `User { Guid Id, string Email, string PasswordHash, string FullName, string Role, DateTime CreatedAt }` — `src/Services/IdentityService/Entities/User.cs:3` — `Role` values `Client | LogisticsManager | RpaBot` per `User.cs:9` and `docs/main plan.md:22`
- **Persistence:**
  - `IdentityDbContext(DbContextOptions): DbSet<User> Users` — `src/Services/IdentityService/Data/IdentityDbContext.cs:6`
  - `OnModelCreating` configures `PK Id`, `IX_Users_Email unique`, `Email IsRequired MaxLength(150)`, `PasswordHash IsRequired`, `Role IsRequired MaxLength(50)` — `IdentityDbContext.cs:15`
  - Migration `20260830173900_InitialCreate` creates `Users` table + `IX_Users_Email` — `src/Services/IdentityService/Migrations/20260830173900_InitialCreate.cs:14`
  - Secure connection: `Program.cs` builds `NpgsqlConnectionStringBuilder` from `ConnectionStrings:IdentityDb` + `DatabaseSettings:Password` (UserSecrets/env) — `src/Services/IdentityService/Program.cs:14`; design-time factory mirrors this with fallback path resolution — `src/Services/IdentityService/Data/IdentityDbContextFactory.cs:13`
- **Config:** `ConnectionStrings:IdentityDb=Host=localhost;Port=5432;Database=sfl_identity_db;...` and `JwtSettings { Secret, Issuer=SmartFreightLogistics.Identity, Audience=SmartFreightLogistics.Gateways, ExpiryInMinutes=60 }` — `src/Services/IdentityService/appsettings.json:2`
- **Pipeline (current):** `AddSharedLogging()` → `AddOpenApi()` → `AddDbContext(UseNpgsql(...))` → `UseHttpsRedirection()` → `UseSharedLogging()` → `UseRouting()` → `MapOpenApi()` (dev only) — `Program.cs:6` — note `MapControllers()` is commented out `Program.cs:34`, no auth controllers yet.
- **Planned:** JWT generation with Claims (roles/IDs) and policy enforcement — `docs/main plan.md:18`.

### 3.4 Services.OrderService — Order Management (Clean Architecture, Event-Driven, Cache-Aside)
- **Paths:**
  - `src/Services/OrderService/OrderService.API/` (`OrderService.API.csproj:1`) — Web host, refs `Logging + Caching + EventBus + Application + Infrastructure` — `OrderService.API.csproj:22`
  - `src/Services/OrderService/OrderService.Application/` (`OrderService.Application.csproj:1`) — `IOrderService`, `OrderService` with `IPublishEndpoint` + `ICacheService` `Cache-Aside`, `Mappings/OrderCreatedIntegrationMapper`
  - `src/Services/OrderService/OrderService.Domain/` (`OrderService.Domain.csproj:1`) — `Order` aggregate with `DomainEvents`, `OrderStatusTransitions`, `OrderCreatedDomainEvent`
  - `src/Services/OrderService/OrderService.Infrastructure/` (`OrderService.Infrastructure.csproj:1`) — `OrderDbContext` `sfl_order_db`, `OrderRepository ExecuteUpdate`, `OrderSeeder`
- **Entrypoint:** `src/Services/OrderService/OrderService.API/Program.cs:1` — `AddSharedLogging()` → `AddOrderDbContext()` → `AddOrderAuth()` → `AddCaching()` (or `AddInMemoryCaching` when `Testing` + `YOUR_` guard) → `AddEventBus()` (MassTransit `RabbitMQ` `Retry 3×1s`) or `InMemory` when `Testing` → `AddControllers()` — `Program.cs:12`
- **Domain Events (4.2):** `Order` `DomainEvents` `IDomainEvent` `OrderCreatedDomainEvent(Cargo)` `OrderStatusChangedDomainEvent` — `Order.cs:18` `DomainEvents` ignored by `OrderDbContext:40`
- **Caching (5.7):** `OrderService.cs:14` `ICacheService` `CacheKeys.Order(id) TTL 2m`, `OrderList{client|all} TTL 2m` — `GetByIdAsync` `GetAsync` before DB with ownership check, `ListAsync` `GetAsync<IReadOnlyList>`, `CreateAsync` `SetAsync Order + RemoveAsync OrderList*`, `UpdateStatusAsync` `Remove+Set Order` `Remove OrderList*` — `BuildingBlocks.Caching` `IDistributedCache` `YOUR_` → `InMemory`
- **Publishing (4.5):** `OrderService.cs:40` after `SaveChangesAsync` `OfType<OrderCreatedDomainEvent>().First() → ToIntegrationEvent() → IPublishEndpoint.Publish(OrderCreatedIntegrationEvent)` `ClearDomainEvents()` — flat `DTO` `OrderId/ClientId/CargoType/WeightKg/Origin/Destination/CreatedAt` via `BuildingBlocks.EventBus`
- **Auth (4.9):** `OrdersController.cs:82` `PUT status` allows `RpaBot` only `Customs` (`IsRpaBot` check), otherwise `Manager` or owner — `OrderService.cs:77` `IsRpaBot`/`IsManager`

### 3.5 BuildingBlocks.Caching — Distributed Cache
- **Path:** `src/BuildingBlocks/Caching/` (`BuildingBlocks.Caching.csproj:1` `StackExchange.Redis 2.8.37 + Microsoft.Extensions.Caching.StackExchangeRedis 9.0.8`)
- **Symbols:** `CacheKeys` (`CacheKeys.cs:5` `TrackingTtl 5m, OrderTtl 2m, OrderListTtl 2m`, `Tracking(id)=>tracking:{id}`), `ICacheService` (`ICacheService.cs:5` `GetAsync/SetAsync/RemoveAsync/GetOrCreateAsync`), `RedisCacheService` (`RedisCacheService.cs:10` `IDistributedCache` `System.Text.Json CamelCase` `YOUR_` → `InMemory`), `CacheServiceCollectionExtensions.AddCaching` (`Extensions/CacheServiceCollectionExtensions.cs:10` `GetConnectionString("Redis")` `StackExchangeRedisCache` else `DistributedMemoryCache` + `InMemory` fallback)
- **Usage:** `TrackingService` `RedisTrackingRepository` `tracking:{orderId}`, `OrderService` `GetById/List` `Cache-Aside`

### 3.6 BuildingBlocks.EventBus — Integration Contracts & Transport
- **Path:** `src/BuildingBlocks/EventBus/` (`BuildingBlocks.EventBus.csproj:1` `MassTransit 8.3.5/RabbitMQ 8.3.5`)
- **Symbols:** `RabbitMqSettings` (`RabbitMqSettings.cs:9` `Host localhost:5672 User/Password required via user-secrets` `ValidateOnStart`), `ServiceCollectionExtensions.AddEventBus` (`Extensions/ServiceCollectionExtensions.cs:16` `Bind RabbitMq` `UseMessageRetry Interval(3,1s) ConfigureEndpoints`)
- **Contracts:** `IntegrationEvents/OrderCreatedIntegrationEvent.cs:1` `sealed record OrderCreatedIntegrationEvent(Guid OrderId, ClientId, string CargoType, decimal WeightKg, string Origin, Destination, DateTime CreatedAt)` — stable cross-service DTO

### 3.7 Services.IntegrationService — RPA Bridge (No DB, Stateless)
- **Path:** `src/Services/IntegrationService/` (`IntegrationService.csproj:1` `Sdk.Web` `MassTransit` `Http.Polly 10.0.0` `JwtBearer 10.0.11`)
- **Entrypoint:** `Program.cs:1` `AddSharedLogging()` → `AddEventBus(x=>AddConsumer<OrderCreatedConsumer>)` → `AddHttpClient<IRpaClient,RpaClient>` / `AddHttpClient<IOrderStatusClient,OrderStatusClient>` with `WaitAndRetry 3×2^retry` + `CircuitBreaker 5/30s` → `UseRouting/Authentication/Authorization MapControllers MapOpenApi` `:5003`
- **Consumer (4.7):** `Consumers/OrderCreatedConsumer.cs:1` `IConsumer<OrderCreatedIntegrationEvent>` logs `OrderId` `SubmitCustomsAsync → MarkCustomsAsync` `MassTransit Retry`
- **Clients (4.8):** `Clients/RpaClient.cs:1` `HttpClient PostAsJsonAsync api/customs/declarations` to `Rpa:BaseUrl localhost:5004` `Clients/OrderStatusClient.cs:1` `HttpClient Put api/orders/{id}/status {newStatus=Customs}` with `RpaBot JWT` `HS256` same `Secret/Issuer/Audience` (`JwtSettings`) `5m` expiry
- **Auth (4.9):** `RpaBot` `2222...` can `Customs` via `YarpGateway rpa-status-route` (`appsettings.json:36` `PUT /api/orders/{id}/status` without `AuthorizationPolicy` → `OrderService` enforces) or direct `:5002`

### 3.8 Services.TrackingService — Real-time Tracking (Redis, No DB)
- **Paths:**
  - `src/Services/TrackingService/TrackingService.Domain/` (`TrackingService.Domain.csproj:1`) — `TrackingEntry` (`OrderId, Lat/Lon, SpeedKmh, Timestamp, Notes`) `GeoCoordinate` VO `[JsonConstructor]` `Validate -90..90/-180..180`
  - `src/Services/TrackingService/TrackingService.Infrastructure/` (`TrackingService.Infrastructure.csproj:1` refs `Domain + Caching`) — `ITrackingRepository` `GetAsync/SetAsync/DeleteAsync`, `RedisTrackingRepository` (`Redis/RedisTrackingRepository.cs:10` `ICacheService` `CacheKeys.Tracking(id) TTL 5m`)
  - `src/Services/TrackingService/TrackingService.Application/` (`TrackingService.Application.csproj:1` refs `Domain + Infrastructure`) — `DTOs UpdateTrackingRequest` (`DTOs/UpdateTrackingRequest.cs:5` `[Range -90/180]`), `TrackingResponse`, `ITrackingService`, `TrackingAppService` (`Services/TrackingService.cs:10` `GetAsync → repo.GetAsync → Map`, `UpdateAsync → Create/Update → repo.SetAsync`)
  - `src/Services/TrackingService/TrackingService.API/` (`TrackingService.API.csproj:1` `Sdk.Web` `JwtBearer 10.0.11 + OpenApi`) — `Controllers/TrackingController.cs:10` `[Authorize] PUT {orderId} [ClientPolicy] → _trackingService.UpdateAsync 200/400` `GET {orderId} 200/404` `GET health AllowAnonymous`, `Extensions/AuthExtensions.cs:10` `AddTrackingAuth` same `Jwt` `Client/LogisticsManager/RpaBot`, `Program.cs:10` `AddSharedLogging → AddCaching → AddTrackingAuth → AddScoped ITrackingRepository/ITrackingService → AddControllers → MapControllers` `:5004`
- **Config:** `ConnectionStrings:Redis localhost:6379,password=YOUR_REDIS_PASSWORD` placeholder (`appsettings.json:3`), `appsettings.Development.json` `{}`, real via `user-secrets` `ConnectionStrings:Redis` dev / `ConnectionStrings__Redis` `docker/.env` `REDIS_PASSWORD` prod (`docker-compose.yml:121`)

### 3.9 Infrastructure — Docker Compose (Stage 5)
- **Path:** `docker/docker-compose.yml:1`, `docker/postgres/init-scripts/init.sql:1`, `docker/.env.example`
 - **Services (all on `logistics-network` bridge — `docker-compose.yml:140`):**
  - `logistics-db` — `postgres:16-alpine`, `logistics-postgres-db`, `5432:5432`, env `POSTGRES_USER/PASSWORD`, `POSTGRES_MULTIPLE_DATABASES="sfl_identity_db,sfl_order_db"`, volumes `postgres_data` + `./postgres/init-scripts:/docker-entrypoint-initdb.d` — `docker-compose.yml:5`
  - `pgadmin` — `dpage/pgadmin4`, `logistics-pgadmin`, `5050:80`, depends_on `logistics-db` — `docker-compose.yml:22`
  - `logistics-cache` — `redis:7-alpine`, `logistics-redis-cache`, `6379:6379`, `redis-server --requirepass ${REDIS_PASSWORD}`, env `REDIS_PASSWORD`, volume `redis_data`, healthcheck `CMD-SHELL redis-cli -a $$REDIS_PASSWORD ping` — `docker-compose.yml:36`
  - `logistics-rabbitmq` — `rabbitmq:3-management-alpine`, `logistics-rabbitmq`, `5672:5672 15672:15672`, env `RABBITMQ_USER/PASSWORD`, volume `rabbitmq_data`, healthcheck `rabbitmq-diagnostics` — `docker-compose.yml:57`
  - `integration-service` — `mcr.microsoft.com/dotnet/aspnet:10.0`, `logistics-integration-service`, `5003:8080 7003:8081`, `depends_on rabbitmq healthy`, env `RabbitMq__Host logistics-rabbitmq` `JwtSettings__Secret` `Rpa__BaseUrl` — `docker-compose.yml:79`
  - `tracking-service` — `mcr.microsoft.com/dotnet/aspnet:10.0`, `logistics-tracking-service`, `5004:8080 7004:8081`, `depends_on redis healthy + rabbitmq healthy`, env `ConnectionStrings__Redis logistics-cache:6379,password=...` `JwtSettings__Secret` — `docker-compose.yml:109`
- **Init:** `init.sql:1` creates `sfl_identity_db` and `sfl_order_db` (runs once on first `docker-compose up`; reset via `down -v` per `docs/tips.md:63`).
- **Operational docs:** `docs/tips.md:1` covers `up -d`, `down`, `logs -f`, `exec -it logistics-redis-cache redis-cli`, `.env` handling, volume persistence.

---

## 4. Key Execution Flows

> Graph `processes` is empty, so flows below are **manually traced from source**. After Stage 3, they should appear as `Process` nodes; until then treat these as canonical intended flows.

### Flow 1 — Gateway Request Routing (Implemented)

**Goal:** Route external `POST /api/auth/**` and `/api/orders/**` to correct microservice.

1. Client `HTTP /api/auth/login` → `YarpGateway:5000`
2. `Program.cs:15` `app.UseSharedLogging()` — `CorrelationIdMiddleware.InvokeAsync` (`CorrelationIdMiddleware.cs:11`) checks `Request.Headers["X-Correlation-ID"]`, generates `Guid` if absent, sets `Response.Headers["X-Correlation-ID"]`, pushes `LogContext` property
3. `Program.cs:20` `app.MapReverseProxy()` — YARP matches `ReverseProxy.Routes.identity-route.Path=/api/auth/{**catch-all}` (`appsettings.json:27`) → `Clusters.identity-cluster.Destinations.destination1.Address=http://localhost:5001` (`appsettings.json:41`)
4. YARP forwards to `IdentityService:5001` (currently returns 404 — no controller at `Program.cs:34`)
5. Response flows back through `CorrelationIdMiddleware` `LogContext` scope, gateway logs `[{CorrelationId}] Forwarded to identity-cluster` via Serilog console sink

*Symmetric for `/api/orders/**` → `order-cluster :5002` (`appsettings.json:30`/`YarpGateway/Program.cs:9`).*

### Flow 2 — Distributed Correlation ID Propagation (Implemented, Cross-Cutting)

**Goal:** Tie logs across gateway + services for a single request.

1. `YarpGateway` ingress: `CorrelationIdMiddleware.cs:13` `TryGetValue("X-Correlation-ID", out correlationId)` else `Guid.NewGuid().ToString()` (`CorrelationIdMiddleware.cs:15`)
2. `CorrelationIdMiddleware.cs:18` `context.Response.Headers[CorrelationIdHeaderKey]=correlationId`
3. `CorrelationIdMiddleware.cs:20` `using (LogContext.PushProperty("CorrelationId", correlationId)) { await next(context); }` — Serilog `Enrich.FromLogContext()` picks it up → console `[{CorrelationId}]` per `appsettings.json:24`
4. Downstream services (`IdentityService`, `OrderService.API`) repeat same middleware (`Program.cs:31`/`Program.cs:16` `UseSharedLogging()`), so they either inherit the header forwarded by YARP or create their own — header is therefore end-to-end if gateway forwards it (YARP does by default).

### Flow 3 — Identity Persistence & Migration (Implemented)

**Goal:** Store/retrieve `User` with secure password handling and unique email.

1. Startup `IdentityService/Program.cs:14` reads `Configuration.GetConnectionString("IdentityDb")` + `Configuration["DatabaseSettings:Password"]` → `NpgsqlConnectionStringBuilder` injects password (`Program.cs:19`) → `AddDbContext<IdentityDbContext>(UseNpgsql(...))` (`Program.cs:25`)
2. Design-time `IdentityDbContextFactory.CreateDbContext` (`IdentityDbContextFactory.cs:15`) mirrors this via `ConfigurationBuilder` over `appsettings.json` + `appsettings.{Env}.json` + UserSecrets + env, with `ResolveBasePath()` walking from `Directory.GetCurrentDirectory()` or assembly dir to find `appsettings.json` (`IdentityDbContextFactory.cs:53`)
3. `IdentityDbContext.OnModelCreating` (`IdentityDbContext.cs:10`) configures `HasKey(Id)`, `HasIndex(Email).IsUnique()`, column lengths/requirements
4. EF `dotnet ef migrations add` produced `20260830173900_InitialCreate.cs:14` `CreateTable Users { Id uuid PK, Email varchar(150) NN, PasswordHash text NN, FullName text NN, Role varchar(50) NN, CreatedAt timestamptz NN }` + `CreateIndex IX_Users_Email unique`
5. Runtime `dotnet ef database update` (or Docker first-run `init.sql:1` `CREATE DATABASE sfl_identity_db`) materializes schema in `logistics-db:5432`.

### Flow 4 — Order CRUD (Implemented — Stage 3, extended Stages 4-5 Cache-Aside)

**Goal (per `docs/main plan.md:28`):** `ClientPolicy` user creates `Order` with `CargoDetails`, reads via ownership + `Cache-Aside`, updates `StatusHistory` via state machine (now with `RpaBot` `Customs` + Redis).

*Implemented sequence (replaces `Class1.cs:1` stubs, extended 4.5/4.9/5.7):*

1. `Client → YarpGateway /api/orders POST` → `order-cluster :5002` (`YarpGateway/appsettings.json:49`, `YarpGateway/Program.cs:26` `MapReverseProxy`, `AuthExtension.cs:38` `ClientPolicy` requires `Client` role)
2. `OrderService.API` `OrdersController.cs:36` `[Authorize(Policy="ClientPolicy")] POST` validates `CreateOrderRequest` (`DTOs/CreateOrderRequest.cs:5` `sealed record` `CargoType/WeightKg/Origin/Destination`), maps to `OrderService.Application` `IOrderService.CreateAsync` (`Services/OrderService.cs:11`) — checks `Origin!=Destination`, creates `CargoDetails` owned VO, calls `Order.Create(clientId,cargo)` (`Domain/Entities/Order.cs:20` validates `Weight>0/Origin/Destination`, adds `StatusHistory` `Created` + `OrderCreatedDomainEvent` `DomainEvents`)
3. `OrderService.Application` persists via `IOrderRepository.AddAsync` + `SaveChanges` (`Infrastructure/Repositories/OrderRepository.cs:32`) → `OrderDbContext.cs:15` `OwnsOne(Cargo)`, `HasIndex(ClientId/Status)`, `DbSet<Order>` to `sfl_order_db` (`docker-compose.yml:11`, `init-scripts`), seeded in `IsDevelopment` via `OrderSeeder.cs:13` `3` dev orders for `dev.client 3333...` (`Program.cs:28` `if IsDevelopment SeedAsync`), then `OfType<OrderCreatedDomainEvent>().First().ToIntegrationEvent()` → `IPublishEndpoint.Publish(OrderCreatedIntegrationEvent)` (`OrderService.cs:40`, `Mappings/OrderCreatedIntegrationMapper.cs:1`) via `MassTransit RabbitMQ` (`BuildingBlocks.EventBus` `ServiceCollectionExtensions.cs:16` `UseMessageRetry 3×1s`) or `InMemory` when `Testing`, then `ICacheService SetAsync Order(id) TTL 2m + RemoveAsync OrderList*` (`OrderService.cs:53` `Cache-Aside`)
4. `GET /api/orders/{id}` `OrdersController.cs:58` `Cache-Aside` `GetAsync<OrderResponse>(order:{id})` before DB with ownership check (`OrderService.cs:55`), `GET /api/orders` `OrdersController.cs:71` `GetAsync<IReadOnlyList>(order:list:{client|all})` (`OrderService.cs:79` `OrderListTtl 2m`) — `Client` sees only `WHERE ClientId==sub` (`ListByClientAsync`), `LogisticsManager` sees all (`ListAllAsync`), `404` for foreign `Client`; on `miss` → `Map` + `SetAsync`; `InMemory` fallback when `Testing` (`YOUR_` guard) or `DistributedMemoryCache`
5. `PUT /api/orders/{id}/status` `OrdersController.cs:82` → `OrderService.cs:72` `UpdateStatusAsync` checks `IsRpaBot` (only `Customs`) else `IsManager` or owner, validates `OrderStatusTransitions.Ensure` (`OrderStatusTransitions.cs:10`), then `TryUpdateStatusWithHistoryAsync` (`OrderRepository.cs:41` `ExecuteUpdate`) → `Remove+Set Order(id)` `Remove OrderList*` (`OrderService.cs:135`) — `DomainException → 409`, `UnauthorizedAccessException → 403`. `Integration` tests via `Testcontainers.PostgreSql` `WebApplicationFactory` `CustomWebApplicationFactory.cs:13` + `JwtHelper.cs:1` `HS256` verify `201/200/404/403/409` + `RpaBot Customs` via `Yarp rpa-status-route` (`YarpGateway/appsettings.json:36` `PUT /api/orders/{id}/status` without `AuthorizationPolicy`).

### Flow 5 — Event-Driven RPA Bridge (Implemented — Stage 4)

**Goal:** `OrderCreatedIntegrationEvent` → `RabbitMQ` → `IntegrationService` → `RPA` → `OrderService Customs` via `RpaBot` `JWT`.

1. `OrderService` `CreateAsync` publishes `OrderCreatedIntegrationEvent(Guid OrderId, ClientId, CargoType, WeightKg, Origin, Destination, CreatedAt)` (`EventBus/IntegrationEvents/OrderCreatedIntegrationEvent.cs:1`) flat DTO to `RabbitMQ` `logistics-rabbitmq:5672` (`docker-compose.yml:57`, `RabbitMqSettings.cs:9` `Host localhost User/Password required` `ValidateOnStart`)
2. `IntegrationService` `OrderCreatedConsumer.cs:1` `IConsumer<OrderCreatedIntegrationEvent>` `Consume` logs `OrderId`/`ClientId`, calls `IRpaClient.SubmitCustomsAsync(event)` (`Clients/RpaClient.cs:1` `HttpClient PostAsJsonAsync api/customs/declarations` to `Rpa:BaseUrl localhost:5004` with `WaitAndRetry 3×2^retry` + `CircuitBreaker 5/30s` via `Microsoft.Extensions.Http.Polly` `Program.cs:18`) — stub returns `true`, `4.8` real `POST`
3. On `true`, `IOrderStatusClient.MarkCustomsAsync(event.OrderId)` (`Clients/OrderStatusClient.cs:1` `HttpClient Put api/orders/{id}/status {newStatus=Customs}` to `OrderService:BaseUrl localhost:5002` with `RpaBot JWT` `HS256` same `Secret/Issuer/Audience` `5m` expiry via `GenerateRpaBotToken()` + same `Polly` policies `Program.cs:27`)
4. `OrderService` `UpdateStatusAsync` validates `IsRpaBot && Customs` → `OrderStatusTransitions.Ensure(InTransit→Customs)` → `ExecuteUpdate` → `200`, otherwise `403`. `MassTransit Retry` handles transient `HttpRequestException`.
5. Tests: `OrderService.Tests.Unit` `Mock IPublishEndpoint Verify Publish<OrderCreatedIntegrationEvent> Once` (`OrderServiceApplicationTests.cs:60` `CreateAsync_ShouldPublishIntegrationEvent`), `IntegrationService.Tests.Integration` `MassTransit.TestFramework InMemoryTestHarness` `Publish OrderCreatedIntegrationEvent → RpaClient mock true → OrderStatusClient MarkCustoms Once` + `false → Never` + `Throws → Retry` (`OrderCreatedConsumerTests.cs:1` `3 tests`), plus `Testcontainers.RabbitMq 4.2.0` available.

### Flow 6 — TrackingService Real-time Coordinates (Implemented — Stage 5)

**Goal:** `ClientPolicy` `PUT /api/tracking/{orderId}` stores `GeoCoordinate` in `Redis` `tracking:{orderId} TTL 5m`, `GET` retrieves via `Cache-Aside`.

1. `Client → YarpGateway /api/tracking/{id} PUT` → `tracking-cluster :5004` (`YarpGateway/appsettings.json:43` `tracking-route /api/tracking/{**catch-all}` no `AuthorizationPolicy` → `TrackingService` enforces `ClientPolicy` on `PUT`)
2. `TrackingService.API` `TrackingController.cs:22` `[Authorize] PUT {orderId} [ClientPolicy]` validates `UpdateTrackingRequest` (`DTOs/UpdateTrackingRequest.cs:5` `[Range -90/90, -180/180]`), calls `ITrackingService.UpdateAsync` (`Application/Services/TrackingService.cs:10` `TrackingAppService` `GetAsync → if null Create(lat,lon) else Update()` `GeoCoordinate.Validate`) → `ITrackingRepository.SetAsync` (`Infrastructure/Redis/RedisTrackingRepository.cs:10` `ICacheService SetAsync CacheKeys.Tracking(id) TTL 5m`) → `200 TrackingResponse` (also logs `Tracking updated {OrderId} lat/lon`)
3. `GET /api/tracking/{id}` `TrackingController.cs:42` → `ITrackingService.GetAsync → repo.GetAsync → ICacheService GetAsync<TrackingEntry>` → `200` or `404` (second `GET` hits `Redis` cache, no DB) — `Tracking.Tests.Integration` verifies `PUT 200` then `GET 200` second `GET` hits cache (`TrackingApiIntegrationTests.cs:118`)
4. `Redis` `logistics-cache:6379` `requirepass` `healthcheck CMD-SHELL redis-cli -a $$REDIS_PASSWORD ping` (`docker-compose.yml:36`, `ConnectionStrings__Redis logistics-cache:6379,password=...`) `IDistributedCache` `YOUR_` → `InMemory` fallback in `Testing`

### Flow 7 — OrderService Cache-Aside (Implemented — Stage 5.7)

**Goal:** `OrderService` `GetById/List` hit `Redis` before `PostgreSQL`, invalidated on `Create/UpdateStatus`.

1. `OrderService.Application` `OrderService.cs:55` `GetByIdAsync` `GetAsync<OrderResponse>(order:{id})` before `repo.GetByIdAsync` with ownership check (`IsManager` or `ClientId` match) → on `miss` `Map` + `SetAsync OrderTtl 2m`; `ListAsync:79` `GetAsync<IReadOnlyList>(order:list:{client|all})` → `miss` → `ListByClient/ListAll` + `SetAsync OrderListTtl 2m`
2. `CreateAsync:53` after `SaveChanges` `SetAsync Order(id)` + `RemoveAsync OrderList(client) + OrderListAll`; `UpdateStatusAsync:135` `Remove+Set Order(id)` + `Remove OrderList*` → ensures no stale `404` hide-existence cache
3. `BuildingBlocks.Caching` `IDistributedCache` `StackExchangeRedisCache` when `ConnectionStrings:Redis` present and not `YOUR_`, else `DistributedMemoryCache` (`CacheServiceCollectionExtensions.cs:10`); `OrderService.API Program.cs:12` `AddCaching()` or `AddInMemoryCaching()` when `Testing` + `YOUR_` guard
4. Tests: `OrderService.Tests.Unit` `Mock<ICacheService>` `GetAsync null` → `Publish` verify still `42 passed`, `OrderService.Tests.Integration` `10 passed` with `InMemory` fallback (no real Redis), `docker exec redis --scan --pattern "order:*"` shows `TTL ~120`

---

## 5. Dependencies

### Solution Graph

```
SmartFreightLogistics.slnx (19 projects)
├── src/BuildingBlocks/Logging (BuildingBlocks.Logging.csproj)
│   └── Serilog.AspNetCore 10.0.0, Serilog.Sinks.Console 6.1.1
├── src/BuildingBlocks/Caching (BuildingBlocks.Caching.csproj) ──FrameworkRef AspNetCore.App
│   └── StackExchange.Redis 2.8.37 + Microsoft.Extensions.Caching.StackExchangeRedis 9.0.8
│   └── CacheKeys (TrackingTtl 5m / Order 2m) + ICacheService + RedisCacheService
├── src/BuildingBlocks/EventBus (BuildingBlocks.EventBus.csproj) ──FrameworkRef AspNetCore.App
│   └── MassTransit 8.3.5, MassTransit.RabbitMQ 8.3.5
├── src/Gateways/YarpGateway (YarpGateway.csproj) ──ProjectRef──► BuildingBlocks.Logging
│   └── Yarp.ReverseProxy 2.3.0 ──Routes identity-route / rpa-status-route / tracking-route / order-route
├── src/Services/IdentityService (IdentityService.csproj) ──ProjectRef──► BuildingBlocks.Logging
│   └── Microsoft.AspNetCore.Authentication.JwtBearer 10.0.11 + OpenApi + Npgsql 10.0.3
├── src/Services/OrderService/
│   ├── OrderService.Domain ──Events IDomainEvent OrderCreatedDomainEvent + OrderStatusTransitions
│   ├── OrderService.Application ──ProjectRef──► Caching + EventBus, Domain + ICacheService Cache-Aside
│   ├── OrderService.Infrastructure ──ProjectRef──► Domain (OrderDbContext sfl_order_db)
│   └── OrderService.API ──ProjectRef──► Logging, Caching, EventBus, Application, Infrastructure
│       └── JwtBearer 10.0.11 + MassTransit + AddCaching (YOUR_ → InMemory)
├── src/Services/TrackingService/
│   ├── TrackingService.Domain ──TrackingEntry + GeoCoordinate [JsonConstructor]
│   ├── TrackingService.Infrastructure ──ProjectRef──► Domain + Caching ── ITrackingRepository + RedisTrackingRepository
│   ├── TrackingService.Application ──ProjectRef──► Domain + Infrastructure ── DTOs + TrackingAppService
│   └── TrackingService.API ──ProjectRef──► Logging, Caching, Application, Infrastructure
│       └── JwtBearer 10.0.11 + OpenApi + AddCaching + AddTrackingAuth :5004
└── src/Services/IntegrationService (IntegrationService.csproj) Sdk.Web No DB
    ├── ProjectRef──► Logging, EventBus
    └── MassTransit, JwtBearer 10.0.11, Http.Polly 10.0.0, OpenApi 10.0.11
        └── Clients RpaClient/OrderStatusClient (HttpClient + Polly + RpaBot JWT)
        └── Consumers OrderCreatedConsumer
── tests/ (MTP xUnit v3)
    ├── OrderService.Tests.Unit (42)
    ├── TrackingService.Tests.Unit (10)
    ├── OrderService.Tests.Integration (10) ── Testcontainers.PostgreSql + InMemory
    ├── TrackingService.Tests.Integration (5) ── Testcontainers.Redis + WebApplicationFactory
    └── IntegrationService.Tests.Integration (3) ── MassTransit TestFramework
```

*Verified via `YarpGateway.csproj:14`, `IdentityService.csproj:21`, `OrderService.API.csproj:14`, `OrderService.Domain.csproj:1`, plus Cypher `IMPORTS` edges.*

### Infrastructure Dependencies

| From | To | Via |
|------|----|-----|
| `YarpGateway` | `IdentityService:5001`, `OrderService.API:5002`, `TrackingService:5004` | YARP `ReverseProxy.Clusters.Destinations.Address` (`YarpGateway/appsettings.json:41`) + `tracking-route /api/tracking/{**catch-all}` → `tracking-cluster :5004` + `rpa-status-route PUT /api/orders/{id}/status` without `AuthorizationPolicy` |
| `IdentityService` | `logistics-db:5432/sfl_identity_db` | `Npgsql` + `IdentityDbContext` (`IdentityService/Program.cs:25`) |
| `OrderService.API` | `logistics-rabbitmq:5672` | `MassTransit RabbitMQ` `IPublishEndpoint.Publish(OrderCreatedIntegrationEvent)` (`OrderService.cs:40`, `EventBus ServiceCollectionExtensions.cs:16`) |
| `OrderService.API` | `logistics-cache:6379` | `IDistributedCache StackExchangeRedis` `Cache-Aside` `order:{id}/order:list:* TTL 2m` via `ICacheService` (`OrderService.cs:55`, `Caching CacheServiceCollectionExtensions.cs:10` `YOUR_` → `InMemory`) |
| `OrderService.Infrastructure` | `logistics-db:5432/sfl_order_db` | `Npgsql` + `OrderDbContext` `sfl_order_db` `Migrate/EnsureCreated` (`OrderService.API/Program.cs:28`) |
| `IntegrationService` | `logistics-rabbitmq:5672` | `MassTransit IConsumer<OrderCreatedIntegrationEvent>` `OrderCreatedConsumer` (`Program.cs:11` `AddConsumer`) |
| `IntegrationService` | `OrderService:5002` | `HttpClient Put api/orders/{id}/status Customs` with `RpaBot JWT` + `Polly Retry/CircuitBreaker` (`OrderStatusClient.cs:1`) |
| `IntegrationService` | `mock-rpa:5004` | `HttpClient Post api/customs/declarations` with `Polly` (`RpaClient.cs:1`) |
| `TrackingService` | `logistics-cache:6379` | `StackExchange.Redis` `ICacheService` `tracking:{orderId} TTL 5m` via `RedisTrackingRepository` (`TrackingService.Infrastructure/Redis/RedisTrackingRepository.cs:10`) |
| `TrackingService` | `logistics-rabbitmq:5672` (future) | `ConnectionStrings__Redis` + `RabbitMq__Host` env `docker-compose.yml:121` (for future `TrackingUpdatedIntegrationEvent`) |
| All services | BuildingBlocks.Logging | `SerilogExtensions.AddSharedLogging/UseSharedLogging` |
| All services (except Gateway) | BuildingBlocks.Caching | `CacheServiceCollectionExtensions.AddCaching/AddInMemoryCaching` |
| All services (with bus) | BuildingBlocks.EventBus | `RabbitMqSettings` `ValidateOnStart` `MassTransit` |

---

## 6. Architecture Diagram

```mermaid
graph TB
  subgraph ClientLayer [Client]
    Client
  end

  subgraph Gateway [Gateways]
    YarpGateway["YarpGateway<br/>YARP ReverseProxy 2.3.0<br/>:5000<br/>MapReverseProxy()<br/>Client/LogisticsManager/RpaBot policies"]
  end

  subgraph BuildingBlocks [BuildingBlocks - Cross-Cutting]
    Logging["BuildingBlocks.Logging<br/>Serilog + CorrelationIdMiddleware<br/>X-Correlation-ID / LogContext<br/>SerilogExtensions.cs"]
    Caching["BuildingBlocks.Caching<br/>StackExchange.Redis 2.8.37<br/>IDistributedCache<br/>ICacheService<br/>CacheKeys 5m/2m"]
  end

  subgraph Services [Services]
    IdentityService["IdentityService<br/>.NET 10 Web API<br/>EF Core + Npgsql 10.0.3<br/>User{Email,Role}<br/>PasswordHasher PBKDF2<br/>JwtTokenGenerator HS256<br/>AuthController register/login/me<br/>:5001"]
    OrderAPI["OrderService.API<br/>OrdersController CRUD<br/>Cache-Aside Redis<br/>JwtBearer 10.0.11<br/>IPublishEndpoint<br/>:5002"]
    OrderApp["OrderService.Application<br/>IOrderService<br/>Create/Get/List/UpdateStatus<br/>OrderStatusTransitions<br/>ICacheService<br/>OrderCreatedIntegrationMapper"]
    OrderDomain["OrderService.Domain<br/>Order/CargoDetails/StatusHistory<br/>State Machine<br/>OrderCreatedDomainEvent"]
    OrderInfra["OrderService.Infrastructure<br/>OrderDbContext sfl_order_db<br/>OrderSeeder IsDevelopment"]
    TrackingAPI["TrackingService.API<br/>TrackingController PUT/GET/health<br/>JwtBearer 10.0.11<br/>AddCaching<br/>:5004"]
    TrackingApp["TrackingService.Application<br/>TrackingAppService<br/>Get/Update<br/>Cache-Aside 5m"]
    TrackingInfra["TrackingService.Infrastructure<br/>RedisTrackingRepository<br/>tracking:{orderId}<br/>ICacheService"]
    TrackingDomain["TrackingService.Domain<br/>TrackingEntry<br/>GeoCoordinate<br/>[JsonConstructor]"]
    IntegrationService["IntegrationService<br/>.NET 10 No DB<br/>MassTransit Consumer<br/>RpaClient + OrderStatusClient<br/>Polly Retry/CircuitBreaker<br/>RpaBot JWT<br/>:5003"]
    EventBus["BuildingBlocks.EventBus<br/>RabbitMqSettings<br/>OrderCreatedIntegrationEvent<br/>MassTransit 8.3.5"]
  end

  subgraph Tests [Tests — MTP 70]
    UnitTests["OrderService.Tests.Unit<br/>42 tests<br/>Publish + ICacheService<br/>Moq"]
    TrackingUnit["TrackingService.Tests.Unit<br/>10 tests<br/>Geo/Entry/TTL<br/>Moq Repo"]
    IntegrationTests["OrderService.Tests.Integration<br/>Testcontainers.PostgreSql<br/>10 tests<br/>InMemory loopback + cache"]
    TrackingIntegration["TrackingService.Tests.Integration<br/>Testcontainers.Redis<br/>5 tests<br/>WebApplicationFactory + Redis"]
    IntegrationServiceTests["IntegrationService.Tests.Integration<br/>MassTransit.TestFramework<br/>3 tests<br/>Rpa/OrderStatus mocks"]
  end

  subgraph Infra [Infrastructure — docker-compose.yml]
    Postgres["PostgreSQL 16-alpine<br/>logistics-postgres-db<br/>sfl_identity_db<br/>sfl_order_db<br/>:5432"]
    Redis["Redis 7-alpine<br/>logistics-redis-cache<br/>:6379<br/>requirepass<br/>healthcheck"]
    RabbitMQ["RabbitMQ 3-management-alpine<br/>logistics-rabbitmq<br/>5672/15672<br/>rabbitmq_data<br/>healthy"]
    PgAdmin["pgAdmin4<br/>logistics-pgadmin<br/>:5050"]
  end

  Client -->|"/api/auth/{**catch-all}"| YarpGateway
  Client -->|"/api/tracking/{**catch-all}"| YarpGateway
  Client -->|"/api/orders/{**catch-all}"| YarpGateway
  Client -->|"/api/orders/{id}/status PUT<br/>rpa-status-route"| YarpGateway

  YarpGateway -->|"identity-cluster → :5001"| IdentityService
  YarpGateway -->|"tracking-cluster → :5004"| TrackingAPI
  YarpGateway -->|"order-cluster → :5002<br/>ClientPolicy + RpaBot"| OrderAPI
  YarpGateway -->|"rpa-status-route → :5002"| OrderAPI

  YarpGateway -. "AddSharedLogging()<br/>CorrelationIdMiddleware" .-> Logging
  IdentityService -. "AddSharedLogging()" .-> Logging
  OrderAPI -. "AddSharedLogging() + AddCaching()" .-> Logging
  TrackingAPI -. "AddSharedLogging() + AddCaching()" .-> Caching
  OrderAPI -. "ICacheService<br/>order:{id} 2m<br/>order:list:*" .-> Caching
  TrackingAPI --> TrackingApp --> TrackingDomain
  TrackingApp --> TrackingInfra --> Caching
  Caching --> Redis
  IntegrationService -. "AddSharedLogging()" .-> Logging

  OrderAPI --> OrderApp --> OrderDomain
  OrderApp --> OrderInfra
  OrderApp -- "Publish OrderCreatedIntegrationEvent<br/>IPublishEndpoint" --> EventBus
  EventBus -- "RabbitMQ logistics-rabbitmq:5672" --> RabbitMQ
  RabbitMQ -- "Consume OrderCreatedIntegrationEvent" --> IntegrationService
  IntegrationService -- "POST api/customs/declarations<br/>Polly" --> RabbitMQ
  IntegrationService -- "PUT api/orders/{id}/status Customs<br/>RpaBot JWT + Polly" --> OrderAPI

  OrderAPI -. "MTP"| UnitTests
  TrackingApp -. "MTP"| TrackingUnit
  OrderAPI -. "Testcontainers"| IntegrationTests
  TrackingAPI -. "Testcontainers.Redis"| TrackingIntegration
  IntegrationService -. "InMemoryTestHarness"| IntegrationServiceTests

  IdentityService -->|"EF Core Npgsql<br/>DatabaseSettings:Password<br/>IX_Users_Email"| Postgres
  OrderInfra -->|"EF Core Npgsql<br/>Migrate/EnsureCreated<br/>OrderSeeder"| Postgres

  Postgres --- PgAdmin

  classDef infra fill:#e8f5e9,stroke:#2e7d32;
  class Postgres,Redis,RabbitMQ,PgAdmin infra

  classDef gateway fill:#e3f2fd,stroke:#1565c0;
  class YarpGateway gateway

  classDef building fill:#fce4ec,stroke:#880e4f;
  class Logging,Caching building

  classDef tests fill:#f3e5f5,stroke:#6a1b9a;
  class UnitTests,TrackingUnit,IntegrationTests,TrackingIntegration,IntegrationServiceTests tests
```

*Implemented nodes (solid) are `Order` aggregate, `OrdersController`, `Jwt` via `user-secrets`; `Tests` via `MTP`. Future edges dashed.*

**Alternative rendering:** If your viewer prefers horizontal layout, swap `graph TB` → `graph LR`; structure is unchanged.

---

## 7. Roadmap (7 Stages — `docs/Smart Freight Logistics main plan.md:1`)

| Stage | Focus | Tech | Status |
|-------|-------|------|--------|
| 1 | Foundation | .NET 10, Docker Compose, YARP, Serilog | ✅ Done — solution, `docker-compose.yml:1`, `YarpGateway/Program.cs:1`, `BuildingBlocks.Logging` |
| 2 | Identity & Auth | Web API, JwtBearer, EF Core, JWT Policies | ✅ Done — `User.cs:1`, `IdentityDbContext.cs:1`, `AuthController.cs:1` `register/login/me` `201/200/401`, `PasswordHasher` PBKDF2, `JwtTokenGenerator` HS256, `YarpGateway` `Client/LogisticsManager/RpaBot` policies `5000`, `OrderService` `AddJwtAuthentication` `5002` |
| 3 | OrderService Core | EF Core (PostgreSQL), xUnit v3, FluentAssertions, Testcontainers, MTP | ✅ Done — `Order.cs:1` `CargoDetails/StatusHistory` + `OrderStatusTransitions` state machine, `OrderDbContext` `sfl_order_db` + `OrderSeeder` `IsDevelopment` `3` orders, `OrdersController` CRUD `201/200/404/403/409`, `42` unit + `10` integration `Testcontainers` `MTP` (now `InMemory` for RabbitMq in tests), `user-secrets` dev `JWT` |
| 4 | Event-Driven & RPA | MassTransit 8.3.5, RabbitMQ 3-management-alpine, `OrderCreatedEvent`, `RPA_Bot_Policy`, `IntegrationService` | ✅ Done — `OrderCreatedDomainEvent` `OrderCreatedIntegrationEvent` flat DTO `ToIntegrationEvent` mapper, `BuildingBlocks.EventBus` `RabbitMqSettings` `ValidateOnStart` `UseMessageRetry 3×1s`, `OrderService` `IPublishEndpoint` `CreateAsync` publish + `InMemory` when `Testing`, `IntegrationService` `No DB` `OrderCreatedConsumer` → `RpaClient` `POST mock-rpa:5004` + `OrderStatusClient` `PUT Customs` with `RpaBot JWT` `Polly 3×2^retry` `CircuitBreaker 5/30s` `:5003`, `Yarp rpa-status-route` `PUT /api/orders/{id}/status`, `42` unit + `10` Order + `3` IntegrationService `InMemoryTestHarness` `55` total |
| 5 | Tracking & Caching | Redis 7, StackExchange.Redis 2.8.37, `BuildingBlocks.Caching` `CacheKeys 5m/2m`, `TrackingService` `Domain/Infrastructure/Application/API` (`TrackingEntry/GeoCoordinate`, `RedisTrackingRepository` `tracking:{id}`, `TrackingAppService`, `TrackingController PUT/GET/health`), `OrderService Cache-Aside` `order:{id}/order:list:* TTL 2m` via `ICacheService`, `YARP tracking-route` → `:5004`, `70 tests` (`42+10+10+5+3`) `Testcontainers.Redis` | ✅ Done — `src/Services/TrackingService/` `4 projects` + `BuildingBlocks.Caching` + `YarpGateway tracking-route` `OrderService Cache-Aside` `70 tests` |
| 6 | CQRS Evolution | MediatR, Commands/Queries, FluentValidation, Redis/Dapper read models | ⏳ Planned — refactor of Order/Tracking services (`docs/main plan.md:56`) |
| 7 | Prod Readiness | OpenTelemetry, HealthChecks, GitHub Actions CI | ⏳ Planned — `docs/main plan.md:65` |

---

## 8. Known Gaps & Next Steps

1. **Graph refreshed** — re-indexed `2026-09-07T20:07:00Z` `891 nodes 1736 edges 35 clusters 35 flows` (was `699/1331` at `2026-09-05` after Stage 4, `558/1103` at Stage 3). `TrackingService` `4 projects` + `BuildingBlocks.Caching` + `YARP tracking-route` + `Order Cache-Aside` now indexed. `analyze --pdg` can populate `explain` taint flows if needed.
2. **Controllers implemented** — `IdentityService/Controllers/AuthController.cs:1` `register/login/me` + `OrderService.API/Controllers/OrdersController.cs:1` `POST/GET/PUT` `ClientPolicy` + `RpaBot Customs` (`OrderService.cs:72` `IsRpaBot`) + `TrackingService.API/Controllers/TrackingController.cs:10` `PUT {orderId} [ClientPolicy]` `GET {orderId}` `health AllowAnonymous` (`TrackingAppService` `Update/Get` via `RedisTrackingRepository` `tracking:{id} TTL 5m`).
3. **JWT wiring done** — `JwtBearer 10.0.11` `AddAuthentication().AddJwtBearer()` in `IdentityService/Services/ServiceCollectionExtensions.cs:34`, `YarpGateway/Extensions/AuthExtension.cs:19`, `OrderService.API/Extensions/AuthExtensions.cs:17`, `TrackingService.API/Extensions/AuthExtensions.cs:10` `AddTrackingAuth` same `Client/LogisticsManager/RpaBot` policies at `5000`/`5001`/`5002`/`5004` + `IntegrationService` `OrderStatusClient` `RpaBot JWT`.
4. **OrderService + EventBus + Caching implemented** — `Order/CargoDetails/StatusHistory` `OrderStatusTransitions` + `DomainEvents` `OrderCreatedIntegrationEvent` flat DTO, `BuildingBlocks.EventBus` `ValidateOnStart` `Retry 3×1s`, `OrderService` `CreateAsync` `IPublishEndpoint` + `Cache-Aside` `order:{id}/order:list* TTL 2m` via `ICacheService` (`YOUR_` → `InMemory` in `Testing`), `42` unit + `10` integration `52` + `TrackingService` `10` unit + `5` integration `Testcontainers.Redis` `15` + `IntegrationService` `3` `InMemoryTestHarness` `70` total.
5. **Secrets management** — `DatabaseSettings:Password` via `user-secrets` + `JwtSettings:Secret` via `user-secrets` (dev) and `docker/.env JWT_SECRET` (prod) + `RabbitMq:User/Password` via `user-secrets` (dev) and `docker/.env RABBITMQ_*` (prod) + `ConnectionStrings:Redis` via `user-secrets localhost:6379,password=...` (dev) and `docker/.env REDIS_PASSWORD` → `ConnectionStrings__Redis logistics-cache:6379,password=...` (prod) + `appsettings.json` `YOUR_SECRET_JWT_KEY` / `YOUR_SECRET_PASSWORD` / `YOUR_REDIS_PASSWORD` fail-fast (`YOUR_` → `DistributedMemoryCache`); `appsettings.Development.json` empty; tests via `Testcontainers` ephemeral `PostgreSql/Redis` + `InMemory` (no `docker/.env` parsing, `Environment.SetEnvironmentVariable` before factory for `Tracking`).
6. **Health/Observability** — Serilog correlation done, `IntegrationService /health` + `TrackingService /health` `200 {status:Healthy}`, `YarpGateway /api/tracking/health` via `tracking-cluster` `200` (downstream `5004` must be running, else `502` — `yarpGateway.http` `PUT/GET tracking` smoke); `HealthChecks` + `OpenTelemetry` (Stage 7) not yet present.

---

*Generated: 2026-09-07T20:07:00Z. Index: `smart-freight-logistics@features/init-tracking-service` `891 nodes 1736 edges 35 clusters 35 flows`. To refresh after code changes: `node .gitnexus/run.cjs analyze --index-only` (auto-selects runner, no global install needed).*
