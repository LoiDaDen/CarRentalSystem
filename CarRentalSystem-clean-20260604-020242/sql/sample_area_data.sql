-- ══════════════════════════════════════════════════════════════
-- SAMPLE DATA: Areas, AreaStaffs, AreaLocations + Bookings
-- Run sau khi da drop + chay migration (dotnet ef database update)
-- ══════════════════════════════════════════════════════════════

-- ╔══════════════════════════════════════╗
-- 1. AREAS (5 khu vực)
-- ╚══════════════════════════════════════╝

INSERT INTO Areas (Name, Code, Province, Address, Latitude, Longitude, ManagerID, Status, CreatedAt, UpdatedAt, IsDeleted)
VALUES
    (N'Khu vực Miền Bắc',      'NORTH',     N'Hà Nội',            N'12 P. Trần Duy Hưng, Trung Hoà, Cầu Giấy',  21.0285,  105.7823, NULL, 'active',   GETUTCDATE(), GETUTCDATE(), 0),
    (N'Khu vực Miền Trung',    'CENTRAL',   N'Đà Nẵng',          N'78 P. Nguyễn Văn Linh, Nam Hải, Hải Châu',  16.0544,  108.2022, NULL, 'active',   GETUTCDATE(), GETUTCDATE(), 0),
    (N'Khu vực Miền Nam',       'SOUTH',     N'TP. Hồ Chí Minh',  N'199 P. Nguyễn Đình Chiểu, Q.3',            10.7801,  106.6882, NULL, 'active',   GETUTCDATE(), GETUTCDATE(), 0),
    (N'Khu vực Đông Nam Bộ',    'SOUTHEAST', N'Bình Dương',        N'KCN Sóng Thần 3, Dĩ An',                  10.9535,  106.8235, NULL, 'active',   GETUTCDATE(), GETUTCDATE(), 0),
    (N'Khu vực Tây Nguyên',     'CENTRAL_H', N'Đắk Lắk',          N'Tổ 8, P. Tân An, TP. Buôn Ma Thuột',     12.6791,  108.0375, NULL, 'inactive', GETUTCDATE(), GETUTCDATE(), 0);


-- ╔══════════════════════════════════════╗
-- 2. TAO TAI KHOAN MANAGER + STAFF
-- mat khau: Manager@123 / Staff@123
-- ╚══════════════════════════════════════╝
DECLARE @mgrHash NVARCHAR(100) = N'$2a$11$sgtQ01zeg6vXEYT4UVSoDuKlrGi4QI8uVQK3aeOmOrfeV.Lvaj.cC';
DECLARE @staffHash NVARCHAR(100) = N'$2a$11$hkGU8oaBNNxFu943h2M30uF6sH7JxhC/qDQRI.ldQ/RnN6Otx5TSG';

INSERT INTO Users (Name, Email, Phone, PasswordHash, Role, IsEmailVerified, CreatedAt)
SELECT * FROM (
    VALUES
        (N'Nguyễn Văn Minh',  'mgr.north@carrental.com',           '0901110001', @mgrHash,  'Manager', 1, GETUTCDATE()),
        (N'Trần Thị Lan',       'mgr.central@carrental.com',          '0901110002', @mgrHash,  'Manager', 1, GETUTCDATE()),
        (N'Phạm Đình Khoa',    'mgr.south@carrental.com',            '0901110003', @mgrHash,  'Manager', 1, GETUTCDATE()),
        (N'Lê Hoàng Nam',       'staff.north@carrental.com',          '0902220001', @staffHash,'Staff',   1, GETUTCDATE()),
        (N'Phạm Thu Hà',        'staff.central@carrental.com',        '0902220002', @staffHash,'Staff',   1, GETUTCDATE()),
        (N'Huỳnh Công Phú',    'staff.south@carrental.com',           '0902220003', @staffHash,'Staff',   1, GETUTCDATE()),
        (N'Đặng Minh Tuấn',    'mgr.southeast@carrental.com',        '0901110004', @mgrHash,  'Manager', 1, GETUTCDATE()),
        (N'Võ Thị Mai',         'staff.southeast@carrental.com',      '0902220004', @staffHash,'Staff',   1, GETUTCDATE()),
        (N'Bùi Đình Khôi',     'mgr.centralhighlands@carrental.com',  '0901110005', @mgrHash,  'Manager', 1, GETUTCDATE()),
        (N'Ngô Thị Lan',       'staff.centralhighlands@carrental.com','0902220005', @staffHash,'Staff',   1, GETUTCDATE())
) AS t(Name,Email,Phone,PasswordHash,Role,IsEmailVerified,CreatedAt)
WHERE NOT EXISTS (SELECT 1 FROM Users WHERE Email IN (
    'mgr.north@carrental.com','mgr.central@carrental.com','mgr.south@carrental.com',
    'staff.north@carrental.com','staff.central@carrental.com','staff.south@carrental.com',
    'mgr.southeast@carrental.com','staff.southeast@carrental.com',
    'mgr.centralhighlands@carrental.com','staff.centralhighlands@carrental.com'));


