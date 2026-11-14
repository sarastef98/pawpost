using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PawsPost.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<User, Guid>(options)
{
    public DbSet<PetAd> PetAds => Set<PetAd>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PetAd>(ad =>
        {
            ad.HasKey(petAd => petAd.Id);
            ad.Property(petAd => petAd.Type).HasConversion<string>().HasMaxLength(20).IsRequired();
            ad.Property(petAd => petAd.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
            ad.Property(petAd => petAd.Title).HasMaxLength(100).IsRequired();
            ad.Property(petAd => petAd.Species).HasMaxLength(50).IsRequired();
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
        });

        // Identity's default email index is not unique. The database must also guarantee unique emails.
        builder.Entity<User>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique();
    }
}
