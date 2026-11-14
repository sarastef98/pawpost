using Microsoft.EntityFrameworkCore;

namespace PawsPost.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
}
