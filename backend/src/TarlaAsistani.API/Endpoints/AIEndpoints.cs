using System.Text.Json;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using TarlaAsistani.API.Common;
using TarlaAsistani.Application.Common.Exceptions;
using TarlaAsistani.Application.Features.AI.Commands;
using TarlaAsistani.Application.Features.AI.DTOs;
using TarlaAsistani.Application.Features.AI.Queries;

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

                if (result.QuotaInfo != null)
                {
                    httpContext.Response.Headers["X-Quota-Photo-Remaining"] = result.QuotaInfo.PhotosRemainingToday.ToString();
                    httpContext.Response.Headers["X-Quota-Text-Remaining"] = result.QuotaInfo.TextsRemainingToday.ToString();
                }

                return Results.Ok(result);
            }
            catch (QuotaExceededException ex)
            {
                return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status429TooManyRequests);
            }
            catch (NotSupportedException ex)
            {
                return Results.UnprocessableEntity(new { detail = ex.Message });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }
            catch (AIAgentExecutionException ex)
            {
                var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AIEndpoints");
                logger.LogError(ex, "AIAgent execution error {ErrorCode} for user {UserId}", ex.ErrorCode, userId);
                return Results.Json(new { detail = "AI hizmeti şu anda kullanılamıyor." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (InvalidOperationException ex)
            {
                var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AIEndpoints");
                logger.LogError(ex, "AIChat operation error for user {UserId}", userId);
                return Results.Json(new { detail = "AI hizmeti şu anda kullanılamıyor." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch (Exception ex)
            {
                var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AIEndpoints");
                logger.LogError(ex, "AIChat unexpected error for user {UserId}", userId);
                return Results.Json(new { detail = "AI hizmeti şu anda kullanılamıyor." }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("AIChat")
        .RequireRateLimiting("AiChatPerUser")
        .DisableAntiforgery()
        .Produces<AIChatResponseDto>(StatusCodes.Status200OK)
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status415UnsupportedMediaType)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .Produces(StatusCodes.Status429TooManyRequests)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        // GET /api/v1/ai/quota — Retrieve authenticated user's daily quota status.
        group.MapGet("/quota", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            IMediator mediator) =>
        {
            var userId = httpContext.ResolveUserId(headerUserId: headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            var quota = await mediator.Send(new GetAIQuotaQuery(userId));
            return Results.Ok(quota);
        })
        .WithName("GetAIQuota")
        .Produces<AIQuotaStatusDto>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);

        // POST /api/v1/ai/chat/stream — Real-time Server-Sent Events (SSE) AI chat stream.
        group.MapPost("/chat/stream", async (
            HttpContext httpContext,
            [FromHeader(Name = "X-User-Id")] Guid? headerUserId,
            IMediator mediator,
            IValidator<StreamAIChatMessageCommand> validator,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.ResolveUserId(headerUserId: headerUserId);
            if (userId == Guid.Empty)
            {
                return Results.Json(new { detail = "Kimlik doğrulanmadı." }, statusCode: StatusCodes.Status401Unauthorized);
            }

            string message = string.Empty;
            string? fieldId = null;
            string? conversationId = null;
            List<ChatHistoryItem>? history = null;
            byte[]? photoBytes = null;
            string? photoContentType = null;

            if (httpContext.Request.HasFormContentType)
            {
                var form = await httpContext.Request.ReadFormAsync(cancellationToken);
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
                    await file.CopyToAsync(ms, cancellationToken);
                    photoBytes = ms.ToArray();
                    photoContentType = file.ContentType;
                }
            }
            else
            {
                try
                {
                    var req = await httpContext.Request.ReadFromJsonAsync<AIChatRequestDto>(cancellationToken: cancellationToken);
                    if (req != null)
                    {
                        message = req.Message;
                        fieldId = req.FieldId;
                        conversationId = req.ConversationId;
                        history = req.History;
                    }
                }
                catch
                {
                    return Results.UnprocessableEntity(new { detail = "İstek gövdesi geçerli bir JSON nesnesi olmalıdır." });
                }
            }

            var command = new StreamAIChatMessageCommand(
                UserId: userId,
                Message: message,
                FieldId: fieldId,
                ConversationId: conversationId,
                History: history,
                PhotoBytes: photoBytes,
                PhotoContentType: photoContentType
            );

            var validation = await validator.ValidateAsync(command, cancellationToken);
            if (!validation.IsValid) return Results.ValidationProblem(validation.ToDictionary());

            httpContext.Response.ContentType = "text/event-stream; charset=utf-8";
            httpContext.Response.Headers.CacheControl = "no-cache";
            httpContext.Response.Headers.Connection = "keep-alive";

            try
            {
                await foreach (var chunk in mediator.CreateStream(command, cancellationToken))
                {
                    var json = JsonSerializer.Serialize(chunk, SseJsonOptions);
                    await httpContext.Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                    await httpContext.Response.Body.FlushAsync(cancellationToken);
                }

                await httpContext.Response.WriteAsync("data: [DONE]\n\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);

                return Results.Empty;
            }
            catch (QuotaExceededException ex)
            {
                if (!httpContext.Response.HasStarted)
                {
                    return Results.Json(new { detail = ex.Message }, statusCode: StatusCodes.Status429TooManyRequests);
                }
                var errJson = JsonSerializer.Serialize(new { error = ex.Message }, SseJsonOptions);
                await httpContext.Response.WriteAsync($"data: {errJson}\n\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
                return Results.Empty;
            }
            catch (NotSupportedException ex)
            {
                if (!httpContext.Response.HasStarted)
                {
                    return Results.UnprocessableEntity(new { detail = ex.Message });
                }
                var errJson = JsonSerializer.Serialize(new { error = ex.Message }, SseJsonOptions);
                await httpContext.Response.WriteAsync($"data: {errJson}\n\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
                return Results.Empty;
            }
            catch (Exception ex)
            {
                var logger = httpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("AIEndpoints");
                logger.LogError(ex, "AIChatStream unexpected error for user {UserId}", userId);

                if (!httpContext.Response.HasStarted)
                {
                    return Results.Json(new { detail = "AI hizmeti şu anda kullanılamıyor." }, statusCode: StatusCodes.Status503ServiceUnavailable);
                }
                var errJson = JsonSerializer.Serialize(new { error = "AI hizmeti şu anda kullanılamıyor." }, SseJsonOptions);
                await httpContext.Response.WriteAsync($"data: {errJson}\n\n", cancellationToken);
                await httpContext.Response.Body.FlushAsync(cancellationToken);
                return Results.Empty;
            }
        })
        .WithName("AIChatStream")
        .RequireRateLimiting("AiChatPerUser")
        .DisableAntiforgery()
        .Produces(StatusCodes.Status200OK, contentType: "text/event-stream")
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status415UnsupportedMediaType)
        .Produces(StatusCodes.Status422UnprocessableEntity)
        .Produces(StatusCodes.Status429TooManyRequests)
        .Produces(StatusCodes.Status503ServiceUnavailable);

        return app;
    }

    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
}
