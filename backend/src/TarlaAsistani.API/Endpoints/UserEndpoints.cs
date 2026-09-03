using System.Text.Json.Serialization;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Application.Features.Auth.Queries;
using TarlaAsistani.Application.Features.Users.Commands;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/users").WithTags("Users");

        // 1. PUT /api/v1/users/me - Update user profile
        group.MapPut("/me", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            UpdateProfileApiRequest req,
            IMediator mediator,
            IValidator<UpdateProfileCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new UpdateProfileCommand(
                UserId: userId,
                FullName: req.FullName ?? string.Empty,
                Province: req.Province ?? string.Empty,
                District: req.District ?? string.Empty,
                TermsAccepted: req.TermsAccepted ?? true,
                NotificationsEnabled: req.NotificationsEnabled ?? true
            );

            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            var result = await mediator.Send(command);
            return result != null ? Results.Ok(result) : Results.NotFound(new { detail = "Kullanıcı bulunamadı." });
        })
        .WithName("UpdateProfile")
        .Produces<UserDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 2. POST /api/v1/users/me/deletion-request - Request account deletion
        group.MapPost("/me/deletion-request", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            AccountDeletionApiRequest req,
            IMediator mediator,
            IValidator<RequestAccountDeletionCommand> validator) =>
        {
            var userId = httpContext.ResolveUserId(req.UserId, headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var command = new RequestAccountDeletionCommand(userId, req.Confirmation);

            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var result = await mediator.Send(command);
                return Results.Accepted($"/api/v1/users/me/deletion-request/{result.RequestId}", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { detail = ex.Message });
            }
        })
        .WithName("RequestAccountDeletion")
        .Produces<AccountDeletionResponseDto>(StatusCodes.Status202Accepted)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);

        // 3. GET /api/v1/users/farmer-area - Role verified farmer area
        group.MapGet("/farmer-area", async (
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

            var result = await mediator.Send(new GetCurrentUserQuery(queryUserId));
            if (result == null) return Results.NotFound(new { detail = "Kullanıcı bulunamadı." });

            if (result.Role != UserRole.Farmer)
            {
                return Results.Json(new { detail = "Bu alana yalnızca çiftçiler erişebilir." }, statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(result);
        })
        .WithName("FarmerArea")
        .Produces<UserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        // 4. GET /api/v1/users/agronomist-area - Role verified agronomist area
        group.MapGet("/agronomist-area", async (
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

            var result = await mediator.Send(new GetCurrentUserQuery(queryUserId));
            if (result == null) return Results.NotFound(new { detail = "Kullanıcı bulunamadı." });

            if (result.Role != UserRole.Agronomist)
            {
                return Results.Json(new { detail = "Bu alana yalnızca ziraat mühendisleri/uzmanlar erişebilir." }, statusCode: StatusCodes.Status403Forbidden);
            }

            return Results.Ok(result);
        })
        .WithName("AgronomistArea")
        .Produces<UserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

public record UpdateProfileApiRequest(
    [property: JsonPropertyName("user_id")] Guid? UserId,
    [property: JsonPropertyName("full_name")] string? FullName,
    [property: JsonPropertyName("province")] string? Province,
    [property: JsonPropertyName("district")] string? District,
    [property: JsonPropertyName("terms_accepted")] bool? TermsAccepted,
    [property: JsonPropertyName("notifications_enabled")] bool? NotificationsEnabled
);

public record AccountDeletionApiRequest(
    Guid? UserId,
    string Confirmation
);
