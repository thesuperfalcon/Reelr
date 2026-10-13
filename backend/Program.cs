using backend.Data;
using backend.Features.Activity;
using backend.Features.Auth;
using backend.Features.Diary;
using backend.Features.Invites;
using backend.Features.Email;
using backend.Features.Moderation;
using backend.Features.Movies;
using backend.Features.Observability;
using backend.Features.Users;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.AddAppLogging();

// Required secrets are checked at start-up, so a missing or weak one stops the app
// instead of failing the first login or the first TMDB request.
var jwtKey = builder.Configuration["Jwt:Key"];

if (string.IsNullOrEmpty(jwtKey) || Encoding.UTF8.GetByteCount(jwtKey) < 32)
{
    throw new InvalidOperationException("Jwt:Key must be set and at least 32 bytes long (HMAC-SHA256).");
}

if (string.IsNullOrEmpty(builder.Configuration["TMDB_READ_ACCESS_TOKEN"]))
{
    throw new InvalidOperationException("TMDB_READ_ACCESS_TOKEN is not configured.");
}

// Account emails need a mail server, a sender address and the site's address for the links in them.
foreach (var key in new[] { "Email:Host", "Email:FromAddress", "App:BaseUrl" })
{
    if (string.IsNullOrEmpty(builder.Configuration[key]))
    {
        throw new InvalidOperationException($"{key} is not configured.");
    }
}

// Services
builder.Services.AddControllers();

// Unhandled exceptions and empty error responses (e.g. 401, 404, 429) get an RFC 7807 problem body.
builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = CorrelationId.AddToProblem);

// The OpenAPI document, built in. In Development it is at /openapi/v1.json and Swagger UI shows it at /swagger.
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Components ??= new OpenApiComponents();
    document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
    document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "An access token, e.g. from /auth/login (no need to type the 'Bearer ' prefix)."
    };
    document.Security ??= [];
    document.Security.Add(new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
    return Task.CompletedTask;
}));

builder.Services.AddDbContext<ReelrContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ReelrContext")
        ?? throw new InvalidOperationException(
            "Connection string 'ReelrContext' not found."
        ))
        .AddInterceptors(new OpenRetryInterceptor()));

builder.Services
    .AddIdentity<User, IdentityRole<int>>(options =>
    {
        options.User.RequireUniqueEmail = true;

        // Identity's default set without "@", so a username can never look like someone's email address.
        // Login looks up the email first, so such a name would hide the account behind another user's address.
        options.User.AllowedUserNameCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._+";

        // The account lockout stops password guessing spread over many addresses: 20 wrong passwords in a row
        // lock the account for 15 minutes. LoginThrottle stops one address after 5, so locking someone else's
        // account (usernames are public) takes at least 4 addresses, and the owner is rarely the one locked out.
        options.Lockout.MaxFailedAccessAttempts = 20;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ReelrContext>()
    .AddDefaultTokenProviders();

// Email confirmation and password reset links stop working after 3 hours.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromHours(3));

// The keys behind those links live in the database, so a restart or a new container does not break sent links.
builder.Services.AddDataProtection()
    .SetApplicationName("Reelr")
    .PersistKeysToDbContext<ReelrContext>();

// Account emails are queued and sent in the background (see EmailQueue).
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
builder.Services.AddSingleton<EmailQueue>();
builder.Services.AddHostedService<EmailDispatcher>();

// Copies stored films from TMDB again now and then ("MovieRefresh" section).
builder.Services.Configure<MovieRefreshOptions>(builder.Configuration.GetSection("MovieRefresh"));
builder.Services.AddSingleton<VisitorSignal>();
builder.Services.AddHostedService<MovieRefresher>();
builder.Services.AddScoped<AccountEmails>();
builder.Services.AddSingleton<SecurityStamps>();
builder.Services.AddSingleton<SecurityLog>();

// Admins are configured by user id under "Admin:UserIds" (see AdminAccess).
var adminAccess = new AdminAccess(builder.Configuration);
builder.Services.AddSingleton(adminAccess);

// Shared demo accounts are configured by user id under "Demo:UserIds" (see DemoAccounts).
builder.Services.AddSingleton<DemoAccounts>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AdminAccess.Policy, policy => policy
        .RequireAuthenticatedUser()
        .RequireAssertion(context => adminAccess.IsAdmin(context.User)));

builder.Services.AddRateLimits(builder.Configuration);
builder.Services.AddSingleton<LoginThrottle>();

// The client IP from X-Forwarded-For is used only when the request comes from a trusted proxy:
// loopback, or a network listed in "ForwardedHeaders:KnownNetworks" (CIDR, e.g. the Docker network nginx runs on).
// Anyone else could send the header themselves and pick a new IP for every request to get past the rate limits.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

    // How many trusted proxies a request passes through: 1 behind nginx in Docker, more where a platform's
    // own proxies sit in front of and behind nginx (Azure Container Apps). Addresses are read from the right
    // and only while each one is a trusted proxy, so a client still cannot pick its own address.
    options.ForwardLimit = builder.Configuration.GetValue("ForwardedHeaders:ForwardLimit", 1);

    foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
    {
        options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    }
});

builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<RefreshTokens>();
builder.Services.AddScoped<MovieCatalog>();
builder.Services.AddScoped<JournalService>();
builder.Services.AddScoped<InviteCodes>();

// Activity feed. "Activity:Implementation" picks how the feed is computed; only "Derived" exists so far.
builder.Services.AddMemoryCache();
builder.Services.Configure<ActivityOptions>(builder.Configuration.GetSection("Activity"));
builder.Services.AddScoped<IActivityFeed>(services =>
    builder.Configuration["Activity:Implementation"] switch
    {
        null or "Derived" => ActivatorUtilities.CreateInstance<DerivedActivityFeed>(services),
        var name => throw new InvalidOperationException($"Unknown Activity:Implementation '{name}'.")
    });

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            // The default is 5 minutes, which would keep an expired token working that long.
            ClockSkew = TimeSpan.FromSeconds(30),
            // TokenService writes the username as the short "name" claim, which is not mapped to
            // ClaimTypes.Name, so User.Identity.Name reads it from there.
            NameClaimType = JwtRegisteredClaimNames.Name
        };

        // A token is refused once its security stamp is out of date, so a password change, a reset,
        // "log out everywhere" or deleting the account revokes it before it expires.
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var stamps = context.HttpContext.RequestServices.GetRequiredService<SecurityStamps>();
                var userId = context.Principal?.FindUserId();
                var stamp = context.Principal?.FindFirstValue(SecurityStamps.ClaimType);

                if (userId is not int id || !await stamps.IsCurrentAsync(id, stamp))
                {
                    context.Fail("The token has been revoked.");
                }
            }
        };
    });

// /health/live and /health/ready (database, and TMDB as Degraded). Docker waits for ready.
builder.Services.AddAppHealthChecks();

// TMDB: a cache, a handler that turns TMDB failures into 502/503, and timeouts, retries and a circuit breaker
// so a slow or failing TMDB does not hold requests open. "Tmdb:Resilience" can override the defaults below.
builder.Services.AddSingleton<TmdbCache>();
builder.Services.AddExceptionHandler<TmdbExceptionHandler>();
builder.Services.AddExceptionHandler<UniqueConflictExceptionHandler>();

// Overridable so the end-to-end tests can point the app at a fake TMDB. Paths are relative ("movie/123"),
// so the address must end in "/" or they would replace its last segment ("/3").
var tmdbBaseUrl = builder.Configuration["Tmdb:BaseUrl"];
tmdbBaseUrl = string.IsNullOrWhiteSpace(tmdbBaseUrl) ? "https://api.themoviedb.org/3/" : tmdbBaseUrl.Trim();
if (!tmdbBaseUrl.EndsWith('/'))
{
    tmdbBaseUrl += "/";
}

var tmdbResilience = builder.Services
    .AddHttpClient<TmdbService>(client => client.BaseAddress = new Uri(tmdbBaseUrl))
    .AddStandardResilienceHandler(options =>
    {
        // TMDB usually answers well within a second; the library defaults (10 s per attempt, 30 s in total) are too long.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(4);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
        options.Retry.MaxRetryAttempts = 2;
    });

// Configure throws on a section that does not exist, so overrides are bound only when some are set.
var tmdbResilienceOverrides = builder.Configuration.GetSection("Tmdb:Resilience");

if (tmdbResilienceOverrides.Exists())
{
    tmdbResilience.Configure(tmdbResilienceOverrides);
}

var app = builder.Build();

// The app never changes the database schema itself. Migrations run as a separate step before it starts
// (the "migrate" service in Docker Compose), with an account that may change tables while the app's may not.

// HTTP pipeline
// First, so the rate limiter, HTTPS redirection and logging see the real client IP and scheme.
app.UseForwardedHeaders();

// Before the request log, so its line and every other line of the request carry the id.
app.UseCorrelationId();

// Logs each request once it is done, with the status the later steps set.
app.UseRequestLogging();

// Background work waits for a visitor, so it never wakes a paused database. Health checks do not count.
var visitors = app.Services.GetRequiredService<VisitorSignal>();
app.Use((context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/auth"))
    {
        visitors.Seen();
    }

    return next(context);
});

// In Development the developer exception page runs first and still shows the stack trace.
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Reelr API"));
}
else
{
    // Tells browsers to use HTTPS only. Not in Development, where it would stick to localhost.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseRateLimiter();

app.MapControllers();
app.MapAppHealthChecks();

app.Run();
