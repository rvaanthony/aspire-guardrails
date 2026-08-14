#if (UseEfCore)
using Microsoft.EntityFrameworkCore;
using Template.Data.Persistence.Entities;

namespace Template.Data.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<ContactSubmissionRecord> ContactSubmissions => Set<ContactSubmissionRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var contact = modelBuilder.Entity<ContactSubmissionRecord>();
        contact.ToTable("contact_submissions");
        contact.HasKey(x => x.Id);
        contact.Property(x => x.Topic).HasMaxLength(64).IsRequired();
        contact.Property(x => x.Name).HasMaxLength(120).IsRequired();
        contact.Property(x => x.Email).HasMaxLength(254).IsRequired();
        contact.Property(x => x.Organization).HasMaxLength(160);
        contact.Property(x => x.Message).HasMaxLength(4000).IsRequired();
        contact.Property(x => x.Status).HasMaxLength(32).IsRequired();
        contact.HasIndex(x => x.CreatedUtc);
    }
}
#endif
