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
using PointsData = BatoBuzz.Points.Data;
using PointsServices = BatoBuzz.Points.Services;
using ChatData = BatoBuzz.Chat.Data;
using ChatServices = BatoBuzz.Chat.Services;
using NotificationsData = BatoBuzz.Notifications.Data;
using NotificationsServices = BatoBuzz.Notifications.Services;
using AwardsData = BatoBuzz.Awards.Data;
using AwardsServices = BatoBuzz.Awards.Services;
using AdminData = BatoBuzz.Admin.Data;
using AdminServices = BatoBuzz.Admin.Services;
using ReservationsData = BatoBuzz.Reservations.Data;
using ReservationsServices = BatoBuzz.Reservations.Services;
using FollowData = BatoBuzz.Follow.Data;
using FollowServices = BatoBuzz.Follow.Services;
using PostInterestData = BatoBuzz.PostInterest.Data;
using PostInterestServices = BatoBuzz.PostInterest.Services;
using UserActivityServices = BatoBuzz.UserActivity.Services;
using BatoBuzz.Chat.Hubs;
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
builder.Services.AddDbContext<PointsData.PointsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("PointsDb")));
builder.Services.AddDbContext<ChatData.ChatDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("ChatDb")));
builder.Services.AddDbContext<NotificationsData.NotificationsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("NotificationsDb")));
builder.Services.AddDbContext<AwardsData.AwardsDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("AwardsDb")));
builder.Services.AddDbContext<AdminData.AdminDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("AdminDb")));
builder.Services.AddDbContext<ReservationsData.ReservationDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("ReservationsDb")));
builder.Services.AddDbContext<FollowData.FollowDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("FollowDb")));
builder.Services.AddDbContext<PostInterestData.PostInterestDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("PostInterestDb")));

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
builder.Services.AddScoped<FeedServices.IVideoStorage, FeedServices.LocalVideoStorage>();
builder.Services.AddScoped<FeedServices.IFavoriteService, FeedServices.FavoriteService>();
builder.Services.AddScoped<FeedServices.IResumeStorage, FeedServices.LocalResumeStorage>();
builder.Services.AddScoped<FeedServices.IJobApplicationService, FeedServices.JobApplicationService>();
builder.Services.AddScoped<FeedServices.IReelTranscoder, FeedServices.FfmpegReelTranscoder>();
builder.Services.AddSingleton<FeedServices.IReelJobQueue, FeedServices.ReelJobQueue>();
builder.Services.AddHostedService<FeedServices.ReelTranscodeWorker>();

// ── Merchant feature services ──────────────────────────────────────────────
builder.Services.AddScoped<MerchantServices.ICurrentActor, MerchantServices.CurrentActor>();
builder.Services.AddScoped<MerchantServices.IFileStorage, MerchantServices.LocalFileStorage>();
builder.Services.AddScoped<MerchantServices.IMerchantService, MerchantServices.MerchantService>();
builder.Services.AddScoped<MerchantServices.IRatingVoteService, MerchantServices.RatingVoteService>();

// ── Service Provider feature (reuses Merchant's ICurrentActor + IFileStorage) ──
builder.Services.AddScoped<ProviderServices.IServiceProviderService, ProviderServices.ServiceProviderService>();

// ── Points / awards / leaderboard ─────────────────────────────────────────
builder.Services.AddScoped<PointsServices.ICurrentUser, PointsServices.CurrentUser>();
builder.Services.AddScoped<PointsServices.IUserDirectory, PointsServices.IdentityUserDirectory>();
builder.Services.AddScoped<PointsServices.IPointsService, PointsServices.PointsService>();
builder.Services.AddScoped<PointsServices.IScanRewardService, PointsServices.ScanRewardService>();

