-- ============================================================
-- SCRIPT: Setup Area Staff & Manager Mapping
-- Run trong SQL Server Management Studio (SSMS)
-- Database: CarRentalSystem
-- ============================================================
-- Mat khau: Manager@123 / Staff@123
-- ============================================================

USE CarRentalSystem;
SET NOCOUNT ON;

PRINT '========================================================';
PRINT ' SETUP: Area Staff & Manager Mapping';
PRINT '========================================================';
PRINT '';

-- ═══════════════════════════════════════════════
-- 1. TAO TAI KHOAN (neu chua co)
-- ═══════════════════════════════════════════════
DECLARE @mgrHash NVARCHAR(100) = N'$2a$11$sgtQ01zeg6vXEYT4UVSoDuKlrGi4QI8uVQK3aeOmOrfeV.Lvaj.cC';
DECLARE @staffHash NVARCHAR(100) = N'$2a$11$hkGU8oaBNNxFu943h2M30uF6sH7JxhC/qDQRI.ldQ/RnN6Otx5TSG';

PRINT '--- 1. Creating Manager & Staff accounts ---';

MERGE INTO Users AS target
USING (VALUES
    (N'Nguyen Van Minh',       'mgr.north@carrental.com',           '0901110001', @mgrHash,  'Manager', 1, GETUTCDATE()),
    (N'Tran Thi Lan',          'mgr.central@carrental.com',        '0901110002', @mgrHash,  'Manager', 1, GETUTCDATE()),
    (N'Pham Dinh Khoa',        'mgr.south@carrental.com',          '0901110003', @mgrHash,  'Manager', 1, GETUTCDATE()),
    (N'Le Hoang Nam',          'staff.north@carrental.com',        '0902220001', @staffHash,'Staff',   1, GETUTCDATE()),
    (N'Pham Thu Ha',          'staff.central@carrental.com',      '0902220002', @staffHash,'Staff',   1, GETUTCDATE()),
    (N'Huynh Cong Phu',        'staff.south@carrental.com',        '0902220003', @staffHash,'Staff',   1, GETUTCDATE()),
    (N'Dang Minh Tuan',        'mgr.southeast@carrental.com',      '0901110004', @mgrHash,  'Manager', 1, GETUTCDATE()),
    (N'Vo Thi Mai',            'staff.southeast@carrental.com',    '0902220004', @staffHash,'Staff',   1, GETUTCDATE()),
    (N'Bui Dinh Khoi',        'mgr.centralhighlands@carrental.com','0901110005', @mgrHash,  'Manager', 1, GETUTCDATE()),
    (N'Ngo Thi Lan',           'staff.centralhighlands@carrental.com','0902220005',@staffHash,'Staff',  1, GETUTCDATE())
) AS source (Name, Email, Phone, PasswordHash, Role, IsEmailVerified, CreatedAt)
ON target.Email = source.Email
WHEN NOT MATCHED THEN
    INSERT (Name, Email, Phone, PasswordHash, Role, IsEmailVerified, CreatedAt)
    VALUES (source.Name, source.Email, source.Phone, source.PasswordHash,
            source.Role, source.IsEmailVerified, source.CreatedAt)
WHEN MATCHED AND target.Role != source.Role THEN
    UPDATE SET target.Role = source.Role, target.Name = source.Name;

PRINT '  + Manager & Staff accounts ready.';


-- ═══════════════════════════════════════════════
-- 2. TAO 5 KHU VUC (neu chua co)
-- ═══════════════════════════════════════════════
PRINT '';
PRINT '--- 2. Creating Areas ---';

