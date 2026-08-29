namespace TarlaAsistani.Domain.Exceptions;

public class TaskNotFoundException : NotFoundException
{
    public TaskNotFoundException(Guid taskId) 
        : base($"Görev bulunamadı: {taskId}") { }

    public TaskNotFoundException(string message = "Görev bulunamadı.") 
        : base(message) { }
}