// ── Chat (SignalR real-time) ──────────────────────────────────────────────
builder.Services.AddScoped<ChatServices.IChatActor, ChatServices.ChatActor>();
builder.Services.AddScoped<ChatServices.IChatDirectory, ChatServices.ChatDirectory>();
builder.Services.AddScoped<ChatServices.IThreadAccessGuard, ChatServices.ThreadAccessGuard>();
builder.Services.AddScoped<ChatServices.IChatMediaStorage, ChatServices.LocalChatMediaStorage>();
builder.Services.AddScoped<ChatServices.IChatService, ChatServices.ChatService>();

// ── Notifications + FCM tokens ────────────────────────────────────────────
builder.Services.AddScoped<NotificationsServices.INotificationService, NotificationsServices.NotificationService>();

// ── Awards (participation + voting) ───────────────────────────────────────
builder.Services.AddScoped<AwardsServices.IAwardService, AwardsServices.AwardService>();

// ── Super-admin ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<AdminServices.IAdminAudit, AdminServices.AdminAudit>();
builder.Services.AddScoped<AdminServices.IAdminManagementService, AdminServices.AdminManagementService>();
builder.Services.AddScoped<AdminServices.IAdminModerationService, AdminServices.AdminModerationService>();
builder.Services.AddScoped<AdminServices.IAdminPointsService, AdminServices.AdminPointsService>();
builder.Services.AddScoped<AdminServices.IAdminBroadcastService, AdminServices.AdminBroadcastService>();
builder.Services.AddScoped<AdminServices.IAdminAuditReader, AdminServices.AdminAuditReader>();

// ── Reservations (grab / reserve holds) ───────────────────────────────
builder.Services.AddScoped<ReservationsServices.ICurrentActor, ReservationsServices.CurrentActor>();
builder.Services.AddScoped<ReservationsServices.IReservationPoints, ReservationsServices.PointsReservationAdapter>();
builder.Services.AddScoped<ReservationsServices.IReservationService, ReservationsServices.ReservationService>();
builder.Services.AddHostedService<ReservationsServices.ReservationExpiryWorker>();

// ── Follow (users following merchants) ─────────────────────────────
builder.Services.AddScoped<FollowServices.ICurrentActor, FollowServices.CurrentActor>();
builder.Services.AddScoped<FollowServices.IFollowService, FollowServices.FollowService>();

// ── Post interest (event "I'm interested") ─────────────────────────
builder.Services.AddScoped<PostInterestServices.ICurrentActor, PostInterestServices.CurrentActor>();
builder.Services.AddScoped<PostInterestServices.IPostInterestService, PostInterestServices.PostInterestService>();

// ── User activity (read-only aggregation over the feed) ────────────────
builder.Services.AddScoped<UserActivityServices.IUserActivityService, UserActivityServices.UserActivityService>();

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

        // WebSockets can't send an Authorization header, so SignalR passes the
        // JWT as ?access_token=... on the hub URL. Pull it from there for /hubs.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var accessToken = ctx.Request.Query["access_token"];
                var path = ctx.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    ctx.Token = accessToken;
                return Task.CompletedTask;
            },
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AppPolicies.ApprovedMerchant, p =>
        p.RequireRole(AppRoles.Merchant)
         .RequireClaim(TokenClaims.MerchantStatus, "approved"));

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddSignalR();

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
    // Two features can legitimately define same-named DTOs (e.g. Admin and
    // Merchant both have a ReviewMerchantRequest / MerchantProfileDto). Swagger
    // keys schemas by short type name and 500s on a clash, so key by full name.
    o.CustomSchemaIds(t => t.FullName!.Replace("+", "."));
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
    scope.ServiceProvider.GetRequiredService<PointsData.PointsDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<ChatData.ChatDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<NotificationsData.NotificationsDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<AwardsData.AwardsDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<AdminData.AdminDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<ReservationsData.ReservationDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<FollowData.FollowDbContext>().Database.Migrate();
    scope.ServiceProvider.GetRequiredService<PostInterestData.PostInterestDbContext>().Database.Migrate();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseStaticFiles();          // serves merchant KYC from wwwroot/uploads
app.UseMiddleware<ExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "batobuzz-api" }));

app.Run();
