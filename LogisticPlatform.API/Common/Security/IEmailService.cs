using System.Threading.Tasks;

namespace LogisticPlatform.API.Common.Security;

public interface IEmailService
{
    Task SendMfaCodeEmailAsync(string toEmail, string userName, string securityCode);
}
