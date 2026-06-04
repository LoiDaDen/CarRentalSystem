using System.Security.Claims;
using CarRentalSystem.Data;
using CarRentalSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CarRentalSystem.Hubs
{
    [Authorize]
    public class TrackingHub : Hub
    {
        private readonly ApplicationDbContext _context;

        public TrackingHub(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task JoinBooking(int bookingId)
        {
            if (!await CanAccessBookingAsync(bookingId))
                throw new HubException("Forbidden");

            await Groups.AddToGroupAsync(Context.ConnectionId, BookingGroup(bookingId));
        }

        public Task LeaveBooking(int bookingId)
        {
            return Groups.RemoveFromGroupAsync(Context.ConnectionId, BookingGroup(bookingId));
        }

        public static string BookingGroup(int bookingId) => $"booking-{bookingId}";

        private async Task<bool> CanAccessBookingAsync(int bookingId)
        {
            var userIdText = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdText, out var userId))
                return false;

            if (Context.User?.IsInRole(UserRoleConstants.Admin) == true ||
                Context.User?.IsInRole(UserRoleConstants.Manager) == true ||
                Context.User?.IsInRole(UserRoleConstants.Staff) == true)
            {
                return true;
            }

            return await _context.Bookings
                .Include(b => b.Car)
                .AnyAsync(b => b.BookingID == bookingId &&
                    (b.UserID == userId || (b.Car != null && b.Car.OwnerID == userId)));
        }
    }
}
