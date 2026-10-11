using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using Limita.Application.Common.Interfaces;

namespace Limita.Infrastructure.Email;

public class EmailService : IEmailService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<EmailService> _logger;

    public EmailService(
        IConfiguration configuration,
        ILogger<EmailService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string htmlBody,
        CancellationToken cancellationToken = default)
    {
        var section = _configuration.GetSection(EmailSettings.SectionName);
        var smtpServer = section["SmtpServer"] ?? "smtp.gmail.com";
        var port = int.TryParse(section["Port"], out var parsedPort) && parsedPort > 0 ? parsedPort : 587;
        var senderName = section["SenderName"] ?? "Limita Bank";
        var senderEmail = section["SenderEmail"] ?? "no-reply@limitabank.com";
        var password = section["Password"] ?? string.Empty;

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(senderName, senderEmail));
        message.To.Add(MailboxAddress.Parse(toEmail));
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder
        {
            HtmlBody = htmlBody
        };
        message.Body = bodyBuilder.ToMessageBody();

        try
        {
            using var client = new SmtpClient();

            // Connect securely using SecureSocketOptions.StartTls
            await client.ConnectAsync(smtpServer, port, SecureSocketOptions.StartTls, cancellationToken);

            // Authenticate if password/credentials are configured
            if (!string.IsNullOrWhiteSpace(password))
            {
                await client.AuthenticateAsync(senderEmail, password, cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);

            _logger.LogInformation("Email successfully dispatched to {ToEmail} with subject '{Subject}'", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send email to {ToEmail} with subject '{Subject}' via SMTP server {SmtpServer}:{Port}. Reason: {Message}",
                toEmail,
                subject,
                smtpServer,
                port,
                ex.Message);

            throw;
        }
    }
}
