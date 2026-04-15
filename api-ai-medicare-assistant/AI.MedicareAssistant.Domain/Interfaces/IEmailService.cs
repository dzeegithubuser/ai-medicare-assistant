namespace Domain.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string toEmail, string userName, string verificationLink);
    Task SendPasswordResetAsync(string toEmail, string userName, string resetLink);
    Task SendReportEmailAsync(string toEmail, string subject, string body, byte[] reportData, string fileName);
}
