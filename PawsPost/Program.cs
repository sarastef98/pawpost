using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PawsPost.Data;
using PawsPost.Features.Auth;
using PawsPost.Features.PetAds;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

// Bound through the options system and checked at startup, so a bad value stops the app instead of
// silently producing cookies that expire immediately.
builder.Services
    .AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.SectionName))
    .Validate(options => options.CookieLifetime > TimeSpan.Zero, "Auth:CookieLifetime must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services
    .AddIdentityCore<User>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager();

builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IPetAdService, PetAdService>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "pawspost.auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.SlidingExpiration = true;

    // An API must answer 401/403 with ProblemDetails, never redirect to a login page.
    options.Events.OnRedirectToLogin = context => WriteProblemAsync(
        context.HttpContext, StatusCodes.Status401Unauthorized, "Authentication required");
    options.Events.OnRedirectToAccessDenied = context => WriteProblemAsync(
        context.HttpContext, StatusCodes.Status403Forbidden, "Access denied");
});

// The cookie lifetime comes from the validated AuthOptions.
builder.Services
    .AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
    .Configure<IOptions<AuthOptions>>((options, authOptions) =>
        options.ExpireTimeSpan = authOptions.Value.CookieLifetime);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Swagger UI is only the page; it reads the OpenAPI document mapped above.
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "PawsPost v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static async Task WriteProblemAsync(HttpContext httpContext, int statusCode, string title)
{
    httpContext.Response.StatusCode = statusCode;
    var problemDetailsService = httpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
    await problemDetailsService.WriteAsync(new ProblemDetailsContext
    {
        HttpContext = httpContext,
        ProblemDetails = { Status = statusCode, Title = title }
    });
}