-- ╔══════════════════════════════════════╗
-- 3. KHAI BAO BIEN
-- ╚══════════════════════════════════════╝
DECLARE @mgr1 INT; DECLARE @mgr2 INT; DECLARE @mgr3 INT;
DECLARE @staff1 INT; DECLARE @staff2 INT; DECLARE @staff3 INT; DECLARE @staff4 INT; DECLARE @staff5 INT;

DECLARE @loc1 INT; DECLARE @loc2 INT; DECLARE @loc3 INT; DECLARE @loc4 INT;
DECLARE @loc5 INT; DECLARE @loc6 INT; DECLARE @loc7 INT; DECLARE @loc8 INT;

DECLARE @userCust1 INT; DECLARE @userCust2 INT; DECLARE @userCust3 INT; DECLARE @userCust4 INT;
DECLARE @southLocID INT; DECLARE @northLocID INT; DECLARE @centralLocID INT;
DECLARE @carSouth1 INT; DECLARE @carSouth2 INT; DECLARE @carNorth1 INT; DECLARE @carCentral1 INT;
DECLARE @stConfirmed INT; DECLARE @stCompleted INT; DECLARE @stPending INT; DECLARE @stCancelled INT;

-- ╔══════════════════════════════════════╗
-- 4. LAY USER IDs (tu tai khoan vua tao)
-- ╚══════════════════════════════════════╝
SELECT @mgr1   = (SELECT TOP 1 UserID FROM Users WHERE Email = 'mgr.north@carrental.com');
SELECT @mgr2   = (SELECT TOP 1 UserID FROM Users WHERE Email = 'mgr.central@carrental.com');
SELECT @mgr3   = (SELECT TOP 1 UserID FROM Users WHERE Email = 'mgr.south@carrental.com');
SELECT @staff1 = (SELECT TOP 1 UserID FROM Users WHERE Email = 'staff.north@carrental.com');
SELECT @staff2 = (SELECT TOP 1 UserID FROM Users WHERE Email = 'staff.central@carrental.com');
SELECT @staff3 = (SELECT TOP 1 UserID FROM Users WHERE Email = 'mgr.southeast@carrental.com');
SELECT @staff4 = (SELECT TOP 1 UserID FROM Users WHERE Email = 'staff.southeast@carrental.com');
SELECT @staff5 = (SELECT TOP 1 UserID FROM Users WHERE Email = 'staff.centralhighlands@carrental.com');

-- ╔══════════════════════════════════════╗
-- 5. LAY LOCATION IDs
-- ╚══════════════════════════════════════╝
SELECT @loc1 = (SELECT TOP 1 LocationID FROM Locations ORDER BY LocationID);
SELECT @loc2 = (SELECT TOP 1 LocationID FROM Locations WHERE LocationID <> @loc1 ORDER BY LocationID);
SELECT @loc3 = (SELECT TOP 1 LocationID FROM Locations WHERE LocationID NOT IN (@loc1,@loc2) ORDER BY LocationID);
SELECT @loc4 = (SELECT TOP 1 LocationID FROM Locations WHERE LocationID NOT IN (@loc1,@loc2,@loc3) ORDER BY LocationID);
SELECT @loc5 = (SELECT TOP 1 LocationID FROM Locations WHERE LocationID NOT IN (@loc1,@loc2,@loc3,@loc4) ORDER BY LocationID);
SELECT @loc6 = (SELECT TOP 1 LocationID FROM Locations WHERE LocationID NOT IN (@loc1,@loc2,@loc3,@loc4,@loc5) ORDER BY LocationID);
SELECT @loc7 = (SELECT TOP 1 LocationID FROM Locations WHERE LocationID NOT IN (@loc1,@loc2,@loc3,@loc4,@loc5,@loc6) ORDER BY LocationID);
SELECT @loc8 = (SELECT TOP 1 LocationID FROM Locations WHERE LocationID NOT IN (@loc1,@loc2,@loc3,@loc4,@loc5,@loc6,@loc7) ORDER BY LocationID);

