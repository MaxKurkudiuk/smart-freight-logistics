# Smart Freight Logistics — Architecture Documentation

> Generated from GitNexus knowledge graph (`smart-freight-logistics`, commit `e9f79df`, indexed `2026-09-05T14:35:19Z`) + manual source verification. The graph now reports `23` execution flows and `21` functional areas after Stage 3 (OrderService Clean Architecture, JWT, YARP, tests, seed). This document supplements the graph with deterministic code reads, each cited as `file:line`.

---

## 1. Overview

Smart Freight Logistics is a **.NET 10 microservices** system for freight order management, tracking, and RPA customs integration. Architecture follows **YARP API Gateway + per-service PostgreSQL databases + Redis cache + shared Serilog building block**.

| Concern | Choice | Evidence |
|---------|--------|----------|
| Runtime | .NET 10 (`net10.0`) | `src/Gateways/YarpGateway/YarpGateway.csproj:4`, `src/Services/IdentityService/IdentityService.csproj:4` |
| Gateway | YARP Reverse Proxy 2.3.0 | `src/Gateways/YarpGateway/YarpGateway.csproj:10`, `src/Gateways/YarpGateway/Program.cs:9` |
| Logging | Serilog.AspNetCore 10.0.0 + Serilog.Sinks.Console 6.1.1 | `src/BuildingBlocks/Logging/BuildingBlocks.Logging.csproj:10` |
| Identity DB | EF Core 10.0.11 + Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3, PostgreSQL 16 | `src/Services/IdentityService/IdentityService.csproj:15`, `docker/docker-compose.yml:6` |
| Auth | Microsoft.AspNetCore.Authentication.JwtBearer 10.0.11 (JWT Policies planned) | `src/Services/IdentityService/IdentityService.csproj:11` |
| Cache | Redis 7 (StackExchange.Redis planned) | `docker/docker-compose.yml:38` |
| Infra | Docker Compose (postgres, redis, pgAdmin) on `logistics-network` bridge | `docker/docker-compose.yml:53` |

**Current maturity:** Stages 1 (Foundation), 2 (Identity + JWT + YARP policies) and 3 (OrderService Clean Architecture, CRUD, tests, seed) are implemented. Stages 4-7 (Event-Driven, Tracking, CQRS, Prod Readiness) remain planned per `docs/Smart Freight Logistics main plan.md:3`.

**Entry point:** All clients hit the gateway; gateway routes `/api/auth/{**catch-all}` → IdentityService (`:5001`) and `/api/orders/{**catch-all}` → OrderService.API (`:5002`) — `src/Gateways/YarpGateway/appsettings.json:22`.

---

## 2. Codebase Stats (Knowledge Graph)

Source: `gitnexus://repo/smart-freight-logistics/context`, `gitnexus://repo/smart-freight-logistics/clusters`, `gitnexus://repo/smart-freight-logistics/processes`, `gitnexus://repos`, and `cypher MATCH (n) RETURN labels(n), n.name`.

| Metric | Value | Notes |
|--------|-------|-------|
| Indexed at | `2026-09-05T14:35:19Z` | Runner `gitnexus@1.6.11`, `node v24.16.0 win32` |
| Commit | `e9f79df` | `features/integration-tests-db-via-testcontainers` |
| Files indexed | 82 | `status: matches all 82 covered file(s)` |
| Symbols | 558 | `nodes: 558` — includes `Order`, `CargoDetails`, `StatusHistory`, `OrderStatusTransitions`, `OrdersController`, `JwtTokenGenerator`, `PasswordHasher`, `YarpGateway` + tests |
| Relationships | 1103 | `edges: 1103` |
| Processes (execution flows) | 23 | `flows: 23` — analyzer now finds `STEP_IN_PROCESS` chains for `Order` CRUD, `Auth` `register/login/me`, `YARP` routing |
| Functional areas (Leiden clusters) | 21 | `clusters: 21` — `Community` nodes detected (Auth, Order, Gateway, Logging) |
| Solution projects | 9 | `SmartFreightLogistics.slnx:1` — 7 src + 2 tests (`OrderService.Tests.Unit`, `Integration`) |

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
- **Routing (YARP `ReverseProxy`):** `src/Gateways/YarpGateway/appsettings.json:22`
  - `identity-route: ClusterId=identity-cluster, Match Path=/api/auth/{**catch-all}, Destination=http://localhost:5001` — `appsettings.json:24`
  - `order-route: ClusterId=order-cluster, Match Path=/api/orders/{**catch-all}, Destination=http://localhost:5002` — `appsettings.json:30`
