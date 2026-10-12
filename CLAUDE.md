# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

Reelr is a social film diary (like Letterboxd): ASP.NET Core API (.NET 10, EF Core, SQL Server) + React 19 SPA (Vite, Tailwind 4, React Router, TanStack Query). Film data comes from TMDB. See README.md for features and setup.

## Commands

Backend (run from repo root):

```bash
dotnet build backend/backend.slnx
dotnet test backend/backend.slnx                                             # all integration tests
dotnet test backend/backend.slnx --filter "FullyQualifiedName~RefreshTokenTests"   # one class
dotnet test backend/backend.slnx --filter "FullyQualifiedName~RefreshTokenTests.Name_Of_Test"  # one test
dotnet run --project backend/backend.csproj --launch-profile http           # API on http://localhost:5084
dotnet ef migrations add <Name> --project backend/backend.csproj
dotnet ef database update --project backend/backend.csproj
```

Tests need a real SQL Server. Default is `.\SQLEXPRESS`; override with `REELR_TEST_SQLSERVER` (a connection string without `Database=`; the factory appends a per-class throwaway database).

Frontend (in `frontend/`): `npm run dev` (http://localhost:5173, proxies `/api` and `/auth` to the API), `npm run lint`, `npm run build` (runs `tsc -b`, so it is also the type check). There are no frontend unit tests.

Full stack: `docker compose up --build` (site http://localhost:8080, API :5084, Mailpit :8025). Needs a root `.env` (see README).

End-to-end (Playwright, in `e2e/`, runs against the Docker stack with a fake TMDB):

```bash
docker compose -p reelr-e2e -f docker-compose.yml -f docker-compose.e2e.yml up -d --build --wait
cd e2e && npm test                        # all specs
cd e2e && npx playwright test journey.spec.ts -g "<test name>"
```

The e2e tests read confirmation emails through Mailpit's API (`E2E_MAILPIT_URL`); base URL is `E2E_BASE_URL` (default http://localhost:8080). The fake TMDB is `e2e/fake-tmdb/server.mjs`, wired in through `Tmdb__BaseUrl`.

CI (`.github/workflows/ci.yml`) runs backend build + tests against a SQL Server container, and frontend lint + build. It does not run e2e.

## Architecture

**Backend feature slices.** Each folder in `backend/Features/` holds that feature's controller, entities and DTOs together. Cross-feature infrastructure also lives there (`Paging`, `Observability`, `Email`). `backend/Data/ReelrContext.cs` is the single DbContext; all wiring is in `backend/Program.cs`.

Central services that hold rules shared by several endpoints — change rules here, not in controllers:
- `Diary/JournalService` — watched/rating/review/watchlist interact (e.g. rating or watching removes from watchlist).
- `Movies/MovieCatalog` — films are stored only when a member interacts with one; fetches from TMDB on demand and handles concurrent creation of the same film. `TmdbService` is the typed HttpClient (with resilience handler and `TmdbCache`); TMDB outages map to ProblemDetails via `TmdbExceptionHandler`.
- `Users/UserVisibility`, `Users/BlockRules` — who can see what, and what blocked members cannot do.
- `Activity/IActivityFeed` — implemented by `DerivedActivityFeed` from existing tables with cursor paging.
- `Moderation/AdminAccess` — admins are user ids in config `Admin:UserIds`, enforced via an authorization policy.

Conventions:
- Paged endpoints take `?cursor=&limit=` (default 30, max 100) and return `{ items, nextCursor }` (`Features/Paging`).
- Errors are `application/problem+json` with a `correlationId`. Unique-constraint violations become 409 via `UniqueConflictExceptionHandler`.
- Logs: Serilog, JSON lines outside Development. Account events go through `Auth/SecurityLog` with user ids only.

**Auth.** ASP.NET Core Identity + 15-minute JWT access tokens. The refresh token is a rotating `HttpOnly`, `Secure`, `SameSite=Strict` cookie scoped to `/auth`; `POST /auth/refresh` requires the `X-Reelr-Csrf` header (`RequireCsrfHeaderAttribute`). Reuse of an old refresh cookie ends that sign-in. Revocation ("log out everywhere", password change) works through the Identity security stamp (`SecurityStamps`). Rate limiting policies are in `Auth/RateLimitPolicies`, plus per-account `LoginThrottle`. Forwarded headers are trusted only from the configured compose subnet.

**Frontend.** `src/lib/api.ts` is the only fetch layer: it keeps the access token in memory only, refreshes it via the cookie (one shared request per tab, serialized across tabs with a Web Lock) and retries on 401. `src/lib/queries.ts` holds the TanStack Query hooks; `src/lib/types.ts` mirrors the API DTOs. `src/auth/` provides `AuthProvider` and `RequireAuth`. Pages are in `src/pages/`, shared UI in `src/components/`. Accessibility matters here: keep focus management (`lib/focus.ts`) and keyboard behaviour intact when changing dialogs, tabs and inline forms.

**Tests.** `tests/backend.Tests` mirrors the feature folders. `Infrastructure/ReelrApiFactory` runs the real API in memory (`WebApplicationFactory<Program>`) against a fresh SQL Server database per test class, with `FakeTmdbHandler` and `FakeEmailSender`. Rate limits and the TMDB cache are effectively off by default; tests that need them override the factory's virtual properties. SQLite is not used because some queries need SQL `APPLY`.

## Workflow

Branches: `feature/*` merge into `develop`, which goes to `main`. Keep commits small and focused, using Conventional Commit prefixes (`feat:`, `fix(a11y):`, ...).
