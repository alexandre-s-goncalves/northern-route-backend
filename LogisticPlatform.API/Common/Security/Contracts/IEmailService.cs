using System.Threading.Tasks;

namespace LogisticPlatform.API.Common.Security.Contracts;

public interface IEmailService
{
    Task SendMfaCodeEmailAsync(string toEmail, string userName, string securityCode);
    Task SendPasswordResetEmailAsync(string toEmail, string userName, string resetLink);
}
