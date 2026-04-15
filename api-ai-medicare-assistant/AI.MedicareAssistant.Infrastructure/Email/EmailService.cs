using System.Net;
using System.Net.Mail;
using Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.Email;

public class EmailService : IEmailService
{
    private readonly EmailSettings _settings;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailSettings> settings, ILogger<EmailService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public async Task SendEmailVerificationAsync(string toEmail, string userName, string verificationLink)
    {
        var subject = "Verify your AiVante account";
        var body = BuildVerificationEmailBody(userName, verificationLink);
        await SendAsync(toEmail, subject, body);
    }

    public async Task SendPasswordResetAsync(string toEmail, string userName, string resetLink)
    {
        var subject = "Reset your AiVante password";
        var body = BuildPasswordResetEmailBody(userName, resetLink);
        await SendAsync(toEmail, subject, body);
    }

    public async Task SendReportEmailAsync(string toEmail, string subject, string body, byte[] reportData, string fileName)
    {
        var fromAddress = new MailAddress(_settings.FromAddress, _settings.FromDisplayName);
        var toAddress = new MailAddress(toEmail);
        using var smtp = CreateSmtpClient();
        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        using var stream = new MemoryStream(reportData);
        var attachment = new Attachment(stream, fileName, "application/pdf");
        message.Attachments.Add(attachment);
        _logger.LogInformation("Sending report email to {ToEmail} with attachment {FileName}", toEmail, fileName);
        await smtp.SendMailAsync(message);
        _logger.LogInformation("Report email sent to {ToEmail}", toEmail);
    }

    private async Task SendAsync(string toEmail, string subject, string body)
    {
        var fromAddress = new MailAddress(_settings.FromAddress, _settings.FromDisplayName);
        var toAddress = new MailAddress(toEmail);
        using var smtp = CreateSmtpClient();
        using var message = new MailMessage(fromAddress, toAddress)
        {
            Subject = subject,
            Body = body,
            IsBodyHtml = true
        };
        _logger.LogInformation("Sending email to {ToEmail} subject={Subject}", toEmail, subject);
        await smtp.SendMailAsync(message);
        _logger.LogInformation("Email sent to {ToEmail}", toEmail);
    }

    private SmtpClient CreateSmtpClient() => new()
    {
        Host = _settings.Host,
        Port = _settings.Port,
        EnableSsl = _settings.EnableSsl,
        DeliveryMethod = SmtpDeliveryMethod.Network,
        UseDefaultCredentials = false,
        Credentials = new NetworkCredential(_settings.FromAddress, _settings.Password),
        Timeout = _settings.TimeoutMs
    };

    private static string BuildVerificationEmailBody(string userName, string verificationLink) =>
        $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8" /></head>
        <body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:30px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#1565c0;padding:24px 32px;">
                  <h1 style="color:#ffffff;margin:0;font-size:22px;">AiVante Medicare Assistant</h1>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="color:#1565c0;margin-top:0;">Verify your email address</h2>
                  <p style="color:#444;line-height:1.6;">Hi {userName},</p>
                  <p style="color:#444;line-height:1.6;">Thanks for signing up! Please verify your email address to activate your account.</p>
                  <p style="text-align:center;margin:32px 0;">
                    <a href="{verificationLink}"
                       style="background:#1565c0;color:#ffffff;text-decoration:none;padding:14px 32px;border-radius:4px;font-size:16px;font-weight:bold;display:inline-block;">
                      Verify Email
                    </a>
                  </p>
                  <p style="color:#888;font-size:13px;">This link expires in 24 hours. If you did not create an account, you can safely ignore this email.</p>
                </td></tr>
                <tr><td style="background:#f4f4f4;padding:16px 32px;text-align:center;">
                  <p style="color:#aaa;font-size:12px;margin:0;">&copy; {DateTime.UtcNow.Year} AiVante. All rights reserved.</p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;

    private static string BuildPasswordResetEmailBody(string userName, string resetLink) =>
        $"""
        <!DOCTYPE html>
        <html>
        <head><meta charset="utf-8" /></head>
        <body style="font-family:Arial,sans-serif;background:#f4f4f4;margin:0;padding:0;">
          <table width="100%" cellpadding="0" cellspacing="0" style="background:#f4f4f4;padding:30px 0;">
            <tr><td align="center">
              <table width="600" cellpadding="0" cellspacing="0" style="background:#ffffff;border-radius:8px;overflow:hidden;">
                <tr><td style="background:#1565c0;padding:24px 32px;">
                  <h1 style="color:#ffffff;margin:0;font-size:22px;">AiVante Medicare Assistant</h1>
                </td></tr>
                <tr><td style="padding:32px;">
                  <h2 style="color:#1565c0;margin-top:0;">Reset your password</h2>
                  <p style="color:#444;line-height:1.6;">Hi {userName},</p>
                  <p style="color:#444;line-height:1.6;">We received a request to reset your password. Click the button below to choose a new password.</p>
                  <p style="text-align:center;margin:32px 0;">
                    <a href="{resetLink}"
                       style="background:#1565c0;color:#ffffff;text-decoration:none;padding:14px 32px;border-radius:4px;font-size:16px;font-weight:bold;display:inline-block;">
                      Reset Password
                    </a>
                  </p>
                  <p style="color:#888;font-size:13px;">This link expires in 30 minutes. If you did not request a password reset, you can safely ignore this email.</p>
                </td></tr>
                <tr><td style="background:#f4f4f4;padding:16px 32px;text-align:center;">
                  <p style="color:#aaa;font-size:12px;margin:0;">&copy; {DateTime.UtcNow.Year} AiVante. All rights reserved.</p>
                </td></tr>
              </table>
            </td></tr>
          </table>
        </body>
        </html>
        """;
}