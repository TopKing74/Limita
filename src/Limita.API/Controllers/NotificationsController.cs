using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Limita.Application.Common;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Notifications.Models;
using Limita.Application.Features.Notifications.Services;

namespace Limita.API.Controllers;

[Authorize]
public class NotificationsController : BaseApiController
{
    private readonly INotificationService _notificationService;
    private readonly ICurrentUserService _currentUserService;

    public NotificationsController(
        INotificationService notificationService,
        ICurrentUserService currentUserService)
    {
        _notificationService = notificationService;
        _currentUserService = currentUserService;
    }

    /// <summary>
    /// Retrieve all notifications for the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<NotificationDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<NotificationDto>>>> GetNotifications(CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var notifications = await _notificationService.GetUserNotificationsAsync(userId, cancellationToken);
        return Ok(ApiResponse<IReadOnlyList<NotificationDto>>.Ok(notifications, "Notifications retrieved successfully."));
    }

    /// <summary>
    /// Retrieve unread notification count for the authenticated user.
    /// </summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<UnreadNotificationCountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<UnreadNotificationCountDto>>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var count = await _notificationService.GetUnreadCountAsync(userId, cancellationToken);
        return Ok(ApiResponse<UnreadNotificationCountDto>.Ok(new UnreadNotificationCountDto(count), "Unread notification count retrieved successfully."));
    }

    /// <summary>
    /// Mark a specific notification as read.
    /// </summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse>> MarkAsRead([FromRoute] Guid id, CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        var success = await _notificationService.MarkAsReadAsync(id, userId, cancellationToken);
        if (!success)
        {
            return NotFound(ApiResponse.Fail("Notification not found or does not belong to you."));
        }

        return Ok(ApiResponse.Ok("Notification marked as read successfully."));
    }

    /// <summary>
    /// Mark all notifications as read for the authenticated user.
    /// </summary>
    [HttpPatch("read-all")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse>> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = GetRequiredUserId();
        if (userId is null)
        {
            return Unauthorized(ApiResponse.Fail("User is not authenticated."));
        }

        await _notificationService.MarkAllAsReadAsync(userId, cancellationToken);
        return Ok(ApiResponse.Ok("All notifications marked as read successfully."));
    }

    private string? GetRequiredUserId() =>
        string.IsNullOrWhiteSpace(_currentUserService.UserId) ? null : _currentUserService.UserId;
}
