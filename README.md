# Smart Freight Logistics — .NET 10 Microservices

> YARP Gateway + Identity + OrderService + TrackingService + IntegrationService (Clean Architecture, EF Core, JWT, Redis Cache-Aside, MassTransit/RabbitMQ, CQRS via MediatR + FluentValidation, Polly, MTP tests, Testcontainers, structured logging).

**Status:** Stages 1-6 implemented (Foundation, Identity + JWT/YARP, OrderService CRUD/State Machine/Tests/Seed, Event-Driven DomainEvents → IntegrationEvents → MassTransit/RabbitMQ → IntegrationService RPA Bridge → RpaBot Customs, TrackingService + Redis Cache-Aside + YARP, CQRS: MediatR Commands/Queries + ValidationBehavior + EF read-model + 92 tests). Stage 7 Prod Readiness next.

**Stack:** `.NET 10` · `YARP 2.3.0` · `EF Core 10 + Npgsql 10.0.3` · `PostgreSQL 16` · `Redis 7` · `StackExchange.Redis 2.8.37` · `RabbitMQ 3-management-alpine` · `MassTransit 8.3.5` · `MediatR 12.4.1` · `FluentValidation 11.11.0` · `Polly 10.0.0` · `Serilog` · `xUnit v3 MTP` · `Testcontainers.Redis/PostgreSql/RabbitMq` · `Docker Compose`

---

## Architecture at a Glance

```
Client → YARP Gateway :5000 (Client/LogisticsManager/RpaBot policies)
        ├─ /api/auth/*              → IdentityService :5001 (PBKDF2, JWT HS256)
        ├─ /api/orders/*            → OrderService.API :5002 (MediatR ISender → Commands/Queries, State Machine, Cache-Aside Redis, IPublishEndpoint, EF read-model)
        │     └─ sfl_order_db / sfl_identity_db → PostgreSQL :5432
        │     └─ Publish OrderCreatedIntegrationEvent → RabbitMQ :5672 → IntegrationService :5003
        ├─ /api/tracking/*          → TrackingService :5004 (UpdateTrackingCommand/GetTrackingQuery, Redis ICacheService TTL 5m)
        │     └─ logistics-cache    → Redis :6379 (StackExchange.Redis)
        └─ /api/orders/{id}/status PUT (rpa-status-route) → OrderService :5002 (RpaBot → Customs)
        Logging: BuildingBlocks.Logging (Serilog + X-Correlation-ID)
        Caching: BuildingBlocks.Caching (IDistributedCache → Redis/ InMemory fallback, ICacheService GetOrCreateAsync, CacheKeys)
        CQRS: BuildingBlocks.CQRS (MediatR 12.4.1 + ValidationBehavior/LoggingBehavior, FluentValidation 11.11.0, ValidationException → 400)
        EventBus: BuildingBlocks.EventBus (RabbitMqSettings ValidateOnStart, MassTransit Retry 3×1s)
        Integration: IntegrationService :5003 (No DB, OrderCreatedConsumer → RpaClient POST + OrderStatusClient PUT Customs via RpaBot JWT + Polly)
```

Full diagram + flows: [`ARCHITECTURE.md`](./ARCHITECTURE.md). Knowledge graph: `1026 nodes 1985 edges 49 clusters 18 flows` (`node .gitnexus/run.cjs analyze --index-only` `2026-09-18T14:45:01Z`, branch `features/init-CQRS`).

---

## Quick Start (dev, `dotnet run` — no Docker for services)

### Prereqs

- `.NET 10 SDK` `10.0.11`, `Docker Desktop` (for `postgres:16-alpine` + `pgAdmin` + `redis`), `Node 24` (for `gitnexus`).

### 1) Infra — Postgres + Redis + RabbitMQ

```powershell
copy docker\.env.example docker\.env   # then edit docker\.env: set POSTGRES_PASSWORD, JWT_SECRET (>=32 chars), REDIS_PASSWORD, RABBITMQ_USER, RABBITMQ_PASSWORD
docker compose -f docker/docker-compose.yml up -d   # logistics-postgres-db :5432, pgadmin :5050, redis :6379 (healthy), logistics-rabbitmq :5672/:15672 (healthy)
# reset volumes: docker compose -f docker/docker-compose.yml down -v
# NOTE (6.9): compose `integration-service`/`tracking-service` entries use the bare `aspnet:10.0` image with no app payload (`build:` commented out) — they crash-loop. Run :5003/:5004 via `dotnet run` locally until Dockerfiles exist.
```

