using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Notifications.Commands;
using TarlaAsistani.Application.Features.Notifications.DTOs;
using TarlaAsistani.Application.Features.Notifications.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class NotificationEndpoints
{
    public static IEndpointRouteBuilder MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/notifications").WithTags("Notifications");

        // 1. POST /api/v1/notifications/devices - Register push device token
        group.MapPost("/devices", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            RegisterDeviceTokenApiRequest req,
            IMediator mediator,
            IValidator<RegisterDeviceTokenCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new RegisterDeviceTokenCommand(
                UserId: userId,
                Token: req.Token,
                Platform: req.Platform
            );

            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/notifications/devices/{result.Id}", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { detail = ex.Message });
            }
        })
        .WithName("RegisterDevice")
        .Produces<DeviceTokenDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 2. DELETE /api/v1/notifications/devices/{deviceId} - Deactivate device token
        group.MapDelete("/devices/{deviceId:guid}", async (
            Guid deviceId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            if (queryUserId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var success = await mediator.Send(new DeactivateDeviceTokenCommand(deviceId, queryUserId));
            return success ? Results.NoContent() : Results.NotFound(new { detail = "Cihaz kaydı bulunamadı." });
        })
        .WithName("DeactivateDevice")
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 3. GET /api/v1/notifications - List user's notifications inbox
        group.MapGet("/", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] bool? unreadOnly,
            [FromQuery] int? limit,
            [FromQuery] int? offset,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            if (queryUserId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await mediator.Send(new ListNotificationsQuery(
                UserId: queryUserId,
                UnreadOnly: unreadOnly ?? false,
                Limit: limit ?? 50,
                Offset: offset ?? 0
            ));

            return Results.Ok(result);
        })
        .WithName("ListNotifications")
        .Produces<NotificationListDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // 4. POST /api/v1/notifications/{notificationId}/read - Mark notification read
        group.MapPost("/{notificationId:guid}/read", async (
            Guid notificationId,
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            IMediator mediator) =>
        {
            var queryUserId = httpContext.ResolveUserId(userId, headerUserId);
            if (queryUserId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await mediator.Send(new MarkNotificationReadCommand(notificationId, queryUserId));
            return result != null ? Results.Ok(result) : Results.NotFound(new { detail = "Bildirim bulunamadı." });
        })
        .WithName("MarkNotificationRead")
        .Produces<NotificationDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

public record RegisterDeviceTokenApiRequest(
    Guid? UserId,
    string Token,
    DevicePlatform Platform
);
