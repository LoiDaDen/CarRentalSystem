using CarRentalSystem.Authorization;
// CarRentalSystem/Areas/Admin/Controllers/AreaApiController.cs
using CarRentalSystem.Data;
using CarRentalSystem.ViewModels.Areas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.Areas.Admin.Controllers
{
    /// <summary>
    /// API endpoints hỗ trợ Select2 AJAX dropdowns trong Area management.
    /// </summary>
    [Route("admin/api")]
    [ApiController]
    [Authorize(Policy = PortalPolicyNames.RequireAdmin)]
    public class AreaApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public AreaApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ═══════════════════════════════════════════════
        // GET /admin/api/users
        // Query params: search, roles (comma-separated), excludeArea
        // ═══════════════════════════════════════════════
        [HttpGet("users")]
        public async Task<IActionResult> GetUsers(
            [FromQuery] string? search,
            [FromQuery] string? roles,
            [FromQuery] int? excludeArea)
        {
            var allowedRoles = new[] { "Manager", "Staff", "Viewer" };

            var query = _context.Users
                .Where(u => allowedRoles.Contains(u.Role))
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(u =>
                    u.Name.ToLower().Contains(term) ||
                    u.Email.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(roles))
            {
                var roleList = roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => allowedRoles.Contains(r))
                    .ToList();

                if (roleList.Any())
                    query = query.Where(u => roleList.Contains(u.Role));
            }

            // Loại trừ users đã thuộc area (nếu excludeArea > 0)
            if (excludeArea.HasValue && excludeArea.Value > 0)
            {
                var excludedUserIds = await _context.AreaStaffs
                    .Where(asx => asx.AreaID == excludeArea.Value && !asx.IsDeleted)
                    .Select(asx => asx.UserID)
                    .ToListAsync();

                query = query.Where(u => !excludedUserIds.Contains(u.UserID));
            }

            var users = await query
                .OrderBy(u => u.Name)
                .Take(50)
                .Select(u => new UserSelect2Result
                {
                    Id = u.UserID,
                    Name = u.Name,
                    Email = u.Email,
                    Role = u.Role
                })
                .ToListAsync();

            return Ok(new { results = users, pagination = new { more = false } });
        }

        // ═══════════════════════════════════════════════
        // GET /admin/api/locations
        // Query params: search, excludeArea
        // ═══════════════════════════════════════════════
        [HttpGet("locations")]
        public async Task<IActionResult> GetLocations(
            [FromQuery] string? search,
            [FromQuery] int? excludeArea)
        {
            var query = _context.Locations
                .Include(l => l.Cars)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLowerInvariant();
                query = query.Where(l =>
                    l.Name.ToLower().Contains(term) ||
                    (l.Address != null && l.Address.ToLower().Contains(term)));
            }

            // Loại trừ locations đã thuộc area
            if (excludeArea.HasValue && excludeArea.Value > 0)
            {
                var excludedLocationIds = await _context.AreaLocations
                    .Where(al => al.AreaID == excludeArea.Value && !al.IsDeleted)
                    .Select(al => al.LocationID)
                    .ToListAsync();

                query = query.Where(l => !excludedLocationIds.Contains(l.LocationID));
            }

            var locations = await query
                .OrderBy(l => l.Name)
                .Take(50)
                .Select(l => new LocationSelect2Result
                {
                    Id = l.LocationID,
                    Name = l.Name,
                    Address = l.Address ?? "",
                    CarCount = l.Cars != null ? l.Cars.Count(c => !c.IsDeleted) : 0
                })
                .ToListAsync();

            return Ok(new { results = locations, pagination = new { more = false } });
        }
    }
}