MERGE INTO Areas AS target
USING (VALUES
    (1, N'Khu vuc Mien Bac',       'NORTH',     N'Ha Noi',            N'12 P. Tran Duy Hung, Trung Hoa, Cau Giay',  21.0285,  105.7823, 'active'),
    (2, N'Khu vuc Mien Trung',     'CENTRAL',   N'Da Nang',          N'78 P. Nguyen Van Linh, Nam Hai, Hai Chau',  16.0544,  108.2022, 'active'),
    (3, N'Khu vuc Mien Nam',       'SOUTH',     N'TP. Ho Chi Minh',  N'199 P. Nguyen Dinh Chieu, Q.3',            10.7801,  106.6882, 'active'),
    (4, N'Khu vuc Dong Nam Bo',    'SOUTHEAST', N'Binh Duong',       N'KCN Song Than 3, D. An',                   10.9535,  106.8235, 'active'),
    (5, N'Khu vuc Tay Nguyen',     'CENTRAL_H', N'Dak Lak',          N'To 8, P. Tan An, TP. Buon Ma Thuot',        12.6791,  108.0375, 'inactive')
) AS source (AreaID, Name, Code, Province, Address, Lat, Lng, Status)
ON target.Code = source.Code
WHEN NOT MATCHED THEN
    INSERT (Name, Code, Province, Address, Latitude, Longitude, Status, CreatedAt, UpdatedAt, IsDeleted)
    VALUES (source.Name, source.Code, source.Province, source.Address,
            source.Lat, source.Lng, source.Status, GETUTCDATE(), GETUTCDATE(), 0)
WHEN MATCHED AND target.IsDeleted = 1 THEN
    UPDATE SET target.IsDeleted = 0, target.Status = source.Status,
               target.Latitude = source.Lat, target.Longitude = source.Lng;

PRINT '  + 5 Areas ready.';


-- ═══════════════════════════════════════════════
-- 3. LAY USER IDs
-- ═══════════════════════════════════════════════
DECLARE @mgr1 INT;  DECLARE @mgr2 INT;  DECLARE @mgr3 INT;
DECLARE @mgr4 INT;  DECLARE @mgr5 INT;
DECLARE @staff1 INT; DECLARE @staff2 INT; DECLARE @staff3 INT;
DECLARE @staff4 INT; DECLARE @staff5 INT;

SELECT @mgr1   = UserID FROM Users WHERE Email = 'mgr.north@carrental.com';
SELECT @mgr2   = UserID FROM Users WHERE Email = 'mgr.central@carrental.com';
SELECT @mgr3   = UserID FROM Users WHERE Email = 'mgr.south@carrental.com';
SELECT @mgr4   = UserID FROM Users WHERE Email = 'mgr.southeast@carrental.com';
SELECT @mgr5   = UserID FROM Users WHERE Email = 'mgr.centralhighlands@carrental.com';
SELECT @staff1 = UserID FROM Users WHERE Email = 'staff.north@carrental.com';
SELECT @staff2 = UserID FROM Users WHERE Email = 'staff.central@carrental.com';
SELECT @staff3 = UserID FROM Users WHERE Email = 'staff.south@carrental.com';
SELECT @staff4 = UserID FROM Users WHERE Email = 'staff.southeast@carrental.com';
SELECT @staff5 = UserID FROM Users WHERE Email = 'staff.centralhighlands@carrental.com';

PRINT '';
PRINT '--- 3. User IDs loaded ---';


-- ═══════════════════════════════════════════════
-- 4. LAY LOCATION IDs (neu can map AreaLocation)
-- ═══════════════════════════════════════════════
DECLARE @loc1 INT; DECLARE @loc2 INT; DECLARE @loc3 INT; DECLARE @loc4 INT;
DECLARE @loc5 INT; DECLARE @loc6 INT; DECLARE @loc7 INT; DECLARE @loc8 INT;