- **Pipeline:** `builder.AddSharedLogging()` → `AddReverseProxy().LoadFromConfig(ReverseProxy)` → `app.UseSharedLogging()` → `UseRouting()` → `MapReverseProxy()` — `Program.cs:6`
- **Future (per roadmap):** JWT policy enforcement (`ClientPolicy`, `LogisticsManagerPolicy`, `RPA_Bot_Policy`) at gateway — `docs/Smart Freight Logistics main plan.md:20` — not yet wired (`Program.cs` has no `UseAuthentication`).

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

### 3.4 Services.OrderService — Order Management (Clean Architecture Stub)
- **Paths:**
  - `src/Services/OrderService/OrderService.API/` (`OrderService.API.csproj:1`) — Web host, refs Logging + Application + Infrastructure — `OrderService.API.csproj:14`
  - `src/Services/OrderService/OrderService.Application/` (`OrderService.Application.csproj:1`) — stub `Class1.cs:1`
  - `src/Services/OrderService/OrderService.Domain/` (`OrderService.Domain.csproj:1`) — stub `Class1.cs:1`
  - `src/Services/OrderService/OrderService.Infrastructure/` (`OrderService.Infrastructure.csproj:1`) — stub `Class1.cs:1`
- **Entrypoint:** `src/Services/OrderService/OrderService.API/Program.cs:1` — `AddSharedLogging()` → `AddControllers()` → `AddOpenApi()` → `UseHttpsRedirection()` → `UseSharedLogging()` → `UseRouting()` → `MapControllers()` → `MapOpenApi()` — `Program.cs:3` — minimal scaffold.
- **Planned domain (per `docs/main plan.md:31`):** `Order`, `CargoDetails`, `StatusHistory` entities; base CRUD; `xUnit + FluentAssertions + Testcontainers.PostgreSql` tests — Stage 3. Later: `MassTransit + RabbitMQ OrderCreatedEvent → IntegrationService/RPA Bridge` (Stage 4), Redis `TrackingService` cache-aside (Stage 5), `MediatR CQRS` refactor (Stage 6), HealthChecks + OpenTelemetry (Stage 7).

### 3.5 Infrastructure — Docker Compose
- **Path:** `docker/docker-compose.yml:1`, `docker/postgres/init-scripts/init.sql:1`, `docker/.env.example`
- **Services (all on `logistics-network` bridge — `docker-compose.yml:53`):**
  - `logistics-db` — `postgres:16-alpine`, `logistics-postgres-db`, `5432:5432`, env `POSTGRES_USER/PASSWORD`, `POSTGRES_MULTIPLE_DATABASES="sfl_identity_db,sfl_order_db"`, volumes `postgres_data` + `./postgres/init-scripts:/docker-entrypoint-initdb.d` — `docker-compose.yml:5`
  - `pgadmin` — `dpage/pgadmin4`, `logistics-pgadmin`, `5050:80`, depends_on `logistics-db` — `docker-compose.yml:22`
  - `logistics-cache` — `redis:7-alpine`, `logistics-redis-cache`, `6379:6379`, `redis-server --requirepass ${REDIS_PASSWORD}`, volume `redis_data` — `docker-compose.yml:37`
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

### Flow 4 — Order CRUD (Implemented — Stage 3)

**Goal (per `docs/main plan.md:28`):** `ClientPolicy` user creates `Order` with `CargoDetails`, reads via ownership, updates `StatusHistory` via state machine.

*Implemented sequence (replaces `Class1.cs:1` stubs):*

