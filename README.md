# Smart Freight Logistics — .NET 10 Microservices

> YARP Gateway + Identity + OrderService (Clean Architecture, EF Core, JWT, MTP tests, Testcontainers, structured logging).

**Status:** Stages 1-3 implemented (Foundation, Identity + JWT/YARP, OrderService CRUD/State Machine/Tests/Seed). Stage 4 Event-Driven (MassTransit/RabbitMQ) planned.

**Stack:** `.NET 10` · `YARP 2.3.0` · `EF Core 10 + Npgsql 10.0.3` · `PostgreSQL 16` · `Redis 7` · `Serilog` · `xUnit v3 MTP` · `Testcontainers` · `Docker Compose`

---

## Architecture at a Glance

```
Client → YARP Gateway :5000 (Client/LogisticsManager/RpaBot policies)
        ├─ /api/auth/*   → IdentityService :5001 (PBKDF2, JWT HS256)
        └─ /api/orders/* → OrderService.API :5002 (Order aggregate, State Machine)
             └─ sfl_order_db / sfl_identity_db → PostgreSQL :5432
        Logging: BuildingBlocks.Logging (Serilog + X-Correlation-ID)
```

Full diagram + flows: [`ARCHITECTURE.md`](./ARCHITECTURE.md). Knowledge graph: `558 nodes 1103 edges 21 clusters 23 flows` (`node .gitnexus/run.cjs analyze --index-only` `2026-09-05`).

---

## Quick Start (dev, `dotnet run` — no Docker for services)

### Prereqs

- `.NET 10 SDK` `10.0.11`, `Docker Desktop` (for `postgres:16-alpine` + `pgAdmin` + `redis`), `Node 24` (for `gitnexus`).

### 1) Infra — Postgres + Redis

```powershell
copy docker\.env.example docker\.env   # then edit docker\.env: set POSTGRES_PASSWORD, JWT_SECRET (>=32 chars), REDIS_PASSWORD
docker compose -f docker/docker-compose.yml up -d   # logistics-postgres-db :5432, pgadmin :5050, redis :6379
# reset volumes: docker compose -f docker/docker-compose.yml down -v
```

`postgres` creates `sfl_identity_db` + `sfl_order_db` via `docker/postgres/init-scripts/init.sql`.

### 2) Secrets — `user-secrets` (dev, not committed)

`appsettings.json` has `YOUR_SECRET_JWT_KEY` placeholder (fail-fast `<32`), `appsettings.Development.json` is `{}` (no secret in repo). **No secrets are committed** — generate dev values locally and store via `user-secrets` (outside repo, per `dotnet new gitignore` `.env` + `UserSecretsId`):

```powershell
# generate a dev JWT secret (>=32 chars) and a dev DB password locally — do NOT commit
# example (PowerShell): $jwt = -join ((48..57)+(65..90)+(97..122) | Get-Random -Count 44 | % {[char]$_})
dotnet user-secrets set "JwtSettings:Secret" "<dev-jwt-secret-32-chars-min>" --project src/Services/IdentityService/IdentityService.csproj
dotnet user-secrets set "JwtSettings:Secret" "<same-dev-jwt-secret>" --project src/Gateways/YarpGateway/YarpGateway.csproj
dotnet user-secrets set "JwtSettings:Secret" "<same-dev-jwt-secret>" --project src/Services/OrderService/OrderService.API/OrderService.API.csproj
dotnet user-secrets set "DatabaseSettings:Password" "<dev-db-password>" --project src/Services/IdentityService/IdentityService.csproj
dotnet user-secrets set "DatabaseSettings:Password" "<same-dev-db-password>" --project src/Services/OrderService/OrderService.API/OrderService.API.csproj
# verify (values stay outside repo): dotnet user-secrets list --project src/Services/IdentityService/IdentityService.csproj
```

Use the **same** `JwtSettings:Secret` for all three services in dev. Prod `docker` uses `docker/.env` `JWT_SECRET=<prod-jwt-secret>` (env `JwtSettings__Secret`, also not committed, `docker/.env` is gitignored via `.gitignore:.env`).

### 3) Build

```powershell
dotnet build -v minimal   # 0 Warning(s) expected
```

