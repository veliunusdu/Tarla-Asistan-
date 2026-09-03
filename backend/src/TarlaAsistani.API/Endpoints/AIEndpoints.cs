using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Features.AI.Commands;
using TarlaAsistani.Application.Features.AI.DTOs;

namespace TarlaAsistani.API.Endpoints;

public static class AIEndpoints
{
    public static IEndpointRouteBuilder MapAIEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai").WithTags("AI Chat");

        // POST /api/v1/ai/chat — Start or continue conversational AI chat with optional image.
        // Authenticated user ID is resolved from JWT ClaimsPrincipal (production source of truth).
        // In dev/test, X-User-Id header fallback is permitted via CurrentUserExtensions.
        group.MapPost("/chat", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            IMediator mediator,
            IValidator<SendAIChatMessageCommand> validator) =>
        {
            // ── Auth: JWT first, header fallback only in non-production ──────
            var userId = httpContext.ResolveUserId(headerUserId: headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            // ── Parse request body ───────────────────────────────────────────
            string message = string.Empty;
            string? fieldId = null;
            string? conversationId = null;
            List<ChatHistoryItem>? history = null;
            byte[]? photoBytes = null;
            string? photoContentType = null;

            if (httpContext.Request.HasFormContentType)
            {
                var form = await httpContext.Request.ReadFormAsync();
                message = form["message"].ToString();
                fieldId = form["field_id"].ToString();
                conversationId = form["conversation_id"].ToString();

                var historyRaw = form["history"].ToString();
                if (!string.IsNullOrWhiteSpace(historyRaw))
                {
                    try
                    {
                        history = JsonSerializer.Deserialize<List<ChatHistoryItem>>(historyRaw);
                    }
                    catch
                    {
                        return Results.UnprocessableEntity(new { detail = "history geçerli JSON olmalıdır." });
                    }
                }

                var file = form.Files.GetFile("photo");
                if (file != null && file.Length > 0)
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    photoBytes = ms.ToArray();
                    photoContentType = file.ContentType;
                }
            }
            else
            {
                try
                {
                    var req = await httpContext.Request.ReadFromJsonAsync<AIChatRequestDto>();
                    if (req != null)
                    {
                        message = req.Message;
                        fieldId = req.FieldId;
                        conversationId = req.ConversationId;
                        history = req.History;
                        // AccountContext from client is intentionally ignored here —
                        // it is built server-side from authenticated user's DB records.
                    }
                }
                catch
                {
                    return Results.UnprocessableEntity(new { detail = "İstek gövdesi geçerli bir JSON nesnesi olmalıdır." });
                }
            }

            // ── Build and dispatch command ────────────────────────────────────
            // fieldId is a hint only; backend validates ownership in AIContextService.
            var command = new SendAIChatMessageCommand(
                UserId: userId,
                Message: message,
                FieldId: fieldId,
                ConversationId: conversationId,
                History: history,
                PhotoBytes: photoBytes,
                PhotoContentType: photoContentType
            );

            var validation = await validator.ValidateAsync(command);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            try
            {
                var result = await mediator.Send(command);
                return Results.Ok(result);
            }
            catch (NotSupportedException ex)
            {
                return Results.UnprocessableEntity(new { detail = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("AIChat")
        .DisableAntiforgery()
        .Produces<AIChatResponseDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status415UnsupportedMediaType)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        return app;
    }
}
