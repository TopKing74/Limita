using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Notifications.Models;
using Limita.Application.Features.Notifications.Services;
using Limita.Domain.Entities;
using Limita.Infrastructure.Hubs;

namespace Limita.Infrastructure.Notifications;

public class NotificationService : INotificationService
{
    private readonly IApplicationDbContext _context;
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        IApplicationDbContext context,
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationService>? logger = null)
    {
        _context = context;
        _hubContext = hubContext;
        _logger = logger ?? NullLogger<NotificationService>.Instance;
    }

    public async Task<NotificationDto> CreateAndSendNotificationAsync(
        string userId,
        string title,
        string message,
        string type,
        string? referenceId = null,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Type = string.IsNullOrWhiteSpace(type) ? "Info" : type,
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _context.Notifications.Add(notification);
        await _context.SaveChangesAsync(cancellationToken);

        var notificationDto = new NotificationDto(
            notification.Id,
            notification.UserId,
            notification.Title,
            notification.Message,
            notification.Type,
            notification.IsRead,
            notification.ReadAt,
            notification.ReferenceId,
            notification.CreatedAt
        );

        try
        {
            await _hubContext.Clients.User(userId).SendAsync("ReceiveNotification", notificationDto, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to push real-time notification to user {UserId} via SignalR", userId);
        }

        return notificationDto;
    }

    public async Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Array.Empty<NotificationDto>();
        }

        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(n => new NotificationDto(
                n.Id,
                n.UserId,
                n.Title,
                n.Message,
                n.Type,
                n.IsRead,
                n.ReadAt,
                n.ReferenceId,
                n.CreatedAt
            ))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return 0;
        }

        return await _context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsRead)
            .CountAsync(cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(
        Guid notificationId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var notification = await _context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);

        if (notification == null || notification.UserId != userId)
        {
            return false;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            notification.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<bool> MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        var unreadNotifications = await _context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        if (unreadNotifications.Count == 0)
        {
            return true;
        }

        var now = DateTime.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadAt = now;
            notification.UpdatedAt = now;
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
