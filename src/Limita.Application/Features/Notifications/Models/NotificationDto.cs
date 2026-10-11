namespace Limita.Application.Features.Notifications.Models;

public record NotificationDto(
    Guid Id,
    string UserId,
    string Title,
    string Message,
    string Type,
    bool IsRead,
    DateTime? ReadAt,
    string? ReferenceId,
    DateTime CreatedAt
);