`postgres` creates `sfl_identity_db` + `sfl_order_db` via `docker/postgres/init-scripts/init.sql`. `rabbitmq` (`rabbitmq:3-management-alpine`) exposes `5672` (AMQP) `15672` (management) with `rabbitmq_data` volume and `healthcheck`.

### 2) Secrets — `user-secrets` (dev, not committed)

`appsettings.json` has `YOUR_SECRET_JWT_KEY` placeholder (fail-fast `<32`), `appsettings.Development.json` is `{}` (no secret in repo). **No secrets are committed** — generate dev values locally and store via `user-secrets` (outside repo, per `dotnet new gitignore` `.env` + `UserSecretsId`):

```powershell
# generate dev secrets locally — do NOT commit (values stay outside repo)
# example (PowerShell): $jwt = -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 44 | % {[char]$_})
dotnet user-secrets set "JwtSettings:Secret" "<dev-jwt-secret-32-chars-min>" --project src/Services/IdentityService/IdentityService.csproj
dotnet user-secrets set "JwtSettings:Secret" "<same-dev-jwt-secret>" --project src/Gateways/YarpGateway/YarpGateway.csproj
dotnet user-secrets set "JwtSettings:Secret" "<same-dev-jwt-secret>" --project src/Services/OrderService/OrderService.API/OrderService.API.csproj
dotnet user-secrets set "JwtSettings:Secret" "<same-dev-jwt-secret>" --project src/Services/IntegrationService/IntegrationService.csproj
dotnet user-secrets set "JwtSettings:Secret" "<same-dev-jwt-secret>" --project src/Services/TrackingService/TrackingService.API/TrackingService.API.csproj
dotnet user-secrets set "DatabaseSettings:Password" "<dev-db-password>" --project src/Services/IdentityService/IdentityService.csproj
dotnet user-secrets set "DatabaseSettings:Password" "<same-dev-db-password>" --project src/Services/OrderService/OrderService.API/OrderService.API.csproj
dotnet user-secrets set "RabbitMq:User" "<dev-rabbit-user>" --project src/Services/OrderService/OrderService.API/OrderService.API.csproj
dotnet user-secrets set "RabbitMq:Password" "<dev-rabbit-password>" --project src/Services/OrderService/OrderService.API/OrderService.API.csproj
dotnet user-secrets set "RabbitMq:User" "<same-dev-rabbit-user>" --project src/Services/IntegrationService/IntegrationService.csproj
dotnet user-secrets set "RabbitMq:Password" "<same-dev-rabbit-password>" --project src/Services/IntegrationService/IntegrationService.csproj
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379,password=<dev-redis-password>" --project src/Services/OrderService/OrderService.API/OrderService.API.csproj
dotnet user-secrets set "ConnectionStrings:Redis" "localhost:6379,password=<same-dev-redis-password>" --project src/Services/TrackingService/TrackingService.API/TrackingService.API.csproj
# verify (values stay outside repo): dotnet user-secrets list --project src/Services/IdentityService/IdentityService.csproj
```

Use the **same** `JwtSettings:Secret` for all five services (`Identity`+`Yarp`+`Order`+`Integration`+`Tracking` — `Integration` generates `RpaBot JWT` `HS256` `5m`) and same `RabbitMq:User/Password` for `Order`+`Integration` + same `ConnectionStrings:Redis` for `Order`+`Tracking` in dev. Prod `docker` uses `docker/.env` `JWT_SECRET=<prod-jwt-secret>` `RABBITMQ_USER/PASSWORD=<prod-rabbit>` `REDIS_PASSWORD=<prod-redis>` (env `JwtSettings__Secret` `RabbitMq__User/Password` `ConnectionStrings__Redis`, also not committed, `docker/.env` is gitignored via `.gitignore:.env`). `appsettings.json` has `YOUR_SECRET_JWT_KEY` / `YOUR_SECRET_PASSWORD` / `YOUR_REDIS_PASSWORD` placeholders fail-fast (`YOUR_` → fallback `DistributedMemoryCache` in tests), `appsettings.Development.json` is `{}` (no secret in repo); `RabbitMqSettings` has `Host localhost:5672` default but `User/Password=""` required `ValidateOnStart`; `Redis` via `BuildingBlocks.Caching` `IDistributedCache` (`YOUR_` → `InMemory`).

