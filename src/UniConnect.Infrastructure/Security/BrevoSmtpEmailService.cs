using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UniConnect.Application.Common;

namespace UniConnect.Infrastructure.Security;

public sealed class BrevoSmtpEmailService(IConfiguration configuration, ILogger<BrevoSmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        var settings = configuration.GetSection("Email:Brevo");
        var host = settings["Host"] ?? "smtp-relay.brevo.com";
        var port = int.TryParse(settings["Port"], out var configuredPort) ? configuredPort : 587;
        var username = settings["Username"];
        var smtpKey = settings["SmtpKey"];
        var fromEmail = settings["FromEmail"];
        var fromName = settings["FromName"] ?? "UniConnect Hub";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(smtpKey) || string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("Brevo email is selected, but its SMTP login, SMTP key, or verified sender address is not configured.");

        using var message = new MailMessage
        {
            From = new MailAddress(fromEmail, fromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };
        message.To.Add(new MailAddress(email));

        using var client = new SmtpClient(host, port)
        {
            EnableSsl = settings.GetValue("EnableSsl", true),
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(username, smtpKey)
        };

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("Sent transactional email through Brevo with subject {Subject}", subject);
    }
}
