using System;
using System.Threading;
using System.Threading.Tasks;

namespace LogisticPlatform.API.Common.Security.Contracts;

public interface IPasswordResetService
{
    string ComputeSha256Hash(string rawData);
    Task<string> GenerateAndSendResetTokenAsync(Guid userId, string userEmail, string userName, CancellationToken cancellationToken);
}