1. `Client → YarpGateway /api/orders POST` → `order-cluster :5002` (`YarpGateway/appsettings.json:30`, `YarpGateway/Program.cs:26` `MapReverseProxy`, `AuthExtension.cs:38` `ClientPolicy` requires `Client` role)
2. `OrderService.API` `OrdersController.cs:36` `[Authorize(Policy="ClientPolicy")] POST` validates `CreateOrderRequest` (`DTOs/CreateOrderRequest.cs:5` `sealed record` `CargoType/WeightKg/Origin/Destination`), maps to `OrderService.Application` `IOrderService.CreateAsync` (`Services/OrderService.cs:11`) — checks `Origin!=Destination`, creates `CargoDetails` owned VO, calls `Order.Create(clientId,cargo)` (`Domain/Entities/Order.cs:20` validates `Weight>0/Origin/Destination`, adds `StatusHistory` `Created`)
3. `OrderService.Application` persists via `IOrderRepository.AddAsync` + `SaveChanges` (`Infrastructure/Repositories/OrderRepository.cs:32`) → `OrderDbContext.cs:15` `OwnsOne(Cargo)`, `HasIndex(ClientId/Status)`, `DbSet<Order>` to `sfl_order_db` (`docker-compose.yml:11`, `init-scripts`), seeded in `IsDevelopment` via `OrderSeeder.cs:13` `3` dev orders for `dev.client 3333...` (`Program.cs:28` `if IsDevelopment SeedAsync`)
4. `GET /api/orders/{id}` `OrdersController.cs:58` and `GET /api/orders` `OrdersController.cs:71` enforce ownership: `Client` sees only `WHERE ClientId==sub` (`OrderService.cs:48` `ListByClientAsync`), `LogisticsManager` sees all (`ListAllAsync`), `GET` returns `404` for foreign `Client` (hide existence)
5. `PUT /api/orders/{id}/status` `OrdersController.cs:82` → `OrderService.cs:56` `UpdateStatusAsync` checks `IsManager` or owner, validates `OrderStatusTransitions.Ensure` (`OrderStatusTransitions.cs:10` `Created->{Confirmed,Cancelled}` etc.), then `TryUpdateStatusWithHistoryAsync` (`OrderRepository.cs:41` `ExecuteUpdate` `Status/UpdatedAt` + `StatusHistories.Add`) — `DomainException → 409`, `UnauthorizedAccessException → 403`. `Integration` tests via `Testcontainers.PostgreSql` `WebApplicationFactory` `CustomWebApplicationFactory.cs:13` + `JwtHelper.cs:1` `HS256` same `Secret/Isp/Aud` verify `201/200/404/403/409`.

---

## 5. Dependencies

### Solution Graph

```
SmartFreightLogistics.slnx
├── src/BuildingBlocks/Logging (BuildingBlocks.Logging.csproj)
│   └── Serilog.AspNetCore 10.0.0, Serilog.Sinks.Console 6.1.1
├── src/Gateways/YarpGateway (YarpGateway.csproj) ──ProjectRef──► BuildingBlocks.Logging
│   └── Yarp.ReverseProxy 2.3.0
├── src/Services/IdentityService (IdentityService.csproj) ──ProjectRef──► BuildingBlocks.Logging
│   └── Microsoft.AspNetCore.Authentication.JwtBearer 10.0.11
│   └── Microsoft.AspNetCore.OpenApi 10.0.11
│   └── Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3
│   └── Microsoft.EntityFrameworkCore.Design 10.0.11
└── src/Services/OrderService/
    ├── OrderService.Domain (no deps)
    ├── OrderService.Application (no deps, stub)
    ├── OrderService.Infrastructure (stub, will ref Domain)
    └── OrderService.API ──ProjectRef──► BuildingBlocks.Logging, OrderService.Application, OrderService.Infrastructure
        └── Microsoft.AspNetCore.OpenApi 10.0.11
```

*Verified via `YarpGateway.csproj:14`, `IdentityService.csproj:21`, `OrderService.API.csproj:14`, `OrderService.Domain.csproj:1`, plus Cypher `IMPORTS` edges.*

### Infrastructure Dependencies

