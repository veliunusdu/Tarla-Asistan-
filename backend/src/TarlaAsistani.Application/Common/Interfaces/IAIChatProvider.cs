using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.Application.Common.Interfaces;

public interface IAIChatProvider
{
    Task<AIChatResponseDto> GenerateAsync(AIChatRequestDto request, CancellationToken cancellationToken = default);
}
