using FluentValidation;

namespace TarlaAsistani.Application.Features.AI.Commands;

public class SendAIChatMessageCommandValidator : AbstractValidator<SendAIChatMessageCommand>
{
    private static readonly HashSet<string> AllowedPhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png"
    };

    public SendAIChatMessageCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEmpty();

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("message boş olamaz.")
            .MaximumLength(12000);

        When(x => x.FieldId != null, () =>
        {
            RuleFor(x => x.FieldId).MaximumLength(200);
        });

        When(x => x.ConversationId != null, () =>
        {
            RuleFor(x => x.ConversationId).MaximumLength(200);
        });

        When(x => x.PhotoBytes != null && x.PhotoBytes.Length > 0, () =>
        {
            RuleFor(x => x.PhotoBytes!.Length)
                .LessThanOrEqualTo(5 * 1024 * 1024)
                .WithMessage("photo en fazla 5 MB olabilir.");

            RuleFor(x => x.PhotoContentType)
                .NotEmpty()
                .Must(ct => ct != null && AllowedPhotoTypes.Contains(ct))
                .WithMessage("photo yalnızca JPEG veya PNG olabilir.");
        });
    }
}
