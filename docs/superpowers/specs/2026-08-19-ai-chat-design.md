# AI Chat Endpoint Design

## Goal

Expose the mobile chat contract as `POST /api/v1/ai/chat`, accepting JSON or multipart form data with an optional photo and returning a stable reply/conversation identifier.

## Scope

- Add a protected AI router under the existing `/api/v1` prefix.
- Accept `message`, optional `photo`, `field_id`, `conversation_id`, and `history`.
- Validate JPEG/PNG uploads and enforce a 5 MB limit for this endpoint.
- Return `{reply, conversation_id}` on success and the existing `{detail: string}` error shape for request errors.
- Keep the AI provider behind an injectable interface. Provider credentials and external AI calls are out of scope for this task.

## Request formats

JSON requests use `application/json` and contain:

```json
{
  "message": "Tarlamda yapraklarda sararma var.",
  "field_id": "optional-field-id",
  "conversation_id": "optional-conversation-id",
  "history": [
    {"role": "user", "content": "Önceki soru"},
    {"role": "assistant", "content": "Önceki cevap"}
  ]
}
```

Photo requests use `multipart/form-data`. Text fields use the same names; `history` is a JSON-encoded array when supplied, and `photo` is the uploaded JPEG/PNG file.

## Validation and errors

- `message` is required and must contain non-whitespace text.
- `history.role` is limited to `user` and `assistant`; each content value must contain text.
- `photo` accepts only `image/jpeg` and `image/png` and is limited to 5 MiB based on streamed bytes.
- Invalid input uses FastAPI validation (`422`); unsupported media uses `415`; oversized media uses `413`.
- Missing authentication uses the existing `401` response.

## Provider boundary

The router calls an `AIChatProvider` interface with a normalized request object. The default provider is a local deterministic provider for development and contract testing; replacing it with a real provider does not change the HTTP contract.

## Testing

- JSON success returns the expected response fields.
- Multipart success accepts a valid PNG.
- Invalid history role returns `422`.
- Invalid photo type returns `415`.
- A photo larger than 5 MiB returns `413`.
- Unauthenticated requests return `401`.
