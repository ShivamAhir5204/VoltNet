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

    public virtual DbSet<UserMaster> UserMasters { get; set; }
    public virtual DbSet<FranchiseMaster> FranchiseMasters { get; set; }
    public virtual DbSet<Station> Stations { get; set; }
    public virtual DbSet<StationManager> StationManagers { get; set; }
    public virtual DbSet<Charger> Chargers { get; set; }
    public virtual DbSet<Booking> Bookings { get; set; }
    public virtual DbSet<ChargingSession> ChargingSessions { get; set; }
    public virtual DbSet<Wallet> Wallets { get; set; }
    public virtual DbSet<WalletTransaction> WalletTransactions { get; set; }
    public virtual DbSet<Invoice> Invoices { get; set; }
    public virtual DbSet<MaintenanceTicket> MaintenanceTickets { get; set; }
    public virtual DbSet<FranchiseSettlement> FranchiseSettlements { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── UserMaster ──────────────────────────────────────────────
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

        // ── FranchiseMaster ─────────────────────────────────────────
        modelBuilder.Entity<FranchiseMaster>(entity =>
        {
            entity.ToTable("FranchiseMaster");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.OwnerUserId)
                .HasColumnName("owner_user_id");
            entity.Property(e => e.FranchiseName)
                .HasMaxLength(200)
                .HasColumnName("franchise_name");
            entity.Property(e => e.RevenueSharePercent)
                .HasColumnType("decimal(5,2)")
                .HasColumnName("revenue_share_percent");
            entity.Property(e => e.ContactEmail)
                .HasMaxLength(100)
                .HasColumnName("contact_email");
            entity.Property(e => e.Address)
                .HasMaxLength(500)
                .HasColumnName("address");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.OwnerUser)
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ── Stations ────────────────────────────────────────────────
        modelBuilder.Entity<Station>(entity =>
        {
            entity.ToTable("Stations");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.Name)
                .HasMaxLength(150)
                .HasColumnName("name");
            entity.Property(e => e.FranchiseId)
                .HasColumnName("franchise_id");
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
                .HasColumnType("decimal(9,6)")
                .HasColumnName("latitude");
            entity.Property(e => e.Longitude)
                .HasColumnType("decimal(9,6)")
                .HasColumnName("longitude");
            entity.Property(e => e.OpeningTime)
                .HasColumnName("opening_time");
            entity.Property(e => e.ClosingTime)
                .HasColumnName("closing_time");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Franchise)
                .WithMany()
                .HasForeignKey(e => e.FranchiseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.City).HasDatabaseName("IX_Stations_City");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_Stations_Status");
        });

        // ── StationManagers ─────────────────────────────────────────
        modelBuilder.Entity<StationManager>(entity =>
        {
            entity.ToTable("StationManagers");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.StationId)
                .HasColumnName("station_id");
            entity.Property(e => e.UserId)
                .HasColumnName("user_id");
            entity.Property(e => e.AssignedAt)
                .HasColumnName("assigned_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Station)
                .WithMany()
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.StationId, e.UserId })
                .IsUnique()
                .HasDatabaseName("IX_StationManagers_StationId_UserId");
        });

        // ── Chargers ────────────────────────────────────────────────
        modelBuilder.Entity<Charger>(entity =>
        {
            entity.ToTable("Chargers");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.StationId)
                .HasColumnName("station_id");
            entity.Property(e => e.ChargerCode)
                .HasMaxLength(30)
                .HasColumnName("charger_code");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("type");
            entity.Property(e => e.ConnectorType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("connector_type");
            entity.Property(e => e.PowerRatingKw)
                .HasColumnType("decimal(6,2)")
                .HasColumnName("power_rating_kw");
            entity.Property(e => e.RatePerKwh)
                .HasColumnType("decimal(8,2)")
                .HasColumnName("rate_per_kwh");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.LastServicedAt)
                .HasColumnName("last_serviced_at");

            entity.HasOne(e => e.Station)
                .WithMany()
                .HasForeignKey(e => e.StationId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ChargerCode)
                .IsUnique()
                .HasDatabaseName("IX_Chargers_ChargerCode");
            entity.HasIndex(e => e.StationId).HasDatabaseName("IX_Chargers_StationId");
            entity.HasIndex(e => e.Status).HasDatabaseName("IX_Chargers_Status");
        });

        // ── Bookings ────────────────────────────────────────────────
        modelBuilder.Entity<Booking>(entity =>
        {
            entity.ToTable("Bookings");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.CustomerUserId)
                .HasColumnName("customer_user_id");
            entity.Property(e => e.ChargerId)
                .HasColumnName("charger_id");
            entity.Property(e => e.BookingTime)
                .HasColumnName("booking_time")
                .HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.SlotStartTime)
                .HasColumnName("slot_start_time");
            entity.Property(e => e.SlotEndTime)
                .HasColumnName("slot_end_time");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("status");
            entity.Property(e => e.CreatedAt)
                .HasColumnName("created_at")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.CustomerUser)
                .WithMany()
                .HasForeignKey(e => e.CustomerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.Charger)
                .WithMany()
                .HasForeignKey(e => e.ChargerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.ChargerId, e.SlotStartTime, e.SlotEndTime })
                .HasDatabaseName("IX_Bookings_ChargerId_SlotStartTime_SlotEndTime");
            entity.HasIndex(e => e.CustomerUserId)
                .HasDatabaseName("IX_Bookings_CustomerUserId");
        });

        // ── ChargingSessions ────────────────────────────────────────
        modelBuilder.Entity<ChargingSession>(entity =>
        {
            entity.ToTable("ChargingSessions");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.BookingId)
                .HasColumnName("booking_id");
            entity.Property(e => e.ActualStartTime)
                .HasColumnName("actual_start_time");
            entity.Property(e => e.ActualEndTime)
                .HasColumnName("actual_end_time");
            entity.Property(e => e.EnergyConsumedKwh)
                .HasColumnType("decimal(8,2)")
                .HasColumnName("energy_consumed_kwh");
            entity.Property(e => e.Cost)
                .HasColumnType("decimal(10,2)")
                .HasColumnName("cost");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("payment_status")
                .HasDefaultValue("Pending");

            entity.HasOne(e => e.Booking)
                .WithOne()
                .HasForeignKey<ChargingSession>(e => e.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.BookingId)
                .IsUnique()
                .HasDatabaseName("IX_ChargingSessions_BookingId");
        });

        // ── Wallets ─────────────────────────────────────────────────
        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.ToTable("Wallets");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.CustomerUserId)
                .HasColumnName("customer_user_id");
            entity.Property(e => e.Balance)
                .HasColumnType("decimal(10,2)")
                .HasColumnName("balance")
                .HasDefaultValue(0m);
            entity.Property(e => e.LastUpdated)
                .HasColumnName("last_updated")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.CustomerUser)
                .WithOne()
                .HasForeignKey<Wallet>(e => e.CustomerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.CustomerUserId)
                .IsUnique()
                .HasDatabaseName("IX_Wallets_CustomerUserId");
        });

        // ── WalletTransactions ──────────────────────────────────────
        modelBuilder.Entity<WalletTransaction>(entity =>
        {
            entity.ToTable("WalletTransactions");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.WalletId)
                .HasColumnName("wallet_id");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(10,2)")
                .HasColumnName("amount");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("type");
            entity.Property(e => e.Timestamp)
                .HasColumnName("timestamp")
                .HasDefaultValueSql("GETUTCDATE()");

            entity.HasOne(e => e.Wallet)
                .WithMany()
                .HasForeignKey(e => e.WalletId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.WalletId)
                .HasDatabaseName("IX_WalletTransactions_WalletId");
        });

        // ── Invoices ────────────────────────────────────────────────
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("Invoices");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.CustomerUserId)
                .HasColumnName("customer_user_id");
            entity.Property(e => e.ChargingSessionId)
                .HasColumnName("charging_session_id");
            entity.Property(e => e.Amount)
                .HasColumnType("decimal(10,2)")
                .HasColumnName("amount");
            entity.Property(e => e.GSTAmount)
                .HasColumnType("decimal(10,2)")
                .HasColumnName("gst_amount");
            entity.Property(e => e.TotalAmount)
                .HasColumnType("decimal(10,2)")
                .HasColumnName("total_amount");
            entity.Property(e => e.IssuedDate)
                .HasColumnName("issued_date")
                .HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.PaymentMethod)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("payment_method");

            entity.HasOne(e => e.CustomerUser)
                .WithMany()
                .HasForeignKey(e => e.CustomerUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ChargingSession)
                .WithOne()
                .HasForeignKey<Invoice>(e => e.ChargingSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ChargingSessionId)
                .IsUnique()
                .HasDatabaseName("IX_Invoices_ChargingSessionId");
        });

        // ── MaintenanceTickets ──────────────────────────────────────
        modelBuilder.Entity<MaintenanceTicket>(entity =>
        {
            entity.ToTable("MaintenanceTickets");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.ChargerId)
                .HasColumnName("charger_id");
            entity.Property(e => e.ReportedByUserId)
                .HasColumnName("reported_by_user_id");
            entity.Property(e => e.IssueDescription)
                .HasMaxLength(500)
                .HasColumnName("issue_description");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("status")
                .HasDefaultValue("Open");
            entity.Property(e => e.AssignedTechnicianId)
                .HasColumnName("assigned_technician_id");
            entity.Property(e => e.ReportedAt)
                .HasColumnName("reported_at")
                .HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.ResolvedAt)
                .HasColumnName("resolved_at");

            entity.HasOne(e => e.Charger)
                .WithMany()
                .HasForeignKey(e => e.ChargerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.ReportedByUser)
                .WithMany()
                .HasForeignKey(e => e.ReportedByUserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.AssignedTechnician)
                .WithMany()
                .HasForeignKey(e => e.AssignedTechnicianId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => e.ChargerId)
                .HasDatabaseName("IX_MaintenanceTickets_ChargerId");
            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_MaintenanceTickets_Status");
            entity.HasIndex(e => e.AssignedTechnicianId)
                .HasDatabaseName("IX_MaintenanceTickets_AssignedTechnicianId");
        });

        // ── FranchiseSettlements ────────────────────────────────────
        modelBuilder.Entity<FranchiseSettlement>(entity =>
        {
            entity.ToTable("FranchiseSettlements");

            entity.Property(e => e.Id)
                .HasColumnName("id");
            entity.Property(e => e.FranchiseId)
                .HasColumnName("franchise_id");
            entity.Property(e => e.PeriodStart)
                .HasColumnName("period_start");
            entity.Property(e => e.PeriodEnd)
                .HasColumnName("period_end");
            entity.Property(e => e.TotalRevenue)
                .HasColumnType("decimal(12,2)")
                .HasColumnName("total_revenue");
            entity.Property(e => e.FranchiseShareAmount)
                .HasColumnType("decimal(12,2)")
                .HasColumnName("franchise_share_amount");
            entity.Property(e => e.CompanyShareAmount)
                .HasColumnType("decimal(12,2)")
                .HasColumnName("company_share_amount");
            entity.Property(e => e.SettlementStatus)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("settlement_status")
                .HasDefaultValue("Pending");

            entity.HasOne(e => e.Franchise)
                .WithMany()
                .HasForeignKey(e => e.FranchiseId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(e => new { e.FranchiseId, e.PeriodStart, e.PeriodEnd })
                .IsUnique()
                .HasDatabaseName("IX_FranchiseSettlements_FranchiseId_Period");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