SELECT @loc1 = LocationID FROM Locations ORDER BY LocationID OFFSET 0 ROWS FETCH NEXT 1 ROWS ONLY;
SELECT @loc2 = LocationID FROM Locations ORDER BY LocationID OFFSET 1 ROWS FETCH NEXT 1 ROWS ONLY;
SELECT @loc3 = LocationID FROM Locations ORDER BY LocationID OFFSET 2 ROWS FETCH NEXT 1 ROWS ONLY;
SELECT @loc4 = LocationID FROM Locations ORDER BY LocationID OFFSET 3 ROWS FETCH NEXT 1 ROWS ONLY;
SELECT @loc5 = LocationID FROM Locations ORDER BY LocationID OFFSET 4 ROWS FETCH NEXT 1 ROWS ONLY;
SELECT @loc6 = LocationID FROM Locations ORDER BY LocationID OFFSET 5 ROWS FETCH NEXT 1 ROWS ONLY;
SELECT @loc7 = LocationID FROM Locations ORDER BY LocationID OFFSET 6 ROWS FETCH NEXT 1 ROWS ONLY;
SELECT @loc8 = LocationID FROM Locations ORDER BY LocationID OFFSET 7 ROWS FETCH NEXT 1 ROWS ONLY;

PRINT '--- 4. Location IDs loaded ---';


-- ═══════════════════════════════════════════════
-- 5. MAP AREA -> STAFF (xoa mapping cu, tao moi)
-- ═══════════════════════════════════════════════
PRINT '';
PRINT '--- 5. Mapping Area -> Staff ---';

-- Xoa mapping cu cua nhung user nay
DELETE FROM AreaStaffs
WHERE UserID IN (@mgr1, @mgr2, @mgr3, @mgr4, @mgr5, @staff1, @staff2, @staff3, @staff4, @staff5)
  AND AreaID IN (1, 2, 3, 4, 5);

-- Mien Bac (AreaID=1): mgr1 = Manager, staff1 = Staff
IF @mgr1 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (1, @mgr1, 'manager', GETUTCDATE(), NULL, 0);
IF @staff1 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (1, @staff1, 'staff', GETUTCDATE(), @mgr1, 0);

-- Mien Trung (AreaID=2): mgr2 = Manager, staff2 = Staff
IF @mgr2 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (2, @mgr2, 'manager', GETUTCDATE(), NULL, 0);
IF @staff2 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (2, @staff2, 'staff', GETUTCDATE(), @mgr2, 0);

-- Mien Nam (AreaID=3): mgr3 = Manager, staff2 = Staff
IF @mgr3 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (3, @mgr3, 'manager', GETUTCDATE(), NULL, 0);
IF @staff2 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (3, @staff2, 'staff', GETUTCDATE(), @mgr3, 0);

-- Dong Nam Bo (AreaID=4): mgr4 = Manager, staff4 = Staff
IF @mgr4 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (4, @mgr4, 'manager', GETUTCDATE(), NULL, 0);
IF @staff4 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (4, @staff4, 'staff', GETUTCDATE(), @mgr4, 0);

-- Tay Nguyen (AreaID=5): mgr5 = Manager, staff5 = Staff
IF @mgr5 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (5, @mgr5, 'manager', GETUTCDATE(), NULL, 0);
IF @staff5 IS NOT NULL
    INSERT INTO AreaStaffs (AreaID, UserID, RoleInArea, AssignedAt, AssignedBy, IsDeleted)
    VALUES (5, @staff5, 'staff', GETUTCDATE(), @mgr5, 0);


-- ═══════════════════════════════════════════════
-- 6. UPDATE MANAGERID CUA AREAS
-- ═══════════════════════════════════════════════
PRINT '';
PRINT '--- 6. Updating Area.ManagerID ---';

UPDATE A
SET A.ManagerID = (SELECT TOP 1 UserID FROM AreaStaffs
                   WHERE AreaID = A.AreaID AND RoleInArea = 'manager' AND IsDeleted = 0)
FROM Areas A
WHERE EXISTS (SELECT 1 FROM AreaStaffs WHERE AreaID = A.AreaID AND RoleInArea = 'manager' AND IsDeleted = 0);


