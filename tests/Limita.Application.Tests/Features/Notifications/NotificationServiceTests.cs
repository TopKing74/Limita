using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Moq;
using Moq.EntityFrameworkCore;
using Limita.Application.Common.Interfaces;
using Limita.Application.Features.Notifications.Services;
using Limita.Domain.Entities;
using Limita.Domain.Enums;
using Limita.Infrastructure.Hubs;
using Limita.Infrastructure.Notifications;
using Xunit;

namespace Limita.Application.Tests.Features.Notifications;

public class NotificationServiceTests
{
    private readonly Mock<IApplicationDbContext> _contextMock;
    private readonly Mock<IHubContext<NotificationHub>> _hubContextMock;
    private readonly Mock<IHubClients> _hubClientsMock;
    private readonly Mock<IClientProxy> _clientProxyMock;
    private readonly NotificationService _notificationService;

    public NotificationServiceTests()
    {
        _contextMock = new Mock<IApplicationDbContext>();
        _hubContextMock = new Mock<IHubContext<NotificationHub>>();
        _hubClientsMock = new Mock<IHubClients>();
        _clientProxyMock = new Mock<IClientProxy>();

        _hubContextMock.Setup(h => h.Clients).Returns(_hubClientsMock.Object);
        _hubClientsMock.Setup(c => c.User(It.IsAny<string>())).Returns(_clientProxyMock.Object);
        _clientProxyMock.Setup(p => p.SendCoreAsync(
            "ReceiveNotification",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _notificationService = new NotificationService(_contextMock.Object, _hubContextMock.Object);
    }

    [Fact]
    public async Task CreateAndSendNotificationAsync_ShouldPersistNotificationAndPushSignalR_WhenValidRequest()
    {
        // Arrange
        var userId = "user-1";
        var title = "Funds Received";
        var message = "You have received 500 EGP.";
        var type = NotificationType.Transaction.ToString();
        var referenceId = "TXN-123456";

        var dbNotifications = new List<Notification>();
        _contextMock.Setup(c => c.Notifications).ReturnsDbSet(dbNotifications);

        // Act
        var result = await _notificationService.CreateAndSendNotificationAsync(
            userId, title, message, type, referenceId);

        // Assert
        _contextMock.Verify(c => c.Notifications.Add(It.Is<Notification>(n =>
            n.UserId == userId &&
            n.Title == title &&
            n.Message == message &&
            n.Type == type &&
            n.ReferenceId == referenceId &&
            !n.IsRead)), Times.Once);

        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);

        _hubClientsMock.Verify(c => c.User(userId), Times.Once);
        _clientProxyMock.Verify(p => p.SendCoreAsync(
            "ReceiveNotification",
            It.Is<object?[]>(args => args.Length == 1),
            It.IsAny<CancellationToken>()), Times.Once);

        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.Title.Should().Be(title);
        result.Message.Should().Be(message);
        result.Type.Should().Be(type);
        result.ReferenceId.Should().Be(referenceId);
        result.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAndSendNotificationAsync_ShouldHandleSignalRExceptionGracefully_AndStillReturnDto()
    {
        // Arrange
        var userId = "user-1";
        var dbNotifications = new List<Notification>();
        _contextMock.Setup(c => c.Notifications).ReturnsDbSet(dbNotifications);

        _clientProxyMock.Setup(p => p.SendCoreAsync(
            "ReceiveNotification",
            It.IsAny<object?[]>(),
            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SignalR connection broken"));

        // Act
        var result = await _notificationService.CreateAndSendNotificationAsync(
            userId, "Alert", "Security alert", "Security");

        // Assert
        result.Should().NotBeNull();
        result.Title.Should().Be("Alert");
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetUserNotificationsAsync_ShouldReturnOrderedUserNotifications_ForGivenUser()
    {
        // Arrange
        var userId = "user-1";
        var otherUserId = "user-2";

        var n1 = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Older",
            Message = "Msg",
            Type = "Info",
            CreatedAt = DateTime.UtcNow.AddMinutes(-10)
        };
        var n2 = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = "Newer",
            Message = "Msg",
            Type = "Info",
            CreatedAt = DateTime.UtcNow
        };
        var nOther = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = otherUserId,
            Title = "Other User",
            Message = "Msg",
            Type = "Info",
            CreatedAt = DateTime.UtcNow
        };

        var dbNotifications = new List<Notification> { n1, n2, nOther };
        _contextMock.Setup(c => c.Notifications).ReturnsDbSet(dbNotifications);

        // Act
        var result = await _notificationService.GetUserNotificationsAsync(userId);

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Newer");
        result[1].Title.Should().Be("Older");
    }

    [Fact]
    public async Task GetUnreadCountAsync_ShouldReturnCountOfUnreadNotifications_ForGivenUser()
    {
        // Arrange
        var userId = "user-1";
        var dbNotifications = new List<Notification>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, IsRead = false },
            new() { Id = Guid.NewGuid(), UserId = userId, IsRead = false },
            new() { Id = Guid.NewGuid(), UserId = userId, IsRead = true },
            new() { Id = Guid.NewGuid(), UserId = "other-user", IsRead = false }
        };
        _contextMock.Setup(c => c.Notifications).ReturnsDbSet(dbNotifications);

        // Act
        var count = await _notificationService.GetUnreadCountAsync(userId);

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldMarkNotificationAsRead_WhenNotificationBelongsToUser()
    {
        // Arrange
        var userId = "user-1";
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            UserId = userId,
            IsRead = false,
            ReadAt = null
        };

        var dbNotifications = new List<Notification> { notification };
        _contextMock.Setup(c => c.Notifications).ReturnsDbSet(dbNotifications);

        // Act
        var result = await _notificationService.MarkAsReadAsync(notificationId, userId);

        // Assert
        result.Should().BeTrue();
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldReturnFalse_WhenNotificationBelongsToDifferentUser_EnforcingOwnership()
    {
        // Arrange
        var ownerUserId = "user-2";
        var unauthorizedUser = "user-1";
        var notificationId = Guid.NewGuid();
        var notification = new Notification
        {
            Id = notificationId,
            UserId = ownerUserId,
            IsRead = false
        };

        var dbNotifications = new List<Notification> { notification };
        _contextMock.Setup(c => c.Notifications).ReturnsDbSet(dbNotifications);

        // Act
        var result = await _notificationService.MarkAsReadAsync(notificationId, unauthorizedUser);

        // Assert
        result.Should().BeFalse();
        notification.IsRead.Should().BeFalse();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldReturnFalse_WhenNotificationDoesNotExist()
    {
        // Arrange
        var dbNotifications = new List<Notification>();
        _contextMock.Setup(c => c.Notifications).ReturnsDbSet(dbNotifications);

        // Act
        var result = await _notificationService.MarkAsReadAsync(Guid.NewGuid(), "user-1");

        // Assert
        result.Should().BeFalse();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ShouldMarkAllUnreadUserNotificationsAsRead()
    {
        // Arrange
        var userId = "user-1";
        var n1 = new Notification { Id = Guid.NewGuid(), UserId = userId, IsRead = false };
        var n2 = new Notification { Id = Guid.NewGuid(), UserId = userId, IsRead = false };
        var nAlreadyRead = new Notification { Id = Guid.NewGuid(), UserId = userId, IsRead = true };
        var nOtherUser = new Notification { Id = Guid.NewGuid(), UserId = "user-2", IsRead = false };

        var dbNotifications = new List<Notification> { n1, n2, nAlreadyRead, nOtherUser };
        _contextMock.Setup(c => c.Notifications).ReturnsDbSet(dbNotifications);

        // Act
        var result = await _notificationService.MarkAllAsReadAsync(userId);

        // Assert
        result.Should().BeTrue();
        n1.IsRead.Should().BeTrue();
        n1.ReadAt.Should().NotBeNull();
        n2.IsRead.Should().BeTrue();
        n2.ReadAt.Should().NotBeNull();
        nOtherUser.IsRead.Should().BeFalse();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ShouldReturnTrue_WhenNoUnreadNotificationsExist()
    {
        // Arrange
        var userId = "user-1";
        var nAlreadyRead = new Notification { Id = Guid.NewGuid(), UserId = userId, IsRead = true };

        var dbNotifications = new List<Notification> { nAlreadyRead };
        _contextMock.Setup(c => c.Notifications).ReturnsDbSet(dbNotifications);

        // Act
        var result = await _notificationService.MarkAllAsReadAsync(userId);

        // Assert
        result.Should().BeTrue();
        _contextMock.Verify(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
