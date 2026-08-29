namespace TarlaAsistani.Domain.Exceptions;

public class UserNotFoundException : NotFoundException
{
    public UserNotFoundException(Guid userId) 
        : base($"Kullanıcı bulunamadı: {userId}") { }

    public UserNotFoundException(string message = "Kullanıcı bulunamadı.") 
        : base(message) { }
}
