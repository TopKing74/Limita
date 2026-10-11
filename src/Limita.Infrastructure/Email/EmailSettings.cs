namespace Limita.Infrastructure.Email;

public class EmailSettings
{
    public const string SectionName = "EmailSettings";

    public string SmtpServer { get; set; } = "smtp.gmail.com";
    public int Port { get; set; } = 587;
    public string SenderName { get; set; } = "Limita Bank";
    public string SenderEmail { get; set; } = "no-reply@limitabank.com";
    public string Password { get; set; } = string.Empty;
}
