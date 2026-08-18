# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

Vistora — SaaS platform for real estate inspection (*vistoria imobiliária*). ASP.NET Core Web API (.NET 10) serving a mobile-first static front-end from `wwwroot`. Domain language and all user-facing strings are Brazilian Portuguese; keep new copy in pt-BR.

## Commands

```powershell
dotnet run --project .\SaasVistoria      # runs on https://localhost:7093 / http://localhost:5062
dotnet build
docker compose up --build                # full stack (API + Postgres) on http://localhost:8080; copy .env.example to .env first
```

- OpenAPI contract: `/openapi/v1.json`. There is no Swagger UI.
- Tests: `dotnet test SaasVistoria.slnx` (xUnit project `SaasVistoria.Tests`, covers `PasswordHasher`, `TokenService`, `RequireRoleAttribute`, and `DemoVistoraStore`'s completion/pending calculation and evidence hashing). CI runs this on every push/PR via `.github/workflows/ci.yml`.
- Demo login: `admin@atelierimoveis.com.br` / `Vistora@2026`.

## Architecture

Single project (`SaasVistoria/`) organized as layers by folder rather than separate assemblies:

- **`Domain/Models.cs`** — all entities are immutable `record`s plus enums. Single file; keep it that way. `InspectionStatus`, `ConditionStatus`, `PropertyType` are serialized as strings via a global `JsonStringEnumConverter` (registered in `Program.cs`).
- **`Application/Contracts.cs`** — the `IVistoraStore` abstraction (the entire data-access surface), request DTOs, and `TokenService`.
- **`Infrastructure/DemoVistoraStore.cs`** — the *only* `IVistoraStore` implementation: an in-memory singleton with hard-coded seed data for one company. Data does not persist across restarts.
- **`Controllers/`** — thin `[ApiController]`s using primary-constructor DI, one per resource: `AuthController` (`/api/auth/*`), `DashboardController`, `PropertiesController`, `TemplatesController`, `InspectionsController` (items + evidence), `OccurrencesController`. All but `AuthController` inherit `VistoraApiControllerBase` for `CurrentActor` and the `PagedOk` pagination helper.

Key design fact: swapping the demo store for a real EF Core/PostgreSQL backend means implementing `IVistoraStore` and re-registering it in `Program.cs` — nothing else should need to change. When adding an endpoint, extend `IVistoraStore` + `DemoVistoraStore` + the relevant controller together.

### Current shortcuts (demo-grade, not production)

These are intentional and documented in the README's "Evolução para produção" section — do not assume they are real security:

- **Auth tokens** (`TokenService`) are **signed JWT (HS256)** using `Jwt:Key`; a middleware in `Program.cs` validates the Bearer token on every `/api/*` route except `/api/auth/*` and puts the `AppUser` in `HttpContext.Items["user"]`. Refresh tokens are issued but not persisted. `TokenService`'s constructor never falls back to a hardcoded signing key: outside `Development` it throws at startup if `Jwt:Key` (32+ chars) isn't configured; inside `Development` with no `Jwt:Key` set, it generates a random ephemeral key for that process only (logged as a warning, tokens don't survive a restart).
- **Login rate limiting**: `/api/auth/login` is capped at 5 attempts/minute per IP via ASP.NET Core's built-in `RateLimiter` (policy `"login"`, `Program.cs`).
- **Role checks**: `RequireRoleAttribute` (`Application/Authorization.cs`) reads `HttpContext.Items["user"]` and returns 403 if the user's `Role` isn't in the allowed list. Applied to property/template mutation endpoints (`Administrador`-only) in `VistoraController`. There is still only one seed role (`Administrador`) — extend the seed data before relying on this for a second role.
- **Passwords** are hashed with **PBKDF2** (`PasswordHasher`, SHA-256 / 120k iterations / per-user salt), verified in constant time. Seed users are hashed at startup.
- **Evidence integrity hash** (`DemoVistoraStore.ComputeIntegrityHash`) is computed only over the decoded image bytes (never a timestamp) and stored in full (`SHA-256:<64 hex chars>`), so it's reproducible/verifiable later against the same `DataUrl` — it does not, by itself, prove *when* the photo was taken, only that its bytes haven't changed since capture.
- **Persistence** is still the in-memory `DemoVistoraStore` (now thread-safe via a lock). Inspection items, evidence, templates, and occurrences are stored per-inspection and mutate at runtime; data does not survive a restart.
- **Multi-tenancy**: every entity carries `CompanyId`, but there is no tenant filtering — the store holds a single company.
- CORS is wide open (`AllowAnyOrigin`).

When asked to "productionize," the intended path is: ASP.NET Identity, signed JWT + persisted refresh tokens, EF Core/Npgsql with a global `CompanyId` filter, FluentValidation/MediatR, blob storage for evidence, and per-company plan limits (`Company.UserLimit`/`PropertyLimit`).

## Conventions

- C# with `Nullable` and `ImplicitUsings` enabled. Style here is terse: single-line expression-bodied controller actions, collection expressions (`[...]`), primary constructors. Match it.
- Not a git repository. There is no VCS history or CI here.
