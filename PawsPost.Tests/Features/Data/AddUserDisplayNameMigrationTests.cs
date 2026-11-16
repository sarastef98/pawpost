using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PawsPost.Data;

namespace PawsPost.Tests.Features.Data;

public class AddUserDisplayNameMigrationTests : IDisposable
{
    private const string PreviousMigration = "20261116095158_AddPetAdEmbeddings";

    private static readonly string[] LegacyUserIds =
    [
        "11111111-1111-1111-1111-111111111111",
        "22222222-2222-2222-2222-222222222222",
        "33333333-3333-3333-3333-333333333333",
        "44444444-4444-4444-4444-444444444444",
        "0aaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    ];

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"pawspost-migration-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task AddUserDisplayName_ExistingUsers_GetPetLoverDefaultName()
    {
        // Arrange
        await SeedLegacyUsersAndMigrateAsync();

        // Act
        var users = await ReadUsersAsync();

        // Assert
        Assert.Equal(LegacyUserIds.Length, users.Count);
        Assert.All(users, user => Assert.Matches(new Regex(@"^Pet lover \d{4}$"), user.DisplayName));
    }

    [Fact]
    public async Task AddUserDisplayName_ExistingUsers_GetUniqueNames()
    {
        // Arrange
        await SeedLegacyUsersAndMigrateAsync();

        // Act
        var users = await ReadUsersAsync();

        // Assert
        Assert.Equal(users.Count, users.Select(user => user.DisplayName).Distinct().Count());
        Assert.Equal(users.Count, users.Select(user => user.DisplayName.ToUpperInvariant()).Distinct().Count());
    }

    [Fact]
    public async Task AddUserDisplayName_ExistingUsers_GetMatchingNormalizedName()
    {
        // Arrange
        await SeedLegacyUsersAndMigrateAsync();

        // Act
        var users = await ReadUsersAsync();

        // Assert
        Assert.All(users, user => Assert.Equal(user.DisplayName.ToUpperInvariant(), user.NormalizedDisplayName));
    }

    [Fact]
    public async Task AddUserDisplayName_AfterMigration_NoUserWithoutDisplayName()
    {
        // Arrange
        await SeedLegacyUsersAndMigrateAsync();

        // Act
        var users = await ReadUsersAsync();

        // Assert
        Assert.All(users, user =>
        {
            Assert.False(string.IsNullOrWhiteSpace(user.DisplayName));
            Assert.False(string.IsNullOrWhiteSpace(user.NormalizedDisplayName));
        });
    }

    [Fact]
    public async Task AddUserDisplayName_AfterMigration_DuplicateNormalizedNameIsRejected()
    {
        // Arrange
        await SeedLegacyUsersAndMigrateAsync();
        await using var db = CreateDbContext();
        var existing = (await ReadUsersAsync())[0];

        // Act
        var insertDuplicate = () => db.Database.ExecuteSqlRawAsync(
            "INSERT INTO AspNetUsers (Id, DisplayName, NormalizedDisplayName, AccessFailedCount, EmailConfirmed, LockoutEnabled, PhoneNumberConfirmed, TwoFactorEnabled) " +
            "VALUES ({0}, 'Other', {1}, 0, 0, 0, 0, 0)",
            "99999999-9999-9999-9999-999999999999", existing.NormalizedDisplayName);

        // Assert
        await Assert.ThrowsAnyAsync<Exception>(insertDuplicate);
    }

    private async Task SeedLegacyUsersAndMigrateAsync()
    {
        // Build the old schema, then store users without a display name using raw SQL.
        await using (var db = CreateDbContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            foreach (var id in LegacyUserIds)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO AspNetUsers (Id, AccessFailedCount, EmailConfirmed, LockoutEnabled, PhoneNumberConfirmed, TwoFactorEnabled) VALUES ({0}, 0, 0, 0, 0, 0)",
                    id);
            }
        }

        // Apply the remaining migrations (the display name migration).
        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }
    }

    private async Task<List<User>> ReadUsersAsync()
    {
        await using var db = CreateDbContext();
        return await db.Users.AsNoTracking().OrderBy(user => user.DisplayName).ToListAsync();
    }

    public void Dispose()
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

    private AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite($"Data Source={_databasePath}")
            .Options;
        return new AppDbContext(options);
    }
}
