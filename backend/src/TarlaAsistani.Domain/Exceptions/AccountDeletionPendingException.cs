using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Domain.Exceptions;

public class AccountDeletionPendingException : DomainException
{
    public Guid UserId { get; }
    public Guid? RequestId { get; }
    public AccountDeletionStatus? JobStatus { get; }

    public AccountDeletionPendingException(Guid userId, Guid? requestId = null, AccountDeletionStatus? jobStatus = null)
        : base("Hesabınız için silme talebi beklemede.")
    {
        UserId = userId;
        RequestId = requestId;
        JobStatus = jobStatus;
    }
}
