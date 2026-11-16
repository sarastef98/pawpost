using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using PawsPost.Data;
using PawsPost.Features.Ai;
using PawsPost.Tests.Infrastructure.Fakes;

namespace PawsPost.Tests.Infrastructure;

/// <summary>
/// Starts the API for tests with its own SQLite file, so tests do not share data.
/// Migrations are applied at startup, which also proves the migration works.
/// </summary>
public class PawsPostFactory : WebApplicationFactory<Program>
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"pawspost-tests-{Guid.NewGuid():N}.db");

    public FakeEmbeddingProvider EmbeddingProvider { get; } = new();

    public FakeChatProvider ChatProvider { get; } = new();

    public FakeVectorStore VectorStore { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_databasePath}");

        // Every test uses the fakes, so no test can reach a real AI provider.
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmbeddingProvider>();
            services.RemoveAll<IChatProvider>();
            services.RemoveAll<IVectorStore>();
            services.AddSingleton(EmbeddingProvider);
            services.AddSingleton<IEmbeddingProvider>(EmbeddingProvider);
            services.AddSingleton(ChatProvider);
            services.AddSingleton<IChatProvider>(ChatProvider);
            services.AddSingleton(VectorStore);
            services.AddSingleton<IVectorStore>(VectorStore);
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();

        return host;
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
