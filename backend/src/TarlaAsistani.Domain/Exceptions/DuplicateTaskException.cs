namespace TarlaAsistani.Domain.Exceptions;

public class DuplicateTaskException : ConflictException
{
    public DuplicateTaskException(string dedupeKey, bool isKey = true) 
        : base(isKey ? $"Aynı görev zaten mevcut: {dedupeKey}" : dedupeKey) { }

    public DuplicateTaskException() 
        : base("Aynı görev bu tarla ve tarih için zaten mevcut.") { }
}
