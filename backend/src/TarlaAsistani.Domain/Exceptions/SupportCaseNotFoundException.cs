namespace TarlaAsistani.Domain.Exceptions;

public class SupportCaseNotFoundException : NotFoundException
{
    public SupportCaseNotFoundException(Guid caseId) 
        : base($"Destek talebi bulunamadı: {caseId}") { }

    public SupportCaseNotFoundException(string message = "Destek talebi bulunamadı.") 
        : base(message) { }
}
