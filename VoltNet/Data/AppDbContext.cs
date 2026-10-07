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
    public virtual DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
    public virtual DbSet<OwnerSubscription> OwnerSubscriptions { get; set; }
    public virtual DbSet<ChargingRate> ChargingRates { get; set; }
    public virtual DbSet<Booking> Bookings { get; set; }
    public virtual DbSet<WaitlistEntry> WaitlistEntries { get; set; }
    public virtual DbSet<StationBlockSlot> StationBlockSlots { get; set; }

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
            entity.Property(e => e.City)
                .HasMaxLength(100)
                .HasColumnName("city");
            entity.Property(e => e.State)
                .HasMaxLength(100)
                .HasColumnName("state");
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

            entity.HasOne(e => e.OwnerSubscription)
                .WithMany(s => s.Stations)
                .HasForeignKey(e => e.OwnerSubscriptionId)
                .OnDelete(DeleteBehavior.SetNull);

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
            entity.Property(e => e.CanManageRates)
                .HasColumnName("can_manage_rates")
                .HasDefaultValue(false);
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

        // -- ChargingRates -------------------------------------------
        modelBuilder.Entity<ChargingRate>(entity =>
        {
            entity.ToTable("ChargingRates");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.StationId)
                .HasColumnName("station_id");
            entity.Property(e => e.ConnectorType)
                .HasMaxLength(50)
                .HasColumnName("connector_type");
            entity.Property(e => e.RatePerKwh)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("rate_per_kwh");
            entity.Property(e => e.RatePerHour)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("rate_per_hour");
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt)
                .HasColumnName("updated_at");

            entity.HasOne(e => e.Station)
                .WithMany(s => s.ChargingRates)
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.StationId).HasDatabaseName("IX_ChargingRates_StationId");
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

        // -- SubscriptionPlan ----------------------------------------
        modelBuilder.Entity<SubscriptionPlan>(entity =>
        {
            entity.ToTable("SubscriptionPlans");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.Description)
                .HasMaxLength(500)
                .HasColumnName("description");
            entity.Property(e => e.PricePerMonth)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("price_per_month");
            entity.Property(e => e.MaxStations)
                .HasColumnName("max_stations");
            entity.Property(e => e.MaxManagersPerStation)
                .HasColumnName("max_managers_per_station");
            entity.Property(e => e.DurationDays)
                .HasColumnName("duration_days");
            entity.Property(e => e.IsActive)
                .HasColumnName("is_active")
                .HasDefaultValue(true);
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");
        });

        // -- OwnerSubscription ---------------------------------------
        modelBuilder.Entity<OwnerSubscription>(entity =>
        {
            entity.ToTable("OwnerSubscriptions");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.StationOwnerId)
                .HasColumnName("station_owner_id");
            entity.Property(e => e.PlanId)
                .HasColumnName("plan_id");
            entity.Property(e => e.StartDate)
                .HasColumnName("start_date");
            entity.Property(e => e.EndDate)
                .HasColumnName("end_date");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("status")
                .HasDefaultValue("Active");
            entity.Property(e => e.PaymentId)
                .HasMaxLength(100)
                .HasColumnName("payment_id");
            entity.Property(e => e.AmountPaid)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("amount_paid");
            entity.Property(e => e.CancellationReason)
                .HasMaxLength(500)
                .HasColumnName("cancellation_reason");
            entity.Property(e => e.CancellationRequestedAt)
                .HasColumnName("cancellation_requested_at");
            entity.Property(e => e.CancellationProcessedAt)
                .HasColumnName("cancellation_processed_at");
            entity.Property(e => e.CancellationAdminRemarks)
                .HasMaxLength(500)
                .HasColumnName("cancellation_admin_remarks");
            entity.Property(e => e.RefundAmount)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("refund_amount");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.StationOwnerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Plan)
                .WithMany()
                .HasForeignKey(e => e.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // -- Bookings ------------------------------------------------
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Bookings");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.CustomerId)
                .HasColumnName("customer_id");
            entity.Property(e => e.StationId)
                .HasColumnName("station_id");
            entity.Property(e => e.ChargerId)
                .HasColumnName("charger_id");
            entity.Property(e => e.BookingDate)
                .HasColumnType("date")
                .HasColumnName("booking_date");
            entity.Property(e => e.StartTime)
                .HasColumnName("start_time");
            entity.Property(e => e.EndTime)
                .HasColumnName("end_time");
            entity.Property(e => e.Status)
                .HasMaxLength(30)
                .IsUnicode(false)
                .HasColumnName("status")
                .HasDefaultValue("Confirmed");
            entity.Property(e => e.EstimatedCost)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("estimated_cost");
            entity.Property(e => e.ActualCost)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("actual_cost");
            entity.Property(e => e.CancellationReason)
                .HasMaxLength(500)
                .HasColumnName("cancellation_reason");
            entity.Property(e => e.CancelledAt)
                .HasColumnName("cancelled_at");
            entity.Property(e => e.CompletedAt)
                .HasColumnName("completed_at");
            entity.Property(e => e.PaymentId)
                .HasMaxLength(100)
                .HasColumnName("payment_id");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UnitsConsumedKwh)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("units_consumed_kwh");
            entity.Property(e => e.StartMeterReading)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("start_meter_reading");
            entity.Property(e => e.EndMeterReading)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("end_meter_reading");
            entity.Property(e => e.AppliedRatePerKwh)
                .HasColumnType("decimal(18,2)")
                .HasColumnName("applied_rate_per_kwh");
            entity.Property(e => e.MeterPhotoUrl)
                .HasMaxLength(500)
                .HasColumnName("meter_photo_url");
            entity.Property(e => e.VehicleNumberPlate)
                .HasMaxLength(30)
                .HasColumnName("vehicle_number_plate");
            entity.Property(e => e.VehicleModel)
                .HasMaxLength(100)
                .HasColumnName("vehicle_model");
            entity.Property(e => e.CustomerPhone)
                .HasMaxLength(20)
                .HasColumnName("customer_phone");
            entity.Property(e => e.CustomerName)
                .HasMaxLength(150)
                .HasColumnName("customer_name");
            entity.Property(e => e.IsWalkIn)
                .HasColumnName("is_walk_in")
                .HasDefaultValue(false);

            entity.HasOne(e => e.Customer)
                .WithMany()
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Station)
                .WithMany()
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Charger)
                .WithMany()
                .HasForeignKey(e => e.ChargerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ChargerId, e.BookingDate, e.Status }).HasDatabaseName("IX_Bookings_Charger_Date_Status");
            entity.HasIndex(e => e.CustomerId).HasDatabaseName("IX_Bookings_CustomerId");
            entity.HasIndex(e => e.StationId).HasDatabaseName("IX_Bookings_StationId");
        });

        // -- WaitlistEntry -------------------------------------------
        modelBuilder.Entity<WaitlistEntry>(entity =>
        {
            entity.ToTable("WaitlistEntries");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.StationId).HasColumnName("station_id");
            entity.Property(e => e.ChargerId).HasColumnName("charger_id");
            entity.Property(e => e.CustomerId).HasColumnName("customer_id");
            entity.Property(e => e.BookingDate).HasColumnType("date").HasColumnName("booking_date");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.QueuePosition).HasColumnName("queue_position").HasDefaultValue(1);
            entity.Property(e => e.Status).HasMaxLength(30).IsUnicode(false).HasColumnName("status").HasDefaultValue("Waiting");
            entity.Property(e => e.VehicleNumberPlate).HasMaxLength(50).HasColumnName("vehicle_number_plate");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.PromotedAt).HasColumnName("promoted_at");

            entity.HasOne(e => e.Station).WithMany().HasForeignKey(e => e.StationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Charger).WithMany().HasForeignKey(e => e.ChargerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Customer).WithMany().HasForeignKey(e => e.CustomerId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ChargerId, e.BookingDate, e.StartTime, e.Status }).HasDatabaseName("IX_Waitlist_Charger_Slot");
        });

        // -- StationBlockSlot ----------------------------------------
        modelBuilder.Entity<StationBlockSlot>(entity =>
        {
            entity.ToTable("StationBlockSlots");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.StationId).HasColumnName("station_id");
            entity.Property(e => e.ChargerId).HasColumnName("charger_id");
            entity.Property(e => e.BlockDate).HasColumnType("date").HasColumnName("block_date");
            entity.Property(e => e.StartTime).HasColumnName("start_time");
            entity.Property(e => e.EndTime).HasColumnName("end_time");
            entity.Property(e => e.Reason).HasMaxLength(100).HasColumnName("reason");
            entity.Property(e => e.Remarks).HasMaxLength(300).HasColumnName("remarks");
            entity.Property(e => e.CreatedByManagerId).HasColumnName("created_by_manager_id");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Station).WithMany().HasForeignKey(e => e.StationId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Charger).WithMany().HasForeignKey(e => e.ChargerId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.CreatedByManager).WithMany().HasForeignKey(e => e.CreatedByManagerId).OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.StationId, e.BlockDate }).HasDatabaseName("IX_StationBlockSlots_Date");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

