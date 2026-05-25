using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using TreasuryAIChat.Data;
using TreasuryAIChat.Hubs;
using TreasuryAIChat.Services;

var builder = WebApplication.CreateBuilder(args);

// ── Kestrel ─────────────────────────────────────────────────────────────────
builder.WebHost.ConfigureKestrel(o =>
{
    var urls = builder.Configuration["Kestrel:Urls"];
    if (!string.IsNullOrWhiteSpace(urls))
    {
        foreach (var url in urls.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var uri = new Uri(url);
            o.Listen(System.Net.IPAddress.Any, uri.Port);
        }
    }
});

// ── Configuration ─────────────────────────────────────────────────────────────
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddEnvironmentVariables();

// ── EF Core + PostgreSQL ─────────────────────────────────────────────────────
var connStr = builder.Configuration.GetConnectionString("TiisgsDb")
              ?? "Host=localhost;Port=5324;Database=tiisgs_db;Username=postgres;Password=your_password_here;";

builder.Services.AddDbContextFactory<TasksDbContext>(o =>
    o.UseNpgsql(connStr));

// ── Chat services ────────────────────────────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSignalR(o =>
{
    o.MaximumReceiveMessageSize = 64 * 1024;
    o.EnableDetailedErrors        = true;
});

builder.Services.AddSingleton<ConversationStore>();
builder.Services.AddScoped<IKnowledgeBaseService, TreasuryKnowledgeBaseService>();
builder.Services.AddScoped<IAIChatService>(sp => ChatServiceFactory.Build(sp.GetRequiredService<IConfiguration>()));
builder.Services.AddScoped<IAuditLogger, PostgreAuditLogger>();
builder.Services.AddScoped<IAIEscalationService, MockEscalationService>();

// ── Response compression ─────────────────────────────────────────────────────
builder.Services.AddResponseCompression(o =>
{
    o.EnableForHttps = true;
    o.Providers.Add<BrotliCompressionProvider>();
    o.Providers.Add<GzipCompressionProvider>();
});

var app = builder.Build();

// ── Pipeline ─────────────────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseResponseCompression();
app.MapStaticAssets();
app.MapHub<ChatHub>("/chathub");
app.MapRazorComponents<TreasuryAIChat.Components.App>()
   .AddInteractiveServerRenderMode();
app.MapFallbackToFile("index.html");
app.Run();
