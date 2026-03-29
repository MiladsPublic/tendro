using Microsoft.EntityFrameworkCore;

namespace Samba.ApiServer.Modern.Data;

/// <summary>
/// EF Core DbContext for SambaPOS Phase 3 modern data access layer.
/// Replaces in-memory repositories with persistent SQL Server storage.
/// </summary>
public class SambaDbContext : DbContext
{
    public SambaDbContext(DbContextOptions<SambaDbContext> options) : base(options)
    {
    }

    public DbSet<TicketEntity> Tickets => Set<TicketEntity>();
    public DbSet<OrderEntity> Orders => Set<OrderEntity>();
    public DbSet<PaymentEntity> Payments => Set<PaymentEntity>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ticket entity configuration
        modelBuilder.Entity<TicketEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TicketNumber).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DepartmentId).IsRequired();
            entity.Property(e => e.TerminalId).IsRequired();
            entity.Property(e => e.TicketTypeId).IsRequired();
            entity.Property(e => e.StateName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.StateValue).HasMaxLength(500);
            entity.Property(e => e.IsClosed).IsRequired();
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();

            // Relationships
            entity.HasMany(e => e.Orders)
                .WithOne(o => o.Ticket)
                .HasForeignKey(o => o.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(e => e.Payments)
                .WithOne(p => p.Ticket)
                .HasForeignKey(p => p.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TicketNumber).IsUnique();
            entity.HasIndex(e => e.DepartmentId);
            entity.HasIndex(e => e.IsClosed);
            entity.HasIndex(e => e.CreatedAtUtc);
        });

        // Order entity configuration
        modelBuilder.Entity<OrderEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TicketId).IsRequired();
            entity.Property(e => e.MenuItemId).IsRequired();
            entity.Property(e => e.PortionName).HasMaxLength(100);
            entity.Property(e => e.Tags).HasMaxLength(500);
            entity.Property(e => e.Quantity).HasPrecision(10, 2).IsRequired();
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.DiscountAmount).HasPrecision(18, 2);
            entity.Property(e => e.TaxAmount).HasPrecision(18, 2);
            entity.Property(e => e.Status).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();

            // Foreign key relationship
            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Orders)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TicketId);
            entity.HasIndex(e => e.Status);
        });

        // Payment entity configuration
        modelBuilder.Entity<PaymentEntity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TicketId).IsRequired();
            entity.Property(e => e.PaymentTypeId).IsRequired();
            entity.Property(e => e.PaymentType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2).IsRequired();
            entity.Property(e => e.TenderedAmount).HasPrecision(18, 2);
            entity.Property(e => e.ChangeAmount).HasPrecision(18, 2);
            entity.Property(e => e.ReferenceNumber).HasMaxLength(100);
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.Property(e => e.UpdatedAtUtc).IsRequired();

            // Foreign key relationship
            entity.HasOne(e => e.Ticket)
                .WithMany(t => t.Payments)
                .HasForeignKey(e => e.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.TicketId);
            entity.HasIndex(e => e.PaymentType);
            entity.HasIndex(e => e.ReferenceNumber);
        });

        // Idempotency record configuration
        modelBuilder.Entity<IdempotencyRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IdempotencyKey).IsRequired().HasMaxLength(100);
            entity.Property(e => e.ResultJson).IsRequired();
            entity.Property(e => e.ExpiresAtUtc).IsRequired();
            entity.Property(e => e.CreatedAtUtc).IsRequired();

            entity.HasIndex(e => e.IdempotencyKey).IsUnique();
            entity.HasIndex(e => e.ExpiresAtUtc);
        });
    }
}