-- ╔══════════════════════════════════════╗
-- 6. LAY BOOKING STATUS IDs
-- ╚══════════════════════════════════════╝
SELECT @stConfirmed  = BookingStatusID FROM BookingStatuses WHERE Name = 'Confirmed';
SELECT @stCompleted  = BookingStatusID FROM BookingStatuses WHERE Name = 'Completed';
SELECT @stPending    = BookingStatusID FROM BookingStatuses WHERE Name = 'Pending';
SELECT @stCancelled  = BookingStatusID FROM BookingStatuses WHERE Name = 'Cancelled';


-- ╔══════════════════════════════════════╗
-- 6. AREA STAFFS
-- Area 1 (North):      manager mgr1,  staff staff1
-- Area 2 (Central):    manager mgr2,  staff staff2
-- Area 3 (South):      manager mgr3,  staff staff1, staff2
-- Area 4 (Southeast):   manager staff3, staff staff4
-- Area 5 (C.Highlands):manager staff3, staff staff5
-- ╚══════════════════════════════════════╝
IF @mgr1 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (1, @mgr1, 'manager', GETUTCDATE(), NULL, 0);

IF @staff1 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (1, @staff1, 'staff', GETUTCDATE(), @mgr1, 0);

IF @mgr2 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (2, @mgr2, 'manager', GETUTCDATE(), NULL, 0);

IF @staff2 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (2, @staff2, 'staff', GETUTCDATE(), @mgr2, 0);

IF @mgr3 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (3, @mgr3, 'manager', GETUTCDATE(), NULL, 0);

IF @staff1 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (3, @staff1, 'staff', GETUTCDATE(), @mgr3, 0);

IF @staff2 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (3, @staff2, 'staff', GETUTCDATE(), @mgr3, 0);

IF @staff3 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (4, @staff3, 'manager', GETUTCDATE(), NULL, 0);

IF @staff4 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (4, @staff4, 'staff', GETUTCDATE(), @staff3, 0);

IF @staff3 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (5, @staff3, 'manager', GETUTCDATE(), NULL, 0);

IF @staff5 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (5, @staff5, 'staff', GETUTCDATE(), @staff3, 0);


-- ╔══════════════════════════════════════╗
-- 7. UPDATE MANAGERID CUA AREAS
-- ╚══════════════════════════════════════╝
UPDATE A SET A.ManagerID = (SELECT TOP 1 UserID FROM AreaStaffs WHERE AreaID = A.AreaID AND RoleInArea = 'manager' AND IsDeleted = 0)
FROM Areas A
WHERE EXISTS (SELECT 1 FROM AreaStaffs WHERE AreaID = A.AreaID AND RoleInArea = 'manager' AND IsDeleted = 0);


-- ╔══════════════════════════════════════╗
-- 8. AREA LOCATIONS
-- ╚══════════════════════════════════════╝
INSERT INTO AreaLocations (AreaID, LocationID, IsDeleted)
VALUES
    (1, ISNULL(@loc1, 1), 0),
    (1, ISNULL(@loc2, 2), 0),
    (2, ISNULL(@loc3, 3), 0),
    (2, ISNULL(@loc4, 4), 0),
    (3, ISNULL(@loc5, 5), 0),
    (3, ISNULL(@loc6, 6), 0),
    (3, ISNULL(@loc7, 7), 0),
    (4, ISNULL(@loc8, 8), 0);