### 3) Build

```powershell
dotnet build -v minimal   # 0 Warning(s) expected
```

Stop any running `IdentityService/YarpGateway/OrderService.API/IntegrationService` first (`Get-Process ... | Stop-Process -Force`) or `MSB3021` file lock.

### 4) Run — `5000/5001/5002/5003/5004` (`7000/7001/7002/7003/7004` https, no `UseHttpsRedirection` on services)

```powershell
# five terminals or -WindowStyle Hidden
dotnet run --project src/Services/IdentityService/IdentityService.csproj --no-build      # :5001
dotnet run --project src/Services/OrderService/OrderService.API/OrderService.API.csproj --no-build  # :5002 (Cache-Aside Redis, fallback InMemory if no secret)
dotnet run --project src/Services/TrackingService/TrackingService.API/TrackingService.API.csproj --no-build # :5004 (Redis ICacheService TTL 5m)
dotnet run --project src/Services/IntegrationService/IntegrationService.csproj --no-build # :5003 (No DB, consumes OrderCreatedIntegrationEvent → Rpa → Customs)
dotnet run --project src/Gateways/YarpGateway/YarpGateway.csproj --no-build               # :5000 (routes /api/auth/* →5001, /api/orders/* →5002, /api/tracking/* →5004)
```

Seed ( `IsDevelopment` only, idempotent): `IdentitySeeder` `admin@example.com 1111... LogisticsManager`, `rpa@example.com 2222... RpaBot`, `dev.client@example.com 3333... Client`; `OrderSeeder` `3` orders for `3333...` (`General` `Created`, `Refrigerated` `Confirmed`, `Hazardous` `Created`). Passwords for seeded dev users are set via `user-secrets`/`IdentitySeeder` hashing at startup (not committed) — see `src/Services/IdentityService/Data/IdentitySeeder.cs`.

### 5) Smoke via `.http` (VS Code Rest Client) or `curl`

Use `Gateway :5000` (YARP validates `ClientPolicy` for `/api/orders`), or `direct :5001/:5002` for comparison.

```http
### Gateway login (Client)
POST http://localhost:5000/api/auth/login
Content-Type: application/json
{"email":"dev.client@example.com","password":"<dev-client-password>"}
# @name login  ← must be directly above POST

### Gateway me (uses {{login.response.body.$.token}})
GET http://localhost:5000/api/auth/me
Authorization: Bearer {{login.response.body.$.token}}

### Gateway create order
POST http://localhost:5000/api/orders
Authorization: Bearer {{login.response.body.$.token}}
Content-Type: application/json
{"cargoType":"General","weightKg":120.5,"volumeM3":2.3,"origin":"Kyiv, UA","destination":"Warsaw, PL","description":"Smoke","declaredValue":5000,"deadline":"2026-09-30T12:00:00Z"}
# @name createdOrder

### Gateway get/list
GET http://localhost:5000/api/orders
Authorization: Bearer {{login.response.body.$.token}}
GET http://localhost:5000/api/orders/{{createdOrder.response.body.$.id}}
Authorization: Bearer {{login.response.body.$.token}}

### Gateway cancel (Created -> Cancelled)
PUT http://localhost:5000/api/orders/{{createdOrder.response.body.$.id}}/status
Authorization: Bearer {{login.response.body.$.token}}
Content-Type: application/json
{"newStatus":5,"notes":"client cancel"}
```

Files: `src/Gateways/YarpGateway/YarpGateway.http` (gateway smoke `register/login/me/orders`), `src/Services/OrderService/OrderService.API/OrderService.API.http` (direct `:5002`), `src/Services/IdentityService/IdentityService.http` (direct `:5001`).

`curl` equivalent:

```powershell
$tok=(Invoke-RestMethod -Method POST -Uri http://localhost:5000/api/auth/login -Body (@{email="dev.client@example.com";password="<dev-client-password>"}|ConvertTo-Json) -ContentType "application/json").token
Invoke-RestMethod -Method GET -Uri http://localhost:5000/api/auth/me -Headers @{Authorization="Bearer $tok"}
```

---

