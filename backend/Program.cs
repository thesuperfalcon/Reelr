using backend.Data;
using backend.Features.Movies;
using backend.Features.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();

builder.Services.AddOpenApi();

builder.Services.AddDbContext<ReelrContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ReelrContext")
        ?? throw new InvalidOperationException(
            "Connection string 'ReelrContext' not found."
        )
    ));

builder.Services
    .AddIdentity<User, IdentityRole<int>>()
    .AddEntityFrameworkStores<ReelrContext>()
    .AddDefaultTokenProviders();

builder.Services.AddHttpClient<TmdbService>(client =>
{
    client.BaseAddress = new Uri("https://api.themoviedb.org/3/");
});

// Swagger
builder.Services.AddSwaggerGen();

var app = builder.Build();

// HTTP pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
