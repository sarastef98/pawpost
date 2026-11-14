using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PawsPost.Data;

namespace PawsPost.Tests.Infrastructure;

/// <summary>
/// Starts the API for tests with its own SQLite file, so tests do not share data.
/// Migrations are applied at startup, which also proves the migration works.
/// </summary>
public class PawsPostFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"pawspost-tests-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        return host;
    }


    /// <summary>
    /// Creates a client with an https base address (the auth cookie is Secure) and no automatic redirects.
    /// </summary>
    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = true
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            SqliteConnection.ClearAllPools();
            foreach (var path in new[] { _databasePath, _databasePath + "-shm", _databasePath + "-wal" })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