| From | To | Via |
|------|----|-----|
| `YarpGateway` | `IdentityService:5001`, `OrderService.API:5002` | YARP `ReverseProxy.Clusters.Destinations.Address` (`YarpGateway/appsettings.json:41`) |
| `IdentityService` | `logistics-db:5432/sfl_identity_db` | `Npgsql` + `IdentityDbContext` (`IdentityService/Program.cs:25`) |
| `OrderService.Infrastructure` (future) | `logistics-db:5432/sfl_order_db` | EF Core (planned) |
| `TrackingService` (future) | `logistics-cache:6379` | `StackExchange.Redis` Cache-Aside (`docs/main plan.md:48`) |
| All services | BuildingBlocks.Logging | `SerilogExtensions.AddSharedLogging/UseSharedLogging` |

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
  end

  subgraph Services [Services]
    IdentityService["IdentityService<br/>.NET 10 Web API<br/>EF Core + Npgsql 10.0.3<br/>User{Email,Role}<br/>PasswordHasher PBKDF2<br/>JwtTokenGenerator HS256<br/>AuthController register/login/me<br/>:5001"]
    OrderAPI["OrderService.API<br/>OrdersController CRUD<br/>JwtBearer 10.0.11<br/>:5002"]
    OrderApp["OrderService.Application<br/>IOrderService<br/>Create/Get/List/UpdateStatus<br/>OrderStatusTransitions"]
    OrderDomain["OrderService.Domain<br/>Order/CargoDetails/StatusHistory<br/>State Machine"]
    OrderInfra["OrderService.Infrastructure<br/>OrderDbContext sfl_order_db<br/>OrderSeeder IsDevelopment"]
  end

  subgraph Tests [Tests — MTP]
    UnitTests["OrderService.Tests.Unit<br/>xUnit v3 MTP<br/>41 tests<br/>FluentAssertions/Moq"]
    IntegrationTests["OrderService.Tests.Integration<br/>Testcontainers.PostgreSql<br/>10 tests<br/>WebApplicationFactory/JwtHelper"]
  end

  subgraph Infra [Infrastructure — docker-compose.yml]
    Postgres["PostgreSQL 16-alpine<br/>logistics-postgres-db<br/>sfl_identity_db<br/>sfl_order_db<br/>:5432"]
    Redis["Redis 7-alpine<br/>logistics-redis-cache<br/>:6379<br/>requirepass"]
    PgAdmin["pgAdmin4<br/>logistics-pgadmin<br/>:5050"]
  end

  Client -->|"/api/auth/{**catch-all}"| YarpGateway
  Client -->|"/api/orders/{**catch-all}"| YarpGateway

  YarpGateway -->|"identity-cluster → :5001"| IdentityService
  YarpGateway -->|"order-cluster → :5002"| OrderAPI

  YarpGateway -. "AddSharedLogging()<br/>UseSharedLogging()<br/>CorrelationIdMiddleware" .-> Logging
  IdentityService -. "AddSharedLogging()" .-> Logging
  OrderAPI -. "AddSharedLogging()" .-> Logging

  OrderAPI --> OrderApp --> OrderDomain
  OrderApp --> OrderInfra
  OrderAPI -. "Tests MTP<br/>dotnet run"| UnitTests
  OrderAPI -. "Testcontainers<br/>WebApplicationFactory"| IntegrationTests

  IdentityService -->|"EF Core Npgsql<br/>NpgsqlConnectionStringBuilder<br/>DatabaseSettings:Password<br/>IX_Users_Email unique"| Postgres
  OrderInfra -->|"EF Core Npgsql<br/>Migrate/EnsureCreated<br/>OrderSeeder IsDevelopment"| Postgres

  OrderAPI -. "future: Cache-Aside<br/>StackExchange.Redis<br/>TrackingService" .-> Redis
  OrderInfra -. "future: Redis read models" .-> Redis

  Postgres --- PgAdmin

  classDef infra fill:#e8f5e9,stroke:#2e7d32;
  class Postgres,Redis,PgAdmin infra

  classDef gateway fill:#e3f2fd,stroke:#1565c0;
  class YarpGateway gateway

  classDef building fill:#fce4ec,stroke:#880e4f;
  class Logging building

  classDef tests fill:#f3e5f5,stroke:#6a1b9a;
  class UnitTests,IntegrationTests tests
