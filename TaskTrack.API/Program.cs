using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Npgsql;
using TaskTrack.API;
using TaskTrack.Repo;
using TaskTrack.Repo.Models;
using TaskTrack.Repo.Repositories;
using TaskTrack.Service.Interfaces;
using TaskTrack.Service.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT" });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement { [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>() });
});
builder.Services.AddDbContext<TaskManagementDbContext>(options => options.UseNpgsql(DatabaseConnection.Resolve(builder.Configuration)));
builder.Services.AddScoped<DepartmentRepository>();
builder.Services.AddScoped<ProjectRepository>();
builder.Services.AddScoped<TaskRepository>();
builder.Services.AddScoped<TagRepository>();
builder.Services.AddScoped<AccountRepository>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<IPasswordHasher<SystemAccount>, PasswordHasher<SystemAccount>>();
builder.Services.AddScoped<TokenIssuer>();
builder.Services.AddScoped<IDepartmentService, DepartmentService>();
builder.Services.AddScoped<IProjectService, ProjectService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ITagService, TagService>();

var jwtKey = TokenIssuer.Key(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    options.MapInboundClaims = false;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true, IssuerSigningKey = jwtKey,
        ValidateIssuer = true, ValidIssuer = TokenIssuer.Issuer, ValidateAudience = true, ValidAudience = TokenIssuer.Audience,
        ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero, RoleClaimType = "Role", NameClaimType = "FullName"
    };
    options.Events = new JwtBearerEvents
    {
        OnTokenValidated = async context =>
        {
            if (!int.TryParse(context.Principal?.FindFirstValue("AccountID"), out var id)) { context.Fail("Invalid account."); return; }
            var repo = context.HttpContext.RequestServices.GetRequiredService<AccountRepository>();
            var account = await repo.FindAsync(id, context.HttpContext.RequestAborted);
            if (account is null || TokenIssuer.RoleName(account.Role) != context.Principal?.FindFirstValue("Role")) context.Fail("Account or role changed. Sign in again.");
        }
    };
});
builder.Services.AddAuthorization();
var origins = (builder.Configuration["CORS_ORIGINS"] ?? "http://localhost:3012").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options => options.AddPolicy("frontend", policy => policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (args.Contains("--seed-admin"))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<AccountService>().SeedAdminAsync(
        builder.Configuration["ADMIN_FULL_NAME"] ?? "Administrator",
        builder.Configuration["ADMIN_EMAIL"] ?? throw new InvalidOperationException("Configure ADMIN_EMAIL."),
        builder.Configuration["ADMIN_PASSWORD"] ?? throw new InvalidOperationException("Configure ADMIN_PASSWORD."), CancellationToken.None);
    Console.WriteLine("Admin seed completed. Credentials were not printed.");
    return;
}
app.UseExceptionHandler(handler => handler.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, message) = error switch
    {
        AccountConflictException ex => (409, ex.Message),
        KeyNotFoundException ex => (404, ex.Message),
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } => (409, "A record with these values already exists."),
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.ForeignKeyViolation } } => (409, "This record is linked to other records, or the referenced record no longer exists."),
        InvalidOperationException ex => (400, ex.Message),
        _ => (500, "The request could not be completed. Please try again.")
    };
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new { message });
}));
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();
