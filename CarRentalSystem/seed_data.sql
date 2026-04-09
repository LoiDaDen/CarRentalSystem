USE [CarRentalDB];
GO

-- Seed MembershipTiers
IF NOT EXISTS (SELECT 1 FROM MembershipTiers WHERE Name = 'Bronze')
BEGIN
    INSERT INTO MembershipTiers (Name, MinSpending, DiscountPercent, FreeDeliveryKm, PrioritySupport, ExtraFreeHours, Icon, Color, SortOrder)
    VALUES 
    ('Bronze', 0, 0, 0, 0, 0, 'bi-award', '#cd7f32', 1),
    ('Silver', 5000000, 3.00, 5, 0, 0, 'bi-award-fill', '#c0c0c0', 2),
    ('Gold', 15000000, 5.00, 10, 1, 1, 'bi-trophy', '#ffd700', 3),
    ('Platinum', 30000000, 8.00, 20, 1, 2, 'bi-trophy-fill', '#e5e4e2', 4),
    ('Diamond', 50000000, 12.00, 50, 1, 3, 'bi-gem', '#b9f2ff', 5);
END
GO

-- Seed ContractTypes
IF NOT EXISTS (SELECT 1 FROM ContractTypes WHERE Name = N'Theo ngày')
BEGIN
    INSERT INTO ContractTypes (Name, DiscountPercent, Description, MinDurationDays, MaxDurationDays, IsActive, FreeMaintenanceCount, FreeInsurance, AfterSalesDescription)
    VALUES 
    (N'Theo ngày', 0, N'Thuê xe theo ngày (1-30 ngày)', 1, 30, 1, 0, 0, NULL),
    (N'Theo tháng', 10.00, N'Thuê xe theo tháng, giảm 10%', 30, 180, 1, 1, 0, N'Miễn phí 1 lần bảo trì'),
    (N'Theo năm', 20.00, N'Thuê xe theo năm, giảm 20% + hậu mãi', 365, 1095, 1, 4, 1, N'Miễn phí 4 lần bảo trì + bảo hiểm miễn phí'),
    (N'Ngày chẵn', 5.00, N'Thuê vào ngày chẵn (2,4,6,...,30), giảm 5%', 1, 365, 1, 0, 0, NULL),
    (N'Ngày lẻ', 5.00, N'Thuê vào ngày lẻ (1,3,5,...,31), giảm 5%', 1, 365, 1, 0, 0, NULL),
    (N'Cuối tuần', 8.00, N'Thuê vào cuối tuần (T7-CN), giảm 8%', 1, 365, 1, 0, 0, NULL),
    (N'Trong tuần', 3.00, N'Thuê vào ngày trong tuần (T2-T6), giảm 3%', 1, 365, 1, 0, 0, NULL);
END
GO
