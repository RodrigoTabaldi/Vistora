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
- No test project exists yet.
- Demo login: `admin@atelierimoveis.com.br` / `Vistora@2026`.

## Architecture

Single project (`SaasVistoria/`) organized as layers by folder rather than separate assemblies:

- **`Domain/Models.cs`** — all entities are immutable `record`s plus enums. Single file; keep it that way. All enums are serialized as strings via a global `JsonStringEnumConverter` (registered in `Program.cs`).
- **`Application/Contracts.cs`** — the `IVistoraStore` abstraction (the entire data-access surface), request DTOs, `TokenService`, `PasswordHasher` and `Permissions` (role → granular permission map).
- **`Application/InspectionServices.cs`** — business logic that does not belong to the store: `InspectionRules` (blocking/warning validation before completing or issuing a report), `ComparisonService` (entry × exit diff) and `ReportRenderer` (printable HTML report + integrity hash).
- **`Application/RequirePermission.cs`** — `[RequirePermission(Permissions.X)]` authorization filter reading `HttpContext.Items["user"]`.
- **`Infrastructure/DemoVistoraStore.cs`** + **`.Fluxo.cs`** — the *only* `IVistoraStore` implementation, split across two files of one `partial` class: the original file holds properties/inspections/items/evidence/templates; `.Fluxo.cs` holds people, contracts, meters, keys, inventory, check-in, reports, signatures and contestations. In-memory singleton, one company, no persistence across restarts.
- **`Controllers/`** — thin `[ApiController]`s using primary-constructor DI: `AuthController` (`/api/auth/*`), `VistoraController` (properties, inspections, checklist, evidence, occurrences), `FluxoController` (parties, contracts, meters, keys, inventory, check-in/out, validation, comparison), `LaudoController` (versioned reports, signature invites, contestations) and `PublicoController` (`/api/publico/*`, unauthenticated).
- **`Pages/`** — Razor Pages hold the HTML shell: `Index.cshtml` (`/`, the app) and `Assinar.cshtml` (`/assinar`, public signing), composed from partials in `Pages/Shared/` (`_Layout`, `_IconSprite`, `_LoginScreen`, `_AppHeader`, `_MobileNav`, `_Modals`). There is no `index.html` any more — `MapFallbackToPage("/Index")` serves unknown routes.
- **`wwwroot/`** — behaviour and styling only: `app.css` (design system), `app.js` (core: offline queue + cache, dialogs, navigation, dashboard, checklist), `vistoria.js` (field panels, reports, signatures, comparison, contestations), `assinar.js` (public page), `sw.js`. The JS files are classic scripts sharing one global scope, loaded in that order.

### Sistema visual

`app.css` is organized as tokens → base → utilities → components → screens → responsive. Use the tokens (`--sp-*`, `--fs-*`, `--r-*`, `--dur-*`, semantic colors) instead of raw values; every text/background pair in the file was checked for WCAG 2.2 AA contrast. Icons come from the SVG sprite in `_IconSprite.cshtml` via `icon('nome')` in JS or `<use href="#i-nome">` in markup — never typographic glyphs, which broke across platforms and were read aloud by screen readers.

Non-negotiables when touching the UI: state is never signalled by colour alone; interactive targets stay ≥44px; dialogs keep the focus trap/Esc/return-focus contract in `openModal`/`closeModal`; form errors use `fieldError()` (aria-invalid + `[data-error-for]` + focus); animation is wrapped by `prefers-reduced-motion`.

Key design fact: swapping the demo store for a real EF Core/PostgreSQL backend means implementing `IVistoraStore` and re-registering it in `Program.cs` — nothing else should need to change. When adding an endpoint, extend `IVistoraStore` + `DemoVistoraStore` + the relevant controller together.

### Current shortcuts (demo-grade, not production)

These are intentional and documented in the README's "Evolução para produção" section — do not assume they are real security:

- **Auth tokens** (`TokenService`) are now **signed JWT (HS256)** using `Jwt:Key`; a middleware in `Program.cs` validates the Bearer token on every `/api/*` route except `/api/auth/*` and puts the `AppUser` in `HttpContext.Items["user"]`. Refresh tokens are issued but not persisted.
- **Passwords** are hashed with **PBKDF2** (`PasswordHasher`, SHA-256 / 120k iterations / per-user salt), verified in constant time. Seed users are hashed at startup.
- **Persistence** is still the in-memory `DemoVistoraStore` (now thread-safe via a lock). Inspection items, evidence, templates, and occurrences are stored per-inspection and mutate at runtime; data does not survive a restart.
- **Multi-tenancy**: every entity carries `CompanyId`, but there is no tenant filtering — the store holds a single company.
- CORS is wide open (`AllowAnyOrigin`).
- **Reports** are sealed HTML (print-to-PDF in the browser), not server-generated PDFs; the QR Code described in the spec is not drawn — only the public validation URL is printed.
- **Signature invites** return the link and OTP in the HTTP response because no e-mail/SMS/WhatsApp provider is wired up.
- **Offline** lives in `localStorage` (unencrypted): a write queue plus a per-path cache of GET responses, pre-warmed by `prefetchForField()` for open inspections so a vistoria can be opened and filled in with no signal. Photos captured offline are still not queued.
- **Service worker is network-first** for the shell (`sw.js`, cache `vistora-v2`), falling back to cache when offline. Do not switch it back to cache-first: it served stale JS against fresh HTML and left whole screens unresponsive. `vistoria.js` reloads the page once on `controllerchange` so a new version never runs half-applied.

### Regras de negócio que não podem regredir

`InspectionRules.Validate` is the single gate before completing a inspection or issuing a report. Blocking rules: exit inspection without a linked entry inspection, required item left unevaluated, damaged/high-severity item without a photo, empty checklist. Warnings: missing meter readings or keys on entry/exit, unevaluated items, inspection date more than 30 days from the contract term. A sealed report is never overwritten — `LaudoController.Emit` always creates the next version under the same document number.

When asked to "productionize," the intended path is: ASP.NET Identity, signed JWT + persisted refresh tokens, EF Core/Npgsql with a global `CompanyId` filter, FluentValidation/MediatR, blob storage for evidence, and per-company plan limits (`Company.UserLimit`/`PropertyLimit`).

## Conventions

- C# with `Nullable` and `ImplicitUsings` enabled. Style here is terse: single-line expression-bodied controller actions, collection expressions (`[...]`), primary constructors. Match it.
- Front-end is dependency-free vanilla JS in classic scripts (no bundler, no modules): `vistoria.js` reuses helpers declared in `app.js` (`$`, `api`, `esc`, `toast`, `openModal`) and registers new screens on the shared `extraViews` object.
- Git repository with history on `main`; there is no CI configured.
