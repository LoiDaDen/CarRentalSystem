using CarRentalSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Car> Cars { get; set; }
        public DbSet<CarImage> CarImages { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<Payment> Payments { get; set; }
        public DbSet<RentalContract> RentalContracts { get; set; }
        public DbSet<DigitalContract> DigitalContracts { get; set; }
        public DbSet<VehicleTracking> VehicleTrackings { get; set; }
        public DbSet<CarRentalSystem.Models.Route> Routes { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<MaintenanceLog> MaintenanceLogs { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<HandoverRecord> HandoverRecords { get; set; }
        
        public DbSet<Location> Locations { get; set; }
        public DbSet<CarBrand> CarBrands { get; set; }
        public DbSet<CarType> CarTypes { get; set; }
        public DbSet<CarStatus> CarStatuses { get; set; }
        public DbSet<BookingStatus> BookingStatuses { get; set; }
        
        // CRM & Marketing
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        
        // Phase 3: Add-ons
        public DbSet<AddOn> AddOns { get; set; }
        public DbSet<BookingAddOn> BookingAddOns { get; set; }

        // Phase 5: Mega Update
        public DbSet<MembershipTier> MembershipTiers { get; set; }
        public DbSet<Driver> Drivers { get; set; }
        public DbSet<DriverBooking> DriverBookings { get; set; }
        public DbSet<ContractType> ContractTypes { get; set; }
        public DbSet<HolidayPricing> HolidayPricings { get; set; }

        // Cart & Transaction
        public DbSet<Cart> Carts { get; set; }
        public DbSet<CartItem> CartItems { get; set; }
        public DbSet<BookingTransaction> BookingTransactions { get; set; }
        
        // Scheduling
        public DbSet<AvailabilitySchedule> AvailabilitySchedules { get; set; }

        // Long Term
        public DbSet<LongTermRental> LongTermRentals { get; set; }
        public DbSet<PaymentSchedule> PaymentSchedules { get; set; }
        public DbSet<InspectionRecord> InspectionRecords { get; set; }

        // Phase 6: Advanced Supplement
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<WalletTransaction> WalletTransactions { get; set; }
        public DbSet<DynamicPricingRule> DynamicPricingRules { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            
            // Configure decimal precision
            modelBuilder.Entity<Car>()
                .Property(c => c.PricePerDay)
                .HasColumnType("decimal(18,2)");
                
            modelBuilder.Entity<Car>()
                .Property(c => c.PricePerHour)
                .HasColumnType("decimal(18,2)");
                
            modelBuilder.Entity<Booking>()
                .Property(b => b.TotalPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<RentalContract>()
                .Property(r => r.StartKm)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<RentalContract>()
                .Property(r => r.EndKm)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<RentalContract>()
                .Property(r => r.TotalKm)
                .HasColumnType("decimal(18,2)");
            
            modelBuilder.Entity<RentalContract>()
                .Property(c => c.BasePrice)
                .HasColumnType("decimal(18,2)");
            
            modelBuilder.Entity<RentalContract>()
                .Property(r => r.FinalPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<MaintenanceLog>()
                .Property(m => m.Cost)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Promotion>()
                .Property(p => p.DiscountPercent)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Promotion>()
                .Property(p => p.MaxDiscountAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Review>()
                .HasOne(r => r.User)
                .WithMany(u => u.Reviews)
                .HasForeignKey(r => r.UserID)
                .OnDelete(DeleteBehavior.NoAction);

            // Configure DeleteBehavior for Location in Booking to prevent multiple cascade paths
            modelBuilder.Entity<Booking>()
                .HasOne(b => b.PickupLocation)
                .WithMany()
                .HasForeignKey(b => b.PickupLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.ReturnLocation)
                .WithMany()
                .HasForeignKey(b => b.ReturnLocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.User)
                .WithMany(u => u.Bookings)
                .HasForeignKey(b => b.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Car)
                .WithMany(c => c.Bookings)
                .HasForeignKey(b => b.CarID)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DeleteBehavior for Location in Car
            modelBuilder.Entity<Car>()
                .HasOne(c => c.Location)
                .WithMany(l => l.Cars)
                .HasForeignKey(c => c.LocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Car>()
                .Property(c => c.DiscountPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<BookingAddOn>()
                .HasKey(ba => new { ba.BookingID, ba.AddOnID });

            modelBuilder.Entity<BookingAddOn>()
                .Property(ba => ba.TotalPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Booking>()
                .Property(b => b.DiscountAmount)
                .HasColumnType("decimal(18,2)");
                
            modelBuilder.Entity<Booking>()
                .Property(b => b.DeliveryFee)
                .HasColumnType("decimal(18,2)");
                
            modelBuilder.Entity<Booking>()
                .Property(b => b.AddOnFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Booking>()
                .Property(b => b.DepositAmount)
                .HasColumnType("decimal(18,2)");



            modelBuilder.Entity<Car>()
                .Property(c => c.OverTimeFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Car>()
                .Property(c => c.CleaningFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Car>()
                .Property(c => c.DefaultDeposit)
                .HasColumnType("decimal(18,2)");

            // Phase 5: New model configurations
            modelBuilder.Entity<MembershipTier>()
                .Property(m => m.MinSpending)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Driver>()
                .Property(d => d.DailyRate)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Driver>()
                .Property(d => d.HourlyRate)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<DriverBooking>()
                .Property(db => db.DriverFee)
                .HasColumnType("decimal(18,2)");
            
            modelBuilder.Entity<DriverBooking>()
                .HasOne(db => db.Booking)
                .WithMany()
                .HasForeignKey(db => db.BookingID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DriverBooking>()
                .HasOne(db => db.Driver)
                .WithMany(d => d.DriverBookings)
                .HasForeignKey(db => db.DriverID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .Property(b => b.LateFeeAmount)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Booking>()
                .Property(b => b.ShippingFee)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<Booking>()
                .Property(b => b.FuelFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Car>()
                .Property(c => c.PartnerDiscountPercent)
                .HasColumnType("decimal(5,2)");

            modelBuilder.Entity<User>()
                .Property(u => u.TotalSpending)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<HolidayPricing>()
                .Property(h => h.PriceMultiplier)
                .HasColumnType("decimal(4,2)");

            modelBuilder.Entity<User>()
                .HasOne(u => u.MembershipTier)
                .WithMany(m => m.Users)
                .HasForeignKey(u => u.MembershipTierID)
                .OnDelete(DeleteBehavior.SetNull);

            // New Models Configuration
            modelBuilder.Entity<CartItem>()
                .Property(ci => ci.EstimatedPrice)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<BookingTransaction>()
                .Property(bt => bt.TotalAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<LongTermRental>()
                .Property(r => r.DepositAmount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<LongTermRental>()
                .Property(r => r.MonthlyRate)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PaymentSchedule>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.BookingTransaction)
                .WithMany(t => t.Bookings)
                .HasForeignKey(b => b.TransactionID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BookingTransaction>()
                .HasOne(bt => bt.DigitalContract)
                .WithOne(dc => dc.BookingTransaction)
                .HasForeignKey<DigitalContract>(dc => dc.TransactionID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
