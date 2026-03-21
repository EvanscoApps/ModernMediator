using Microsoft.EntityFrameworkCore;

namespace ModernMediator.Audit.EntityFramework;

/// <summary>
/// A dedicated <see cref="DbContext"/> for persisting <see cref="AuditRecord"/>
/// instances. Kept separate from the caller's application context to avoid
/// transactional coupling and thread-safety issues. See ADR-003.
/// </summary>
public class AuditDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of <see cref="AuditDbContext"/>.
    /// </summary>
    public AuditDbContext(DbContextOptions<AuditDbContext> options)
        : base(options) { }

    /// <summary>
    /// Gets the <see cref="DbSet{TEntity}"/> of audit records.
    /// </summary>
    public DbSet<AuditRecord> AuditRecords => Set<AuditRecord>();

    /// <inheritdoc/>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new AuditRecordEntityTypeConfiguration());
    }
}
