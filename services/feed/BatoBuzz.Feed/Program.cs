using System.Text;
using System.Text.Json.Serialization;
using BatoBuzz.Feed.Data;
using BatoBuzz.Feed.Middleware;
using BatoBuzz.Feed.Services;
using BatoBuzz.Shared.Auth;
using BatoBuzz.Shared.Results;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ── Config ──────────────────────────────────────────────────────────────
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
var jwt = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()!;

// ── EF Core (PostgreSQL) ────────────────────────────────────────────────
builder.Services.AddDbContext<FeedDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("FeedDb")));

// ── Feed services ───────────────────────────────────────────────────────
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, CurrentActor>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<ICommentService, CommentService>();
builder.Services.AddScoped<ICityService, CityService>();

// ── JWT bearer + policies ───────────────────────────────────────────────
// Same signing key, issuer and audience as Identity: tokens minted there are
// accepted here without a round trip back to Identity on every request.
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
    .AddJsonOptions(o =>
    {
        // Enum-valued DTO fields are already strings on the wire; this keeps any
        // future enum from silently serializing as an int the apps can't read.
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });

// Model-binding failures return the same ApiResponse envelope as everything
// else, so the Flutter error handling has exactly one shape to parse.
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
    o.SwaggerDoc("v1", new OpenApiInfo { Title = "BatoBuzz Feed", Version = "v1" });

    // Lets Swagger UI send the bearer token while testing the feed endpoints.
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token returned by Identity (no 'Bearer ' prefix).",
    });
    o.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer",
                },
            },
            Array.Empty<string>()
        },
    });
});

var app = builder.Build();

// Apply migrations on startup (dev convenience) — same as Identity.
using (var scope = app.Services.CreateScope())
    scope.ServiceProvider.GetRequiredService<FeedDbContext>().Database.Migrate();

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "feed" }));

app.Run();
