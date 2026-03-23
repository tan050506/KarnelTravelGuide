using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace KarnelTravelGuide.Web.Models.Entities;

public partial class KarnelTravelGuideContext : DbContext
{
    public KarnelTravelGuideContext()
    {
    }

    public KarnelTravelGuideContext(DbContextOptions<KarnelTravelGuideContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Account> Accounts { get; set; }

    public virtual DbSet<Branch> Branches { get; set; }

    public virtual DbSet<Feedback> Feedbacks { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<Restaurant> Restaurants { get; set; }

    public virtual DbSet<RestaurantBooking> RestaurantBookings { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<RoomBooking> RoomBookings { get; set; }

    public virtual DbSet<Stay> Stays { get; set; }

    public virtual DbSet<TicketBooking> TicketBookings { get; set; }

    public virtual DbSet<TouristSpot> TouristSpots { get; set; }

    public virtual DbSet<Transportation> Transportations { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server=MSI;Database=KarnelTravelGuide;Trusted_Connection=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.HasKey(e => e.AccountId).HasName("PK__Account__349DA5868CF3E15F");

            entity.ToTable("Account");

            entity.HasIndex(e => e.PhoneNumber, "UQ__Account__85FB4E38383AD4FD").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Account__A9D105340ADFB34A").IsUnique();

            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.Address).HasMaxLength(200);
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
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("Customer");

            entity.HasOne(d => d.Branch).WithMany(p => p.Accounts)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK__Account__BranchI__3D5E1FD2");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId).HasName("PK__Branch__A1682FA55D83A2A4");

            entity.ToTable("Branch");

            entity.Property(e => e.BranchId).HasColumnName("BranchID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.BranchName).HasMaxLength(100);
            entity.Property(e => e.EmailBranch)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PhoneBranch)
                .HasMaxLength(15)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__Feedback__6A4BEDF676EFF2C2");

            entity.ToTable("Feedback");

            entity.Property(e => e.FeedbackId).HasColumnName("FeedbackID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.RestaurantId).HasColumnName("RestaurantID");
            entity.Property(e => e.StayId).HasColumnName("StayID");

            entity.HasOne(d => d.Account).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Feedback__Accoun__60A75C0F");

            entity.HasOne(d => d.Restaurant).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("FK__Feedback__Restau__628FA481");

            entity.HasOne(d => d.Stay).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.StayId)
                .HasConstraintName("FK__Feedback__StayID__619B8048");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoice__D796AAD5799964EF");

            entity.ToTable("Invoice");

            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.CreatedDate)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.DiscountAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");
            entity.Property(e => e.FinalTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.PaymentStatus).HasMaxLength(50);
            entity.Property(e => e.ResBookingId).HasColumnName("ResBookingID");
            entity.Property(e => e.RoomBookingId).HasColumnName("RoomBookingID");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TicketBookingId).HasColumnName("TicketBookingID");

            entity.HasOne(d => d.Account).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Invoice__Account__59063A47");

            entity.HasOne(d => d.ResBooking).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.ResBookingId)
                .HasConstraintName("FK__Invoice__ResBook__5CD6CB2B");

