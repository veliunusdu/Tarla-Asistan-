using MediatR;
using TarlaAsistani.Application.Features.Auth.DTOs;

namespace TarlaAsistani.Application.Features.Auth.Commands;

public record RequestOtpCommand(string PhoneNumber) : IRequest<RequestOtpResponseDto>;
