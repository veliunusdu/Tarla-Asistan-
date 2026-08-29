namespace TarlaAsistani.Domain.Exceptions;

public class CropPeriodMismatchException : ValidationException
{
    public CropPeriodMismatchException(Guid cropPeriodId, Guid farmId) 
        : base($"Üretim dönemi ({cropPeriodId}) bu tarlaya ({farmId}) ait değil.") { }

    public CropPeriodMismatchException(string message = "Üretim dönemi bu tarlaya ait değil.") 
        : base(message) { }
}
