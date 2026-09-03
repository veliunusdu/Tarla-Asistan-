namespace TarlaAsistani.Application.Common.Exceptions;

public class QuotaExceededException : Exception
{
    public QuotaExceededException(string message) : base(message)
    {
    }
}
