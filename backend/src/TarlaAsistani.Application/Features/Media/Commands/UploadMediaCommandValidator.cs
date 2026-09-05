using FluentValidation;

namespace TarlaAsistani.Application.Features.Media.Commands;

public class UploadMediaCommandValidator : AbstractValidator<UploadMediaCommand>
{
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "audio/mpeg",
        "audio/mp4",
        "audio/x-m4a",
        "audio/wav",
        "audio/ogg"
    };

    public UploadMediaCommandValidator()
    {
        RuleFor(x => x.OwnerId)
            .NotEmpty();

        RuleFor(x => x.Data)
            .NotEmpty().WithMessage("Boş dosya yüklenemez.")
            .Must(data => data != null && data.Length > 0 && data.Length <= 10 * 1024 * 1024)
            .WithMessage("Dosya en fazla 10 MB olabilir.");

        RuleFor(x => x.ContentType)
            .NotEmpty().WithMessage("İçerik türü belirtilmelidir.")
            .Must(ct => AllowedContentTypes.Contains(ct))
            .WithMessage("Yalnızca JPG, PNG, WEBP ve desteklenen ses dosyaları yüklenebilir.");

        RuleFor(x => x.FileName)
            .NotEmpty().WithMessage("Dosya adı belirtilmelidir.")
            .Must((command, fileName) => MediaFileSignatureValidator.IsExtensionCompatible(fileName, command.ContentType))
            .WithMessage("Dosya uzantısı içerik türüyle uyumlu değil.");

        RuleFor(x => x.Data)
            .Must((command, data) => MediaFileSignatureValidator.IsImageSignatureValid(command.ContentType, data))
            .WithMessage("Dosya içeriği bildirilen görsel türüyle uyumlu değil.");
    }
}
