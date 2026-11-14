using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace PawsPost.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityUserContext<User, Guid>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Identity's default email index is not unique. The database must also guarantee unique emails.
        builder.Entity<User>()
            .HasIndex(user => user.NormalizedEmail)
            .IsUnique();
    }
}
