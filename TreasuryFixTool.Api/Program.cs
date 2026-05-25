using System.Text.Json;
using TreasuryFixTool.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using TreasuryFixTool.Api.Models;
using TreasuryFixTool.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Configuration
       .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
       .AddEnvironmentVariables();

// ── EF Core + Identity ────────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("TiisgsDb")
              ?? throw new InvalidOperationException("TiisgsDb connection string missing");

builder.Services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connStr));

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequiredLength         = 8;
    options.Password.RequireDigit            = true;
    options.Password.RequireUppercase        = true;
    options.Password.RequireLowercase        = true;
    options.Password.RequireNonAlphanumeric  = true;
    options.Lockout.MaxFailedAccessAttempts  = 5;
    options.Lockout.DefaultLockoutTimeSpan   = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail          = true;
    options.User.AllowedUserNameCharacters   = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders()
    .AddSignInManager();

// ── JWT Auth ─────────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"] ?? "dev-key";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = "Bearer";
    options.DefaultChallengeScheme    = "Bearer";
})
.AddJwtBearer("Bearer", options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer           = true,
        ValidateAudience         = true,
        ValidateLifetime         = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer              = builder.Configuration["Jwt:Issuer"],
        ValidAudience            = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew                = TimeSpan.FromSeconds(30)
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = ctx =>
        {
            // Allow token in: Authorization: Bearer <token> OR ?access_token=<token>
            if (string.IsNullOrEmpty(ctx.Token))
            {
                var accessToken = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken))
                    ctx.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// ── Authorization ─────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly",  p => p.RequireRole("Admin"));
    options.AddPolicy("TechOrAbove",p => p.RequireRole("Admin", "Technician"));
    options.AddPolicy("AnyUser",    p => p.RequireAuthenticatedUser());
});

// ── CORS (for WPF / desktop clients) ─────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// ── Controllers + JSON ────────────────────────────────────────────────────────
builder.Services.AddControllers()
       .AddJsonOptions(o => o.JsonSerializerOptions.PropertyNamingPolicy = null);

// ── DI registrations ──────────────────────────────────────────────────────────
builder.Services.AddSingleton<IJwtService, JwtService>();

var app = builder.Build();

// ── Migrate + Seed ─────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db           = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager  = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var roleManager  = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    db.Database.Migrate();

    // ── Seed roles ────────────────────────────────────────────────────────────
    foreach (var role in new[] { "Admin", "Technician", "Viewer" })
    {
        if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
            roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
    }

    // ── Seed bootstrap admin if absent ─────────────────────────────────────────
    const string adminEmail = "admin@treasury.gov.za";
    const string adminUser  = "admin";
    if (userManager.FindByNameAsync(adminUser).GetAwaiter().GetResult() is null)
    {
        var admin = new User
        {
            UserName    = adminUser,
            Email       = adminEmail,
            FullName    = "System Administrator",
            Department  = "ICTSU",
            IsActive    = true,
            EmailConfirmed = true
        };
        userManager.CreateAsync(admin, "Treasury@Admin2026!").GetAwaiter().GetResult();
        userManager.AddToRoleAsync(admin, "Admin").GetAwaiter().GetResult();

        // Record bootstrap event manually (no requesting user yet)
        using var auditScope = scope.ServiceProvider.CreateScope();
        var auditDb = auditScope.ServiceProvider.GetRequiredService<AppDbContext>();
        auditDb.AuditLogs.Add(new AuditLog
        {
            UserId        = admin.Id,
            Action        = "BootstrapSeed",
            EntityType    = nameof(User),
            EntityId      = admin.Id,
            IpAddress     = "127.0.0.1",
            UserAgent     = "System",
            Success       = true,
            CreatedAt     = DateTime.UtcNow
        });
        auditDb.SaveChanges();
    }
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

app.UseCors();
app.UseHttpsRedirection();
app.MapControllers();

app.Run();
