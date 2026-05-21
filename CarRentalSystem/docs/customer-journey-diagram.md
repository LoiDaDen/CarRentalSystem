# Customer Journey Diagram

Updated: 2026-05-19

```mermaid
journey
    title Self-drive car rental customer journey
    section Discovery
      Browse cars: 4: Customer
      Filter by location, dates, seats, price: 4: Customer
      Open car details and pricing tiers: 4: Customer
    section Account and Eligibility
      Register with phone, DOB, CCCD, driving license: 3: Customer
      Verify email OTP: 3: Customer
      Complete eKYC and license approval: 2: Customer, Staff
    section Booking
      Build checkout quote: 4: Customer
      Hold car availability: 4: System
      Pay reservation fee through PayOS or manual transfer: 3: Customer, System
      Confirm booking: 4: System
    section Handover
      Staff verifies booking and area: 3: Staff
      Capture check-in evidence and signature: 3: Staff, Customer
      Start active rental: 4: System
    section Return and Settlement
      Capture check-out evidence: 3: Staff, Customer
      Review late/damage/fuel/toll deductions: 2: Staff, Manager
      Refund or settle deposit: 4: System
    section Support
      Raise ticket or dispute: 3: Customer
      Export evidence package if needed: 3: Staff, Manager
```

## Evidence Notes

- Pricing display and checkout now share `ISelfDrivePricingService`.
- Handover now requires booking status guards and area/role authorization.
- Production journey still needs browser, eKYC sandbox, PayOS webhook, and staging evidence.