Stop any running `IdentityService/YarpGateway/OrderService.API` first (`Get-Process ... | Stop-Process -Force`) or `MSB3021` file lock.

### 4) Run — `5000/5001/5002` (`7000/7001/7002` https, no `UseHttpsRedirection` on services)

```powershell
# three terminals or -WindowStyle Hidden
dotnet run --project src/Services/IdentityService/IdentityService.csproj --no-build      # :5001
dotnet run --project src/Services/OrderService/OrderService.API/OrderService.API.csproj --no-build  # :5002
dotnet run --project src/Gateways/YarpGateway/YarpGateway.csproj --no-build               # :5000
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
| `YarpGateway` | `5000` | `7000` | `ReverseProxy` `identity-route /api/auth/{**catch-all} → 5001`, `order-route /api/orders/{**catch-all} → 5002` `ClientPolicy` |
| `IdentityService` | `5001` | `7001` | `POST /api/auth/register 201`, `POST /api/auth/login 200 {token,expiresAt}`, `GET /api/auth/me 200` |
| `OrderService.API` | `5002` | `7002` | `POST /api/orders 201` `[ClientPolicy]`, `GET /api/orders 200` (Client own / Manager all), `GET /{id} 200/404`, `PUT /{id}/status 200/403/409` |

No `UseHttpsRedirection` on services (removed for `http` dev via `5000`); `YarpGateway` also removed to avoid `307` stripping `Authorization`.

---

## Auth & Roles

- **Hasher:** `PBKDF2 HMAC-SHA256` `Rfc2898DeriveBytes.Pbkdf2` static, format `iterations:saltHex:hashHex`, `600k` prod / `100k` dev (`PasswordHasherOptions` `16/32`), `IPasswordHasher` `Hash/Verify/NeedsRehash`, `FixedTimeEquals`.
- **JWT:** `JwtSettings {Secret>=32, Issuer=SmartFreightLogistics.Identity, Audience=SmartFreightLogistics.Gateways, Expiry 60}` `HS256` `JwtSecurityToken` `sub/email/NameIdentifier/Name/Role/jti/iat` `notBefore/expires` `ClockSkew Zero`. Same `Secret` via `user-secrets` dev / `docker/.env` prod for `Identity`+`Yarp`+`Order`.
- **Policies:** `ClientPolicy` (`Client`), `LogisticsManagerPolicy` (`LogisticsManager`), `RPA_Bot_Policy` (`RpaBot`) — `AddAuthentication JwtBearer` `ValidateIssuer/Audience/IssuerSigningKey/Lifetime` in all services + `YarpGateway` `UseAuthentication/UseAuthorization` before `MapReverseProxy`.
- **Seed:** `dev.client 3333...` owns `3` orders; `admin 1111...` `LogisticsManager` sees all via direct `:5002` (via `Yarp` `ClientPolicy` blocks `Manager` on `/api/orders` — use direct for manager). Dev passwords are not committed — seeded via `IdentitySeeder` hashing at startup.

---

## Tests (MTP — `dotnet run`)

`.NET 10` `Microsoft.Testing.Platform` (`xUnit v3` `4.0.0`, `coverlet 10.0.1`). `dotnet test -v minimal` is `VSTest` deprecated on `.NET 10` — use `dotnet run`.

```powershell
# build first (stop services to avoid file lock)
dotnet build -v minimal

# unit (isolated, no DB)
dotnet run --project tests/OrderService.Tests.Unit
# Passed 41 (OrderStatusTransitions matrix, Weight/Origin, TransitionTo, Application Moq IPublishEndpoint)

