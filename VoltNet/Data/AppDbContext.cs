using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using VoltNet.Models;

namespace VoltNet.Data;

public partial class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminUser> AdminUsers { get; set; }
    public virtual DbSet<UserMaster> UserMasters { get; set; }
    public virtual DbSet<StationOwner> StationOwners { get; set; }
    public virtual DbSet<Station> Stations { get; set; }
    public virtual DbSet<StationManager> StationManagers { get; set; }

    public virtual DbSet<Charger> Chargers { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // -- AdminUser -----------------------------------------------
        modelBuilder.Entity<AdminUser>(entity =>
        {
            entity.ToTable("AdminUsers");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Username)
                .HasMaxLength(100)
                .HasColumnName("username");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Isactive)
                .HasColumnName("isactive")
                .HasDefaultValue(true);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.Role)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("role");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasIndex(e => e.Username)
                .IsUnique()
                .HasDatabaseName("IX_AdminUsers_Username");
        });

        // -- UserMaster ----------------------------------------------
        modelBuilder.Entity<UserMaster>(entity =>
        {
            entity.ToTable("UserMaster");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Email)
                .HasMaxLength(50)
                .HasColumnName("email");
            entity.Property(e => e.Fullname)
                .HasMaxLength(100)
                .HasColumnName("fullname");
            entity.Property(e => e.Isactive).HasColumnName("isactive");
            entity.Property(e => e.Mobile)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasColumnName("mobile");
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .HasColumnName("password");
            entity.Property(e => e.Role)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("role");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // -- StationOwner --------------------------------------------
        modelBuilder.Entity<StationOwner>(entity =>
        {
            entity.ToTable("StationOwners");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.UserId)
                .HasColumnName("user_id");
            entity.Property(e => e.FullName)
                .HasMaxLength(150)
                .HasColumnName("full_name");
            entity.Property(e => e.BusinessName)
                .HasMaxLength(150)
                .HasColumnName("business_name");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.BusinessRegistrationNumber)
                .HasMaxLength(100)
                .HasColumnName("business_registration_number");
            entity.Property(e => e.Address)
                .HasMaxLength(300)
                .HasColumnName("address");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.State)
                .HasMaxLength(100)
                .HasColumnName("state");
            entity.Property(e => e.GSTNumber)
                .HasMaxLength(20)
                .HasColumnName("gst_number");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("status")
                .HasDefaultValue("Pending");
            entity.Property(e => e.RejectionReason)
                .HasMaxLength(500)
                .HasColumnName("rejection_reason");
            entity.Property(e => e.ApprovedAt)
                .HasColumnName("approved_at");
            entity.Property(e => e.ApprovedBy)
                .HasColumnName("approved_by");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<StationOwner>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ApprovedByUser)
                .WithMany()
                .HasForeignKey(e => e.ApprovedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasDatabaseName("IX_StationOwners_UserId");
        });

        // -- Stations ------------------------------------------------
        modelBuilder.Entity<Station>(entity =>
        {
            entity.ToTable("Stations");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.OwnerUserId)
                .HasColumnName("owner_user_id");
            entity.Property(e => e.Address)
                .HasMaxLength(300)
                .HasColumnName("address");
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.State)
                .HasMaxLength(100)
                .HasColumnName("state");
            entity.Property(e => e.Latitude)
                .HasColumnType("decimal(18,9)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(18,9)")
                .HasColumnName("longitude");
            entity.Property(e => e.OpeningTime)
                .HasColumnName("opening_time");
            entity.Property(e => e.ClosingTime)
                .HasColumnName("closing_time");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.RejectionReason)
                .HasMaxLength(500)
                .HasColumnName("rejection_reason");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.OwnerUser)
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.City).HasDatabaseName("IX_Stations_City");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_Stations_Status");
        });

        // -- StationManagers -----------------------------------------
        modelBuilder.Entity<StationManager>(entity =>
        {
            entity.ToTable("StationManagers");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.StationId)
                .HasColumnName("station_id");
            entity.Property(e => e.UserId)
                .HasColumnName("user_id");
            entity.Property(e => e.FullName)
                .HasMaxLength(150)
                .HasColumnName("full_name");
            entity.Property(e => e.Phone)
                .HasMaxLength(20)
                .HasColumnName("phone");
            entity.Property(e => e.CreatedBy)
                .HasColumnName("created_by");
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Station)
                .WithMany()
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<StationManager>(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.CreatedByOwner)
                .WithMany()
                .HasForeignKey(e => e.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.StationId, e.UserId })
                .IsUnique()
                .HasDatabaseName("IX_StationManagers_StationId_UserId");

            entity.HasIndex(e => e.UserId)
                .IsUnique()
                .HasDatabaseName("IX_StationManagers_UserId");
        });

        // -- Chargers ------------------------------------------------
        modelBuilder.Entity<Charger>(entity =>
        {
            entity.ToTable("Chargers");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.StationId)
                .HasColumnName("station_id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.ConnectorType)
                .HasMaxLength(50)
                .HasColumnName("connector_type");
            entity.Property(e => e.CapacityKw)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("capacity_kw");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .HasColumnName("status")
                .HasDefaultValue("Active");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Station)
                .WithMany(s => s.Chargers)
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.Cascade); // If a station is deleted, chargers are deleted
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

