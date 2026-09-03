using Microsoft.EntityFrameworkCore;
using OrderService.Domain.Entities;

namespace OrderService.Infrastructure.Data;

public class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<StatusHistory> StatusHistories => Set<StatusHistory>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Order>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.ClientId).IsRequired();
            e.Property(x => x.Status).IsRequired().HasConversion<int>();
            e.Property(x => x.CreatedAt).IsRequired();
            e.Property(x => x.UpdatedAt).IsRequired();
            e.HasIndex(x => x.ClientId);
            e.HasIndex(x => x.Status);

            e.OwnsOne(x => x.Cargo, c =>
            {
                c.Property(p => p.CargoType).IsRequired().HasMaxLength(50);
                c.Property(p => p.Origin).IsRequired().HasMaxLength(200);
                c.Property(p => p.Destination).IsRequired().HasMaxLength(200);
                c.Property(p => p.Description).HasMaxLength(500);
                c.Property(p => p.WeightKg).IsRequired();
                c.HasIndex(p => p.Deadline);
            });

            e.HasMany(x => x.History)
                .WithOne(h => h.Order)
                .HasForeignKey(h => h.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            e.Navigation(x => x.History).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        b.Entity<StatusHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.OrderId).IsRequired();
            e.Property(x => x.FromStatus).HasConversion<int>();
            e.Property(x => x.ToStatus).HasConversion<int>();
            e.Property(x => x.ChangedAt).IsRequired();
            e.Property(x => x.ChangedBy).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(500);
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.ChangedAt);
        });
    }
}
