using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UniConnect.Application.Common;

namespace UniConnect.Infrastructure.Security;

public sealed class GmailSmtpEmailService(IConfiguration configuration, ILogger<GmailSmtpEmailService> logger) : IEmailService
{
    public async Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        var settings = configuration.GetSection("Email:Gmail");
        var host = settings["Host"] ?? "smtp.gmail.com";
        var port = int.TryParse(settings["Port"], out var configuredPort) ? configuredPort : 587;
        var username = settings["Username"];
        var appPassword = settings["AppPassword"];
        var fromEmail = settings["FromEmail"];
        var fromName = settings["FromName"] ?? "UniConnect Hub";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(appPassword) || string.IsNullOrWhiteSpace(fromEmail))
            throw new InvalidOperationException("Gmail email is selected, but its username, app password, or sender address is not configured.");

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
            Credentials = new NetworkCredential(username, appPassword)
        };

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("Sent email with subject {Subject}", subject);
    }
}