            entity.HasOne(d => d.RoomBooking).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.RoomBookingId)
                .HasConstraintName("FK__Invoice__RoomBoo__5BE2A6F2");

            entity.HasOne(d => d.TicketBooking).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.TicketBookingId)
                .HasConstraintName("FK__Invoice__TicketB__5DCAEF64");
        });

        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.HasKey(e => e.RestaurantId).HasName("PK__Restaura__87454CB5D5590DCA");

            entity.ToTable("Restaurant");

            entity.Property(e => e.RestaurantId).HasColumnName("RestaurantID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.PriceRes).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RestaurantName).HasMaxLength(100);
            entity.Property(e => e.SpotId).HasColumnName("SpotID");

            entity.HasOne(d => d.Spot).WithMany(p => p.Restaurants)
                .HasForeignKey(d => d.SpotId)
                .HasConstraintName("FK__Restauran__SpotI__4D94879B");
        });

        modelBuilder.Entity<RestaurantBooking>(entity =>
        {
            entity.HasKey(e => e.ResBookingId).HasName("PK__Restaura__1829B625EB3F678E");

            entity.ToTable("RestaurantBooking");

            entity.Property(e => e.ResBookingId).HasColumnName("ResBookingID");
            entity.Property(e => e.ReservationDateTime).HasColumnType("datetime");
            entity.Property(e => e.RestaurantId).HasColumnName("RestaurantID");
            entity.Property(e => e.TableType).HasMaxLength(50);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Restaurant).WithMany(p => p.RestaurantBookings)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("FK__Restauran__Resta__534D60F1");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK__Room__32863919B98216FA");

            entity.ToTable("Room");

            entity.Property(e => e.RoomId).HasColumnName("RoomID");
            entity.Property(e => e.PriceRoom).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RoomType).HasMaxLength(50);
            entity.Property(e => e.StayId).HasColumnName("StayID");

            entity.HasOne(d => d.Stay).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.StayId)
                .HasConstraintName("FK__Room__StayID__4AB81AF0");
        });

        modelBuilder.Entity<RoomBooking>(entity =>
        {
            entity.HasKey(e => e.RoomBookingId).HasName("PK__RoomBook__1FAA5777829F52E9");

            entity.ToTable("RoomBooking");

            entity.Property(e => e.RoomBookingId).HasColumnName("RoomBookingID");
            entity.Property(e => e.RoomId).HasColumnName("RoomID");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomBookings)
                .HasForeignKey(d => d.RoomId)
                .HasConstraintName("FK__RoomBooki__RoomI__5070F446");
        });

        modelBuilder.Entity<Stay>(entity =>
        {
            entity.HasKey(e => e.StayId).HasName("PK__Stay__04BA16467C830796");

            entity.ToTable("Stay");

            entity.Property(e => e.StayId).HasColumnName("StayID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.SpotId).HasColumnName("SpotID");
            entity.Property(e => e.StayType).HasMaxLength(50);

            entity.HasOne(d => d.Spot).WithMany(p => p.Stays)
                .HasForeignKey(d => d.SpotId)
                .HasConstraintName("FK__Stay__SpotID__46E78A0C");
        });

        modelBuilder.Entity<TicketBooking>(entity =>
        {
            entity.HasKey(e => e.TicketBookingId).HasName("PK__TicketBo__2D9E022F287396EC");

            entity.ToTable("TicketBooking");

            entity.Property(e => e.TicketBookingId).HasColumnName("TicketBookingID");
            entity.Property(e => e.Seat)
                .HasMaxLength(10)
                .IsUnicode(false);
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TransportationId).HasColumnName("TransportationID");

            entity.HasOne(d => d.Transportation).WithMany(p => p.TicketBookings)
                .HasForeignKey(d => d.TransportationId)
                .HasConstraintName("FK__TicketBoo__Trans__5629CD9C");
        });

        modelBuilder.Entity<TouristSpot>(entity =>
        {
            entity.HasKey(e => e.SpotId).HasName("PK__TouristS__61645FE70C8F166E");

            entity.ToTable("TouristSpot");

            entity.Property(e => e.SpotId).HasColumnName("SpotID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.BranchId).HasColumnName("BranchID");
            entity.Property(e => e.SpotName).HasMaxLength(100);

            entity.HasOne(d => d.Branch).WithMany(p => p.TouristSpots)
                .HasForeignKey(d => d.BranchId)
                .HasConstraintName("FK__TouristSp__Branc__403A8C7D");
        });

        modelBuilder.Entity<Transportation>(entity =>
        {
            entity.HasKey(e => e.TransportationId).HasName("PK__Transpor__87E47956C39F91D9");

            entity.ToTable("Transportation");

            entity.Property(e => e.TransportationId).HasColumnName("TransportationID");
            entity.Property(e => e.DepartureTime).HasColumnType("datetime");
            entity.Property(e => e.FromBranchId).HasColumnName("FromBranchID");
            entity.Property(e => e.PriceTransport).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ToSpotId).HasColumnName("ToSpotID");
            entity.Property(e => e.TransportName).HasMaxLength(100);
            entity.Property(e => e.TransportType).HasMaxLength(50);

            entity.HasOne(d => d.FromBranch).WithMany(p => p.Transportations)
                .HasForeignKey(d => d.FromBranchId)
                .HasConstraintName("FK__Transport__FromB__4316F928");

            entity.HasOne(d => d.ToSpot).WithMany(p => p.Transportations)
                .HasForeignKey(d => d.ToSpotId)
                .HasConstraintName("FK__Transport__ToSpo__440B1D61");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
