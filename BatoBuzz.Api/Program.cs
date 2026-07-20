using System.Text;
using System.Text.Json.Serialization;
using BatoBuzz.Api.Middleware;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// Feature namespaces — the three former services now live together in one app.
using IdentityData = BatoBuzz.Identity.Data;
using IdentityServices = BatoBuzz.Identity.Services;
using FeedData = BatoBuzz.Feed.Data;
using FeedServices = BatoBuzz.Feed.Services;
using MerchantData = BatoBuzz.Merchant.Data;
using MerchantServices = BatoBuzz.Merchant.Services;
using ProviderData = BatoBuzz.ServiceProvider.Data;
using ProviderServices = BatoBuzz.ServiceProvider.Services;
using BatoBuzz.Identity.Services;  // for GoogleAuthOptions

var builder = WebApplication.CreateBuilder(args);

// KYC uploads (merchant feature) are written to and served from wwwroot. Pin
// WebRootPath so the storage service and static-file middleware agree on the
// folder even in a fresh container where wwwroot doesn't exist yet.
var webRootPath = Path.Combine(builder.Environment.ContentRootPath, "wwwroot");
Directory.CreateDirectory(Path.Combine(webRootPath, "uploads"));
builder.Environment.WebRootPath = webRootPath;

// ── Config ────────────────────────────────────────────────────────────────
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection(GoogleAuthOptions.SectionName));
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

// ── Databases (separate DB per feature, one app) ───────────────────────────
// Keeping three DbContexts on three databases preserves clean data boundaries:
// features never share tables, and splitting back into services later stays
// easy. They're all reached through this one process.
builder.Services.AddDbContext<IdentityData.IdentityDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("IdentityDb")));
builder.Services.AddDbContext<FeedData.FeedDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("FeedDb")));
builder.Services.AddDbContext<MerchantData.MerchantDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("MerchantDb")));
builder.Services.AddDbContext<ProviderData.ServiceProviderDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("ServiceProviderDb")));

// ── Identity feature services ──────────────────────────────────────────────
builder.Services.AddScoped<IdentityServices.IPasswordHasher, IdentityServices.PasswordHasher>();
builder.Services.AddScoped<IdentityServices.ITokenService, IdentityServices.TokenService>();
builder.Services.AddScoped<IdentityServices.IGoogleAuthValidator, IdentityServices.GoogleAuthValidator>();
builder.Services.AddScoped<IdentityServices.IUserAuthService, IdentityServices.UserAuthService>();
builder.Services.AddScoped<IdentityServices.IMerchantAuthService, IdentityServices.MerchantAuthService>();
builder.Services.AddScoped<IdentityServices.IRefreshService, IdentityServices.RefreshService>();

// ── Feed feature services ──────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<FeedServices.ICurrentActor, FeedServices.CurrentActor>();
builder.Services.AddScoped<FeedServices.IPostService, FeedServices.PostService>();
builder.Services.AddScoped<FeedServices.ICommentService, FeedServices.CommentService>();
builder.Services.AddScoped<FeedServices.ICityService, FeedServices.CityService>();

// ── Merchant feature services ──────────────────────────────────────────────
builder.Services.AddScoped<MerchantServices.ICurrentActor, MerchantServices.CurrentActor>();
builder.Services.AddScoped<MerchantServices.IFileStorage, MerchantServices.LocalFileStorage>();
builder.Services.AddScoped<MerchantServices.IMerchantService, MerchantServices.MerchantService>();

// ── Service Provider feature (reuses Merchant's ICurrentActor + IFileStorage) ──
builder.Services.AddScoped<ProviderServices.IServiceProviderService, ProviderServices.ServiceProviderService>();

// ── JWT bearer + policies (one auth setup for the whole app) ───────────────
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SigningKey)),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppPolicies.ApprovedMerchant, p =>
        p.RequireRole(AppRoles.Merchant)
         .RequireClaim(TokenClaims.MerchantStatus, "approved"));

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// Model-binding failures share the ApiResponse envelope, one shape everywhere.
builder.Services.Configure<ApiBehaviorOptions>(o =>
{
    o.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState
            .SelectMany(kvp => kvp.Value?.Errors.Select(e => e.ErrorMessage) ?? Array.Empty<string>())
            .FirstOrDefault() ?? "Please check the details you entered.";
        return new BadRequestObjectResult(ApiResponse<object>.Fail(message));
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "BatoBuzz API", Version = "v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste an access token (no 'Bearer ' prefix).",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

// Apply every feature's migrations on startup, each against its own database.
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<IdentityData.IdentityDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<FeedData.FeedDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<MerchantData.MerchantDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<ProviderData.ServiceProviderDbContext>().Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();          // serves merchant KYC from wwwroot/uploads
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "batobuzz-api" }));

app.Run();
