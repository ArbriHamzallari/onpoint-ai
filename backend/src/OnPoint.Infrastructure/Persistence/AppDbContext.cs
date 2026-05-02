using Microsoft.EntityFrameworkCore;
using OnPoint.Domain;

namespace OnPoint.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        // Example table
        public DbSet<Feedback> Feedbacks { get; set; }
    }
}