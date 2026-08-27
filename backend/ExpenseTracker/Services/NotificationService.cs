using ExpenseTracker.Data;
using ExpenseTracker.DTOs;
using ExpenseTracker.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Services
{
    public class NotificationService
    {
        private readonly ExpenseTrackerDbContext _context;

        public NotificationService(ExpenseTrackerDbContext context)
        {
            _context = context;
        }

        public async Task<List<NotificationResponseDto>> GetNotificationsAsync(string userId, bool unreadOnly = false)
        {
            var query = _context.Notifications
                .Where(n => n.UserId == userId)
                .AsNoTracking();

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            return await query
                .OrderByDescending(n => n.CreatedAt)
                .Select(n => new NotificationResponseDto
                {
                    NotificationId = n.NotificationId,
                    UserId = n.UserId,
                    Type = n.Type,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAt = n.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<bool> MarkAsReadAsync(string userId, int notificationId)
        {
            var notif = await _context.Notifications.FirstOrDefaultAsync(n => n.NotificationId == notificationId && n.UserId == userId);
            if (notif == null) return false;

            notif.IsRead = true;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task MarkAllAsReadAsync(string userId)
        {
            var unread = await _context.Notifications.Where(n => n.UserId == userId && !n.IsRead).ToListAsync();
            foreach (var n in unread)
            {
                n.IsRead = true;
            }
            await _context.SaveChangesAsync();
        }

        public async Task<bool> CreateNotificationIfNotExistsAsync(string userId, string type, string title, string message, string referenceKey)
        {
            var exists = await _context.Notifications.AnyAsync(n => n.UserId == userId && n.ReferenceKey == referenceKey);
            if (exists) return false;

            var notification = new Notification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                ReferenceKey = referenceKey,
                IsRead = false,
                CreatedAt = DateTime.UtcNow
            };

            await _context.Notifications.AddAsync(notification);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
