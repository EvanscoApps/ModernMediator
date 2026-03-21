using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ModernMediator.Audit.EntityFramework;

/// <summary>
/// Entity Framework Core configuration for <see cref="AuditRecord"/>.
/// Applied automatically by <see cref="AuditDbContext.OnModelCreating"/>.
/// Callers who prefer to host audit records in their existing database can apply
/// this configuration manually in their own <c>OnModelCreating</c>.
/// </summary>
public sealed class AuditRecordEntityTypeConfiguration
    : IEntityTypeConfiguration<AuditRecord>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditRecord> builder)
    {
        builder.ToTable("AuditRecords");
        builder.HasNoKey();

        builder.Property(r => r.RequestTypeName)
            .IsRequired()
            .HasMaxLength(512);

        builder.Property(r => r.SerializedPayload)
            .IsRequired()
            .HasColumnType("nvarchar(max)");

        builder.Property(r => r.UserId)
            .HasMaxLength(256);

        builder.Property(r => r.UserName)
            .HasMaxLength(256);

        builder.Property(r => r.Timestamp)
            .IsRequired();

        builder.Property(r => r.Succeeded)
            .IsRequired();

        builder.Property(r => r.FailureReason)
            .HasMaxLength(2048);

        builder.Property(r => r.Duration)
            .IsRequired()
            .HasConversion(
                v => v.Ticks,
                v => TimeSpan.FromTicks(v));

        builder.Property(r => r.CorrelationId)
            .HasMaxLength(256);
    }
}
