# Development guide

Practical instructions for running, building and changing Reelr locally. Every command below appears in a project file (`README.md`, `CLAUDE.md`, `package.json`, `.github/workflows/ci.yml`, a Dockerfile or a compose file) or follows directly from one; anything that could not be checked is marked.

## Required tools

| Tool | Version | Evidence |
|---|---|---|
| .NET SDK | 10.0 | `TargetFramework` `net10.0` in `backend/backend.csproj`; `dotnet-version: 10.0.x` in CI |
| `dotnet-ef` | 10.0.12 | `backend/dotnet-tools.json` (local tool manifest) |
| Node.js | 24 | `node:24-alpine` in `frontend/Dockerfile`; `node-version: 24` in CI |
| npm | bundled with Node | `package-lock.json` in `frontend/` and `e2e/` |
| SQL Server | 2022 in Docker, or SQL Server Express locally | `docker-compose.yml`, `backend/appsettings.Development.json` |
| Docker Desktop with Compose | any recent version (not pinned) | `docker-compose.yml` |
| Azure CLI, PowerShell | only for deploying | `infra/deploy.ps1` |

A TMDB API Read Access Token is needed to run the app against real film data (free from TMDB's API settings page). The end-to-end stack uses a fake TMDB and needs none.

## Repository setup

```bash
git clone https://github.com/thesuperfalcon/Reelr.git
cd Reelr
```

Branches: `feature/*` merge into `develop`, which goes to `main` (`CLAUDE.md`). CI runs on pushes and pull requests to both. Deploys run from `main`.

## Option 1: Docker Compose

1. Create `.env` in the repository root (git-ignored). The header comment in `docker-compose.yml` defines what it must contain:

   | Variable | Content |
   |---|---|
   | `MSSQL_SA_PASSWORD` | SQL Server `sa` password: 8 or more characters with upper and lower case, a digit and a symbol |
   | `ConnectionStrings__ReelrContext` | Connection string to host `db`, database `Reelr`, user `sa`, the password above, `TrustServerCertificate=True` |
   | `Jwt__Key` | Random string of at least 32 bytes |
   | `TMDB_READ_ACCESS_TOKEN` | Your TMDB token |

   Optional: `Admin__UserIds__0=<user id>` to make an account an admin.

2. Start the stack:

   ```bash
   docker compose up --build
   ```

3. Services, all bound to `127.0.0.1`:

   | URL | Service |
   |---|---|
   | http://localhost:8080 | Site (nginx serving the SPA and proxying `/api` and `/auth`) |
   | http://localhost:5084 | API directly; Swagger UI at `/swagger` (the backend runs in Development) |
   | http://localhost:8025 | Mailpit inbox for confirmation and reset emails |
   | localhost:1433 | SQL Server |

The `migrate` service applies migrations with an EF bundle before the backend starts. Data persists in the `sqldata` volume; `docker compose down -v` removes it.

## Option 2: Run the parts locally

1. Install the EF Core tool. Either globally, as the root README does:

   ```bash
   dotnet tool install --global dotnet-ef
   ```

   or from the local manifest, as CI does (run in `backend/`):

   ```bash
   dotnet tool restore
   ```

   With the local tool, run `dotnet ef ...` from inside `backend/`.

2. Store secrets with .NET user secrets (the project has a `UserSecretsId`):

   ```bash
   dotnet user-secrets set "Jwt:Key" "<random string, 32+ characters>" --project backend/backend.csproj
   dotnet user-secrets set "TMDB_READ_ACCESS_TOKEN" "<your TMDB token>" --project backend/backend.csproj
   ```

   `backend/appsettings.Development.json` already provides `App:BaseUrl` (http://localhost:5173), `Email:*` for Mailpit on port 1025, `Registration:InviteOnly=false` and a connection string to `.\SQLEXPRESS` with Windows authentication. Override the connection string with user secrets if your SQL Server differs.

3. Create or update the database:

   ```bash
   dotnet restore backend/backend.csproj
   dotnet ef database update --project backend/backend.csproj
   ```

4. Start the API on http://localhost:5084:

   ```bash
   dotnet run --project backend/backend.csproj --launch-profile http
   ```

5. Optional, to receive emails:

   ```bash
   docker run --rm -p 1025:1025 -p 8025:8025 axllent/mailpit:v1.31.2
   ```

   Without it the API works, but `EmailDispatcher` logs send failures and confirmation emails never arrive. To confirm an account without email, read the link from Mailpit or set `EmailConfirmed` in the database (not scripted in the repository).

6. Start the frontend on http://localhost:5173 (Vite proxies `/api` and `/auth` to port 5084):

   ```bash
   cd frontend
   npm install
   npm run dev
   ```

## Configuration reference

ASP.NET Core configuration: `appsettings.json`, then `appsettings.{Environment}.json`, user secrets (Development), environment variables (`__` for `:`). The app checks the first five keys at start-up and refuses to run without them.

| Key | Required | Default | Purpose |
|---|---|---|---|
| `ConnectionStrings:ReelrContext` | yes | Development: SQL Express | Database |
| `Jwt:Key` | yes, 32+ bytes | none | HMAC signing key |
| `TMDB_READ_ACCESS_TOKEN` | yes | none | TMDB bearer token |
| `Email:Host`, `Email:FromAddress` | yes | Development: Mailpit | SMTP |
| `App:BaseUrl` | yes | Development: http://localhost:5173 | Links in emails and invites |
| `Jwt:Issuer`, `Jwt:Audience` | | `Reelr` | Token validation |
| `Jwt:ExpiryMinutes`, `Jwt:RefreshDays` | | 15, 30 | Token lifetimes |
| `Email:Port`, `Email:Security`, `Email:UserName`, `Email:Password`, `Email:FromName` | | 587, `StartTls`, empty, empty, `Reelr` | SMTP |
| `Registration:InviteOnly` | | true (false in `appsettings.Development.json`) | Invite-only sign-up |
| `Admin:UserIds`, `Demo:UserIds` | | empty | Admin and demo account ids (arrays: `Admin__UserIds__0`, ...) |
| `ForwardedHeaders:KnownNetworks`, `ForwardedHeaders:ForwardLimit` | | empty, 1 | Trusted proxies |
| `RateLimiting:<Policy>:PermitLimit`, `WindowSeconds` | | see [API_REFERENCE.md](API_REFERENCE.md#rate-limits) | Rate limits; `LoginPerAccount` for the login throttle |
| `Tmdb:BaseUrl`, `Tmdb:CacheSizeMegabytes`, `Tmdb:Resilience:*` | | TMDB v3, 50, see [INTEGRATIONS.md](INTEGRATIONS.md#tmdb) | TMDB client |
| `MovieRefresh:*` | | enabled, 6 h, 30 days, 100 | Maintenance loop: film refresh and refresh-token cleanup |
| `Activity:Implementation`, `CommunityDays`, `FollowingDays`, `GroupWindowMinutes`, `CommunityCacheSeconds` | | `Derived`, 30, 365, 60, 60 | Feed tuning |
| `Serilog:*` | | Information, framework noise at Warning | Log levels, for example `Serilog__MinimumLevel__Default=Debug` |

Frontend configuration is limited to the proxy target in `frontend/vite.config.ts` (`http://localhost:5084`) and `BACKEND_URL` for the nginx container (default `http://backend:8080`).

## Secret management

- Never commit secrets. `.env`, `*.local`, `appsettings.Local.json` and `appsettings.*.Local.json` are git-ignored (`.gitignore`).
- Locally: user secrets for `dotnet run`, `.env` for Docker Compose.
- CI: no repository secrets. The backend and end-to-end jobs make up a SQL Server password per run, and the end-to-end job generates a random JWT key, so pull requests without secrets (Dependabot, forks) are tested too.
- Azure: Key Vault (`jwt-key` generated by `infra/deploy.ps1`; `tmdb-token` and `email-password` entered at a hidden prompt). The database needs no password (managed identities).
- `.gitguardian.yaml` ignores `tests/**` and `e2e/tests/**`, which contain made-up test passwords.
- The Postman environment's `password` variable is empty in the repository; set it locally.

## Database and migrations

| Task | Command |
|---|---|
| Add a migration | `dotnet ef migrations add <Name> --project backend/backend.csproj` |
| Apply migrations locally | `dotnet ef database update --project backend/backend.csproj` |
| Check the model matches the migrations (as CI does, in `backend/`) | `dotnet ef migrations has-pending-model-changes` |
| List migrations (in `backend/`) | `dotnet ef migrations list` |

The API never migrates at start-up. Review generated migrations before committing; CI also applies every migration to an empty database. See [DATA_MODEL.md](DATA_MODEL.md#migrations).

### Backward-compatible migrations

Every migration must work with the previous version of the backend. The deploy runs migrations before the new backend starts, and a rollback (below) runs the older backend against the newer schema, because migrations are never rolled back.

- Add columns as nullable or with a default, so the older backend can still insert rows without them.
- Remove or rename a column or table in two releases: first stop using it in the code, then drop or rename it in a later release.
- Do not change a column's type or tighten a constraint in a way the older backend's writes would break.

## Running tests

Summary; [TESTING.md](TESTING.md) has details.

```bash
dotnet test backend/backend.slnx
```

```bash
cd frontend && npm test
```

```bash
docker compose -p reelr-e2e -f docker-compose.yml -f docker-compose.e2e.yml up -d --build --wait
```

```bash
cd e2e && npm ci && npx playwright install chromium && npx playwright test
```

Backend tests use `.\SQLEXPRESS` unless `REELR_TEST_SQLSERVER` is set to a connection string without `Database=`.

## Building

| Target | Command | Notes |
|---|---|---|
| Backend and tests | `dotnet build backend/backend.slnx` | CI adds `--configuration Release` |
| Frontend | `cd frontend && npm run build` | Runs `tsc -b` (type check) then `vite build` into `frontend/dist/` |
| Backend image | `docker build backend` | `runtime` stage by default |
| Migrations image | `docker build --target migrations backend` | EF bundle |
| Frontend image | `docker build frontend` | nginx with the built SPA |

The Docker commands follow from the Dockerfiles and compose files; they were not run while writing this guide.

## Linting and formatting

- Frontend: `cd frontend && npm run lint` (ESLint 10 with `typescript-eslint`, `react-hooks` and `react-refresh` rules, `frontend/eslint.config.js`). CI runs it.
- Type checking: `npm run build` (`tsc -b`).
- Backend: no analyser or formatter is configured beyond the SDK defaults with `Nullable` enabled. `.editorconfig` sets UTF-8, LF line endings, 4-space indentation, a final newline and trimmed trailing whitespace for all files. No Prettier or `dotnet format` step exists in CI.
- Claude Code: `.claude/settings.json` runs `.claude/hooks/eslint-file.mjs` after each edit to a `frontend/src` TypeScript file.

## Deploying

Deployment to Azure is described in the root [README.md](../README.md#deploying-to-azure) and [INTEGRATIONS.md](INTEGRATIONS.md#azure-hosting). In short: `infra/deploy.ps1` for the first setup, `infra/setup-github.ps1` for the workflow identity, then every successful CI run on `main` deploys through `.github/workflows/deploy.yml`.

To roll back, open Actions > Deploy > Run workflow on `main` and enter the full commit SHA to go back to in `sha`. The run builds and rolls out that commit's images and template. Its migration step applies nothing, since the database already has every migration of that commit, and it does not undo newer ones. That is why migrations must be backward-compatible (see [Backward-compatible migrations](#backward-compatible-migrations)). An empty `sha` deploys the commit the run starts on.

## Common problems

| Symptom | Cause and fix |
|---|---|
| API exits at start-up with "Jwt:Key must be set and at least 32 bytes long", "TMDB_READ_ACCESS_TOKEN is not configured" or "`<key>` is not configured" | A required setting is missing (`backend/Program.cs`). Set it with user secrets or `.env`. Outside Development, `App:BaseUrl` and `Email:*` are not provided by any appsettings file. |
| Sign-in works on localhost but not from another device on the network | The refresh cookie is `Secure`; browsers accept it over plain HTTP only on `localhost`. Use HTTPS or localhost. |
| Every page logs you out after reload | Same cause, or the `/auth/refresh` request lacks the `X-Reelr-Csrf` header (only if you wrote your own client). |
| Login answers 403 "Confirm your email address first" | Open the confirmation link from Mailpit (http://localhost:8025). |
| Registration fails with an invite error | `Registration:InviteOnly` is true outside Development. Set it to false to create the first account, then create invites as an admin. |
| Admin page shows nothing or 403 | Add your user id under `Admin:UserIds` and restart the backend. |
| 429 Too Many Requests while testing by hand | Registration allows 5 per hour per IP and login 10 per minute. Raise `RateLimiting__Register__PermitLimit` and similar, as `docker-compose.e2e.yml` does. |
| Film pages return 502 or 503 | TMDB rejected the token or is unreachable. Check the token and the API log (`TmdbService` warnings). |
| Backend tests fail to connect | No SQL Server at `.\SQLEXPRESS`. Set `REELR_TEST_SQLSERVER`. |
| `docker compose` port already in use | The everyday stack and the `reelr-e2e` stack use the same ports. Stop one first. |
| `dotnet ef` cannot find the tool | Install it globally, or run `dotnet tool restore` and `dotnet ef` from `backend/`. |
| Schema errors after pulling | Run `dotnet ef database update`, or rebuild the Compose stack so `migrate` runs. |
