namespace Limita.Domain.Entities;

public class PasswordResetOtp
{
    public string Email { get; set; } = string.Empty;
    public string OtpCode { get; set; } = string.Empty;
    public DateTime ExpiryTime { get; set; }
    public int AttemptsCount { get; set; }
    public bool IsUsed { get; set; }
}