## Ports & Routes

| Service | http | https | Route |
|---------|------|-------|-------|
| `YarpGateway` | `5000` | `7000` | `ReverseProxy` `identity-route /api/auth/{**catch-all} → 5001`, `tracking-route /api/tracking/{**catch-all} → 5004` (no `AuthorizationPolicy`, service enforces), `rpa-status-route PUT /api/orders/{id}/status → 5002` (no `AuthorizationPolicy`, `OrderService` enforces `RpaBot→Customs`), `order-route /api/orders/{**catch-all} → 5002` `ClientPolicy` |
| `IdentityService` | `5001` | `7001` | `POST /api/auth/register 201`, `POST /api/auth/login 200 {token,expiresAt}`, `GET /api/auth/me 200` |
| `OrderService.API` | `5002` | `7002` | `POST /api/orders 201` `[ClientPolicy]`, `GET /api/orders 200` (Client own / Manager all, **Cache-Aside** `order:list:{client}/all` `OrderListTtl 2m`), `GET /{id} 200/404` (**Cache-Aside** `order:{id}` `OrderTtl 2m`), `PUT /{id}/status 200/403/409` (`RpaBot` only `Customs`, invalidates `order:*`) |
| `TrackingService` | `5004` | `7004` | `PUT /api/tracking/{orderId} 200` `[ClientPolicy]` `lat/lon/speed/notes` → `Redis tracking:{orderId} TTL 5m`, `GET /api/tracking/{orderId} 200/404`, `GET /health 200` |
| `IntegrationService` | `5003` | `7003` | `GET /health 200 {status:Healthy}`, consumes `OrderCreatedIntegrationEvent` via `RabbitMQ` `logistics-rabbitmq:5672` `OrderCreatedConsumer` → `RpaClient POST api/customs/declarations` (`Rpa:BaseUrl`) → `OrderStatusClient PUT api/orders/{id}/status Customs` with `RpaBot JWT` `Polly 3×2^retry` `CircuitBreaker 5/30s` |

No `UseHttpsRedirection` on services (removed for `http` dev via `5000`); `YarpGateway` also removed to avoid `307` stripping `Authorization`.

---

## Auth & Roles

- **Hasher:** `PBKDF2 HMAC-SHA256` `Rfc2898DeriveBytes.Pbkdf2` static, format `iterations:saltHex:hashHex`, `600k` prod / `100k` dev (`PasswordHasherOptions` `16/32`), `IPasswordHasher` `Hash/Verify/NeedsRehash`, `FixedTimeEquals`.
- **JWT:** `JwtSettings {Secret>=32, Issuer=SmartFreightLogistics.Identity, Audience=SmartFreightLogistics.Gateways, Expiry 60}` `HS256` `JwtSecurityToken` `sub/email/NameIdentifier/Name/Role/jti/iat` `notBefore/expires` `ClockSkew Zero`. Same `Secret` via `user-secrets` dev / `docker/.env` prod for `Identity`+`Yarp`+`Order`+`Integration` (`Integration` `OrderStatusClient` generates `RpaBot` `HS256` `5m` via same `Secret/Issuer/Audience`).
- **Policies:** `ClientPolicy` (`Client`), `LogisticsManagerPolicy` (`LogisticsManager`), `RPA_Bot_Policy` (`RpaBot`) — `AddAuthentication JwtBearer` `ValidateIssuer/Audience/IssuerSigningKey/Lifetime` in all services + `YarpGateway` `UseAuthentication/UseAuthorization` before `MapReverseProxy` (`rpa-status-route` has no `AuthorizationPolicy`, `OrderService` enforces `RpaBot→Customs`).
- **Caching:** `BuildingBlocks.Caching` `CacheKeys {TrackingTtl 5m, OrderTtl 2m, OrderListTtl 2m}` `ICacheService {GetAsync/SetAsync/RemoveAsync/GetOrCreateAsync}` `RedisCacheService` `IDistributedCache` (`StackExchangeRedis` when `ConnectionStrings:Redis` present and not `YOUR_`, else `DistributedMemoryCache`) `System.Text.Json CamelCase` — `OrderService` `GetById/List` `Cache-Aside` with ownership-aware check before cache hit + invalidation on `Create/UpdateStatus`, `TrackingService` `RedisTrackingRepository` `tracking:{orderId}`.
- **EventBus:** `RabbitMqSettings {Host=localhost Port=5672 VHost=/ User/Password="" required ValidateOnStart}` `MassTransit 8.3.5` `UseMessageRetry Interval(3,1s)` `ConfigureEndpoints` — `BuildingBlocks.EventBus` `ServiceCollectionExtensions.cs:16`.
- **Seed:** `dev.client 3333...` owns `3` orders; `admin 1111...` `LogisticsManager` sees all via direct `:5002` (via `Yarp` `ClientPolicy` blocks `Manager` on `/api/orders` — use direct for manager). Dev passwords are not committed — seeded via `IdentitySeeder` hashing at startup.

