using MediatR;
using TarlaAsistani.Application.Features.Auth.DTOs;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public record VerifyOtpCommand(string PhoneNumber, string OtpCode) : IRequest<TokenResponseDto>;
