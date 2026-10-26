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
    public DbSet<TournamentTemplate> TournamentTemplates => Set<TournamentTemplate>();
    public DbSet<TemplateMatch> TemplateMatches => Set<TemplateMatch>();
    public DbSet<TemplateMatchFeed> TemplateMatchFeeds => Set<TemplateMatchFeed>();
    public DbSet<TournamentMatch> TournamentMatches => Set<TournamentMatch>();
    public DbSet<TournamentMatchFeed> TournamentMatchFeeds => Set<TournamentMatchFeed>();
    public DbSet<TemplateCanvasRound> TemplateCanvasRounds => Set<TemplateCanvasRound>();
    public DbSet<TournamentCanvasRound> TournamentCanvasRounds => Set<TournamentCanvasRound>();
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
            entity.Property(e => e.GraphWidthCells).HasDefaultValue(30);
            entity.Property(e => e.GraphHeightCells).HasDefaultValue(10);
            entity.Property(e => e.FormatDescription).HasMaxLength(4000);
            entity.Property(e => e.UniqueFactions).HasDefaultValue(false);
            entity.HasOne(e => e.TournamentTemplate)
                .WithMany()
                .HasForeignKey(e => e.TournamentTemplateId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.WinnerUser)
                .WithMany()
                .HasForeignKey(e => e.WinnerUserId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.FinaleTournamentMatch)
                .WithMany()
                .HasForeignKey(e => e.FinaleTournamentMatchId)
                .OnDelete(DeleteBehavior.NoAction);
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
            entity.Property(e => e.IsActive).HasDefaultValue(true);
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
        modelBuilder.Entity<TournamentTemplate>(entity =>
        {
            entity.ToTable("TournamentTemplates");
            entity.HasKey(e => e.TournamentTemplateId);
            entity.Property(e => e.TournamentTemplateId).UseIdentityColumn();
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.GraphWidthCells).HasDefaultValue(30);
            entity.Property(e => e.GraphHeightCells).HasDefaultValue(10);
            entity.Property(e => e.FormatDescription).HasMaxLength(4000);
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
        });
        modelBuilder.Entity<TemplateMatch>(entity =>
        {
            entity.ToTable("TemplateMatches");
            entity.HasKey(e => e.TemplateMatchId);
            entity.Property(e => e.TemplateMatchId).UseIdentityColumn();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.SlotAOpenLabel).HasMaxLength(200);
            entity.Property(e => e.SlotBOpenLabel).HasMaxLength(200);
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
            entity.HasOne(e => e.TournamentTemplate)
                .WithMany(t => t.Matches)
                .HasForeignKey(e => e.TournamentTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TournamentMatch>(entity =>
        {
            entity.ToTable("TournamentMatches");
            entity.HasKey(e => e.TournamentMatchId);
            entity.Property(e => e.TournamentMatchId).UseIdentityColumn();
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.SlotAOpenLabel).HasMaxLength(200);
            entity.Property(e => e.SlotBOpenLabel).HasMaxLength(200);
            entity.Property(e => e.ReplayUrl).HasMaxLength(2000);
            entity.Property(e => e.Status).HasConversion<int>();
            entity.Property(e => e.PlayMode).HasConversion<int>();
            entity.Property(e => e.Winner).HasConversion<int>();
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
            entity.HasOne(e => e.Tournament)
                .WithMany()
                .HasForeignKey(e => e.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.SlotAUser)
                .WithMany()
                .HasForeignKey(e => e.SlotAUserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SlotBUser)
                .WithMany()
                .HasForeignKey(e => e.SlotBUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TournamentMatchFeed>(entity =>
        {
            entity.ToTable("TournamentMatchFeeds");
            entity.HasKey(e => e.TournamentMatchFeedId);
            entity.Property(e => e.TournamentMatchFeedId).UseIdentityColumn();
            entity.Property(e => e.TargetSlot).HasConversion<int>();
            entity.Property(e => e.SourceOutcome).HasConversion<int>();
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
            entity.HasIndex(e => new { e.TargetTournamentMatchId, e.TargetSlot }).IsUnique();
            entity.HasIndex(e => new { e.SourceTournamentMatchId, e.SourceOutcome }).IsUnique();
            entity.HasOne(e => e.TargetMatch)
                .WithMany(m => m.IncomingFeeds)
                .HasForeignKey(e => e.TargetTournamentMatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SourceMatch)
                .WithMany(m => m.OutgoingFeeds)
                .HasForeignKey(e => e.SourceTournamentMatchId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TemplateCanvasRound>(entity =>
        {
            entity.ToTable("TemplateCanvasRounds");
            entity.HasKey(e => e.TemplateCanvasRoundId);
            entity.Property(e => e.TemplateCanvasRoundId).UseIdentityColumn();
            entity.Property(e => e.Scenario).HasConversion<int>();
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
            entity.HasIndex(e => new { e.TournamentTemplateId, e.SortOrder }).IsUnique();
            entity.HasOne(e => e.TournamentTemplate)
                .WithMany()
                .HasForeignKey(e => e.TournamentTemplateId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TournamentCanvasRound>(entity =>
        {
            entity.ToTable("TournamentCanvasRounds");
            entity.HasKey(e => e.TournamentCanvasRoundId);
            entity.Property(e => e.TournamentCanvasRoundId).UseIdentityColumn();
            entity.Property(e => e.Scenario).HasConversion<int>();
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
            entity.HasIndex(e => new { e.TournamentId, e.SortOrder }).IsUnique();
            entity.HasOne(e => e.Tournament)
                .WithMany()
                .HasForeignKey(e => e.TournamentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TemplateMatchFeed>(entity =>
        {
            entity.ToTable("TemplateMatchFeeds");
            entity.HasKey(e => e.TemplateMatchFeedId);
            entity.Property(e => e.TemplateMatchFeedId).UseIdentityColumn();
            entity.Property(e => e.TargetSlot).HasConversion<int>();
            entity.Property(e => e.SourceOutcome).HasConversion<int>();
            entity.Property(e => e.TimeCreatedUtc).HasColumnType("datetimeoffset");
            entity.Property(e => e.TimeUpdatedUtc).HasColumnType("datetimeoffset");
            entity.HasIndex(e => new { e.TargetTemplateMatchId, e.TargetSlot }).IsUnique();
            entity.HasIndex(e => new { e.SourceTemplateMatchId, e.SourceOutcome }).IsUnique();
            entity.HasOne(e => e.TargetMatch)
                .WithMany(m => m.IncomingFeeds)
                .HasForeignKey(e => e.TargetTemplateMatchId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.SourceMatch)
                .WithMany(m => m.OutgoingFeeds)
                .HasForeignKey(e => e.SourceTemplateMatchId)
                .OnDelete(DeleteBehavior.Restrict);
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
