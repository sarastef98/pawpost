using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PawsPost.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<User, Guid>(options)
{
    public DbSet<PetAd> PetAds => Set<PetAd>();

    public DbSet<PetAdEmbedding> PetAdEmbeddings => Set<PetAdEmbedding>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PetAdEmbedding>(embedding =>
        {
            embedding.HasKey(item => item.PetAdId);
            embedding.Property(item => item.Vector).IsRequired();
            embedding.Property(item => item.Model).HasMaxLength(100).IsRequired();

            // A hard-deleted ad must not leave a vector behind.
            embedding.HasOne<PetAd>()
                .WithOne()
                .HasForeignKey<PetAdEmbedding>(item => item.PetAdId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PetAd>(ad =>
        {
            ad.HasKey(petAd => petAd.Id);
            ad.Property(petAd => petAd.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            ad.Property(petAd => petAd.Status).HasConversion<string>().HasMaxLength(20).IsRequired()
                .IsConcurrencyToken();
            ad.Property(petAd => petAd.Title).HasMaxLength(100).IsRequired();
            ad.Property(petAd => petAd.Species).HasConversion<string>().HasMaxLength(20).IsRequired();
            ad.Property(petAd => petAd.Breed).HasMaxLength(100);
            ad.Property(petAd => petAd.Description).HasMaxLength(2000).IsRequired();
            ad.Property(petAd => petAd.Location).HasMaxLength(100);
            ad.Property(petAd => petAd.Price).HasPrecision(18, 2);
            ad.Property(petAd => petAd.OwnerId).IsRequired();

            ad.HasOne(petAd => petAd.Owner)
                .WithMany()
                .HasForeignKey(petAd => petAd.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            ad.HasIndex(petAd => petAd.Species);
            ad.HasIndex(petAd => petAd.Type);
            ad.HasIndex(petAd => petAd.Status);
            ad.HasIndex(petAd => petAd.CreatedAt);
            ad.HasIndex(petAd => petAd.OwnerId);
            ad.HasIndex(petAd => new { petAd.Status, petAd.CreatedAt });
            ad.HasIndex(petAd => new { petAd.OwnerId, petAd.Status, petAd.CreatedAt });
        });

        // Identity's default email index is not unique. The database must also guarantee unique emails.
        builder.Entity<User>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique();

        // Display names are unique ignoring case. The unique index on the normalized copy makes the database
        // enforce it for every letter (SQLite NOCASE would only fold A to Z), so parallel requests cannot both win.
        builder.Entity<User>(user =>
        {
            user.Property(item => item.DisplayName).HasMaxLength(30).IsRequired();
            user.Property(item => item.NormalizedDisplayName).HasMaxLength(30).IsRequired();
            user.HasIndex(item => item.NormalizedDisplayName).IsUnique();
        });
    }
}
