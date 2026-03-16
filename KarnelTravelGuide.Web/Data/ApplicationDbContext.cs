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

    public virtual DbSet<Hotel> Hotels { get; set; }

    public virtual DbSet<Invoice> Invoices { get; set; }

    public virtual DbSet<Resort> Resorts { get; set; }

    public virtual DbSet<Restaurant> Restaurants { get; set; }

    public virtual DbSet<RestaurantBooking> RestaurantBookings { get; set; }

    public virtual DbSet<Room> Rooms { get; set; }

    public virtual DbSet<RoomBooking> RoomBookings { get; set; }

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
            entity.HasKey(e => e.AccountId).HasName("PK__Account__349DA5867A3E6A57");

            entity.ToTable("Account");

            entity.HasIndex(e => e.PhoneNumber, "UQ__Account__85FB4E383DEA77A9").IsUnique();

            entity.HasIndex(e => e.Password, "UQ__Account__87909B150DEEC2B9").IsUnique();

            entity.HasIndex(e => e.FullName, "UQ__Account__89C60F11029126B2").IsUnique();

            entity.HasIndex(e => e.Email, "UQ__Account__A9D105347CE427A9").IsUnique();

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
                .HasConstraintName("FK__Account__BranchI__403A8C7D");
        });

        modelBuilder.Entity<Branch>(entity =>
        {
            entity.HasKey(e => e.BranchId).HasName("PK__Branch__A1682FA53997453C");

            entity.ToTable("Branch");

            entity.Property(e => e.BranchId).HasColumnName("BranchID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.BranchName).HasMaxLength(150);
            entity.Property(e => e.EmailBranch)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.PhoneBranch)
                .HasMaxLength(20)
                .IsUnicode(false);
        });

        modelBuilder.Entity<Feedback>(entity =>
        {
            entity.HasKey(e => e.FeedbackId).HasName("PK__Feedback__6A4BEDF6245631AB");

            entity.ToTable("Feedback");

            entity.Property(e => e.FeedbackId).HasColumnName("FeedbackID");
            entity.Property(e => e.AccountId).HasColumnName("AccountID");
            entity.Property(e => e.Email)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.FullName).HasMaxLength(100);
            entity.Property(e => e.HotelId).HasColumnName("HotelID");
            entity.Property(e => e.ResortId).HasColumnName("ResortID");
            entity.Property(e => e.RestaurantId).HasColumnName("RestaurantID");

            entity.HasOne(d => d.Account).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.AccountId)
                .HasConstraintName("FK__Feedback__Accoun__68487DD7");

            entity.HasOne(d => d.Hotel).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.HotelId)
                .HasConstraintName("FK__Feedback__HotelI__693CA210");

            entity.HasOne(d => d.Resort).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.ResortId)
                .HasConstraintName("FK__Feedback__Resort__6A30C649");

            entity.HasOne(d => d.Restaurant).WithMany(p => p.Feedbacks)
                .HasForeignKey(d => d.RestaurantId)
                .HasConstraintName("FK__Feedback__Restau__6B24EA82");
        });

        modelBuilder.Entity<Hotel>(entity =>
        {
            entity.HasKey(e => e.HotelId).HasName("PK__Hotel__46023BBFC10548DE");

            entity.ToTable("Hotel");

            entity.Property(e => e.HotelId).HasColumnName("HotelID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.HotelName).HasMaxLength(150);
            entity.Property(e => e.SpotId).HasColumnName("SpotID");
            entity.Property(e => e.StarRating).HasDefaultValue(0);

            entity.HasOne(d => d.Spot).WithMany(p => p.Hotels)
                .HasForeignKey(d => d.SpotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Hotel__SpotID__4316F928");
        });

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.InvoiceId).HasName("PK__Invoice__D796AAD515CFF513");

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
            entity.Property(e => e.PaymentStatus)
                .HasMaxLength(50)
                .HasDefaultValue("Pending");
            entity.Property(e => e.SubTotal).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Account).WithMany(p => p.Invoices)
                .HasForeignKey(d => d.AccountId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Invoice__Account__5629CD9C");
        });

        modelBuilder.Entity<Resort>(entity =>
        {
            entity.HasKey(e => e.ResortId).HasName("PK__Resort__7D2D742EF7CC9FD9");

            entity.ToTable("Resort");

            entity.Property(e => e.ResortId).HasColumnName("ResortID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.ResortName).HasMaxLength(150);
            entity.Property(e => e.SpotId).HasColumnName("SpotID");
            entity.Property(e => e.StarRating).HasDefaultValue(0);

            entity.HasOne(d => d.Spot).WithMany(p => p.Resorts)
                .HasForeignKey(d => d.SpotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Resort__SpotID__46E78A0C");
        });

        modelBuilder.Entity<Restaurant>(entity =>
        {
            entity.HasKey(e => e.RestaurantId).HasName("PK__Restaura__87454CB589FB67BD");

            entity.ToTable("Restaurant");

            entity.Property(e => e.RestaurantId).HasColumnName("RestaurantID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.PriceRes).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.RestaurantName).HasMaxLength(150);
            entity.Property(e => e.SpotId).HasColumnName("SpotID");
            entity.Property(e => e.StarRating).HasDefaultValue(0);

            entity.HasOne(d => d.Spot).WithMany(p => p.Restaurants)
                .HasForeignKey(d => d.SpotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Restauran__SpotI__4AB81AF0");
        });

        modelBuilder.Entity<RestaurantBooking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__Restaura__73951ACDC5BAFE1B");

            entity.ToTable("Restaurant Booking");

            entity.Property(e => e.BookingId).HasColumnName("BookingID");
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.ReservationDateTime).HasColumnType("datetime");
            entity.Property(e => e.RestaurantId).HasColumnName("RestaurantID");
            entity.Property(e => e.TotalAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.RestaurantBookings)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Restauran__Invoi__6383C8BA");

            entity.HasOne(d => d.Restaurant).WithMany(p => p.RestaurantBookings)
                .HasForeignKey(d => d.RestaurantId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Restauran__Resta__6477ECF3");
        });

        modelBuilder.Entity<Room>(entity =>
        {
            entity.HasKey(e => e.RoomId).HasName("PK__Room__32863919CA373054");

            entity.ToTable("Room");

            entity.Property(e => e.RoomId).HasColumnName("RoomID");
            entity.Property(e => e.HotelId).HasColumnName("HotelID");
            entity.Property(e => e.PriceRoom).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.ResortId).HasColumnName("ResortID");
            entity.Property(e => e.RoomType).HasMaxLength(100);

            entity.HasOne(d => d.Hotel).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.HotelId)
                .HasConstraintName("FK__Room__HotelID__52593CB8");

            entity.HasOne(d => d.Resort).WithMany(p => p.Rooms)
                .HasForeignKey(d => d.ResortId)
                .HasConstraintName("FK__Room__ResortID__534D60F1");
        });

        modelBuilder.Entity<RoomBooking>(entity =>
        {
            entity.HasKey(e => e.BookingId).HasName("PK__Room Boo__73951ACD491500CD");

            entity.ToTable("Room Booking");

            entity.Property(e => e.BookingId).HasColumnName("BookingID");
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.RoomId).HasColumnName("RoomID");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");

            entity.HasOne(d => d.Invoice).WithMany(p => p.RoomBookings)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Room Book__Invoi__5FB337D6");

            entity.HasOne(d => d.Room).WithMany(p => p.RoomBookings)
                .HasForeignKey(d => d.RoomId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Room Book__RoomI__60A75C0F");
        });

        modelBuilder.Entity<TicketBooking>(entity =>
        {
            entity.HasKey(e => e.TicketId).HasName("PK__Ticket B__712CC627BC935447");

            entity.ToTable("Ticket Booking");

            entity.Property(e => e.TicketId).HasColumnName("TicketID");
            entity.Property(e => e.InvoiceId).HasColumnName("InvoiceID");
            entity.Property(e => e.TotalAmount).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TransportationId).HasColumnName("TransportationID");

            entity.HasOne(d => d.Invoice).WithMany(p => p.TicketBookings)
                .HasForeignKey(d => d.InvoiceId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Ticket Bo__Invoi__5BE2A6F2");

            entity.HasOne(d => d.Transportation).WithMany(p => p.TicketBookings)
                .HasForeignKey(d => d.TransportationId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Ticket Bo__Trans__5CD6CB2B");
        });

        modelBuilder.Entity<TouristSpot>(entity =>
        {
            entity.HasKey(e => e.SpotId).HasName("PK__Tourist __61645FE76BCD9803");

            entity.ToTable("Tourist Spot");

            entity.Property(e => e.SpotId).HasColumnName("SpotID");
            entity.Property(e => e.Address).HasMaxLength(200);
            entity.Property(e => e.SpotName).HasMaxLength(150);
        });

        modelBuilder.Entity<Transportation>(entity =>
        {
            entity.HasKey(e => e.TransportationId).HasName("PK__Transpor__87E47956436DE908");

            entity.ToTable("Transportation");

            entity.Property(e => e.TransportationId).HasColumnName("TransportationID");
            entity.Property(e => e.DepartureSpotId).HasColumnName("DepartureSpotID");
            entity.Property(e => e.DepartureTime).HasColumnType("datetime");
            entity.Property(e => e.DestinationSpotId).HasColumnName("DestinationSpotID");
            entity.Property(e => e.PriceTransport).HasColumnType("decimal(18, 2)");
            entity.Property(e => e.TransportType).HasMaxLength(50);

            entity.HasOne(d => d.DepartureSpot).WithMany(p => p.TransportationDepartureSpots)
                .HasForeignKey(d => d.DepartureSpotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Transport__Depar__4E88ABD4");

            entity.HasOne(d => d.DestinationSpot).WithMany(p => p.TransportationDestinationSpots)
                .HasForeignKey(d => d.DestinationSpotId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK__Transport__Desti__4F7CD00D");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
