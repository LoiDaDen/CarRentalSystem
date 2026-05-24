using CarRentalSystem.Helpers;
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
        public DbSet<TrackingIngestNonce> TrackingIngestNonces { get; set; }
        public DbSet<CarRentalSystem.Models.Route> Routes { get; set; }
        public DbSet<Alert> Alerts { get; set; }
        public DbSet<MaintenanceLog> MaintenanceLogs { get; set; }
        public DbSet<Review> Reviews { get; set; }
        public DbSet<HandoverRecord> HandoverRecords { get; set; }
        
        public DbSet<Location> Locations { get; set; }
        public DbSet<CarBrand> CarBrands { get; set; }
        public DbSet<CarModel> CarModels { get; set; }
        public DbSet<CarType> CarTypes { get; set; }
        public DbSet<CarStatus> CarStatuses { get; set; }
        public DbSet<CarAmenity> CarAmenities { get; set; }
        public DbSet<CarAmenityMapping> CarAmenityMappings { get; set; }
        public DbSet<BookingStatus> BookingStatuses { get; set; }
        public DbSet<ServiceType> ServiceTypes { get; set; }
        public DbSet<TripType> TripTypes { get; set; }
        public DbSet<CustomerDrivingLicense> CustomerDrivingLicenses { get; set; }
        public DbSet<TripBooking> TripBookings { get; set; }
        public DbSet<TripStop> TripStops { get; set; }
        public DbSet<RoutePrice> RoutePrices { get; set; }
        public DbSet<Surcharge> Surcharges { get; set; }
        
        // CRM & Marketing
        public DbSet<Promotion> Promotions { get; set; }
        public DbSet<ContactMessage> ContactMessages { get; set; }
        public DbSet<Blog> Blogs { get; set; }
        public DbSet<BlogCategory> BlogCategories { get; set; }
        public DbSet<BlogTag> BlogTags { get; set; }
        public DbSet<BlogTagMapping> BlogTagMappings { get; set; }
        
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
        public DbSet<PayOSTransaction> PayOSTransactions { get; set; }
        public DbSet<BookingHold> BookingHolds { get; set; }
        public DbSet<PaymentLedgerEntry> PaymentLedgerEntries { get; set; }
        public DbSet<PaymentWebhookEvent> PaymentWebhookEvents { get; set; }
        public DbSet<RefundRequest> RefundRequests { get; set; }
        public DbSet<RefundPayout> RefundPayouts { get; set; }
        public DbSet<SettlementReview> SettlementReviews { get; set; }
        
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

        // Phase 7: Partner & Driver Contracts
        public DbSet<DriverContract> DriverContracts { get; set; }
        public DbSet<PartnerContract> PartnerContracts { get; set; }
        public DbSet<PhysicalContract> PhysicalContracts { get; set; }
        public DbSet<PhysicalContractType> PhysicalContractTypes { get; set; }

        // Phase 8: Driver Reviews
        public DbSet<DriverReview> DriverReviews { get; set; }

        // Phase 9: Enterprise Audit & Invoice
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Invoice> Invoices { get; set; }

        // Phase 10: Area Management
        public DbSet<Area> Areas { get; set; }
        public DbSet<AreaLocation> AreaLocations { get; set; }
        public DbSet<AreaStaff> AreaStaffs { get; set; }
        public DbSet<CameraDevice> CameraDevices { get; set; }
        public DbSet<CameraGateEvent> CameraGateEvents { get; set; }
        public DbSet<VehicleAreaPresence> VehicleAreaPresences { get; set; }
        public DbSet<CameraWebhookNonce> CameraWebhookNonces { get; set; }

        // Snapshot / Backup
        public DbSet<DbSnapshot> DbSnapshots { get; set; }
        public DbSet<PrivacyRequest> PrivacyRequests { get; set; }
        public DbSet<LegalDocumentVersion> LegalDocumentVersions { get; set; }
        public DbSet<ConsentRecord> ConsentRecords { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }

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

            modelBuilder.Entity<ServiceType>()
                .HasIndex(st => st.Code)
                .IsUnique();

            modelBuilder.Entity<TripType>()
                .HasIndex(tt => tt.Code)
                .IsUnique();

            modelBuilder.Entity<Surcharge>()
                .HasIndex(s => s.Code)
                .IsUnique();

            modelBuilder.Entity<CustomerDrivingLicense>()
                .HasIndex(l => new { l.UserID, l.LicenseNumberHash })
                .HasFilter("[LicenseNumberHash] IS NOT NULL");

            modelBuilder.Entity<TrackingIngestNonce>()
                .HasIndex(n => new { n.DeviceId, n.Nonce })
                .IsUnique();

            modelBuilder.Entity<TrackingIngestNonce>()
                .HasIndex(n => n.CreatedAt);

            modelBuilder.Entity<Payment>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PayOSTransaction>()
                .Property(t => t.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PayOSTransaction>()
                .HasIndex(t => t.OrderCode)
                .IsUnique();

            modelBuilder.Entity<PayOSTransaction>()
                .HasIndex(t => t.BookingID);

            modelBuilder.Entity<PayOSTransaction>()
                .HasIndex(t => t.BookingHoldID);

            modelBuilder.Entity<PayOSTransaction>()
                .HasIndex(t => t.TransactionID);

            modelBuilder.Entity<PayOSTransaction>()
                .HasIndex(t => t.UserID);

            modelBuilder.Entity<PayOSTransaction>()
                .HasOne(t => t.Booking)
                .WithMany()
                .HasForeignKey(t => t.BookingID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PayOSTransaction>()
                .HasOne(t => t.BookingHold)
                .WithMany()
                .HasForeignKey(t => t.BookingHoldID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PayOSTransaction>()
                .HasOne(t => t.BookingTransaction)
                .WithMany()
                .HasForeignKey(t => t.TransactionID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PayOSTransaction>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PayOSTransaction>()
                .HasOne(t => t.Payment)
                .WithMany()
                .HasForeignKey(t => t.PaymentID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<BookingHold>()
                .Property(h => h.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<BookingHold>()
                .HasIndex(h => new { h.CarID, h.StartDate, h.EndDate, h.Status });

            modelBuilder.Entity<BookingHold>()
                .HasIndex(h => h.ExpiresAt);

            modelBuilder.Entity<BookingHold>()
                .HasOne(h => h.User)
                .WithMany()
                .HasForeignKey(h => h.UserID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<BookingHold>()
                .HasOne(h => h.Car)
                .WithMany()
                .HasForeignKey(h => h.CarID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<BookingHold>()
                .HasOne(h => h.Booking)
                .WithMany()
                .HasForeignKey(h => h.BookingID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<BookingHold>()
                .HasOne(h => h.BookingTransaction)
                .WithMany()
                .HasForeignKey(h => h.TransactionID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PaymentLedgerEntry>()
                .Property(l => l.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<PaymentLedgerEntry>()
                .Property(l => l.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<PaymentLedgerEntry>()
                .HasIndex(l => l.IdempotencyKey)
                .IsUnique()
                .HasFilter("[IdempotencyKey] IS NOT NULL");

            modelBuilder.Entity<PaymentLedgerEntry>()
                .HasIndex(l => new { l.BookingID, l.EntryType, l.CreatedAt });

            modelBuilder.Entity<Payment>()
                .HasIndex(p => new { p.BookingID, p.PaymentDate });

            modelBuilder.Entity<PaymentWebhookEvent>()
                .HasIndex(e => new { e.Provider, e.EventKey })
                .IsUnique();

            modelBuilder.Entity<PaymentWebhookEvent>()
                .HasIndex(e => new { e.OrderCode, e.Status });

            modelBuilder.Entity<SettlementReview>()
                .Property(s => s.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<SettlementReview>()
                .HasIndex(s => new { s.BookingID, s.Status, s.CreatedAt });

            modelBuilder.Entity<SettlementReview>()
                .HasOne(s => s.Booking)
                .WithMany()
                .HasForeignKey(s => s.BookingID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SettlementReview>()
                .HasOne(s => s.HandoverRecord)
                .WithMany()
                .HasForeignKey(s => s.HandoverID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PrivacyRequest>()
                .HasIndex(p => new { p.UserID, p.Status, p.CreatedAt });

            modelBuilder.Entity<PrivacyRequest>()
                .HasIndex(p => p.DueAt);

            modelBuilder.Entity<PrivacyRequest>()
                .HasOne(p => p.User)
                .WithMany()
                .HasForeignKey(p => p.UserID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PrivacyRequest>()
                .HasOne(p => p.ReviewedByUser)
                .WithMany()
                .HasForeignKey(p => p.ReviewedByUserID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<PrivacyRequest>()
                .HasOne(p => p.CompletedByUser)
                .WithMany()
                .HasForeignKey(p => p.CompletedByUserID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<LegalDocumentVersion>()
                .HasIndex(d => new { d.Code, d.Version })
                .IsUnique();

            modelBuilder.Entity<LegalDocumentVersion>()
                .HasIndex(d => new { d.Code, d.IsActive, d.EffectiveAt });

            modelBuilder.Entity<ConsentRecord>()
                .HasIndex(c => new { c.UserID, c.PurposeCode, c.GrantedAt });

            modelBuilder.Entity<ConsentRecord>()
                .HasIndex(c => new { c.UserID, c.LegalDocumentVersionID, c.PurposeCode, c.IsGranted });

            modelBuilder.Entity<ConsentRecord>()
                .HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ConsentRecord>()
                .HasOne(c => c.LegalDocumentVersion)
                .WithMany(d => d.ConsentRecords)
                .HasForeignKey(c => c.LegalDocumentVersionID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<SupportTicket>()
                .HasIndex(t => new { t.UserID, t.Status, t.CreatedAt });

            modelBuilder.Entity<SupportTicket>()
                .HasIndex(t => new { t.Priority, t.Status, t.FirstResponseDueAt });

            modelBuilder.Entity<SupportTicket>()
                .HasOne(t => t.User)
                .WithMany()
                .HasForeignKey(t => t.UserID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<SupportTicket>()
                .HasOne(t => t.AssignedToUser)
                .WithMany()
                .HasForeignKey(t => t.AssignedToUserID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<ApiKey>()
                .HasIndex(k => k.KeyHash)
                .IsUnique();

            modelBuilder.Entity<ApiKey>()
                .HasIndex(k => new { k.OwnerUserID, k.Status });

            modelBuilder.Entity<ApiKey>()
                .HasOne(k => k.OwnerUser)
                .WithMany()
                .HasForeignKey(k => k.OwnerUserID)
                .OnDelete(DeleteBehavior.NoAction);

            // (Duplicate PaymentLedgerEntry/PaymentWebhookEvent index khai báo đã được gộp ở phía trên — TASK-D01)

            modelBuilder.Entity<BlogCategory>()
                .HasIndex(c => c.Slug)
                .IsUnique();

            modelBuilder.Entity<BlogTag>()
                .HasIndex(t => t.Slug)
                .IsUnique();

            modelBuilder.Entity<Blog>()
                .HasIndex(b => b.Slug)
                .IsUnique();

            modelBuilder.Entity<Blog>()
                .HasIndex(b => new { b.Status, b.PublishedDate });

            modelBuilder.Entity<Blog>()
                .HasIndex(b => b.IsFeatured);

            modelBuilder.Entity<Blog>()
                .HasOne(b => b.Category)
                .WithMany(c => c.Blogs)
                .HasForeignKey(b => b.CategoryID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<BlogTagMapping>()
                .HasKey(m => new { m.BlogID, m.BlogTagID });

            modelBuilder.Entity<BlogTagMapping>()
                .HasOne(m => m.Blog)
                .WithMany(b => b.BlogTagMappings)
                .HasForeignKey(m => m.BlogID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<BlogTagMapping>()
                .HasOne(m => m.BlogTag)
                .WithMany(t => t.BlogTagMappings)
                .HasForeignKey(m => m.BlogTagID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RefundRequest>()
                .Property(r => r.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<RefundRequest>()
                .Property(r => r.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<RefundRequest>()
                .HasIndex(r => new { r.BookingID, r.Status });

            modelBuilder.Entity<RefundPayout>()
                .Property(p => p.Amount)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<RefundPayout>()
                .Property(p => p.RowVersion)
                .IsRowVersion();

            modelBuilder.Entity<RefundPayout>()
                .HasIndex(p => p.RefundRequestID);

            modelBuilder.Entity<RefundPayout>()
                .HasIndex(p => p.ReferenceId)
                .IsUnique();

            modelBuilder.Entity<RefundPayout>()
                .HasIndex(p => p.IdempotencyKey)
                .IsUnique();

            modelBuilder.Entity<RefundPayout>()
                .HasOne(p => p.RefundRequest)
                .WithMany()
                .HasForeignKey(p => p.RefundRequestID)
                .OnDelete(DeleteBehavior.Cascade);

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

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.ServiceType)
                .WithMany(st => st.Bookings)
                .HasForeignKey(b => b.ServiceTypeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.Driver)
                .WithMany()
                .HasForeignKey(b => b.DriverID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.CustomerDrivingLicense)
                .WithMany()
                .HasForeignKey(b => b.CustomerDrivingLicenseID)
                .OnDelete(DeleteBehavior.Restrict);

            // Configure DeleteBehavior for Location in Car
            modelBuilder.Entity<Car>()
                .HasOne(c => c.Location)
                .WithMany(l => l.Cars)
                .HasForeignKey(c => c.LocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Car>()
                .HasOne(c => c.ServiceType)
                .WithMany(st => st.Cars)
                .HasForeignKey(c => c.ServiceTypeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CarModel>()
                .HasIndex(cm => new { cm.BrandID, cm.Name })
                .IsUnique();

            modelBuilder.Entity<CarModel>()
                .HasOne(cm => cm.CarBrand)
                .WithMany(cb => cb.CarModels)
                .HasForeignKey(cm => cm.BrandID);

            modelBuilder.Entity<CarAmenity>()
                .HasIndex(ca => ca.Code)
                .IsUnique();

            modelBuilder.Entity<CarAmenityMapping>()
                .HasKey(cam => new { cam.CarID, cam.CarAmenityID });

            modelBuilder.Entity<CarAmenityMapping>()
                .HasOne(cam => cam.Car)
                .WithMany(c => c.CarAmenityMappings)
                .HasForeignKey(cam => cam.CarID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<CarAmenityMapping>()
                .HasOne(cam => cam.CarAmenity)
                .WithMany(ca => ca.CarAmenityMappings)
                .HasForeignKey(cam => cam.CarAmenityID)
                .OnDelete(DeleteBehavior.Cascade);

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

            modelBuilder.Entity<Booking>()
                .Property(b => b.ReservationFee)
                .HasColumnType("decimal(18,2)");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.ReferralCode)
                .IsUnique()
                .HasFilter("[ReferralCode] IS NOT NULL");

            modelBuilder.Entity<User>()
                .HasOne(u => u.ReferredByUser)
                .WithMany(u => u.ReferredUsers)
                .HasForeignKey(u => u.ReferredByUserID)
                .OnDelete(DeleteBehavior.NoAction);



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

            modelBuilder.Entity<Driver>()
                .HasOne(d => d.User)
                .WithMany()
                .HasForeignKey(d => d.UserID);

            modelBuilder.Entity<Driver>()
                .HasOne(d => d.Partner)
                .WithMany()
                .HasForeignKey(d => d.PartnerID);

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

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.TermsDocumentVersion)
                .WithMany()
                .HasForeignKey(b => b.TermsDocumentVersionID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<Booking>()
                .HasOne(b => b.PrivacyNoticeVersion)
                .WithMany()
                .HasForeignKey(b => b.PrivacyNoticeVersionID)
                .OnDelete(DeleteBehavior.NoAction);

            modelBuilder.Entity<BookingTransaction>()
                .HasOne(bt => bt.DigitalContract)
                .WithOne(dc => dc.BookingTransaction)
                .HasForeignKey<DigitalContract>(dc => dc.TransactionID)
                .OnDelete(DeleteBehavior.Cascade);

            // ═══ DigitalContract Configuration ═══
            modelBuilder.Entity<DigitalContract>()
                .Property(dc => dc.RentalPricePerDay)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DigitalContract>()
                .Property(dc => dc.TotalContractValue)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DigitalContract>()
                .Property(dc => dc.DepositAmount)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DigitalContract>()
                .Property(dc => dc.TermsAndConditions)
                .HasMaxLength(8000);

            // ═══ Driver Contract Configuration ═══
            modelBuilder.Entity<DriverContract>()
                .Property(dc => dc.BaseSalary)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DriverContract>()
                .Property(dc => dc.TripFee)
                .HasColumnType("decimal(18,2)");
            modelBuilder.Entity<DriverContract>()
                .HasOne(dc => dc.Driver)
                .WithMany()
                .HasForeignKey(dc => dc.DriverID)
                .OnDelete(DeleteBehavior.Restrict);

            // ═══ Partner Contract Configuration ═══
            modelBuilder.Entity<PartnerContract>()
                .HasOne(pc => pc.PartnerUser)
                .WithMany()
                .HasForeignKey(pc => pc.PartnerUserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PhysicalContractType>()
                .HasIndex(pct => pct.Code)
                .IsUnique();

            modelBuilder.Entity<PhysicalContract>()
                .HasOne(pc => pc.Booking)
                .WithMany()
                .HasForeignKey(pc => pc.BookingID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PhysicalContract>()
                .HasOne(pc => pc.Car)
                .WithMany()
                .HasForeignKey(pc => pc.CarID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<PhysicalContract>()
                .HasOne(pc => pc.Owner)
                .WithMany()
                .HasForeignKey(pc => pc.OwnerID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<PhysicalContract>()
                .HasOne(pc => pc.PhysicalContractType)
                .WithMany()
                .HasForeignKey(pc => pc.PhysicalContractTypeID)
                .OnDelete(DeleteBehavior.SetNull);

            // ═══ Driver Review Configuration ═══
            modelBuilder.Entity<DriverReview>()
                .HasOne(dr => dr.Driver)
                .WithMany(d => d.DriverReviews)
                .HasForeignKey(dr => dr.DriverID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<DriverReview>()
                .HasOne(dr => dr.User)
                .WithMany()
                .HasForeignKey(dr => dr.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CustomerDrivingLicense>()
                .HasOne(l => l.User)
                .WithMany(u => u.CustomerDrivingLicenses)
                .HasForeignKey(l => l.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<TripBooking>()
                .HasOne(t => t.Booking)
                .WithMany()
                .HasForeignKey(t => t.BookingID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripBooking>()
                .HasOne(t => t.Customer)
                .WithMany()
                .HasForeignKey(t => t.CustomerID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripBooking>()
                .HasOne(t => t.Car)
                .WithMany()
                .HasForeignKey(t => t.CarID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TripBooking>()
                .HasOne(t => t.Driver)
                .WithMany()
                .HasForeignKey(t => t.DriverID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<TripBooking>()
                .HasOne(t => t.TripType)
                .WithMany()
                .HasForeignKey(t => t.TripTypeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<TripStop>()
                .HasOne(s => s.TripBooking)
                .WithMany(t => t.TripStops)
                .HasForeignKey(s => s.TripBookingID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<RoutePrice>()
                .HasOne(r => r.TripType)
                .WithMany()
                .HasForeignKey(r => r.TripTypeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<RoutePrice>()
                .HasOne(r => r.CarType)
                .WithMany()
                .HasForeignKey(r => r.CarTypeID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Surcharge>()
                .HasOne(s => s.ApplyToTripType)
                .WithMany()
                .HasForeignKey(s => s.ApplyToTripTypeID)
                .OnDelete(DeleteBehavior.SetNull);

            // ═══ Invoice Configuration ═══
            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.Booking)
                .WithMany()
                .HasForeignKey(i => i.BookingID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasOne(i => i.User)
                .WithMany()
                .HasForeignKey(i => i.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Invoice>()
                .HasIndex(i => i.InvoiceNumber)
                .IsUnique();

            // ═══ AuditLog Configuration ═══
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany()
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.SetNull);

            // ═══ Area Management Configuration ═══

            // Area — composite PK on AreaLocation
            modelBuilder.Entity<AreaLocation>()
                .HasKey(al => new { al.AreaID, al.LocationID });

            modelBuilder.Entity<AreaLocation>()
                .HasOne(al => al.Area)
                .WithMany(a => a.AreaLocations)
                .HasForeignKey(al => al.AreaID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AreaLocation>()
                .HasOne(al => al.Location)
                .WithMany(l => l.AreaLocations)
                .HasForeignKey(al => al.LocationID)
                .OnDelete(DeleteBehavior.Restrict);

            // AreaStaff — unique constraint + relationships
            modelBuilder.Entity<AreaStaff>()
                .HasIndex(asx => new { asx.AreaID, asx.UserID })
                .IsUnique();

            modelBuilder.Entity<AreaStaff>()
                .HasOne(asx => asx.Area)
                .WithMany(a => a.AreaStaffs)
                .HasForeignKey(asx => asx.AreaID)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<AreaStaff>()
                .HasOne(asx => asx.User)
                .WithMany(u => u.AreaStaffs)
                .HasForeignKey(asx => asx.UserID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<AreaStaff>()
                .HasOne(asx => asx.AssignedByUser)
                .WithMany()
                .HasForeignKey(asx => asx.AssignedBy)
                .OnDelete(DeleteBehavior.SetNull);

            // Area — Manager self-referential FK
            modelBuilder.Entity<Area>()
                .HasOne(a => a.Manager)
                .WithMany()
                .HasForeignKey(a => a.ManagerID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<Area>()
                .HasIndex(a => a.Code)
                .IsUnique();

            modelBuilder.Entity<Area>()
                .HasIndex(a => a.Status);

            modelBuilder.Entity<Area>()
                .HasQueryFilter(a => !a.IsDeleted);

            // Camera ANPR — devices, gate events and vehicle presence
            modelBuilder.Entity<CameraDevice>()
                .HasIndex(c => c.Code)
                .IsUnique();

            modelBuilder.Entity<CameraDevice>()
                .HasIndex(c => new { c.AreaID, c.LocationID, c.Status });

            modelBuilder.Entity<CameraDevice>()
                .HasOne(c => c.Area)
                .WithMany(a => a.CameraDevices)
                .HasForeignKey(c => c.AreaID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CameraDevice>()
                .HasOne(c => c.Location)
                .WithMany(l => l.CameraDevices)
                .HasForeignKey(c => c.LocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CameraGateEvent>()
                .HasIndex(e => new { e.CameraDeviceID, e.ProviderEventId })
                .IsUnique()
                .HasFilter("[ProviderEventId] IS NOT NULL");

            modelBuilder.Entity<CameraGateEvent>()
                .HasIndex(e => new { e.AreaID, e.LocationID, e.CapturedAt });

            modelBuilder.Entity<CameraGateEvent>()
                .HasIndex(e => new { e.MatchStatus, e.CapturedAt });

            modelBuilder.Entity<CameraGateEvent>()
                .HasIndex(e => e.LicensePlateNormalized);

            modelBuilder.Entity<CameraGateEvent>()
                .HasOne(e => e.CameraDevice)
                .WithMany(c => c.GateEvents)
                .HasForeignKey(e => e.CameraDeviceID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CameraGateEvent>()
                .HasOne(e => e.Car)
                .WithMany(c => c.CameraGateEvents)
                .HasForeignKey(e => e.CarID)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<CameraGateEvent>()
                .HasOne(e => e.Area)
                .WithMany(a => a.CameraGateEvents)
                .HasForeignKey(e => e.AreaID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CameraGateEvent>()
                .HasOne(e => e.Location)
                .WithMany(l => l.CameraGateEvents)
                .HasForeignKey(e => e.LocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VehicleAreaPresence>()
                .HasIndex(p => new { p.CarID, p.AreaID, p.LocationID })
                .IsUnique()
                .HasFilter(null);

            modelBuilder.Entity<VehicleAreaPresence>()
                .HasIndex(p => new { p.CarID, p.AreaID })
                .IsUnique()
                .HasFilter("[AreaID] IS NOT NULL AND [LocationID] IS NULL");

            modelBuilder.Entity<VehicleAreaPresence>()
                .HasIndex(p => new { p.CarID, p.LocationID })
                .IsUnique()
                .HasFilter("[AreaID] IS NULL AND [LocationID] IS NOT NULL");

            modelBuilder.Entity<VehicleAreaPresence>()
                .HasIndex(p => new { p.AreaID, p.LocationID, p.IsInside });

            modelBuilder.Entity<VehicleAreaPresence>()
                .HasOne(p => p.Car)
                .WithMany(c => c.AreaPresences)
                .HasForeignKey(p => p.CarID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VehicleAreaPresence>()
                .HasOne(p => p.Area)
                .WithMany(a => a.VehicleAreaPresences)
                .HasForeignKey(p => p.AreaID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VehicleAreaPresence>()
                .HasOne(p => p.Location)
                .WithMany(l => l.VehicleAreaPresences)
                .HasForeignKey(p => p.LocationID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VehicleAreaPresence>()
                .HasOne(p => p.LastCameraGateEvent)
                .WithMany()
                .HasForeignKey(p => p.LastCameraGateEventID)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<CameraWebhookNonce>()
                .HasIndex(n => new { n.CameraDeviceID, n.Nonce })
                .IsUnique();

            modelBuilder.Entity<CameraWebhookNonce>()
                .HasIndex(n => n.CreatedAt);

            modelBuilder.Entity<CameraWebhookNonce>()
                .HasOne(n => n.CameraDevice)
                .WithMany()
                .HasForeignKey(n => n.CameraDeviceID)
                .OnDelete(DeleteBehavior.Cascade);

            // ═══ DbSnapshot Configuration ═══
            modelBuilder.Entity<DbSnapshot>()
                .HasIndex(ds => ds.Status);

            modelBuilder.Entity<DbSnapshot>()
                .HasIndex(ds => ds.CreatedAt);

            modelBuilder.Entity<DbSnapshot>()
                .HasIndex(ds => ds.IsAutoGenerated);

            // ═══════════════════════════════════════════
            // ⚡ DATABASE INDEXES — Tối ưu hiệu năng query
            // ═══════════════════════════════════════════

            // User indexes
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            modelBuilder.Entity<User>()
                .HasIndex(u => u.CitizenId)
                .IsUnique()
                .HasFilter("[CitizenId] IS NOT NULL");

            modelBuilder.Entity<User>()
                .HasIndex(u => u.Role);

            // Car indexes
            modelBuilder.Entity<Car>()
                .HasIndex(c => c.CarStatusID);

            modelBuilder.Entity<Car>()
                .HasIndex(c => c.LocationID);

            modelBuilder.Entity<Car>()
                .HasIndex(c => c.BrandID);

            modelBuilder.Entity<Car>()
                .HasIndex(c => c.CarTypeID);

            modelBuilder.Entity<Car>()
                .HasIndex(c => c.LicensePlate)
                .IsUnique();

            modelBuilder.Entity<Car>()
                .HasIndex(c => c.LicensePlateNormalized);

            modelBuilder.Entity<Car>()
                .HasIndex(c => c.VIN)
                .IsUnique();

            // Booking indexes
            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.UserID);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.CarID);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.BookingStatusID);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.CreatedAt);

            modelBuilder.Entity<Booking>()
                .HasIndex(b => new { b.StartDate, b.EndDate });

            modelBuilder.Entity<Booking>()
                .HasIndex(b => new { b.CarID, b.StartDate, b.EndDate, b.BookingStatusID });

            modelBuilder.Entity<Booking>()
                .HasIndex(b => new { b.CreatedAt, b.BookingStatusID });

            modelBuilder.Entity<Booking>()
                .HasIndex(b => new { b.UserID, b.CreatedAt });

            // Payment index
            modelBuilder.Entity<Payment>()
                .HasIndex(p => p.BookingID);

            // AuditLog indexes
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.Timestamp);

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new { a.EntityType, a.Action });

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new { a.Timestamp, a.EntityType, a.Action });

            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => new { a.UserId, a.Timestamp });

            // Phase 10: Soft Delete Global Query Filters
            modelBuilder.Entity<Car>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<CarAmenityMapping>().HasQueryFilter(cam => !cam.IsDeleted && !cam.Car!.IsDeleted);
            modelBuilder.Entity<BookingHold>().HasQueryFilter(h => !h.Car!.IsDeleted);
            modelBuilder.Entity<Alert>().HasQueryFilter(a => !a.IsDeleted);
            modelBuilder.Entity<Booking>().HasQueryFilter(b => !b.IsDeleted);
            modelBuilder.Entity<BookingAddOn>().HasQueryFilter(ba => !ba.IsDeleted);
            modelBuilder.Entity<CarImage>().HasQueryFilter(ci => !ci.IsDeleted);
            modelBuilder.Entity<CartItem>().HasQueryFilter(ci => !ci.IsDeleted);
            modelBuilder.Entity<DigitalContract>().HasQueryFilter(dc => !dc.IsDeleted);
            modelBuilder.Entity<DriverBooking>().HasQueryFilter(db => !db.IsDeleted);
            modelBuilder.Entity<TripBooking>().HasQueryFilter(tb => !tb.IsDeleted);
            modelBuilder.Entity<TripStop>().HasQueryFilter(ts => !ts.TripBooking!.IsDeleted);
            modelBuilder.Entity<HandoverRecord>().HasQueryFilter(hr => !hr.IsDeleted);
            modelBuilder.Entity<InspectionRecord>().HasQueryFilter(ir => !ir.IsDeleted);
            modelBuilder.Entity<Invoice>().HasQueryFilter(i => !i.IsDeleted);
            modelBuilder.Entity<LongTermRental>().HasQueryFilter(ltr => !ltr.IsDeleted);
            modelBuilder.Entity<MaintenanceLog>().HasQueryFilter(ml => !ml.IsDeleted);
            modelBuilder.Entity<Payment>().HasQueryFilter(p => !p.IsDeleted);
            modelBuilder.Entity<PayOSTransaction>().HasQueryFilter(t => !t.IsDeleted);
            modelBuilder.Entity<RefundRequest>().HasQueryFilter(r => !r.Booking!.IsDeleted);
            modelBuilder.Entity<RefundPayout>().HasQueryFilter(rp => !rp.RefundRequest!.Booking!.IsDeleted);
            modelBuilder.Entity<SettlementReview>().HasQueryFilter(sr => !sr.Booking!.IsDeleted);
            modelBuilder.Entity<RentalContract>().HasQueryFilter(rc => !rc.IsDeleted);
            modelBuilder.Entity<Review>().HasQueryFilter(r => !r.IsDeleted);
            modelBuilder.Entity<Models.Route>().HasQueryFilter(r => !r.IsDeleted);
            modelBuilder.Entity<PaymentSchedule>().HasQueryFilter(ps => !ps.IsDeleted);
            modelBuilder.Entity<VehicleTracking>().HasQueryFilter(vt => !vt.IsDeleted);
            modelBuilder.Entity<AreaLocation>().HasQueryFilter(al => !al.IsDeleted);
            modelBuilder.Entity<AreaStaff>().HasQueryFilter(asx => !asx.IsDeleted);
            modelBuilder.Entity<CameraDevice>().HasQueryFilter(c => !c.IsDeleted);
            modelBuilder.Entity<CameraGateEvent>().HasQueryFilter(e => !e.IsDeleted);
            modelBuilder.Entity<VehicleAreaPresence>().HasQueryFilter(p => !p.IsDeleted);
        }

        public override int SaveChanges()
        {
            ApplyDerivedValues();
            ApplySoftDeleteRules();
            return base.SaveChanges();
        }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyDerivedValues();
            ApplySoftDeleteRules();
            return await base.SaveChangesAsync(cancellationToken);
        }

        private void ApplyDerivedValues()
        {
            var now = DateTime.UtcNow;
            foreach (var entry in ChangeTracker.Entries<Car>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
            {
                entry.Entity.LicensePlateNormalized = LicensePlateNormalizer.Normalize(entry.Entity.LicensePlate);
            }

            foreach (var entry in ChangeTracker.Entries<CameraDevice>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
            {
                entry.Entity.Code = entry.Entity.Code.Trim().ToUpperInvariant();
                entry.Entity.UpdatedAt = now;
            }
        }

        private void ApplySoftDeleteRules()
        {
            var entries = ChangeTracker
                .Entries()
                .Where(e => e.State == EntityState.Deleted);
            
            var now = DateTime.UtcNow;
            foreach (var entityEntry in entries)
            {
                entityEntry.State = EntityState.Modified;
                switch (entityEntry.Entity)
                {
                    case Car car:
                        car.IsDeleted = true;
                        car.DeletedAt = now;
                        break;
                    case Alert alert:
                        alert.IsDeleted = true;
                        break;
                    case Booking booking:
                        booking.IsDeleted = true;
                        break;
                    case BookingAddOn bookingAddOn:
                        bookingAddOn.IsDeleted = true;
                        break;
                    case CarImage carImage:
                        carImage.IsDeleted = true;
                        break;
                    case CartItem cartItem:
                        cartItem.IsDeleted = true;
                        break;
                    case DigitalContract digitalContract:
                        digitalContract.IsDeleted = true;
                        break;
                    case DriverBooking driverBooking:
                        driverBooking.IsDeleted = true;
                        break;
                    case TripBooking tripBooking:
                        tripBooking.IsDeleted = true;
                        break;
                    case HandoverRecord handoverRecord:
                        handoverRecord.IsDeleted = true;
                        break;
                    case InspectionRecord inspectionRecord:
                        inspectionRecord.IsDeleted = true;
                        break;
                    case Invoice invoice:
                        invoice.IsDeleted = true;
                        break;
                    case LongTermRental longTermRental:
                        longTermRental.IsDeleted = true;
                        break;
                    case MaintenanceLog maintenanceLog:
                        maintenanceLog.IsDeleted = true;
                        break;
                    case Payment payment:
                        payment.IsDeleted = true;
                        break;
                    case PayOSTransaction payOSTransaction:
                        payOSTransaction.IsDeleted = true;
                        break;
                    case RentalContract rentalContract:
                        rentalContract.IsDeleted = true;
                        break;
                    case Review review:
                        review.IsDeleted = true;
                        break;
                    case Models.Route route:
                        route.IsDeleted = true;
                        break;
                    case PaymentSchedule paymentSchedule:
                        paymentSchedule.IsDeleted = true;
                        break;
                    case VehicleTracking vehicleTracking:
                        vehicleTracking.IsDeleted = true;
                        break;
                    case AreaLocation areaLocation:
                        areaLocation.IsDeleted = true;
                        break;
                    case AreaStaff areaStaff:
                        areaStaff.IsDeleted = true;
                        break;
                    case CameraDevice cameraDevice:
                        cameraDevice.IsDeleted = true;
                        break;
                    case CameraGateEvent cameraGateEvent:
                        cameraGateEvent.IsDeleted = true;
                        break;
                    case VehicleAreaPresence vehicleAreaPresence:
                        vehicleAreaPresence.IsDeleted = true;
                        break;
                }
            }
        }
    }
}
