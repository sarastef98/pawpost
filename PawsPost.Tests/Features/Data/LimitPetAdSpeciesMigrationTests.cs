using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PawsPost.Data;

namespace PawsPost.Tests.Features.Data;

public class LimitPetAdSpeciesMigrationTests : IDisposable
{
    private const string PreviousMigration = "20261114202843_AddPetAdStatusCreatedAtIndex";
    private static readonly Guid OwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"pawspost-migration-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task LimitPetAdSpeciesToList_LegacyFreeTextSpecies_AreMappedToTheFixedList()
    {
        // Arrange: build the old schema, then store old free-text species with raw SQL.
        var legacy = new (string Id, string Species)[]
        {
            ("00000000-0000-0000-0000-000000000001", "dog"),
            ("00000000-0000-0000-0000-000000000002", " Dog "),
            ("00000000-0000-0000-0000-000000000003", "Hamster"),
            ("00000000-0000-0000-0000-000000000004", "куче"),
            ("00000000-0000-0000-0000-000000000005", "CAT"),
            ("00000000-0000-0000-0000-000000000006", "Bird"),
            ("00000000-0000-0000-0000-000000000007", "rabbit"),
        };
        await using (var db = CreateDbContext())
        {
            await db.GetService<IMigrator>().MigrateAsync(PreviousMigration);
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO AspNetUsers (Id, AccessFailedCount, EmailConfirmed, LockoutEnabled, PhoneNumberConfirmed, TwoFactorEnabled) VALUES ({0}, 0, 0, 0, 0, 0)",
                OwnerId.ToString());
            foreach (var (id, species) in legacy)
            {
                await db.Database.ExecuteSqlRawAsync(
                    "INSERT INTO PetAds (Id, Title, Type, Species, AgeInMonths, Description, Status, OwnerId, CreatedAt) " +
                    "VALUES ({0}, 'Title', 'Adoption', {1}, 12, 'Description', 'Active', {2}, '2026-01-01 12:00:00')",
                    id, species, OwnerId.ToString());
            }
        }

        // Act: apply the remaining migrations (the species migration).
        await using (var db = CreateDbContext())
        {
            await db.Database.MigrateAsync();
        }

        // Assert
        await using var check = CreateDbContext();
        var ads = await check.PetAds.AsNoTracking().ToListAsync();
        var speciesById = ads.ToDictionary(ad => ad.Id.ToString(), ad => ad.Species);
        Assert.Equal(legacy.Length, ads.Count);
        Assert.Equal(PetSpecies.Dog, speciesById["00000000-0000-0000-0000-000000000001"]);
        Assert.Equal(PetSpecies.Dog, speciesById["00000000-0000-0000-0000-000000000002"]);
        Assert.Equal(PetSpecies.Other, speciesById["00000000-0000-0000-0000-000000000003"]);
        Assert.Equal(PetSpecies.Other, speciesById["00000000-0000-0000-0000-000000000004"]);
        Assert.Equal(PetSpecies.Cat, speciesById["00000000-0000-0000-0000-000000000005"]);
        Assert.Equal(PetSpecies.Bird, speciesById["00000000-0000-0000-0000-000000000006"]);
        Assert.Equal(PetSpecies.Rabbit, speciesById["00000000-0000-0000-0000-000000000007"]);
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
