namespace TarlaAsistani.Domain.Exceptions;

public class FarmNotFoundException : NotFoundException
{
    public FarmNotFoundException(Guid farmId) 
        : base($"Tarla bulunamadı: {farmId}") { }

    public FarmNotFoundException(string message = "Tarla bulunamadı.") 
        : base(message) { }
}
