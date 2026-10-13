# Reelr technical documentation

Reelr is a social film diary in the spirit of Letterboxd. Members log the films they watch, rate and review them, keep a watchlist, build lists of films and follow other members to see what they watch. Film data (titles, posters, cast, search) comes from the [TMDB](https://www.themoviedb.org/) API. Reelr stores only what members create, plus a local copy of each film someone has interacted with.

This folder is the technical knowledge base. The repository root [README.md](../README.md) is the shorter product and setup overview; [CLAUDE.md](../CLAUDE.md) is a compact guide for AI coding assistants. These documents were written from the code at commit `4540153` on `develop` (October 2026) and checked against it.

## Who uses it

The evidence in the repository points to a small, invite-only deployment:

- The privacy page says Reelr "is in a closed beta for invited members" (`frontend/src/pages/LegalPages.tsx`).
- Registration is invite-only by default (`Registration:InviteOnly`, see `backend/Features/Invites/InviteCodes.cs`).
- Shared demo accounts exist "for showing Reelr to someone, e.g. in a job interview" (`backend/Features/Users/DemoAccounts.cs`).

## Main features

| Area | What exists |
|---|---|
| Films and people | Trending and popular films, one search across films, cast, crew, studios and members, film pages with cast, crew, trailers and recommendations, person pages with filmography. All proxied from TMDB. |
| Diary | Mark as watched, rate (0.5 to 5 stars in half steps), diary entries with watch date, like and rewatch flags, Markdown reviews with a spoiler flag, watchlist, public or private lists. |
| Social | Follow members, activity feed (Following and Community), likes and comments on reviews, watchlist and diary visibility settings. |
| Account | Email confirmation, password reset, avatar upload, password change, "log out everywhere", data export, account deletion. |
| Safety | Reports on reviews, comments and lists, an admin moderation page, blocking, rate limits, login throttling. |

[PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) describes the features and their rules in detail.

## Technology

| Layer | Technology | Evidence |
|---|---|---|
| API | ASP.NET Core on .NET 10, controllers, EF Core 10, SQL Server | `backend/backend.csproj` |
| Auth | ASP.NET Core Identity, JWT bearer tokens, rotating refresh cookie | `backend/Program.cs`, `backend/Features/Auth/` |
| Email | MailKit over SMTP (Mailpit locally) | `backend/Features/Email/SmtpEmailSender.cs` |
| Images | SkiaSharp (avatar re-encoding to WebP) | `backend/Features/Settings/AvatarImage.cs` |
| Logging | Serilog | `backend/Features/Observability/Logging.cs` |
| SPA | React 19, TypeScript 6, Vite 8, Tailwind CSS 4, React Router 8, TanStack Query 5 | `frontend/package.json` |
| Hosting | Docker Compose locally; Azure Container Apps, Azure SQL and Key Vault in production | `docker-compose.yml`, `infra/` |
| Tests | xUnit integration tests, Vitest, Playwright | `tests/backend.Tests/`, `frontend/src/**/*.test.ts*`, `e2e/` |

## Quick start

Prerequisites: Docker Desktop, or the .NET 10 SDK, Node.js 24 and SQL Server (for example SQL Server Express). You also need a free TMDB API Read Access Token.

The fastest path is Docker Compose:

1. Create a `.env` file in the repository root with `MSSQL_SA_PASSWORD`, `ConnectionStrings__ReelrContext`, `Jwt__Key` and `TMDB_READ_ACCESS_TOKEN`. The comment at the top of `docker-compose.yml` lists what each one must contain.
2. Run the stack:

   ```bash
   docker compose up --build
   ```

3. Open http://localhost:8080. The API is on http://localhost:5084 (Swagger at `/swagger`), and Mailpit shows sent emails at http://localhost:8025.

To run the parts separately, and for every test command, see [DEVELOPMENT_GUIDE.md](DEVELOPMENT_GUIDE.md) and [TESTING.md](TESTING.md).

## Documents

| Document | Read it for |
|---|---|
| [PROJECT_OVERVIEW.md](PROJECT_OVERVIEW.md) | Purpose, domain concepts, user workflows, business rules, what is not supported |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Components, request flow, dependency injection, design decisions, architectural risks |
| [CODEBASE_GUIDE.md](CODEBASE_GUIDE.md) | Where things are, and where to start for common changes |
| [DATA_MODEL.md](DATA_MODEL.md) | Entities, relationships, constraints, indexes, migrations, DTOs |
| [API_REFERENCE.md](API_REFERENCE.md) | Every HTTP endpoint with auth, parameters, responses and errors |
| [INTEGRATIONS.md](INTEGRATIONS.md) | TMDB, SMTP, SQL Server, Azure, GitHub |
| [DEVELOPMENT_GUIDE.md](DEVELOPMENT_GUIDE.md) | Local setup, configuration, secrets, migrations, builds, common problems |
| [TESTING.md](TESTING.md) | Test suites, fixtures, coverage, gaps |
| [SECURITY.md](SECURITY.md) | Authentication, authorization, input handling, secrets, observed risks |
| [TECHNICAL_DEBT.md](TECHNICAL_DEBT.md) | Limitations and improvement opportunities with evidence and priority |

## Conventions used in these documents

- File paths are relative to the repository root.
- "Verified" means the statement was checked against code or configuration. Statements that rely on reading code without running it, or that could not be checked, say so.
- No secrets, keys or connection strings with credentials are reproduced here.
