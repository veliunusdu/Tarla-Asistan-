using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.Media.Commands;
using TarlaAsistani.Application.Features.Media.DTOs;
using TarlaAsistani.Application.Features.Media.Queries;
using TarlaAsistani.Domain.Enums;

namespace TarlaAsistani.API.Endpoints;

public static class MediaEndpoints
{
    public static IEndpointRouteBuilder MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/media").WithTags("Media");

        // 1. POST /api/v1/media - Upload image or audio file
        group.MapPost("/", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromForm] Guid? userId,
            IFormFile file,
            IMediator mediator,
            IValidator<UploadMediaCommand> validator) =>
        {
            var ownerId = httpContext.ResolveUserId(userId, headerUserId);
            if (ownerId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            if (file == null || file.Length == 0)
            {
                return Results.UnprocessableEntity(new { detail = "Boş dosya yüklenemez." });
            }

            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            var data = memoryStream.ToArray();

            var command = new UploadMediaCommand(
                OwnerId: ownerId,
                FileName: file.FileName,
                ContentType: file.ContentType,
                Data: data
            );

            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid)
            {
                return Results.ValidationProblem(validation.ToDictionary());
            }

            try
            {
                var result = await mediator.Send(command);
                return Results.Created($"/api/v1/media/{result.Id}/content", result);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { detail = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status415UnsupportedMediaType);
            }
        })
        .WithName("UploadMedia")
        .DisableAntiforgery()
        .Produces<MediaAssetDto>(StatusCodes.Status201Created)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status415UnsupportedMediaType)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .Produces(StatusCodes.Status404NotFound);

        // 2. GET /api/v1/media/{id}/content - Download media content
        group.MapGet("/{id:guid}/content", async (
            Guid id,
            HttpContext context,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            [FromQuery] Guid? userId,
            [FromQuery] UserRole? role,
            IMediator mediator) =>
        {
            var queryUserId = context.ResolveUserId(userId, headerUserId);
            var userRole = context.ResolveUserRole(role, defaultRole: UserRole.Farmer);

            var result = await mediator.Send(new GetMediaContentQuery(id, queryUserId, userRole));
            if (result == null)
            {
                return Results.NotFound(new { detail = "Medya bulunamadı." });
            }

            context.Response.Headers.CacheControl = "private, max-age=3600";
            return Results.File(result.Content, result.ContentType);
        })
        .WithName("GetMediaContent")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);

        return app;
    }
}