---

## Tests (MTP — `dotnet run`)

`.NET 10` `Microsoft.Testing.Platform` (`xUnit v3` `4.0.0`, `coverlet 10.0.1`). `dotnet test -v minimal` is `VSTest` deprecated on `.NET 10` — use `dotnet run`.

```powershell
# build first (stop services to avoid file lock)
dotnet build -v minimal

# unit (isolated, no DB)
dotnet run --project tests/OrderService.Tests.Unit
# Passed 55 (OrderStatusTransitions matrix, Weight/Origin, TransitionTo, handler tests via CreateOrder/UpdateOrderStatus/GetOrderById/ListOrders handlers incl. cache-hit, FluentValidation validator tests)
dotnet run --project tests/TrackingService.Tests.Unit
# Passed 17 (GeoCoordinate Validate, TrackingEntry Create/Update/Notes, CacheKeys TTL, UpdateTracking/GetTracking handler tests, UpdateTrackingCommandValidator tests)

# integration OrderService (Testcontainers, real postgres:16-alpine via Docker, InMemory for RabbitMQ/Caching when Testing)
dotnet run --project tests/OrderService.Tests.Integration
# Passed 11 (WebApplicationFactory + Testcontainers.PostgreSql, MigrateAsync via EnsureCreated, JwtHelper HS256 same Secret, WebApplicationFactory + HttpClient 401/201/400/403/404/409, ownership, state machine, ISender direct ValidationException via ValidationBehavior, InMemory loopback://localhost/ + DistributedMemoryCache fallback for Cache-Aside)

# integration TrackingService (Testcontainers.Redis + WebApplicationFactory + HttpClient YARP-like health/CRUD)
dotnet run --project tests/TrackingService.Tests.Integration
# Passed 6 (Testcontainers.Redis 7-alpine + WebApplicationFactory health 200, PUT without token 401, PUT invalid lat 400, PUT+GET 200 Cache-Aside tracking:{id} second GET hit, GET 404, ISender direct ValidationException; Environment.SetEnvironmentVariable Jwt+Redis before factory to override placeholder)

# integration IntegrationService (MassTransit.TestFramework InMemoryTestHarness + Testcontainers.RabbitMq)
dotnet run --project tests/IntegrationService.Tests.Integration
# Passed 3 (OrderCreatedConsumer: Publish OrderCreatedIntegrationEvent → RpaClient mock true → OrderStatusClient MarkCustoms Once; false → Never; Throws → Retry)
# total 92 (55+17+11+6+3)
```

`OrderService Integration` uses `CustomWebApplicationFactory : WebApplicationFactory<Program>` `IAsyncLifetime` `PostgreSqlBuilder` `sfl_order_db_test` `EnsureDeleted+EnsureCreated` per test, `JwtHelper` `test-secret-must-be-at-least-32-chars-...` (isolated, not `user-secrets`), `UseEnvironment Testing` → `InMemory` `loopback://localhost/` (`RabbitMq`) + `DistributedMemoryCache` (`YOUR_` guard) (fully isolated, no `docker/.env` file parsing). `TrackingService Integration` uses `Testcontainers.Redis` `RedisBuilder` + `WebApplicationFactory` `Environment.SetEnvironmentVariable JwtSettings__Secret/ConnectionStrings__Redis` before host. `IntegrationService` uses `MassTransit.TestFramework InMemoryTestHarness` `Moq IRpaClient/IOrderStatusClient`.

---

## Project Structure