-- ╔══════════════════════════════════════╗
-- 9. SAMPLE BOOKINGS
-- ╚══════════════════════════════════════╝
SELECT @userCust1 = (SELECT TOP 1 UserID FROM Users WHERE Role = 'Customer' ORDER BY UserID);
SELECT @userCust2 = (SELECT TOP 1 UserID FROM Users WHERE Role = 'Customer' AND UserID <> @userCust1 ORDER BY UserID);
SELECT @userCust3 = (SELECT TOP 1 UserID FROM Users WHERE Role = 'Customer' AND UserID NOT IN (@userCust1, @userCust2) ORDER BY UserID);
SELECT @userCust4 = (SELECT TOP 1 UserID FROM Users WHERE Role = 'Customer' AND UserID NOT IN (@userCust1, @userCust2, @userCust3) ORDER BY UserID);

SELECT @southLocID  = (SELECT TOP 1 al.LocationID FROM AreaLocations al WHERE al.AreaID = 3 AND al.IsDeleted = 0);
SELECT @northLocID  = (SELECT TOP 1 al.LocationID FROM AreaLocations al WHERE al.AreaID = 1 AND al.IsDeleted = 0);
SELECT @centralLocID= (SELECT TOP 1 al.LocationID FROM AreaLocations al WHERE al.AreaID = 2 AND al.IsDeleted = 0);

SELECT @carSouth1   = (SELECT TOP 1 CarID FROM Cars WHERE LocationID = @southLocID AND IsDeleted = 0);
SELECT @carSouth2   = (SELECT TOP 1 CarID FROM Cars WHERE LocationID = @southLocID AND IsDeleted = 0 AND CarID <> @carSouth1);
SELECT @carNorth1   = (SELECT TOP 1 CarID FROM Cars WHERE LocationID = @northLocID AND IsDeleted = 0);
SELECT @carCentral1 = (SELECT TOP 1 CarID FROM Cars WHERE LocationID = @centralLocID AND IsDeleted = 0);

-- SOUTH bookings
IF @carSouth1 IS NOT NULL AND @userCust1 IS NOT NULL
BEGIN
    INSERT INTO Bookings (UserID, CarID, StartDate, EndDate, PickupLocationID, ReturnLocationID,
                          TotalPrice, TotalHours, BookingStatusID, InsuranceTier, CreatedAt, IsDeleted)
    VALUES
        (@userCust1, @carSouth1, DATEADD(day,-2,GETUTCDATE()), DATEADD(hour,8,DATEADD(day,-2,GETUTCDATE())),
         @southLocID, @southLocID, 1200000, 8, @stCompleted, 'Premium', DATEADD(day,-2,GETUTCDATE()), 0),
        (@userCust2, @carSouth1, DATEADD(day,-5,GETUTCDATE()), DATEADD(hour,4,DATEADD(day,-5,GETUTCDATE())),
         @southLocID, @southLocID, 600000, 4, @stCompleted, 'Basic', DATEADD(day,-5,GETUTCDATE()), 0),
        (@userCust3, @carSouth1, GETUTCDATE(), DATEADD(hour,6,GETUTCDATE()),
         @southLocID, @southLocID, 900000, 6, @stConfirmed, 'Basic', GETUTCDATE(), 0);
END

IF @carSouth2 IS NOT NULL AND @userCust4 IS NOT NULL
BEGIN
    INSERT INTO Bookings (UserID, CarID, StartDate, EndDate, PickupLocationID, ReturnLocationID,
                          TotalPrice, TotalHours, BookingStatusID, InsuranceTier, CreatedAt, IsDeleted)
    VALUES
        (@userCust4, @carSouth2, DATEADD(day,-10,GETUTCDATE()), DATEADD(day,-9,GETUTCDATE()),
         @southLocID, @southLocID, 2400000, 24, @stCompleted, 'Full Cover', DATEADD(day,-10,GETUTCDATE()), 0),
        (@userCust1, @carSouth2, DATEADD(day,-1,GETUTCDATE()), DATEADD(hour,8,DATEADD(day,-1,GETUTCDATE())),
         @southLocID, @southLocID, 800000, 8, @stPending, 'Basic', DATEADD(day,-1,GETUTCDATE()), 0);
