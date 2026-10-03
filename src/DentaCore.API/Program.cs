using System.Text;
using System.Text.Json.Serialization;
using DentaCore.API.Middleware;
using DentaCore.API.Services;
using DentaCore.Application.Common;
using DentaCore.Application.Extensions;
using DentaCore.Infrastructure.Extensions;
using DentaCore.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Load .env file if present in solution root or app root
LoadDotEnvFiles();

// Register HTTP Context & Current User
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// Register Application & Infrastructure layers
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add Controllers with JSON Options
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

// Configure JWT Authentication
var jwtSecret = builder.Configuration["JWT_SECRET"] ?? "YourSuperSecretKeyThatIsAtLeast32CharactersLong!!";
var jwtIssuer = builder.Configuration["JWT_ISSUER"] ?? "DentaCore";
var jwtAudience = builder.Configuration["JWT_AUDIENCE"] ?? "DentaCoreApp";

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
    // Allow PDF download links (window.open) to pass the token via ?access_token= query string
    // because browsers cannot set Authorization headers for window.open / <a href> requests.
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        },
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Authentication required or token expired.\",\"code\":\"Unauthorized\"}");
        },
        OnForbidden = async context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync("{\"message\":\"Access denied. You do not have permission for this resource.\",\"code\":\"Forbidden\"}");
        }
    };
});

builder.Services.AddAuthorization();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Configure Swagger / OpenAPI with Bearer Auth
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DentaCore Dental Management API",
        Version = "v1",
        Description = "Clinical Dental Portal API (.NET 9) — Appointments, Doctor Schedules, Invoicing, and QuestPDF Receipts."
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Ensure Database schema exists and seed defaults
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await context.Database.EnsureCreatedAsync();

    var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
    await seeder.SeedAsync();
}

// Middleware Pipeline
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "DentaCore API v1");
    c.RoutePrefix = "swagger";
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors("AllowAll");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

void LoadDotEnvFiles()
{
    var current = Directory.GetCurrentDirectory();
    var pathsToSearch = new[]
    {
        Path.Combine(current, ".env"),
        Path.Combine(current, "..", ".env"),
        Path.Combine(current, "..", "..", ".env")
    };

    foreach (var path in pathsToSearch)
    {
        if (File.Exists(path))
        {
            foreach (var line in File.ReadAllLines(path))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                    continue;

                var idx = trimmed.IndexOf('=');
                if (idx > 0)
                {
                    var key = trimmed[..idx].Trim();
                    var val = trimmed[(idx + 1)..].Trim();
                    Environment.SetEnvironmentVariable(key, val);
                    builder.Configuration[key] = val;
                }
            }
            break;
        }
    }
}
