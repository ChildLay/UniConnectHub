using Microsoft.Extensions.Logging;
using UniConnect.Application.Common;

namespace UniConnect.Infrastructure.Security;

public sealed class ConsoleEmailService(ILogger<ConsoleEmailService> logger) : IEmailService
{
    public Task SendAsync(string email, string subject, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("Development email queued for {EmailAddress} with subject {Subject}", email, subject);
        return Task.CompletedTask;
    }
}
