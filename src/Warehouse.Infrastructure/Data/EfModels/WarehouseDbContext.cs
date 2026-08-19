using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace Warehouse.Infrastructure.Data.EfModels;

public partial class WarehouseDbContext : DbContext
{
    public WarehouseDbContext()
    {
    }

    public WarehouseDbContext(DbContextOptions<WarehouseDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Product> Products { get; set; }

    public virtual DbSet<Productimage> Productimages { get; set; }

    public virtual DbSet<Shipment> Shipments { get; set; }

    public virtual DbSet<ShipmentLine> ShipmentLines { get; set; }

    public virtual DbSet<ShipmentStatusChange> ShipmentStatusChanges { get; set; }

    public virtual DbSet<Supplier> Suppliers { get; set; }

    public virtual DbSet<WarehouseFile> WarehouseFiles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? throw new InvalidOperationException(
                    "ConnectionStrings__DefaultConnection is not set. Required for design-time tooling (e.g. dotnet ef) when no DI-configured options are supplied.");

            optionsBuilder.UseNpgsql(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Products_pkey");

            entity.HasIndex(e => e.Sku, "Products_SKU_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.ExpiryDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.IsArchived).ValueGeneratedNever();
            entity.Property(e => e.LastUpdatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.QuantityInStock).ValueGeneratedNever();
            entity.Property(e => e.Sku)
                .HasMaxLength(100);
            entity.Property(e => e.SupplierName).HasMaxLength(255);

            entity.HasOne(d => d.Supplier).WithMany(p => p.Products)
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("Products_SupplierId_fkey");
        });

        modelBuilder.Entity<Productimage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Productimages_pkey");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.FileName).HasMaxLength(255);
            entity.Property(e => e.FilePath).HasMaxLength(500);

            entity.HasOne(d => d.Product).WithMany(p => p.Productimages)
                .HasForeignKey(d => d.ProductId)
                .HasConstraintName("Productimages_ProductId_fkey");
        });

        modelBuilder.Entity<Shipment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("Shipments_pkey");

            entity.HasIndex(e => e.ReferenceNumber, "Shipments_ReferenceNumber_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ReferenceNumber).HasMaxLength(50);
            entity.Property(e => e.SupplierName).HasMaxLength(255);
            entity.Property(e => e.Status).HasMaxLength(40);
            entity.Property(e => e.DestinationLine1).HasMaxLength(255);
            entity.Property(e => e.DestinationCity).HasMaxLength(120);
            entity.Property(e => e.DestinationPostalCode).HasMaxLength(20);
            entity.Property(e => e.DestinationCountry).HasMaxLength(100);
            entity.Property(e => e.TrackingNumber).HasMaxLength(100);
            entity.Property(e => e.ExpectedDeliveryDate).HasColumnType("timestamp without time zone");
            entity.Property(e => e.DispatchedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.DeliveredAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.SupplierNotifiedAt).HasColumnType("timestamp without time zone");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone");
            entity.Property(e => e.LastUpdatedAt).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Supplier).WithMany()
                .HasForeignKey(d => d.SupplierId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("Shipments_SupplierId_fkey");
        });

        modelBuilder.Entity<ShipmentLine>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ShipmentLines_pkey");

            // One line per product per shipment: AssignProduct tops up an existing line
            // instead of adding a second one, and the database enforces the same thing.
            entity.HasIndex(e => new { e.ShipmentId, e.ProductId }, "ShipmentLines_ShipmentId_ProductId_key").IsUnique();

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ProductName).HasMaxLength(255);
            entity.Property(e => e.Sku).HasMaxLength(100);
            entity.Property(e => e.Quantity).ValueGeneratedNever();

            entity.HasOne(d => d.Shipment).WithMany(p => p.Lines)
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ShipmentLines_ShipmentId_fkey");

            entity.HasOne(d => d.Product).WithMany()
                .HasForeignKey(d => d.ProductId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("ShipmentLines_ProductId_fkey");
        });

        modelBuilder.Entity<ShipmentStatusChange>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("ShipmentStatusChanges_pkey");

            entity.HasIndex(e => new { e.ShipmentId, e.OccurredAt }, "ShipmentStatusChanges_ShipmentId_OccurredAt_idx");

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.FromStatus).HasMaxLength(40);
            entity.Property(e => e.ToStatus).HasMaxLength(40);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.Property(e => e.OccurredAt).HasColumnType("timestamp without time zone");

            entity.HasOne(d => d.Shipment).WithMany(p => p.StatusHistory)
                .HasForeignKey(d => d.ShipmentId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("ShipmentStatusChanges_ShipmentId_fkey");
        });

        modelBuilder.Entity<Supplier>(entity =>
        {
            entity.HasKey(e => e.SupplierId).HasName("Suppliers_pkey");

            entity.Property(e => e.SupplierId)
                .HasColumnName("Id")
                .ValueGeneratedNever();
            entity.Property(e => e.ContactEmail).HasMaxLength(255);
            entity.Property(e => e.Country).HasMaxLength(100);
            entity.Property(e => e.IsActive).ValueGeneratedNever();
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.PhoneNumber).HasMaxLength(50);
        });

        modelBuilder.Entity<WarehouseFile>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Id).ValueGeneratedNever();
            entity.Property(e => e.ObjectKey).IsRequired().HasMaxLength(500);
            entity.Property(e => e.FileName).IsRequired().HasMaxLength(255);
            entity.Property(e => e.ContentType).IsRequired().HasMaxLength(150);
            entity.Property(e => e.UploadedByUid).IsRequired().HasMaxLength(128);
            entity.Property(e => e.RelatedEntityType).IsRequired().HasMaxLength(100);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
