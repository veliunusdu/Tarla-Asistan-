namespace TarlaAsistani.Domain.Exceptions;

public class CropPeriodNotFoundException : NotFoundException
{
    public CropPeriodNotFoundException(Guid cropPeriodId) 
        : base($"Üretim dönemi bulunamadı: {cropPeriodId}") { }

    public CropPeriodNotFoundException(string message = "Üretim dönemi bulunamadı.") 
        : base(message) { }
}
