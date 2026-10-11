using Limita.Application.Features.Notifications.Models;

namespace Limita.Application.Features.Notifications.Services;

public interface INotificationService
{
    Task<NotificationDto> CreateAndSendNotificationAsync(
        string userId,
        string title,
        string message,
        string type,
        string? referenceId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<NotificationDto>> GetUserNotificationsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAsReadAsync(
        Guid notificationId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