-- ═══════════════════════════════════════════════
-- 7. MAP AREA -> LOCATION (neu can)
-- ═══════════════════════════════════════════════
PRINT '';
PRINT '--- 7. Mapping Area -> Location ---';

-- Xoa mapping cu
DELETE FROM AreaLocations WHERE AreaID IN (1,2,3,4);

-- Mien Bac: loc1 + loc2
IF @loc1 IS NOT NULL
    INSERT INTO AreaLocations (AreaID, LocationID, IsDeleted) VALUES (1, @loc1, 0);
IF @loc2 IS NOT NULL
    INSERT INTO AreaLocations (AreaID, LocationID, IsDeleted) VALUES (1, @loc2, 0);

-- Mien Trung: loc3 + loc4
IF @loc3 IS NOT NULL
    INSERT INTO AreaLocations (AreaID, LocationID, IsDeleted) VALUES (2, @loc3, 0);
IF @loc4 IS NOT NULL
    INSERT INTO AreaLocations (AreaID, LocationID, IsDeleted) VALUES (2, @loc4, 0);

-- Mien Nam: loc5 + loc6 + loc7
IF @loc5 IS NOT NULL
    INSERT INTO AreaLocations (AreaID, LocationID, IsDeleted) VALUES (3, @loc5, 0);
IF @loc6 IS NOT NULL
    INSERT INTO AreaLocations (AreaID, LocationID, IsDeleted) VALUES (3, @loc6, 0);
IF @loc7 IS NOT NULL
    INSERT INTO AreaLocations (AreaID, LocationID, IsDeleted) VALUES (3, @loc7, 0);

-- Dong Nam Bo: loc8
IF @loc8 IS NOT NULL
    INSERT INTO AreaLocations (AreaID, LocationID, IsDeleted) VALUES (4, @loc8, 0);


-- ═══════════════════════════════════════════════
-- 8. KET QUA
-- ═══════════════════════════════════════════════
PRINT '';
PRINT '========================================================';
PRINT ' KET QUA SETUP';
PRINT '========================================================';

PRINT '';
PRINT '--- TAI KHOAN ---';
SELECT u.Name AS [HoTen], u.Email, u.Role AS [VaiTroHeThong],
       STUFF((
           SELECT ', ' + a.Code + ':' + ast2.RoleInArea
           FROM AreaStaffs ast2
           JOIN Areas a ON ast2.AreaID = a.AreaID
           WHERE ast2.UserID = u.UserID AND ast2.IsDeleted = 0
           FOR XML PATH(''), TYPE
       ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS [KhuVuc_VaiTro]
FROM Users u
WHERE u.Role IN ('Manager', 'Staff')
ORDER BY u.Role DESC, u.Name;

PRINT '';
PRINT '--- KHU VUC ---';
SELECT a.Code, a.Name, a.Province, a.Status,
       mgr.Name AS [Manager], mgr.Email AS [MgrEmail],
       (SELECT COUNT(*) FROM AreaStaffs WHERE AreaID = a.AreaID AND IsDeleted = 0) AS [SoNhanSu],
       (SELECT COUNT(*) FROM AreaLocations WHERE AreaID = a.AreaID AND IsDeleted = 0) AS [SoDiaDiem]
FROM Areas a
LEFT JOIN AreaStaffs am ON a.AreaID = am.AreaID AND am.RoleInArea = 'manager' AND am.IsDeleted = 0
LEFT JOIN Users mgr ON am.UserID = mgr.UserID
WHERE a.IsDeleted = 0
ORDER BY a.AreaID;

PRINT '';
PRINT '========================================================';
PRINT ' DONE! Tat ca mapping da duoc thiet lap.';
PRINT '========================================================';
PRINT '';
PRINT 'Dang nhap voi:';
PRINT '  Manager: mgr.north@carrental.com  / Manager@123';
PRINT '  Staff:    staff.north@carrental.com / Staff@123';