```
src/
  BuildingBlocks/Logging          Serilog + CorrelationIdMiddleware (X-Correlation-ID)
  BuildingBlocks/Caching          StackExchange.Redis 2.8.37 + IDistributedCache, CacheKeys (TrackingTtl 5m/Order 2m), ICacheService GetOrCreateAsync, RedisCacheService
  BuildingBlocks/CQRS             MediatR 12.4.1 + FluentValidation 11.11.0, ValidationBehavior/LoggingBehavior IPipelineBehavior, CqrsExtensions.AddCqrs (ValidationException → 400)
  BuildingBlocks/EventBus         MassTransit 8.3.5 + RabbitMQ 3-management-alpine, RabbitMqSettings ValidateOnStart, OrderCreatedIntegrationEvent
  Gateways/YarpGateway            YARP :5000 → 5001/5002/5004, AuthExtension Client/LogisticsManager/RpaBot (tracking-route /api/tracking/{**catch-all} →5004, rpa-status-route PUT /api/orders/{id}/status)
  Services/IdentityService        User, IdentityDbContext, PasswordHasher, JwtTokenGenerator, AuthController, IdentitySeeder
  Services/OrderService/
    OrderService.Domain           Order/CargoDetails/StatusHistory, OrderStatusTransitions, Events IDomainEvent/OrderCreatedDomainEvent
    OrderService.Application      DTOs sealed record, IOrderRepository + IOrderReadRepository, Features/Orders Commands (CreateOrder/UpdateOrderStatus + validators) / Queries (GetOrderById + validator / ListOrders), OrderReadModel projection, Mappings/OrderCreatedIntegrationMapper (obsolete IOrderService/OrderService shims removed in 6.8 cleanup)
    OrderService.Infrastructure   OrderDbContext (OwnsOne Cargo, History Field), OrderRepository (ExecuteUpdate), ReadModels/OrderReadRepository (AsNoTracking Select, no History join), OrderSeeder
    OrderService.API              OrdersController → ISender (ValidationException → 400, DomainException → 409), AuthExtensions, Program (IsDevelopment SeedAsync, AddCqrs + AddScoped IOrderRepository/IOrderReadRepository, AddCaching InMemory when Testing + AddEventBus)
  Services/TrackingService/
    TrackingService.Domain        TrackingEntry (OrderId, Lat/Lon [90/180], Speed, Timestamp, Notes), GeoCoordinate ValueObject [JsonConstructor]
    TrackingService.Infrastructure ITrackingRepository, RedisTrackingRepository (tracking:{orderId} via ICacheService)
    TrackingService.Application   DTOs UpdateTrackingRequest/TrackingResponse, Features/Tracking Commands (UpdateTracking + validator) / Queries (GetTracking) (obsolete ITrackingService/TrackingAppService shims removed in 6.8 cleanup)
    TrackingService.API           TrackingController → ISender (ValidationException → 400), AuthExtensions, Program (AddCaching + AddTrackingAuth + AddCqrs), appsettings Redis placeholder
  Services/IntegrationService     (No DB, Stateless) MassTransit Consumer OrderCreatedConsumer → RpaClient + OrderStatusClient (HttpClient + Polly + RpaBot JWT), Program :5003, /health
tests/
  OrderService.Tests.Unit         xUnit v3 MTP, FluentAssertions, Moq (55, handlers + validators + IOrderReadRepository)
  OrderService.Tests.Integration  Testcontainers.PostgreSql, WebApplicationFactory, JwtHelper (11, InMemory loopback + InMemory cache + ISender ValidationException)
  TrackingService.Tests.Unit      xUnit v3 MTP, FluentAssertions, Moq (17, GeoCoordinate/TrackingEntry/CacheKeys + handlers + UpdateTrackingCommandValidator)
  TrackingService.Tests.Integration Testcontainers.Redis 4.2.0, WebApplicationFactory, FluentAssertions (6, health/401/400/PUT+GET Cache-Aside 200/404, ISender ValidationException, env var override)
  IntegrationService.Tests.Integration  MassTransit.TestFramework InMemoryTestHarness, Testcontainers.RabbitMq, Moq IRpaClient/IOrderStatusClient (3)
docker/
  docker-compose.yml              postgres:16-alpine (sfl_identity_db, sfl_order_db), pgadmin :5050, redis :6379 (healthy, REDIS_PASSWORD env), rabbitmq :5672/:15672 (healthy), integration-service :5003:8080, tracking-service :5004:8080 (depends_on redis/rabbitmq healthy)
  .env / .env.example             POSTGRES_PASSWORD, JWT_SECRET, REDIS_PASSWORD, RABBITMQ_USER, RABBITMQ_PASSWORD
docs/
  Smart Freight Logistics main plan.md  Roadmap 1-7 (Stages 1-6 done, 6.1-6.9 detailed, 7 planned)
ARCHITECTURE.md                   Codebase stats — nodes/edges/clusters/flows (updated via gitnexus analyze), functional areas, flows (Gateway/Correlation/Identity/Order CRUD/Cache-Aside/Tracking/Event-Driven RPA), mermaid, roadmap
```

