using Microsoft.EntityFrameworkCore;

namespace Xwing.Web.Data;

public sealed class XwingDbContext : DbContext
{
    private readonly TimeProvider timeProvider;

    public XwingDbContext(DbContextOptions<XwingDbContext> options, TimeProvider timeProvider)
        : base(options)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        this.timeProvider = timeProvider;
    }

    public DbSet<Tournament> Tournaments => Set<Tournament>();

    public DbSet<User> Users => Set<User>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        StampTimes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        StampTimes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        modelBuilder.Entity<Tournament>(entity =>
        {
            entity.ToTable("Tournaments");
            entity.HasKey(e => e.TournamentId);
            entity.Property(e => e.TournamentId).UseIdentityColumn();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.StartDateUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.EndDateUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
        });
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).UseIdentityColumn();
            entity.Property(e => e.UserName).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => e.UserName).IsUnique();
            entity.Property(e => e.PasswordHash).HasMaxLength(500).IsRequired();
            entity.Property(e => e.Role).HasMaxLength(50).IsRequired();
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
        });
        foreach (Type clrType in modelBuilder.Model.GetEntityTypes().Select(entityType => entityType.ClrType))
        {
            if (!typeof(BaseModel).IsAssignableFrom(clrType))
            {
                continue;
            }
            modelBuilder.Entity(clrType)
                .Property(nameof(BaseModel.RowVersion))
                .IsRowVersion();
        }
    }

    private void StampTimes()
    {
        DateTimeOffset utcNow = timeProvider.GetUtcNow();
        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<BaseModel> entry in ChangeTracker.Entries<BaseModel>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.TimeCreatedUtc = utcNow;
                entry.Entity.TimeUpdatedUtc = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.TimeUpdatedUtc = utcNow;
                entry.Property(e => e.TimeCreatedUtc).IsModified = false;
            }
        }
    }
}
