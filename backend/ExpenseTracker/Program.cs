using ExpenseTracker.Data;
using ExpenseTracker.Middleware;
using ExpenseTracker.Models;
using ExpenseTracker.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. Database & Identity
var dbProvider = builder.Configuration["DatabaseProvider"] ?? "Sqlite";
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Data Source=ExpenseTracker.db";

if (dbProvider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
{
    var sqlConn = builder.Configuration.GetConnectionString("SqlServerConnection") ?? connectionString;
    builder.Services.AddDbContext<ExpenseTrackerDbContext>(options => options.UseSqlServer(sqlConn));
}
else
{
    builder.Services.AddDbContext<ExpenseTrackerDbContext>(options => options.UseSqlite(connectionString));
}

builder.Services.AddIdentity<User, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ExpenseTrackerDbContext>()
.AddDefaultTokenProviders();

// 2. Application Services
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<QuickExpenseParserService>();
builder.Services.AddScoped<TransactionService>();
builder.Services.AddScoped<CategoryService>();
builder.Services.AddScoped<BudgetService>();
builder.Services.AddScoped<DashboardService>();
builder.Services.AddScoped<AIChatbotService>();

// 3. Authentication & JWT
var jwtKey = builder.Configuration["Jwt:Key"] ?? "ExpenseTrackerAcademicProjectSecretKey123456789!";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "ExpenseTracker";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "ExpenseTrackerUsers";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// 4. CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// 5. Controllers & Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Smart Expense Tracker with AI Insights API",
        Version = "v1",
        Description = "College Project Web API with Quick Entry, Budgets, 50/30/20 Rule, and AI Financial Chatbot."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using Bearer scheme. Example: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// 6. Initialize & Seed Database
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ExpenseTrackerDbContext>();
    await DbInitializer.InitializeAsync(db);
}

// 7. Pipeline Setup
app.UseMiddleware<ExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Expense Tracker API v1");
    c.RoutePrefix = "swagger";
});

app.UseCors("AllowAll");

// Serve frontend static files (from ../../frontend, ../frontend, or wwwroot)
var candidatePaths = new[]
{
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "frontend")),
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "frontend")),
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "frontend")),
    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "frontend"))
};

string? frontendRoot = candidatePaths.FirstOrDefault(Directory.Exists);

if (frontendRoot != null)
{
    var fileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(frontendRoot);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider
    });
}
else
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program { }