---

## Troubleshooting

- `MSB3021 Unable to copy ... is being used by another process` → `Get-Process IdentityService,YarpGateway,OrderService.API,IntegrationService | Stop-Process -Force` before `dotnet build`.
- `Failed to bind to address http://127.0.0.1:5002: address already in use` → same — stop previous `dotnet run`.
- `401 Unauthorized` on `GET /api/auth/me` via `5000` → check `# @name login` is directly above `POST` (not above `###`), and `Authorization: Bearer {{login.response.body.$.token}}` uses `.$.token` (not `$.token`), and the same `user-secrets` dev `JwtSettings:Secret` is set for all services (`Identity`+`Yarp`+`Order`+`Integration`+`Tracking` — a missing secret validates against the `YOUR_SECRET_JWT_KEY` placeholder and rejects every token).
- `42P01: relation "Orders" does not exist` on `POST /api/orders` → `OrderService` `sfl_order_db` not migrated — `OrderService.API` `Program` `MigrateAsync` runs on startup, or `dotnet ef database update --project src/Services/OrderService/OrderService.Infrastructure --startup-project src/Services/OrderService/OrderService.API` with `DatabaseSettings__Password`.
- `DbUpdateConcurrencyException 0 rows` on `PUT /status` → fixed via `ExecuteUpdate` `TryUpdateStatusWithHistoryAsync` (bypasses tracking).

---

## Roadmap

| Stage | Focus | Status |
|-------|-------|--------|
| 1 Foundation | YARP, Serilog, Docker | ✅ |
| 2 Identity | PBKDF2, JWT, Policies | ✅ |
| 3 OrderService | Clean Arch, State Machine, CRUD, MTP 42+10 tests, Seed | ✅ |
| 4 Event-Driven | MassTransit 8.3.5 + RabbitMQ 3-management-alpine `OrderCreatedDomainEvent` → `OrderCreatedIntegrationEvent` flat DTO, `BuildingBlocks.EventBus` `ValidateOnStart` `Retry 3×1s`, `OrderService` `IPublishEndpoint` + `InMemory` when `Testing`, `IntegrationService` `No DB` `OrderCreatedConsumer` → `RpaClient` `POST` + `OrderStatusClient` `PUT Customs` `RpaBot JWT` `Polly` `:5003`, `Yarp rpa-status-route` | ✅ |
| 5 Tracking | `BuildingBlocks.Caching` `StackExchange.Redis` `CacheKeys 5m/2m` `TrackingService` `Domain/Infrastructure/Application/API` `RedisTrackingRepository` `TrackingAppService` `YARP tracking-route` `OrderService Cache-Aside` `70 tests` `10+5` `Testcontainers.Redis` | ✅ |
| 6 CQRS | MediatR 12.4.1 + FluentValidation 11.11.0, `BuildingBlocks.CQRS` pipeline, Order/Tracking Commands/Queries + validators, `OrdersController`/`TrackingController` → `ISender`, EF `OrderReadModel` projection (`IOrderReadRepository`), obsolete service shims removed, `92 tests` (`55+17+11+6+3`) | ✅ |
| 7 Prod | OpenTelemetry, HealthChecks | ⏳ |

`gitnexus: node .gitnexus/run.cjs analyze --index-only` (auto `npx`/`bunx`), `git status` before `detect_changes`.

---

*Generated for implemented Stages 1-6. To refresh graph: `node .gitnexus/run.cjs analyze --index-only` (now `1026 nodes 1985 edges 49 clusters 18 flows` `2026-09-18T14:45:01Z` branch `features/init-CQRS`, `20 projects` + `BuildingBlocks.CQRS`, `92 tests`).*
