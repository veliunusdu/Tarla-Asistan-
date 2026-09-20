namespace TarlaAsistani.Domain.Exceptions;

public class ConflictException : DomainException
{
    public string? Code { get; }

    public ConflictException(string message) : base(message) { }

    public ConflictException(string code, string message) : base(message)
    {
        Code = code;
    }

    public ConflictException(string message, Exception innerException) : base(message, innerException) { }
}
