using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.Application.Features.Auth.Commands;
using TarlaAsistani.Application.Features.Auth.DTOs;
using TarlaAsistani.Application.Features.Auth.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/auth").WithTags("Authentication");

        // 1. POST /api/v1/auth/request-otp
        group.MapPost("/request-otp", async (
            RequestOtpApiRequest req,
            IMediator mediator,
            IValidator<RequestOtpCommand> validator) =>
        {
            var command = new RequestOtpCommand(req.PhoneNumber);
            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var result = await mediator.Send(command);
                return Results.Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status429TooManyRequests);
            }
        })
        .WithName("RequestOtp")
        .Produces<RequestOtpResponseDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status429TooManyRequests);

        // 2. POST /api/v1/auth/verify-otp
        group.MapPost("/verify-otp", async (
            VerifyOtpApiRequest req,
            IMediator mediator,
            IValidator<VerifyOtpCommand> validator) =>
        {
            var command = new VerifyOtpCommand(req.PhoneNumber, req.OtpCode);
            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var result = await mediator.Send(command);
                return Results.Ok(result);
            }
            catch (ArgumentException ex)
            {
                return Results.BadRequest(new { detail = ex.Message });
            }
        })
        .WithName("VerifyOtp")
        .Produces<TokenResponseDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status400BadRequest);

        // 3. POST /api/v1/auth/firebase
        group.MapPost("/firebase", async (
            FirebaseLoginApiRequest req,
            IMediator mediator,
            IValidator<FirebaseLoginCommand> validator) =>
        {
            var command = new FirebaseLoginCommand(req.IdToken, req.Role);
            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var result = await mediator.Send(command);
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { detail = ex.Message });
            }
        })
        .WithName("FirebaseLogin")
        .Produces<TokenResponseDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status400BadRequest);

        // 4. POST /api/v1/auth/refresh
        group.MapPost("/refresh", async (
            RefreshTokenApiRequest req,
            IMediator mediator) =>
        {
            try
            {
                var result = await mediator.Send(new RefreshTokenCommand(req.RefreshToken));
                return Results.Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status401Unauthorized);
            }
        })
        .WithName("RefreshSession")
        .Produces<TokenResponseDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // 5. POST /api/v1/auth/logout
        group.MapPost("/logout", async (
            RefreshTokenApiRequest req,
            IMediator mediator) =>
        {
            await mediator.Send(new LogoutCommand(req.RefreshToken));
            return Results.NoContent();
        })
        .WithName("Logout")
        .Produces(StatusCodes.Status204NoContent);

        // 6. GET /api/v1/auth/me
        group.MapGet("/me", async (
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            IMediator mediator) =>
        {
            var queryUserId = headerUserId ?? userId ?? Guid.Empty;
            var result = await mediator.Send(new GetCurrentUserQuery(queryUserId));
            return result != null ? Results.Ok(result) : Results.NotFound(new { detail = "Kullanıcı bulunamadı." });
        })
        .WithName("GetCurrentUser")
        .Produces<UserDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}

public record RequestOtpApiRequest(string PhoneNumber);
public record VerifyOtpApiRequest(string PhoneNumber, string OtpCode);
public record FirebaseLoginApiRequest(string IdToken, UserRole? Role = null);
public record RefreshTokenApiRequest(string RefreshToken);