END

-- NORTH bookings
IF @carNorth1 IS NOT NULL AND @userCust2 IS NOT NULL
BEGIN
    INSERT INTO Bookings (UserID, CarID, StartDate, EndDate, PickupLocationID, ReturnLocationID,
                          TotalPrice, TotalHours, BookingStatusID, InsuranceTier, CreatedAt, IsDeleted)
    VALUES
        (@userCust2, @carNorth1, DATEADD(day,-3,GETUTCDATE()), DATEADD(hour,10,DATEADD(day,-3,GETUTCDATE())),
         @northLocID, @northLocID, 1500000, 10, @stCompleted, 'Premium', DATEADD(day,-3,GETUTCDATE()), 0),
        (@userCust3, @carNorth1, GETUTCDATE(), DATEADD(hour,5,GETUTCDATE()),
         @northLocID, @northLocID, 750000, 5, @stConfirmed, 'Basic', GETUTCDATE(), 0),
        (@userCust4, @carNorth1, DATEADD(day,-7,GETUTCDATE()), DATEADD(day,-6,GETUTCDATE()),
         @northLocID, @northLocID, 2000000, 24, @stCompleted, 'Full Cover', DATEADD(day,-7,GETUTCDATE()), 0),
        (@userCust1, @carNorth1, DATEADD(day,-14,GETUTCDATE()), DATEADD(day,-13,GETUTCDATE()),
         @northLocID, @northLocID, 1000000, 8, @stCancelled, 'Basic', DATEADD(day,-14,GETUTCDATE()), 0);
END

-- CENTRAL bookings
IF @carCentral1 IS NOT NULL AND @userCust3 IS NOT NULL
BEGIN
    INSERT INTO Bookings (UserID, CarID, StartDate, EndDate, PickupLocationID, ReturnLocationID,
                          TotalPrice, TotalHours, BookingStatusID, InsuranceTier, CreatedAt, IsDeleted)
    VALUES
        (@userCust3, @carCentral1, DATEADD(day,-1,GETUTCDATE()), DATEADD(hour,8,DATEADD(day,-1,GETUTCDATE())),
         @centralLocID, @centralLocID, 1000000, 8, @stCompleted, 'Basic', DATEADD(day,-1,GETUTCDATE()), 0),
        (@userCust4, @carCentral1, GETUTCDATE(), DATEADD(hour,4,GETUTCDATE()),
         @centralLocID, @centralLocID, 500000, 4, @stConfirmed, 'Basic', GETUTCDATE(), 0);
END


-- ╔══════════════════════════════════════╗
-- 10. VERIFY
-- ╚══════════════════════════════════════╝
PRINT '=== KET QUA ===';
SELECT 'Areas' AS [Bang], COUNT(*) AS [SoDong] FROM Areas WHERE IsDeleted = 0
UNION ALL SELECT 'AreaStaffs', COUNT(*) FROM AreaStaffs WHERE IsDeleted = 0
UNION ALL SELECT 'AreaLocations', COUNT(*) FROM AreaLocations WHERE IsDeleted = 0
UNION ALL SELECT 'Bookings', COUNT(*) FROM Bookings WHERE IsDeleted = 0;

PRINT '';
PRINT '=== NHAN VIEN THEO KHU VUC ===';
SELECT a.Code AS [KhuVuc], a.Name AS [TenKhuVuc],
       u.Name AS [NhanVien], u.Email,
       ast.RoleInArea AS [VaiTro]
FROM AreaStaffs ast
JOIN Areas a ON ast.AreaID = a.AreaID
JOIN Users u ON ast.UserID = u.UserID
WHERE ast.IsDeleted = 0
ORDER BY a.Code, CASE ast.RoleInArea WHEN 'manager' THEN 1 ELSE 2 END;

PRINT '';
PRINT '=== DIA DIEM THEO KHU VUC ===';
SELECT a.Code, a.Name, l.Name AS [Location], l.Address
FROM Areas a
JOIN AreaLocations al ON a.AreaID = al.AreaID AND al.IsDeleted = 0
JOIN Locations l ON al.LocationID = l.LocationID
WHERE a.IsDeleted = 0
ORDER BY a.Code;
