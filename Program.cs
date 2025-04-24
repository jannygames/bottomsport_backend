using bottomsport_backend.Services;
using Microsoft.AspNetCore.Http;          // SameSiteMode, CookieSecurePolicy
using Microsoft.AspNetCore.Routing;       // For route debugging
using Microsoft.AspNetCore.Mvc.Routing;   // For HttpMethodMetadata
using System.Linq;                        // For LINQ operations

var builder = WebApplication.CreateBuilder(args);

// ───────────────────────────────────────── SESSION ─────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name       = ".BottomSport.Session";
    options.Cookie.HttpOnly   = true;
    options.Cookie.IsEssential= true;

    if (builder.Environment.IsDevelopment())                   // dev: plain HTTP
    {
        options.Cookie.SameSite    = SameSiteMode.Lax;         // same-site = ok on localhost
        options.Cookie.SecurePolicy= CookieSecurePolicy.None;  // do NOT set Secure flag
    }
    else                                                       // prod: cross-site / HTTPS
    {
        options.Cookie.SameSite    = SameSiteMode.None;        // will travel cross-site
        options.Cookie.SecurePolicy= CookieSecurePolicy.Always;// Secure required for None
    }
});

// ──────────────────────────────────────── DATABASE ────────────────────────────────────────
builder.Services.AddScoped<DatabaseService>();

// Fill in the connection string if none is present in appsettings.*
var connectionString = builder.Configuration.GetConnectionString("bottomsport")
                    ?? "Server=localhost;Database=bottomsport;Uid=root;Pwd=;";
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
{
    ["ConnectionStrings:bottomsport"] = connectionString
});
Console.WriteLine($"Using connection string: {connectionString}");

// ──────────────────────────────────────────  CORS  ─────────────────────────────────────────
const string FrontendOrigin = "http://localhost:5173";         // adjust for HTTPS if needed

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", p => p
        .WithOrigins(FrontendOrigin)
        .AllowCredentials()            // send cookies
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// ───────────────────────────────────────── MISC API ────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ──────────────────────────────────────── PIPELINE ─────────────────────────────────────────
app.UseRouting();

app.UseCors("Frontend");           // must come BEFORE Session / endpoints
app.UseSession();                  // cookie now survives

// Optional: only keep the verbose logging in dev
if (app.Environment.IsDevelopment())
{
    app.Use(async (ctx, next) =>
    {
        Console.WriteLine($"[DEV] {ctx.Request.Method} {ctx.Request.Path} – SessionId {ctx.Session.Id}");
        Console.WriteLine(
            ctx.Session.TryGetValue("UserId", out var bytes)
            ? $"[DEV] UserId: {BitConverter.ToInt32(bytes)}"
            : "[DEV] No UserId in session");

        await next();
    });
}

app.UseAuthorization();

// Log all registered endpoints for debugging
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api/debug/routes"))
    {
        var endpointDataSource = app.Services.GetRequiredService<Microsoft.AspNetCore.Routing.EndpointDataSource>();
        var endpoints = endpointDataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Select(e => new
            {
                Method = e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.FirstOrDefault(),
                Route = e.RoutePattern.RawText,
                DisplayName = e.DisplayName
            })
            .ToList();
        
        await context.Response.WriteAsJsonAsync(endpoints);
        return;
    }
    
    await next();
});

app.MapControllers();

// Enable HTTPS redirection in prod if you set Secure cookies
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();     // ensures Secure cookies are usable
}

app.Run();
