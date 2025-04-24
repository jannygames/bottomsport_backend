using bottomsport_backend.Services;

var builder = WebApplication.CreateBuilder(args);

// Add session support
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
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

// Important: UseCors must be called before UseRouting and UseAuthorization
app.UseCors("AllowFrontend");
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllers();
app.Run();
