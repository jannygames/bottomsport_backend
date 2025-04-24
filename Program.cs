using bottomsport_backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Lax;  // Changed to Lax for better compatibility
    options.Cookie.SecurePolicy = CookieSecurePolicy.None;  // Set to None for development
    options.Cookie.Name = ".BottomSport.Session";  // Set a specific cookie name
});

// Add database service
builder.Services.AddScoped<DatabaseService>();

// Add connection string
var connectionString = builder.Configuration.GetConnectionString("bottomsport") 
    ?? "Server=localhost;Database=bottomsport;Uid=root;Pwd=;";
Console.WriteLine($"Using connection string: {connectionString}");

builder.Configuration.AddInMemoryCollection(new Dictionary<string, string>
{
    {"ConnectionStrings:bottomsport", connectionString}
});

// Add CORS service
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Configure the HTTP request pipeline
app.UseRouting();

// Use Session BEFORE accessing it in middleware
app.UseSession();

// Add session debugging middleware
app.Use(async (context, next) =>
{
    Console.WriteLine($"Request Path: {context.Request.Path}");
    Console.WriteLine($"Session ID: {context.Session.Id}");
    if (context.Session.TryGetValue("UserId", out byte[] userIdBytes))
    {
        var userId = BitConverter.ToInt32(userIdBytes);
        Console.WriteLine($"User ID in session: {userId}");
    }
    else
    {
        Console.WriteLine("No User ID in session");
    }
    await next();
});

// CORS should be after UseRouting but before UseAuthorization
app.UseCors("AllowFrontend");
app.UseAuthorization();

app.MapControllers();
app.Run();
