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

    public DbSet<TournamentParticipant> TournamentParticipants => Set<TournamentParticipant>();

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
        modelBuilder.Entity<TournamentParticipant>(entity =>
        {
            entity.ToTable("TournamentParticipants");
            entity.HasKey(e => e.TournamentParticipantId);
            entity.Property(e => e.TournamentParticipantId).UseIdentityColumn();
            entity.HasIndex(e => new { e.TournamentId, e.UserId }).IsUnique();
            entity.Property(e => e.SquadFaction).HasMaxLength(100);
            entity.Property(e => e.SquadShipsJson).HasMaxLength(4000);
            entity.Property(e => e.SquadUrl).HasMaxLength(2000);
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
            entity.HasOne(e => e.Tournament)
                .WithMany()
                .HasForeignKey(e => e.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
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