# integration (Testcontainers, real postgres:16-alpine via Docker)
dotnet run --project tests/OrderService.Tests.Integration
# Passed 10 (WebApplicationFactory + Testcontainers.PostgreSql, MigrateAsync via EnsureCreated, JwtHelper HS256 same Secret, WebApplicationFactory + HttpClient 401/201/400/403/404/409, ownership, state machine)
# total 51
```

`Integration` uses `CustomWebApplicationFactory : WebApplicationFactory<Program>` `IAsyncLifetime` `PostgreSqlBuilder` `sfl_order_db_test` `EnsureDeleted+EnsureCreated` per test, `JwtHelper` `test-secret-must-be-at-least-32-chars-...` (isolated, not `user-secrets`).

---

## Project Structure

```
src/
  BuildingBlocks/Logging          Serilog + CorrelationIdMiddleware (X-Correlation-ID)
  Gateways/YarpGateway            YARP :5000 → 5001/5002, AuthExtension Client/LogisticsManager/RpaBot
  Services/IdentityService        User, IdentityDbContext, PasswordHasher, JwtTokenGenerator, AuthController, IdentitySeeder
  Services/OrderService/
    OrderService.Domain           Order/CargoDetails/StatusHistory, OrderStatusTransitions (Created→Confirmed→InTransit→Customs→Delivered/Cancelled)
    OrderService.Application      DTOs sealed record, IOrderService, IOrderRepository, OrderService (Create/Get/List/UpdateStatus)
    OrderService.Infrastructure   OrderDbContext (OwnsOne Cargo, History Field), OrderRepository (ExecuteUpdate), OrderSeeder
    OrderService.API              OrdersController, AuthExtensions, Program (IsDevelopment SeedAsync)
tests/
  OrderService.Tests.Unit         xUnit v3 MTP, FluentAssertions, Moq (41)
  OrderService.Tests.Integration  Testcontainers.PostgreSql, WebApplicationFactory, JwtHelper (10)
docker/
  docker-compose.yml              postgres:16-alpine (sfl_identity_db, sfl_order_db), pgadmin :5050, redis :6379 (Stage 5), rabbitmq planned Stage 4
  .env / .env.example             POSTGRES_PASSWORD, JWT_SECRET, REDIS_PASSWORD, RABBITMQ_* (Stage 4)
docs/
  Smart Freight Logistics main plan.md  Roadmap 1-7 (Stages 1-3 done, 4-7 planned, 4.1-4.10 detailed)
ARCHITECTURE.md                   Codebase stats, functional areas, flows (Gateway/Correlation/Identity/Order CRUD), mermaid, roadmap
```

---

## Troubleshooting

- `MSB3021 Unable to copy ... is being used by another process` → `Get-Process IdentityService,YarpGateway,OrderService.API | Stop-Process -Force` before `dotnet build`.
- `Failed to bind to address http://127.0.0.1:5002: address already in use` → same — stop previous `dotnet run`.
- `401 Unauthorized` on `GET /api/auth/me` via `5000` → check `# @name login` is directly above `POST` (not above `###`), and `Authorization: Bearer {{login.response.body.$.token}}` uses `.$.token` (not `$.token`), and `user-secrets` dev secret is set for all 3 projects.
- `42P01: relation "Orders" does not exist` on `POST /api/orders` → `OrderService` `sfl_order_db` not migrated — `OrderService.API` `Program` `MigrateAsync` runs on startup, or `dotnet ef database update --project src/Services/OrderService/OrderService.Infrastructure --startup-project src/Services/OrderService/OrderService.API` with `DatabaseSettings__Password`.
- `DbUpdateConcurrencyException 0 rows` on `PUT /status` → fixed via `ExecuteUpdate` `TryUpdateStatusWithHistoryAsync` (bypasses tracking).

---

## Roadmap

| Stage | Focus | Status |
|-------|-------|--------|
| 1 Foundation | YARP, Serilog, Docker | ✅ |
| 2 Identity | PBKDF2, JWT, Policies | ✅ |
| 3 OrderService | Clean Arch, State Machine, CRUD, MTP 51 tests, Seed | ✅ |
| 4 Event-Driven | MassTransit + RabbitMQ `OrderCreatedDomainEvent` → `OrderCreatedIntegrationEvent` (Domain≠Integration), `IHttpClientFactory` + `Polly` `Rest API` to `RPA` → `Customs` via `RPA_Bot` | ⏳ Next — `4.1-4.10` in `docs/main plan.md` |
| 5 Tracking | Redis `Cache-Aside` | ⏳ |
| 6 CQRS | MediatR | ⏳ |
| 7 Prod | OpenTelemetry, HealthChecks | ⏳ |

`gitnexus: node .gitnexus/run.cjs analyze --index-only` (auto `npx`/`bunx`), `git status` before `detect_changes`.

---

*Generated for implemented Stages 1-3.10. To refresh graph: `node .gitnexus/run.cjs analyze --index-only`.*
