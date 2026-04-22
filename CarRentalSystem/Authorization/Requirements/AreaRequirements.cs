// CarRentalSystem/Authorization/Requirements/AreaRequirements.cs
using Microsoft.AspNetCore.Authorization;

namespace CarRentalSystem.Authorization.Requirements;

/// <summary>
/// Requirement để xem danh sách tất cả Areas.
/// Cho phép: Manager, Staff, Viewer thuộc bất kỳ Area nào.
/// </summary>
public class AreaViewRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Requirement để tạo Area mới.
/// Chỉ Manager hệ thống mới được tạo Area.
/// </summary>
public class AreaCreateRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Requirement để cập nhật thông tin Area (tên, địa chỉ, trạng thái).
/// Cho phép: Manager hệ thống HOẶC User có RoleInArea='manager' của Area đó.
/// </summary>
public class AreaUpdateRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Requirement để xóa Area.
/// Chỉ Manager hệ thống mới được xóa, và Area không còn booking active.
/// </summary>
public class AreaDeleteRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Requirement để quản lý nhân sự Area (thêm/bớt staff).
/// Cho phép: Manager hệ thống HOẶC User có RoleInArea='manager' của Area đó.
/// </summary>
public class AreaManageStaffRequirement : IAuthorizationRequirement
{
}

/// <summary>
/// Requirement để xem thống kê Area (số xe, booking, doanh thu).
/// Cho phép: Manager hệ thống HOẶC User thuộc Area đó (qua AreaStaff).
/// </summary>
public class AreaViewStatsRequirement : IAuthorizationRequirement
{
}