```

*Implemented nodes (solid) are `Order` aggregate, `OrdersController`, `Jwt` via `user-secrets`; `Tests` via `MTP`. Future edges dashed.*

**Alternative rendering:** If your viewer prefers horizontal layout, swap `graph TB` → `graph LR`; structure is unchanged.

---

## 7. Roadmap (7 Stages — `docs/Smart Freight Logistics main plan.md:1`)

| Stage | Focus | Tech | Status |
|-------|-------|------|--------|
| 1 | Foundation | .NET 10, Docker Compose, YARP, Serilog | ✅ Done — solution, `docker-compose.yml:1`, `YarpGateway/Program.cs:1`, `BuildingBlocks.Logging` |
| 2 | Identity & Auth | Web API, JwtBearer, EF Core, JWT Policies | ✅ Done — `User.cs:1`, `IdentityDbContext.cs:1`, `AuthController.cs:1` `register/login/me` `201/200/401`, `PasswordHasher` PBKDF2, `JwtTokenGenerator` HS256, `YarpGateway` `Client/LogisticsManager/RpaBot` policies `5000`, `OrderService` `AddJwtAuthentication` `5002` |
| 3 | OrderService Core | EF Core (PostgreSQL), xUnit v3, FluentAssertions, Testcontainers, MTP | ✅ Done — `Order.cs:1` `CargoDetails/StatusHistory` + `OrderStatusTransitions` state machine, `OrderDbContext` `sfl_order_db` + `OrderSeeder` `IsDevelopment` `3` orders, `OrdersController` CRUD `201/200/404/403/409`, `41` unit + `10` integration `Testcontainers` `MTP`, `user-secrets` dev `JWT` |
| 4 | Event-Driven & RPA | MassTransit, RabbitMQ, `OrderCreatedEvent`, `RPA_Bot_Policy` | ⏳ Planned — `docs/main plan.md:38` |
| 5 | Tracking & Caching | Redis, Cache-Aside, `TrackingService` real-time coordinates | ⏳ Planned — `docs/main plan.md:48`; infra `logistics-cache:6379` already in `docker-compose.yml:37` |
| 6 | CQRS Evolution | MediatR, Commands/Queries, FluentValidation, Redis/Dapper read models | ⏳ Planned — refactor of Order/Tracking services (`docs/main plan.md:56`) |
| 7 | Prod Readiness | OpenTelemetry, HealthChecks, GitHub Actions CI | ⏳ Planned — `docs/main plan.md:65` |

---

## 8. Known Gaps & Next Steps

1. **Graph refreshed** — re-indexed `2026-09-05` `558 nodes 1103 edges 21 clusters 23 flows` (was `0` at scaffold). No re-index needed until Stage 4; `analyze --pdg` can populate `explain` taint flows if needed.
2. **Controllers implemented** — `IdentityService/Controllers/AuthController.cs:1` `register/login/me` + `OrderService.API/Controllers/OrdersController.cs:1` `POST/GET/PUT` with `Authorize` `ClientPolicy` (`Program.cs:28` `UseAuthentication/UseAuthorization`, `AddJwtAuthentication` `ValidateIssuer/Audience/Lifetime/IssuerSigningKey`).
3. **JWT wiring done** — `JwtBearer 10.0.11` `AddAuthentication().AddJwtBearer()` in `IdentityService/Services/ServiceCollectionExtensions.cs:34`, `YarpGateway/Extensions/AuthExtension.cs:19`, `OrderService.API/Extensions/AuthExtensions.cs:17` with `Client/LogisticsManager/RpaBot` policies enforced at `5000`/`5001`/`5002`.
4. **OrderService implemented** — Clean Architecture `Order/CargoDetails/StatusHistory` `OrderStatusTransitions` state machine, `OrderDbContext` `sfl_order_db` `Migrate` + `OrderSeeder` `IsDevelopment`, `41` unit (`xUnit v3` `MTP`) + `10` integration (`Testcontainers.PostgreSql` `WebApplicationFactory` `JwtHelper`) `51` total.
5. **Secrets management** — `DatabaseSettings:Password` via `user-secrets` (`SecretPassword123!`) + `JwtSettings:Secret` via `user-secrets` `dev-only...` (dev) and `docker/.env JWT_SECRET Pcvgm2...` (prod), `appsettings.json` placeholder `YOUR_SECRET_JWT_KEY` fail-fast if `<32`; `appsettings.Development.json` now empty (no secret in repo).
6. **Health/Observability** — Serilog correlation done, HealthChecks and OpenTelemetry (Stage 7) not yet present.

---

*Generated: 2026-09-05. Index: `smart-freight-logistics@e9f79df` `558 nodes 1103 edges 21 clusters 23 flows`. To refresh after code changes: `node .gitnexus/run.cjs analyze --index-only` (auto-selects runner, no global install needed).*
