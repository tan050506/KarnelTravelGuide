using System;
using System.Collections.Generic;
using KarnelTravelGuide.Web.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace KarnelTravelGuide.Web.Data;

public partial class ApplicationDbContext : DbContext
{
    public ApplicationDbContext()
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<Order> Orders { get; set; }

    public virtual DbSet<OrderDetail> OrderDetails { get; set; }

    public virtual DbSet<Restaurant> Restaurants { get; set; }

    public virtual DbSet<RestaurantBooking> RestaurantBookings { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<RoomBooking> RoomBookings { get; set; }

    public virtual DbSet<Stay> Stays { get; set; }

    public virtual DbSet<TicketBooking> TicketBookings { get; set; }

    public virtual DbSet<TouristSpot> TouristSpots { get; set; }

    public virtual DbSet<Transportation> Transportations { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Account__349DA586AFFF07EA");

            entity.ToTable("Account");

            entity.HasIndex(e => e.Email, "UQ__Account__A9D10534E8DD0892").IsUnique();

            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.AvatarUrl).IsUnicode(false);
            entity.Property(e => e.BranchId).HasColumnName("BranchID");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.Password)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.PhoneNumber)
                .HasMaxLength(15)
                .IsUnicode(false);
            entity.Property(e => e.RoleId).HasColumnName("RoleID");

            entity.HasOne(d => d.Branch).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK__Account__BranchI__3E52440B");

            entity.HasOne(d => d.Role).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.RoleId)
                .HasConstraintName("FK__Account__RoleID__3D5E1FD2");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId).HasName("PK__Branch__A1682FA5EC760ED2");

            entity.ToTable("Branch");

            entity.Property(e => e.BranchId).HasColumnName("BranchID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.BranchName).HasMaxLength(100);
            entity.Property(e => e.EmailBranch)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.ImageUrl).IsUnicode(false);
            entity.Property(e => e.PhoneBranch)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__Feedback__6A4BEDF62E6FED86");

            entity.ToTable("Feedback");

            entity.Property(e => e.FeedbackId).HasColumnName("FeedbackID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.RestaurantId).HasColumnName("RestaurantID");
            entity.Property(e => e.StayId).HasColumnName("StayID");

            entity.HasOne(d => d.Account).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Feedback__Accoun__4E88ABD4");

            entity.HasOne(d => d.Restaurant).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("FK__Feedback__Restau__5070F446");

            entity.HasOne(d => d.Stay).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.StayId)
                .HasConstraintName("FK__Feedback__StayID__4F7CD00D");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoice__D796AAD55C823DBA");

            entity.ToTable("Invoice");

            entity.HasIndex(e => e.OrderId, "UQ_Invoice_Order").IsUnique();

            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DiscountAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Account).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Invoice__Account__6D0D32F4");

            entity.HasOne(d => d.Order).WithOne(p => p.Invoice)
                .HasForeignKey<Invoice>(d => d.OrderId)
                .HasConstraintName("FK__Invoice__OrderID__6E01572D");
        });

        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.OrderId).HasName("PK__Order__C3905BAF72AF7D28");

            entity.ToTable("Order");

            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreateDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.Status)
                .HasMaxLength(50)
                .IsUnicode(false);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Account).WithMany(p => p.Orders)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Order__AccountID__60A75C0F");
        });

        modelBuilder.Entity<OrderDetail>(entity =>
        {
            entity.HasKey(e => e.OrderDetailId).HasName("PK__OrderDet__D3B9D30CF2CB326C");

            entity.ToTable("OrderDetail");

            entity.Property(e => e.OrderDetailId).HasColumnName("OrderDetailID");
            entity.Property(e => e.OrderId).HasColumnName("OrderID");
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.Quantity).HasDefaultValue(1);
            entity.Property(e => e.ResBookingId).HasColumnName("ResBookingID");
            entity.Property(e => e.RoomBookingId).HasColumnName("RoomBookingID");
            entity.Property(e => e.TicketBookingId).HasColumnName("TicketBookingID");

            entity.HasOne(d => d.Order).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.OrderId)
                .HasConstraintName("FK__OrderDeta__Order__6477ECF3");

            entity.HasOne(d => d.ResBooking).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.ResBookingId)
                .HasConstraintName("FK__OrderDeta__ResBo__6754599E");

            entity.HasOne(d => d.RoomBooking).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.RoomBookingId)
                .HasConstraintName("FK__OrderDeta__RoomB__656C112C");

            entity.HasOne(d => d.TicketBooking).WithMany(p => p.OrderDetails)
                .HasForeignKey(d => d.TicketBookingId)
                .HasConstraintName("FK__OrderDeta__Ticke__66603565");
        });

        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.HasKey(e => e.RestaurantId).HasName("PK__Restaura__87454CB536407C61");

            entity.ToTable("Restaurant");

            entity.Property(e => e.RestaurantId).HasColumnName("RestaurantID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.ImageUrl).IsUnicode(false);
            entity.Property(e => e.PriceRes).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RestaurantName).HasMaxLength(100);
            entity.Property(e => e.SpotId).HasColumnName("SpotID");

            entity.HasOne(d => d.Spot).WithMany(p => p.Restaurants)
                .HasForeignKey(d => d.SpotId)
                .HasConstraintName("FK__Restauran__SpotI__47DBAE45");
        });

        modelBuilder.Entity<RestaurantBooking>(entity =>
        {
            entity.HasKey(e => e.ResBookingId).HasName("PK__Restaura__1829B6253ED8CD00");

            entity.ToTable("RestaurantBooking");

            entity.Property(e => e.ResBookingId).HasColumnName("ResBookingID");
            entity.Property(e => e.ReservationDateTime).HasColumnType("datetime");
            entity.Property(e => e.RestaurantId).HasColumnName("RestaurantID");
            entity.Property(e => e.TableType).HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Restaurant).WithMany(p => p.RestaurantBookings)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("FK__Restauran__Resta__5CD6CB2B");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId).HasName("PK__Role__8AFACE3AF3205071");

            entity.ToTable("Role");

            entity.HasIndex(e => e.RoleName, "UQ__Role__8A2B616092EC9BFD").IsUnique();

            entity.Property(e => e.RoleId).HasColumnName("RoleID");
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.Property(e => e.RoleName)
                .HasMaxLength(50)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK__Room__32863919F895DFFF");

            entity.ToTable("Room");

            entity.Property(e => e.RoomId).HasColumnName("RoomID");
            entity.Property(e => e.PriceRoom).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RoomType).HasMaxLength(50);
            entity.Property(e => e.StayId).HasColumnName("StayID");

            entity.HasOne(d => d.Stay).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.StayId)
                .HasConstraintName("FK__Room__StayID__5441852A");
        });

        modelBuilder.Entity<RoomBooking>(entity =>
        {
            entity.HasKey(e => e.RoomBookingId).HasName("PK__RoomBook__1FAA577784968189");

            entity.ToTable("RoomBooking");

            entity.Property(e => e.RoomBookingId).HasColumnName("RoomBookingID");
            entity.Property(e => e.RoomId).HasColumnName("RoomID");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomBookings)
                .HasForeignKey(d => d.RoomId)
                .HasConstraintName("FK__RoomBooki__RoomI__571DF1D5");
        });

        modelBuilder.Entity<Stay>(entity =>
        {
            entity.HasKey(e => e.StayId).HasName("PK__Stay__04BA16466C161D1F");

            entity.ToTable("Stay");

            entity.Property(e => e.StayId).HasColumnName("StayID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.ImageUrl).IsUnicode(false);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.SpotId).HasColumnName("SpotID");
            entity.Property(e => e.StayType).HasMaxLength(50);

            entity.HasOne(d => d.Spot).WithMany(p => p.Stays)
                .HasForeignKey(d => d.SpotId)
                .HasConstraintName("FK__Stay__SpotID__44FF419A");
        });

        modelBuilder.Entity<TicketBooking>(entity =>
        {
            entity.HasKey(e => e.TicketBookingId).HasName("PK__TicketBo__2D9E022FAE76E349");

            entity.ToTable("TicketBooking");

            entity.Property(e => e.TicketBookingId).HasColumnName("TicketBookingID");
            entity.Property(e => e.Seat)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TransportationId).HasColumnName("TransportationID");

            entity.HasOne(d => d.Transportation).WithMany(p => p.TicketBookings)
                .HasForeignKey(d => d.TransportationId)
                .HasConstraintName("FK__TicketBoo__Trans__59FA5E80");
        });

        modelBuilder.Entity<TouristSpot>(entity =>
        {
            entity.HasKey(e => e.SpotId).HasName("PK__TouristS__61645FE79D5A7B2A");

            entity.ToTable("TouristSpot");

            entity.Property(e => e.SpotId).HasColumnName("SpotID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.BranchId).HasColumnName("BranchID");
            entity.Property(e => e.ImageUrl).IsUnicode(false);
            entity.Property(e => e.SpotName).HasMaxLength(100);

            entity.HasOne(d => d.Branch).WithMany(p => p.TouristSpots)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK__TouristSp__Branc__412EB0B6");
        });

        modelBuilder.Entity<Transportation>(entity =>
        {
            entity.HasKey(e => e.TransportationId).HasName("PK__Transpor__87E47956923E4AB6");

            entity.ToTable("Transportation");

            entity.Property(e => e.TransportationId).HasColumnName("TransportationID");
            entity.Property(e => e.DepartureTime).HasColumnType("datetime");
            entity.Property(e => e.FromBranchId).HasColumnName("FromBranchID");
            entity.Property(e => e.ImageUrl).IsUnicode(false);
            entity.Property(e => e.PriceTransport).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ToSpotId).HasColumnName("ToSpotID");
            entity.Property(e => e.TransportName).HasMaxLength(100);
            entity.Property(e => e.TransportType).HasMaxLength(50);

            entity.HasOne(d => d.FromBranch).WithMany(p => p.Transportations)
                .HasForeignKey(d => d.FromBranchId)
                .HasConstraintName("FK__Transport__FromB__4AB81AF0");

            entity.HasOne(d => d.ToSpot).WithMany(p => p.Transportations)
                .HasForeignKey(d => d.ToSpotId)
                .HasConstraintName("FK__Transport__ToSpo__4BAC3F29");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
