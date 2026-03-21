using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModernMediator.Idempotency.EntityFramework;

/// <summary>
/// A dedicated <see cref="DbContext"/> for persisting idempotency records.
/// Kept separate from the caller's application context following the same
/// pattern as <c>AuditDbContext</c>. See ADR-003 and ADR-004.
/// </summary>
public sealed class IdempotencyDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="IdempotencyDbContext"/>.
    /// </summary>
    public IdempotencyDbContext(DbContextOptions<IdempotencyDbContext> options)
        : base(options) { }

    /// <summary>
    /// Gets the <see cref="DbSet{TEntity}"/> of idempotency records.
    /// </summary>
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.ToTable("IdempotencyRecords");
            entity.HasKey(r => r.Fingerprint);

            entity.Property(r => r.Fingerprint)
                .IsRequired()
                .HasMaxLength(64)
                .IsFixedLength();

            entity.Property(r => r.SerializedResponse)
                .IsRequired()
                .HasColumnType("nvarchar(max)");

            entity.Property(r => r.ResponseTypeName)
                .IsRequired()
                .HasMaxLength(512);

            entity.Property(r => r.CachedAt)
                .IsRequired();

            entity.Property(r => r.ExpiresAt)
                .IsRequired();

            entity.HasIndex(r => r.ExpiresAt)
                .HasDatabaseName("IX_IdempotencyRecords_ExpiresAt");
        });
    }
}
