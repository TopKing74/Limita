using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Limita.Infrastructure.Email;
using Xunit;

namespace Limita.Application.Tests.Features.Auth;

public class EmailServiceTests
{
    private readonly Mock<ILogger<EmailService>> _loggerMock;

    public EmailServiceTests()
    {
        _loggerMock = new Mock<ILogger<EmailService>>();
    }

    [Fact]
    public async Task SendEmailAsync_ShouldThrowAndLogError_WhenSmtpServerCannotBeReached()
    {
        // Arrange
        var inMemorySettings = new Dictionary<string, string?>
        {
            { "EmailSettings:SmtpServer", "127.0.0.1" },
            { "EmailSettings:Port", "2525" }, // Dummy local port with no listener
            { "EmailSettings:SenderName", "Limita Test Bank" },
            { "EmailSettings:SenderEmail", "test@limitabank.com" },
            { "EmailSettings:Password", "secret" }
        };

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        var emailService = new EmailService(configuration, _loggerMock.Object);

        // Act & Assert
        var act = async () => await emailService.SendEmailAsync(
            "recipient@example.com",
            "Limita Bank — Password Reset Request",
            "<p>Test reset token</p>",
            CancellationToken.None);

        await act.Should().ThrowAsync<Exception>();

        // Verify that error was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
