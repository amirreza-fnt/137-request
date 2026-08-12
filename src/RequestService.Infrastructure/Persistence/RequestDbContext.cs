using Microsoft.EntityFrameworkCore;
using RequestService.Domain.Entities;

namespace RequestService.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the request-service. Schema follows the sibling-services
/// convention: GUID PKs, enums stored as strings, explicit indexes for the hot
/// query paths (tracking code lookup, cartable search by status/group/date).
/// </summary>
public sealed class RequestDbContext : DbContext
{
    public RequestDbContext(DbContextOptions<RequestDbContext> options)
        : base(options)
    {
    }

    public DbSet<Request> Requests => Set<Request>();
    public DbSet<RequestFile> RequestFiles => Set<RequestFile>();
    public DbSet<RequestLogItem> RequestLogs => Set<RequestLogItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Request>(e =>
        {
            e.ToTable("Requests");
            e.HasKey(x => x.Id);

            e.Property(x => x.TrackingCode).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.TrackingCode).IsUnique();

            e.Property(x => x.NationalCode).HasMaxLength(20);
            e.HasIndex(x => x.NationalCode);

            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.LocationLat).HasPrecision(18, 6);
            e.Property(x => x.LocationLng).HasPrecision(18, 6);

            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();

            e.Property(x => x.CurrentGroupId).HasMaxLength(64);
            e.HasIndex(x => x.CurrentGroupId);

            e.Property(x => x.CreatedBySourcePhone).HasMaxLength(20);

            e.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<RequestFile>(e =>
        {
            e.ToTable("RequestFiles");
            e.HasKey(x => x.Id);

            e.Property(x => x.FileId).HasMaxLength(64).IsRequired();
            e.Property(x => x.FileType).HasConversion<string>().HasMaxLength(16).IsRequired();

            e.HasOne(x => x.Request)
                .WithMany(x => x.Files)
                .HasForeignKey(x => x.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.RequestId, x.FileId }).IsUnique();
        });

        modelBuilder.Entity<RequestLogItem>(e =>
        {
            e.ToTable("RequestLogs");
            e.HasKey(x => x.Id);

            e.Property(x => x.ActionType).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(x => x.ActorType).HasConversion<string>().HasMaxLength(32).IsRequired();
            e.Property(x => x.ActorId).HasMaxLength(64);
            e.Property(x => x.PreviousStatus).HasMaxLength(32);
            e.Property(x => x.NewStatus).HasMaxLength(32);
            e.Property(x => x.PreviousGroupId).HasMaxLength(64);
            e.Property(x => x.NewGroupId).HasMaxLength(64);
            e.Property(x => x.Description).HasMaxLength(4000);

            e.HasOne(x => x.Request)
                .WithMany(x => x.Logs)
                .HasForeignKey(x => x.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasIndex(x => new { x.RequestId, x.CreatedAtUtc });
        });
    }
}
