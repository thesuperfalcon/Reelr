# Reelr

[![CI](https://github.com/thesuperfalcon/Reelr/actions/workflows/ci.yml/badge.svg?branch=develop)](https://github.com/thesuperfalcon/Reelr/actions/workflows/ci.yml)

Reelr is a social film diary, in the spirit of Letterboxd. You log the films you watch, rate and review them, keep a watchlist, build lists, and follow other people to see what they are watching. Film data comes from [TMDB](https://www.themoviedb.org/).

## Features

**Films and people**
- Trending and popular films on the start page.
- One search for films, cast, crew, studios and Reelr members.
- Film pages with synopsis, cast and crew, trailer, similar films and member reviews.
- Person pages with full filmography.

**Your film diary**
- Mark films as watched and rate them from half a star to 5 stars in half steps.
- Diary entries with watch date, rating, like and rewatch flag. Entries can be edited and deleted.
- Markdown reviews with a spoiler flag, linked to a diary entry or standalone.
- Watchlist. A film leaves the watchlist automatically when you rate it or mark it as watched.
- Public or private lists of films.

**Social**
- Follow members and see their diary, reviews, lists and watchlist.
- Activity feed with a Following tab and a Community tab.
- Likes and comments on reviews.
- Watchlist visibility: public, followers only or private.
- Profile settings with avatar upload, password change and "log out everywhere".
- Email confirmation on sign-up and password reset by email.
- Diary visibility (everyone, followers or only you), data export and account deletion.

**Safety**
- Report reviews, comments and lists; admins remove or dismiss them on a moderation page.
- Block members: they cannot follow you, comment on or like your reviews, and you stop seeing them.

## Tech stack

| Area | Technology |
|---|---|
| Backend | .NET 10, ASP.NET Core Web API, Entity Framework Core 10, SQL Server |
| Auth | ASP.NET Core Identity, 15-minute JWT access tokens kept in memory, rotating refresh tokens in an HttpOnly cookie, revocation through the security stamp, email confirmation, password reset, lockout and rate limiting |
| Email | SMTP through MailKit; Mailpit catches mail in development |
| Frontend | React 19, TypeScript, Vite, Tailwind CSS 4, React Router, TanStack Query |
| Tests | xUnit integration tests with `WebApplicationFactory` against a real SQL Server and a fake TMDB |
| Infrastructure | Docker Compose (SQL Server, API, nginx), GitHub Actions CI |
| External API | TMDB |

## Architecture

```
React SPA ──► nginx ──► ASP.NET Core API ──► SQL Server
                              │
                              └──► TMDB API
```

The backend is organised as **feature slices**. Each folder in `backend/Features/` (Movies, Diary, Reviews, MovieLists, Users, Activity and so on) holds its controller, entities and DTOs together, so a feature can be read and changed in one place.

A few design decisions:

- **TMDB is the source of film data.** The database stores a film only when a member interacts with it (rating, diary entry, review, list or watchlist). `MovieCatalog` fetches and stores films on demand and handles two requests creating the same film at once. A background service copies each stored film from TMDB again once it is 30 days old (`MovieRefresh` settings).
- **One instance for now.** The TMDB cache, rate limits, login throttling and security stamp cache are kept in memory, so they belong to one app instance. Running several instances would need Redis (or another shared store) for these first.
- **Journal rules live in one service.** Watching, rating, reviewing and the watchlist depend on each other. `JournalService` keeps those rules in one place so every endpoint follows them the same way.
- **The activity feed sits behind an interface.** `IActivityFeed` is built from existing tables today, with cursor pagination. It can be replaced with a stored event table without changing the controller.
- **One place decides visibility.** `UserVisibility` decides who can see a member's watchlist.
- **Errors use ProblemDetails.** Unhandled exceptions and empty error responses return `application/problem+json`, with a `correlationId` a user can quote. `GET /health/live` answers while the process runs; `GET /health/ready` checks the database (503 when it is down) and TMDB (reported as `Degraded`, still 200).
- **Logs are JSON lines.** Serilog writes one JSON object per line to the console outside Development (readable text in Development); levels are set in the `Serilog` section, e.g. `Serilog__MinimumLevel__Default=Debug`. Every request is logged once without its query string, and every line of a request carries a `CorrelationId` that matches `request_id` in the nginx access log. Account events (logins, password changes, deletions, admin actions) are logged under `backend.Features.Auth.SecurityLog` with user ids only.
- **Sign-in is a cookie, not a stored token.** Login sets a refresh token in a `Secure`, `HttpOnly`, `SameSite=Strict` cookie on `/auth`; the page keeps only a short access token in memory and gets a new one from `POST /auth/refresh` (which needs the `X-Reelr-Csrf` header). Each refresh replaces the cookie, and an old cookie that comes back ends that sign-in. The cookie is `Secure`, so outside `localhost` the site must be served over HTTPS.
- **Lists are paged.** List endpoints take `?cursor=&limit=` (default 30, max 100) and return `{ items, nextCursor }`.

```
backend/          ASP.NET Core API (Features/, Data/, Migrations/)
frontend/         React app (pages/, components/, lib/)
tests/            Backend integration tests
.github/workflows CI pipeline
```

## Getting started

### Option 1: Docker (recommended)

Requires Docker Desktop.

1. Create a `.env` file in the repository root:

   ```
   MSSQL_SA_PASSWORD=<strong password: 8+ chars, upper, lower, digit, symbol>
   ConnectionStrings__ReelrContext=Server=db;Database=Reelr;User Id=sa;Password=<same password>;TrustServerCertificate=True;
   Jwt__Key=<random string, 32+ characters>
   TMDB_READ_ACCESS_TOKEN=<your TMDB API Read Access Token>
   ```

   A TMDB token is free from [TMDB's API settings](https://www.themoviedb.org/settings/api).

2. Start everything:

   ```bash
   docker compose up --build
   ```

3. Open http://localhost:8080. The API is on http://localhost:5084, with Swagger at `/swagger`.
   Emails (sign-up confirmation, password reset) are caught by Mailpit at http://localhost:8025; none reach a real inbox.

A one-off `migrate` service applies the database migrations (an EF Core migrations bundle) before the backend starts; the app itself never changes the schema. All ports are bound to `127.0.0.1`.

To make an account an admin (the moderation page), add its user id to `.env` and restart the backend:

```
Admin__UserIds__0=<user id>
```

### Option 2: Run locally

Requires the .NET 10 SDK, Node.js 24 and SQL Server (for example SQL Server Express).

1. Install the EF Core tool:

   ```bash
   dotnet tool install --global dotnet-ef
   ```

2. Set the secrets for the API:

   ```bash
   dotnet user-secrets set "Jwt:Key" "<random string, 32+ characters>" --project backend/backend.csproj
   dotnet user-secrets set "TMDB_READ_ACCESS_TOKEN" "<your TMDB token>" --project backend/backend.csproj
   ```

   The default connection string in `backend/appsettings.Development.json` points to `.\SQLEXPRESS` with Windows authentication.

3. Create the database:

   ```bash
   dotnet restore backend/backend.csproj
   dotnet ef database update --project backend/backend.csproj
   ```

4. Start the API on http://localhost:5084:

   ```bash
   dotnet run --project backend/backend.csproj --launch-profile http
   ```

5. Emails go to an SMTP server on `localhost:1025` (see `backend/appsettings.Development.json`). To read them, run Mailpit and open http://localhost:8025:

   ```bash
   docker run --rm -p 1025:1025 -p 8025:8025 axllent/mailpit:v1.31.2
   ```

   Without it the API still works, but sign-up confirmation and password reset emails are not delivered.

6. Start the frontend on http://localhost:5173. Vite proxies `/api` and `/auth` to the API.

   ```bash
   cd frontend
   npm install
   npm run dev
   ```

## Tests

The integration tests run the whole API in memory against a throwaway SQL Server database and a fake TMDB, so they test real HTTP behaviour and real SQL.

```bash
dotnet test backend/backend.slnx
```

By default the tests use `.\SQLEXPRESS`. To use another server, set `REELR_TEST_SQLSERVER` to a connection string without a database name:

```powershell
$env:REELR_TEST_SQLSERVER = "Server=(localdb)\MSSQLLocalDB;Trusted_Connection=True;TrustServerCertificate=True;"
```

### End-to-end tests

`e2e/` holds Playwright tests that use the real site the way a member would: sign up, confirm the email from Mailpit, log a film with a review, follow someone and delete the account. They run against the Docker stack with a fake TMDB (`e2e/fake-tmdb`), so they need no TMDB account:

```bash
docker compose -p reelr-e2e -f docker-compose.yml -f docker-compose.e2e.yml up -d --build --wait
cd e2e && npm ci && npx playwright install chromium && npx playwright test
docker compose -p reelr-e2e -f docker-compose.yml -f docker-compose.e2e.yml down -v
```

The `reelr-e2e` project name keeps its database apart from the everyday Docker stack. Stop the everyday stack first, since both use the same ports.

CI runs the backend tests on every push and pull request to `main` and `develop`, against SQL Server in a container. It also lints and builds the frontend, and runs the end-to-end tests against the Docker stack.

## API

Swagger UI is available at `/swagger` in Development. A Postman collection and environment are in the repository root: `Reelr.postman_collection.json` and `Reelr.postman_environment.json`. Set the environment's `password` variable in Postman before you register or log in; it is empty in the repository.

## Attribution

This product uses the TMDB API but is not endorsed or certified by TMDB.

## Deploying to Azure

The beta runs in Azure Container Apps with Azure SQL (free offer) and Key Vault in Sweden Central. `infra/` describes all of it as Bicep. There are no passwords for the database: the backend and the migration job sign in with managed identities, and the secrets (JWT key, TMDB token, SMTP key) live in Key Vault.

First-time setup, with the Azure CLI signed in and the .NET 10 SDK installed:

```powershell
./infra/deploy.ps1 -EmailUserName '<smtp login>' -EmailFromAddress '<verified sender>'
./infra/setup-github.ps1 -EmailUserName '<smtp login>' -EmailFromAddress '<verified sender>'
```

`deploy.ps1` creates the resources, asks for the TMDB token and the SMTP key (hidden input) and creates the database users. `setup-github.ps1` lets the Deploy workflow sign in to Azure with OIDC and prints the repository variables it needs.

After that, `.github/workflows/deploy.yml` deploys every commit on `main` that passes CI: it pushes the images to GitHub Container Registry, runs the migrations as a Container Apps job and rolls out the new images. The images must be public on GitHub so Azure can pull them.

**Invites and demo accounts.** Registration is invite only: an admin makes a one-time code on the Admin page and sends its sign-up link (`/register?invite=…`). Demo accounts, listed under `Demo:UserIds`, can do everything a member does except change their name, password or picture, sign out other devices or delete the account, so a shared login for interviews stays as it was. To create the first accounts on an empty database:

1. Run `deploy.ps1` with `-OpenRegistration` and register your own account and the demo account.
2. Run it again with `-AdminUserIds '<your id>' -DemoUserIds '<demo id>'` and without `-OpenRegistration`, which closes registration.
3. Set the same ids as the repository variables `REELR_ADMIN_USER_IDS` and `REELR_DEMO_USER_IDS`, so the Deploy workflow keeps them.

**Backups and alerts.** Azure SQL keeps point-in-time backups for 7 days. A restore goes to a new database next to the live one (`az sql db restore -g rg-reelr -s <server> -n reelr --dest-name reelr-restored --time <UTC time>`); point the connection strings at it, or copy back what was lost, then delete it. This was tried on 2026-10-12: the copy had the same accounts and migrations as the live database. With `-AlertEmail` (and the variable `REELR_ALERT_EMAIL` for the Deploy workflow), Azure Monitor emails that address when the backend answers with more than 5 server errors in 15 minutes, restarts more than twice in 30 minutes, or when less than 20 % of the database's monthly free amount is left (the database pauses until the 1st of the next month once it runs out). Those alerts need a request to reach the backend, so an external UptimeRobot monitor (free account, not in this repository) also checks `https://reelr.se/` every 5 minutes for status 200 and the word `Reelr`, warns before the certificate expires and emails the same address; see [docs/INTEGRATIONS.md](docs/INTEGRATIONS.md#azure-hosting).

**Own domain.** The beta runs at https://reelr.se (registered at Loopia, DNS at Cloudflare, "DNS only"): a CNAME to the app's default address and a TXT record `asuid.<name>` with the environment's custom domain verification id. With `-CustomDomain` (and the variable `REELR_CUSTOM_DOMAIN`), the template binds it with a free managed certificate that Azure renews, and account emails link to it.

The backend and the frontend each keep one replica warm (`backendMinReplicas` and `frontendMinReplicas`, both 1 by default; 0 lets them sleep). The database still pauses after an idle hour, so the first visit after a quiet spell can take up to a minute while it resumes; pages show the header at once and wait for it under "Signing in". Background film refreshes wait until the app uses the database anyway, so a warm backend does not wake the database and use up the free amount.

## Before going live

The privacy policy and terms (`frontend/src/pages/LegalPages.tsx`) describe what the code and the Azure setup do, for the closed beta. They have not been reviewed by a lawyer; have them reviewed before opening to the public, and keep them in step when what is stored, where or for how long changes.
