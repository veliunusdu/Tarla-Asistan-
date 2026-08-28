namespace TarlaAsistani.Domain.Exceptions;

public class ActivityNotFoundException : NotFoundException
{
    public ActivityNotFoundException(Guid activityId) 
        : base($"Tarla aktivitesi bulunamadı: {activityId}") { }

    public ActivityNotFoundException(string message = "Tarla aktivitesi bulunamadı.") 
        : base(message) { }
}
