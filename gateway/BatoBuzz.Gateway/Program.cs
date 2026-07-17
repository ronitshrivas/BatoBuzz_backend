var builder = WebApplication.CreateBuilder(args);

// Routes and clusters live in appsettings so a new service can be added, or a
// port changed per environment, without a redeploy of the gateway code.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "gateway" }));
app.MapReverseProxy();

app.Run();
