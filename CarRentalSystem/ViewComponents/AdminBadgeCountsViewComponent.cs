using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using System.Linq;
using System.Threading.Tasks;

namespace CarRentalSystem.ViewComponents
{
    public class AdminBadgeCountsViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;

        public AdminBadgeCountsViewComponent(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var pendingDriverCount = await _context.Drivers
                .CountAsync(d => !d.IsApproved);

            var pendingContractCount = await _context.DriverContracts
                .CountAsync(dc => dc.Status == "Pending");

            var expiringContractCount = await _context.DriverContracts
                .CountAsync(dc => dc.Status == "Active" && dc.ExpiresAt <= System.DateTime.UtcNow.AddDays(30));

            var partnerPendingCount = await _context.PartnerContracts
                .CountAsync(pc => pc.Status == "Pending");

            var pendingCarApprovalCount = await _context.CarChangeRequests
                .CountAsync(request =>
                    request.Status == CarChangeRequestStatusConstants.PendingStaff ||
                    request.Status == CarChangeRequestStatusConstants.PendingManager ||
                    request.Status == CarChangeRequestStatusConstants.PendingAdmin);

            return View(new AdminBadgeCounts
            {
                PendingDriverCount = pendingDriverCount,
                PendingContractCount = pendingContractCount + partnerPendingCount,
                ExpiringContractCount = expiringContractCount,
                PendingCarApprovalCount = pendingCarApprovalCount
            });
        }
    }

    public class AdminBadgeCounts
    {
        public int PendingDriverCount { get; set; }
        public int PendingContractCount { get; set; }
        public int ExpiringContractCount { get; set; }
        public int PendingCarApprovalCount { get; set; }

        public int DriverBadgeCount => PendingDriverCount;
        public int ContractBadgeCount => PendingContractCount + ExpiringContractCount;
        public int CarApprovalBadgeCount => PendingCarApprovalCount;
    }
}
