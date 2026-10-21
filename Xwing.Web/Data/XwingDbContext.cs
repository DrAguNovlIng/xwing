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
