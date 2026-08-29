using MediatR;
using TarlaAsistani.Application.Features.Notifications.DTOs;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.Application.Features.Notifications.Commands;

public record RegisterDeviceTokenCommand(
    Guid UserId,
    string Token,
    DevicePlatform Platform
) : IRequest<DeviceTokenDto>;
