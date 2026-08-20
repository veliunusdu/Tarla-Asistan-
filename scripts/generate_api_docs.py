from __future__ import annotations

import html
import json
import re
import sys
from collections import OrderedDict
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parents[1]
BACKEND = ROOT / "backend"
DOCS = ROOT / "docs"
sys.path.insert(0, str(BACKEND))

from app.main import app  # noqa: E402


HTTP_METHODS = ("get", "post", "put", "patch", "delete")
METHOD_ORDER = {method: index for index, method in enumerate(HTTP_METHODS)}
LOCAL_BASE_URL = "http://localhost:8100"

AGRONOMIST_ONLY = {
    ("POST", "/api/v1/farms/{farm_id}/tasks"),
    ("PATCH", "/api/v1/cases/{case_id}/status"),
    ("POST", "/api/v1/cases/{case_id}/expert-response"),
    ("GET", "/api/v1/pilot/feedback"),
    ("PATCH", "/api/v1/pilot/feedback/{feedback_id}"),
    ("GET", "/api/v1/pilot/metrics"),
}
FARMER_ONLY = {
    ("POST", "/api/v1/cases"),
    ("POST", "/api/v1/farms/{farm_id}/activities"),
    ("PATCH", "/api/v1/activities/{activity_id}"),
    ("POST", "/api/v1/activities/{activity_id}/confirm"),
    ("DELETE", "/api/v1/activities/{activity_id}"),
    ("POST", "/api/v1/activities/{activity_id}/restore"),
}
ROLE_SCOPED = {
    ("GET", "/api/v1/cases"),
    ("GET", "/api/v1/cases/{case_id}"),
    ("POST", "/api/v1/cases/{case_id}/messages"),
    ("GET", "/api/v1/media/{media_id}/content"),
    ("GET", "/api/v1/farms/{farm_id}/activities"),
    ("GET", "/api/v1/activities/{activity_id}/revisions"),
    ("GET", "/api/v1/farms/{farm_id}/journal"),
}


def slug(value: str) -> str:
    value = value.casefold().replace("ı", "i").replace("ş", "s")
    value = value.replace("ğ", "g").replace("ü", "u").replace("ö", "o")
    value = value.replace("ç", "c")
    return re.sub(r"[^a-z0-9]+", "-", value).strip("-")


def ref_name(schema: dict[str, Any]) -> str | None:
    ref = schema.get("$ref")
    return ref.rsplit("/", 1)[-1] if ref else None


def pick_schema(content: dict[str, Any]) -> tuple[str, dict[str, Any]] | None:
    if not content:
        return None
    content_type = next(iter(content))
    return content_type, content[content_type].get("schema", {})


def schema_type(schema: dict[str, Any], *, markdown: bool = False) -> str:
    name = ref_name(schema)
    if name:
        if markdown:
            return f"[{name}](#schema-{slug(name)})"
        return name
    if "anyOf" in schema:
        parts = [schema_type(item, markdown=markdown) for item in schema["anyOf"]]
        return " | ".join(dict.fromkeys(parts))
    if "allOf" in schema:
        return " & ".join(
            schema_type(item, markdown=markdown) for item in schema["allOf"]
        )
    kind = schema.get("type", "object")
    if kind == "array":
        return f"array<{schema_type(schema.get('items', {}), markdown=markdown)}>"
    schema_format = schema.get("format")
    return f"{kind} ({schema_format})" if schema_format else kind


def constraints(schema: dict[str, Any]) -> str:
    values: list[str] = []
    if "default" in schema:
        values.append(f"varsayılan: `{schema['default']}`")
    for key, label in (
        ("minLength", "min uzunluk"),
        ("maxLength", "maks uzunluk"),
        ("minimum", "min"),
        ("maximum", "maks"),
        ("exclusiveMinimum", ">"),
        ("exclusiveMaximum", "<"),
        ("minItems", "min öğe"),
        ("maxItems", "maks öğe"),
    ):
        if key in schema:
            values.append(f"{label}: `{schema[key]}`")
    if schema.get("enum"):
        values.append("değerler: " + ", ".join(f"`{item}`" for item in schema["enum"]))
    if schema.get("description"):
        values.append(schema["description"])
    return "; ".join(values) or "—"


def example_for_schema(
    schema: dict[str, Any], components: dict[str, Any], *, depth: int = 0
) -> Any:
    if depth > 4:
        return None
    if "example" in schema:
        return schema["example"]
    if "default" in schema:
        return schema["default"]
    name = ref_name(schema)
    if name:
        return example_for_schema(components.get(name, {}), components, depth=depth + 1)
    if "anyOf" in schema:
        candidate = next(
            (item for item in schema["anyOf"] if item.get("type") != "null"),
            schema["anyOf"][0],
        )
        return example_for_schema(candidate, components, depth=depth + 1)
    if "allOf" in schema:
        merged: dict[str, Any] = {}
        for item in schema["allOf"]:
            value = example_for_schema(item, components, depth=depth + 1)
            if isinstance(value, dict):
                merged.update(value)
        return merged
    if schema.get("enum"):
        return schema["enum"][0]
    kind = schema.get("type", "object")
    schema_format = schema.get("format")
    title = schema.get("title", "").casefold().replace(" ", "_")
    if kind == "object" or "properties" in schema:
        result: dict[str, Any] = {}
        for field, field_schema in schema.get("properties", {}).items():
            result[field] = example_for_schema(
                field_schema, components, depth=depth + 1
            )
        return result
    if kind == "array":
        return [
            example_for_schema(schema.get("items", {}), components, depth=depth + 1)
        ]
    if kind == "boolean":
        return False
    if kind == "integer":
        return max(1, int(schema.get("minimum", 1)))
    if kind == "number":
        return max(1.0, float(schema.get("minimum", 1.0)))
    if kind == "null":
        return None
    if schema_format == "uuid":
        return "11111111-1111-4111-8111-111111111111"
    if schema_format == "date":
        return "2026-08-09"
    if schema_format == "date-time":
        return "2026-08-09T12:00:00Z"
    if schema_format == "uri":
        return "https://example.com/resource"
    if "phone" in title:
        return "+905551234567"
    if "otp" in title or title.endswith("code"):
        return "123456"
    if "token" in title:
        return "example-device-or-refresh-token"
    if "name" in title:
        return "Örnek kayıt"
    if "title" in title:
        return "Örnek başlık"
    if "description" in title or "comment" in title or "reason" in title:
        return "Örnek açıklama"
    if "url" in title:
        return "https://example.com/resource"
    return "string"


def access_label(method: str, path: str, operation: dict[str, Any]) -> str:
    key = (method, path)
    if not operation.get("security"):
        return "Herkese açık"
    if key in AGRONOMIST_ONLY:
        return "Yalnızca AGRONOMIST"
    if key in FARMER_ONLY:
        return "Yalnızca FARMER"
    if key in ROLE_SCOPED:
        return "FARMER / AGRONOMIST (rol kapsamı uygulanır)"
    if path.startswith("/api/v1/farms") or path.startswith("/api/v1/tasks"):
        return "Bearer; sahiplik ve rol denetimi"
    return "Bearer token"


def iter_operations(spec: dict[str, Any]) -> list[tuple[str, str, dict[str, Any]]]:
    operations: list[tuple[str, str, dict[str, Any]]] = []
    for path, path_item in spec["paths"].items():
        for method in HTTP_METHODS:
            if method in path_item:
                operations.append((method.upper(), path, path_item[method]))
    return operations


def group_operations(
    operations: list[tuple[str, str, dict[str, Any]]],
) -> OrderedDict[str, list[tuple[str, str, dict[str, Any]]]]:
    grouped: OrderedDict[str, list[tuple[str, str, dict[str, Any]]]] = OrderedDict()
    for operation in operations:
        tag = operation[2].get("tags", ["Diğer"])[0]
        grouped.setdefault(tag, []).append(operation)
    return grouped


def response_rows(
    operation: dict[str, Any], *, markdown: bool
) -> list[tuple[str, str, str]]:
    rows: list[tuple[str, str, str]] = []
    for status_code, response in operation.get("responses", {}).items():
        selected = pick_schema(response.get("content", {}))
        response_type = "Boş gövde"
        if selected:
            content_type, schema = selected
            response_type = f"{content_type} · {schema_type(schema, markdown=markdown)}"
        description = response.get("description", "")
        if description == "Successful Response":
            description = "Başarılı yanıt"
        elif description == "Validation Error":
            description = "Doğrulama hatası"
        rows.append((status_code, description, response_type))
    return rows


def success_response_example(
    operation: dict[str, Any], components: dict[str, Any]
) -> tuple[str, Any] | None:
    for status_code, response in operation.get("responses", {}).items():
        if not str(status_code).startswith("2"):
            continue
        selected = pick_schema(response.get("content", {}))
        if not selected:
            continue
        content_type, schema = selected
        if content_type != "application/json":
            continue
        return str(status_code), example_for_schema(schema, components)
    return None


def curl_example(
    method: str,
    path: str,
    operation: dict[str, Any],
    components: dict[str, Any],
) -> str:
    lines = [f"curl -X {method} '{LOCAL_BASE_URL}{path}'"]
    if operation.get("security"):
        lines.append("  -H 'Authorization: Bearer <access_token>'")
    body = operation.get("requestBody", {})
    selected = pick_schema(body.get("content", {}))
    if selected:
        content_type, schema = selected
        if content_type == "multipart/form-data":
            lines.append("  -F 'file=@ornek.jpg'")
        else:
            example = json.dumps(
                example_for_schema(schema, components), ensure_ascii=False, indent=2
            )
            lines.append(f"  -H 'Content-Type: {content_type}'")
            lines.append("  --data-binary '" + example + "'")
    return " \\\n".join(lines)


def render_markdown(spec: dict[str, Any]) -> str:
    operations = iter_operations(spec)
    grouped = group_operations(operations)
    components = spec.get("components", {}).get("schemas", {})
    lines = [
        "# Tarla Asistanı API Dokümantasyonu",
        "",
        "> Bu dosya `scripts/generate_api_docs.py` ile backend kodundaki gerçek FastAPI "
        "OpenAPI şemasından üretilir. Elle düzenlemek yerine üretim aracını çalıştırın.",
        "",
        "## Genel Bakış",
        "",
        f"- **API sürümü:** `{spec['info'].get('version', '0.1.0')}`",
        f"- **Yerel Docker adresi:** `{LOCAL_BASE_URL}`",
        "- **API öneki:** `/api/v1`",
        "- **İçerik tipi:** `application/json`; medya yükleme için `multipart/form-data`",
        f"- **Kapsam:** {len(operations)} işlem, {len(spec['paths'])} yol, {len(components)} şema",
        "- **Makine tarafından okunabilir tanım:** [`openapi.json`](./openapi.json)",
        "- **Paylaşılabilir görsel doküman:** [`api-docs.html`](./api-docs.html)",
        "",
        "## Kimlik Doğrulama ve Oturum",
        "",
        "Korunan uçlarda `Authorization: Bearer <access_token>` başlığı kullanılır. "
        "Access token varsayılan olarak 60 dakika geçerlidir. `POST /api/v1/auth/refresh` "
        "refresh tokenı döndürür; eski refresh token tekrar kullanılamaz. OTP kodu varsayılan "
        "olarak 180 saniye geçerlidir ve istek/deneme sınırları uygulanır.",
        "",
        "Roller: `FARMER` kendi tarlaları, faaliyetleri ve vakaları üzerinde çalışır. "
        "`AGRONOMIST` uzman görevleri, vaka değerlendirmeleri ve pilot metrikleri yönetir. "
        "Kaynak sahipliği sunucu tarafında kontrol edilir; yetkisiz kaynağın varlığını "
        "açıklamamak için birçok uç `404` döndürür.",
        "",
        "## Ortak Kurallar",
        "",
        "- Tarihler ISO 8601 biçimindedir: `YYYY-MM-DD`; zamanlar UTC `date-time` olarak gönderilir.",
        "- Kimlikler UUID biçimindedir.",
        "- Liste uçları çoğunlukla `limit` ve `offset` kullanır; yanıt `items` ve `total` içerir.",
        "- `client_operation_id`, çevrimdışı tekrar gönderimlerini tekilleştirir. Aynı kimlik farklı "
        "içerikle kullanılırsa `409 Conflict` döner.",
        "- Arşivleme uçları fiziksel silme yapmaz; kayıtlar geçmiş ve denetim amacıyla korunur.",
        "- Her yanıtta `X-Request-ID` bulunur. Sunucu hatalarında aynı değer JSON gövdesindeki "
        "`request_id` alanında da döner.",
        "",
        "## Endpoint Özeti",
        "",
        "| Grup | Metot | Yol | Açıklama | Erişim |",
        "|---|---|---|---|---|",
    ]
    for tag, tag_operations in grouped.items():
        for method, path, operation in tag_operations:
            lines.append(
                f"| {tag} | `{method}` | `{path}` | {operation.get('summary', '—')} | "
                f"{access_label(method, path, operation)} |"
            )

    lines.extend(["", "## Endpoint Ayrıntıları", ""])
    for tag, tag_operations in grouped.items():
        lines.extend([f"### {tag}", ""])
        for method, path, operation in tag_operations:
            endpoint_id = (
                f"endpoint-{slug(operation.get('operationId', method + '-' + path))}"
            )
            lines.extend(
                [
                    f'<a id="{endpoint_id}"></a>',
                    f"#### `{method} {path}` — {operation.get('summary', '')}",
                    "",
                    f"- **Erişim:** {access_label(method, path, operation)}",
                    f"- **Operation ID:** `{operation.get('operationId', '—')}`",
                ]
            )
            if operation.get("description"):
                lines.append(f"- **Açıklama:** {operation['description']}")
            parameters = operation.get("parameters", [])
            if parameters:
                lines.extend(
                    [
                        "",
                        "**Parametreler**",
                        "",
                        "| Ad | Konum | Tip | Zorunlu | Kurallar |",
                        "|---|---|---|---|---|",
                    ]
                )
                for parameter in parameters:
                    parameter_schema = parameter.get("schema", {})
                    lines.append(
                        f"| `{parameter['name']}` | {parameter['in']} | "
                        f"{schema_type(parameter_schema, markdown=True)} | "
                        f"{'Evet' if parameter.get('required') else 'Hayır'} | "
                        f"{constraints(parameter_schema)} |"
                    )
            request_body = operation.get("requestBody")
            if request_body:
                selected = pick_schema(request_body.get("content", {}))
                if selected:
                    content_type, body_schema = selected
                    lines.extend(
                        [
                            "",
                            f"**Request body** — `{content_type}` · "
                            f"{schema_type(body_schema, markdown=True)}",
                            "",
                            "```json",
                            json.dumps(
                                example_for_schema(body_schema, components),
                                ensure_ascii=False,
                                indent=2,
                            ),
                            "```",
                        ]
                    )
            lines.extend(
                [
                    "",
                    "**Yanıtlar**",
                    "",
                    "| HTTP | Açıklama | Gövde |",
                    "|---|---|---|",
                ]
            )
            for status_code, description, response_type in response_rows(
                operation, markdown=True
            ):
                lines.append(f"| `{status_code}` | {description} | {response_type} |")
            response_example = success_response_example(operation, components)
            if response_example:
                status_code, example = response_example
                lines.extend(
                    [
                        "",
                        f"**Örnek başarılı yanıt (`{status_code}`)**",
                        "",
                        "```json",
                        json.dumps(example, ensure_ascii=False, indent=2),
                        "```",
                    ]
                )
            lines.extend(
                [
                    "",
                    "**cURL**",
                    "",
                    "```bash",
                    curl_example(method, path, operation, components),
                    "```",
                    "",
                ]
            )

    lines.extend(
        [
            "## Ortak Hata Cevapları",
            "",
            "| HTTP | Anlam | Tipik neden |",
            "|---|---|---|",
            "| `400` | Bad Request | Geçersiz OTP veya iş isteği |",
            "| `401` | Unauthorized | Eksik, süresi dolmuş veya geçersiz oturum |",
            "| `403` | Forbidden | Rolün işlemi yapmaya yetkili olmaması |",
            "| `404` | Not Found | Kaynak yok veya kullanıcı için görünür değil |",
            "| `409` | Conflict | Geçersiz durum geçişi, yinelenen işlem veya aktif dönem çakışması |",
            "| `413` | Payload Too Large | Medya dosyasının boyut sınırını aşması |",
            "| `415` | Unsupported Media Type | Desteklenmeyen medya tipi |",
            "| `422` | Validation Error | Alan, biçim veya iş kuralı doğrulamasının başarısız olması |",
            "| `429` | Too Many Requests | OTP bekleme süresi dolmadan yeniden istek |",
            "| `500` | Internal Server Error | Beklenmeyen hata; destek için `request_id` kullanılır |",
            "| `503` | Service Unavailable | Hava sağlayıcısı kullanılamıyor ve geçerli önbellek yok |",
            "",
            "Doğrulama hatası örneği:",
            "",
            "```json",
            '{"detail":[{"loc":["body","field"],"msg":"Field required","type":"missing"}]}',
            "```",
            "",
            "Sunucu hatası örneği:",
            "",
            "```json",
            '{"detail":"Beklenmeyen bir hata oluştu.","request_id":"uuid"}',
            "```",
            "",
            "## Operasyonel Uçlar",
            "",
            "| Metot | Yol | Açıklama |",
            "|---|---|---|",
            "| `GET` | `/health/live` | Sürecin çalıştığını kontrol eder. |",
            "| `GET` | `/health/ready` | Veritabanı ve Redis erişimini kontrol eder. |",
            "| `GET` | `/health` | Hazırlık kontrolünün uyumluluk adresidir. |",
            "| `GET` | `/metrics` | Etkinse Prometheus metni döndürür; OpenAPI dışında tutulur. |",
            "",
            "## Model Şemaları",
            "",
        ]
    )
    for name, schema in components.items():
        lines.extend([f'<a id="schema-{slug(name)}"></a>', f"### `{name}`", ""])
        if schema.get("description"):
            lines.extend([schema["description"], ""])
        if schema.get("enum"):
            lines.extend(
                [
                    f"- **Tip:** `{schema_type(schema)}`",
                    "- **Değerler:** "
                    + ", ".join(f"`{value}`" for value in schema["enum"]),
                    "",
                ]
            )
            continue
        properties = schema.get("properties", {})
        if not properties:
            lines.extend([f"- **Tip:** `{schema_type(schema)}`", ""])
            continue
        required = set(schema.get("required", []))
        lines.extend(
            [
                "| Alan | Tip | Zorunlu | Kurallar / Açıklama |",
                "|---|---|---|---|",
            ]
        )
        for field, field_schema in properties.items():
            lines.append(
                f"| `{field}` | {schema_type(field_schema, markdown=True)} | "
                f"{'Evet' if field in required else 'Hayır'} | {constraints(field_schema)} |"
            )
        lines.append("")
    return "\n".join(lines).rstrip() + "\n"


def html_table(headers: list[str], rows: list[list[str]]) -> str:
    head = "".join(f"<th>{html.escape(value)}</th>" for value in headers)
    body = "".join(
        "<tr>" + "".join(f"<td>{value}</td>" for value in row) + "</tr>" for row in rows
    )
    return f'<div class="table-wrap"><table><thead><tr>{head}</tr></thead><tbody>{body}</tbody></table></div>'


def render_html(spec: dict[str, Any]) -> str:
    operations = iter_operations(spec)
    grouped = group_operations(operations)
    components = spec.get("components", {}).get("schemas", {})
    nav = [
        '<a href="#genel">Genel bakış</a>',
        '<a href="#hatalar">Hata cevapları</a>',
        '<a href="#operasyonel">Operasyonel uçlar</a>',
    ]
    endpoint_sections: list[str] = []
    for tag, tag_operations in grouped.items():
        tag_id = "grup-" + slug(tag)
        nav.append(
            f'<a href="#{tag_id}">{html.escape(tag)} <span>{len(tag_operations)}</span></a>'
        )
        cards: list[str] = []
        for method, path, operation in tag_operations:
            endpoint_id = "endpoint-" + slug(
                operation.get("operationId", method + path)
            )
            parameter_rows: list[list[str]] = []
            for parameter in operation.get("parameters", []):
                parameter_schema = parameter.get("schema", {})
                parameter_rows.append(
                    [
                        f"<code>{html.escape(parameter['name'])}</code>",
                        html.escape(parameter["in"]),
                        html.escape(schema_type(parameter_schema)),
                        "Evet" if parameter.get("required") else "Hayır",
                        html.escape(constraints(parameter_schema)),
                    ]
                )
            parameters_html = ""
            if parameter_rows:
                parameters_html = "<h4>Parametreler</h4>" + html_table(
                    ["Ad", "Konum", "Tip", "Zorunlu", "Kurallar"], parameter_rows
                )
            request_html = ""
            request_body = operation.get("requestBody")
            if request_body:
                selected = pick_schema(request_body.get("content", {}))
                if selected:
                    content_type, body_schema = selected
                    body_example = json.dumps(
                        example_for_schema(body_schema, components),
                        ensure_ascii=False,
                        indent=2,
                    )
                    request_html = (
                        "<h4>Request body</h4>"
                        f"<p><code>{html.escape(content_type)}</code> · "
                        f'<a href="#schema-{slug(ref_name(body_schema) or "")}">'
                        f"{html.escape(schema_type(body_schema))}</a></p>"
                        f'<div class="code"><button onclick="copyCode(this)">Kopyala</button>'
                        f"<pre>{html.escape(body_example)}</pre></div>"
                    )
            response_table = html_table(
                ["HTTP", "Açıklama", "Gövde"],
                [
                    [
                        f"<code>{html.escape(status_code)}</code>",
                        html.escape(description),
                        html.escape(response_type),
                    ]
                    for status_code, description, response_type in response_rows(
                        operation, markdown=False
                    )
                ],
            )
            response_example_html = ""
            response_example = success_response_example(operation, components)
            if response_example:
                status_code, example = response_example
                response_json = json.dumps(example, ensure_ascii=False, indent=2)
                response_example_html = (
                    f"<h4>Örnek başarılı yanıt ({html.escape(status_code)})</h4>"
                    '<div class="code"><button onclick="copyCode(this)">Kopyala</button>'
                    f"<pre>{html.escape(response_json)}</pre></div>"
                )
            curl = curl_example(method, path, operation, components)
            search_text = " ".join(
                [
                    tag,
                    method,
                    path,
                    operation.get("summary", ""),
                    access_label(method, path, operation),
                ]
            )
            cards.append(
                f"""
<article class="endpoint-card" id="{endpoint_id}" data-search="{html.escape(search_text.casefold())}">
  <div class="endpoint-head">
    <span class="method {method.casefold()}">{method}</span>
    <code class="path">{html.escape(path)}</code>
    <span class="access">{html.escape(access_label(method, path, operation))}</span>
  </div>
  <h3>{html.escape(operation.get("summary", ""))}</h3>
  <p class="operation-id">Operation ID: <code>{html.escape(operation.get("operationId", "—"))}</code></p>
  <details>
    <summary>Request, response ve örnekleri göster</summary>
    {parameters_html}
    {request_html}
    <h4>Yanıtlar</h4>
    {response_table}
    {response_example_html}
    <h4>cURL</h4>
    <div class="code"><button onclick="copyCode(this)">Kopyala</button><pre>{html.escape(curl)}</pre></div>
  </details>
</article>"""
            )
        endpoint_sections.append(
            f'<section id="{tag_id}"><div class="section-title"><h2>{html.escape(tag)}</h2>'
            f"<span>{len(tag_operations)} işlem</span></div>{''.join(cards)}</section>"
        )

    schema_cards: list[str] = []
    for name, schema in components.items():
        search_text = name + " " + " ".join(schema.get("properties", {}).keys())
        if schema.get("enum"):
            content = (
                f"<p><strong>Tip:</strong> <code>{html.escape(schema_type(schema))}</code></p>"
                '<div class="enum-list">'
                + "".join(
                    f"<code>{html.escape(str(value))}</code>"
                    for value in schema["enum"]
                )
                + "</div>"
            )
        else:
            rows = []
            required = set(schema.get("required", []))
            for field, field_schema in schema.get("properties", {}).items():
                rows.append(
                    [
                        f"<code>{html.escape(field)}</code>",
                        html.escape(schema_type(field_schema)),
                        "Evet" if field in required else "Hayır",
                        html.escape(constraints(field_schema)),
                    ]
                )
            content = (
                html_table(["Alan", "Tip", "Zorunlu", "Kurallar"], rows)
                if rows
                else "<p>Alanı olmayan şema.</p>"
            )
        schema_cards.append(
            f"""
<details class="schema-card" id="schema-{slug(name)}" data-search="{html.escape(search_text.casefold())}">
  <summary><code>{html.escape(name)}</code></summary>
  {content}
</details>"""
        )

    template = """<!doctype html>
<html lang="tr">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Tarla Asistanı API Dokümantasyonu</title>
  <style>
    :root { --bg:#f3f6f2;--panel:#fff;--text:#172019;--muted:#637066;--line:#dbe4da;--brand:#1f6a38;--soft:#e8f3ea;--code:#111815;--get:#0f766e;--post:#2563eb;--put:#7c3aed;--patch:#b45309;--delete:#b91c1c }
    *{box-sizing:border-box}html{scroll-behavior:smooth}body{margin:0;background:var(--bg);color:var(--text);font:15px/1.55 Inter,ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif}
    header{background:linear-gradient(135deg,#12331d,#255d36);color:#fff;padding:38px max(24px,calc((100vw - 1440px)/2));border-bottom:4px solid #7fb083}header h1{margin:0 0 8px;font-size:clamp(30px,4vw,46px)}header p{max-width:900px;margin:0;color:#e1efe3}.stats{display:flex;flex-wrap:wrap;gap:10px;margin-top:20px}.stats span{background:#ffffff18;border:1px solid #ffffff32;border-radius:999px;padding:6px 12px}
    main{max-width:1440px;margin:auto;padding:24px;display:grid;grid-template-columns:290px minmax(0,1fr);gap:24px}aside{position:sticky;top:16px;align-self:start;max-height:calc(100vh - 32px);overflow:auto;background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:14px}aside input{width:100%;padding:11px 12px;border:1px solid var(--line);border-radius:8px;margin-bottom:10px;font:inherit}aside a{display:flex;justify-content:space-between;color:var(--text);text-decoration:none;padding:7px 8px;border-radius:7px;font-size:13px}aside a:hover{background:var(--soft);color:var(--brand)}aside a span{color:var(--muted)}
    section{background:var(--panel);border:1px solid var(--line);border-radius:12px;padding:24px;margin-bottom:20px}.section-title{display:flex;align-items:center;justify-content:space-between;gap:12px}.section-title h2,h2{margin:0 0 16px;color:#173d24}.section-title span,.pill{color:var(--brand);background:var(--soft);border-radius:999px;padding:4px 10px;font-size:13px}h3{margin:10px 0 4px;font-size:18px}h4{margin:20px 0 8px;color:#284831}p{margin:7px 0 14px}.muted,.operation-id{color:var(--muted);font-size:13px}
    .endpoint-card{border:1px solid var(--line);border-radius:10px;padding:15px;margin:13px 0;background:#fcfdfc}.endpoint-head{display:flex;align-items:center;gap:9px;flex-wrap:wrap}.method{color:#fff;border-radius:5px;padding:3px 8px;font:bold 12px ui-monospace,monospace}.get{background:var(--get)}.post{background:var(--post)}.put{background:var(--put)}.patch{background:var(--patch)}.delete{background:var(--delete)}.path{font-size:14px;font-weight:650}.access{margin-left:auto;color:var(--muted);font-size:12px;border:1px solid var(--line);border-radius:999px;padding:3px 8px}
    details>summary{cursor:pointer;font-weight:650;color:var(--brand);padding:6px 0}.schema-card{border:1px solid var(--line);border-radius:9px;margin:10px 0;padding:8px 12px}.schema-card>summary{font-size:15px}.enum-list{display:flex;gap:7px;flex-wrap:wrap}.enum-list code{background:var(--soft);padding:3px 7px;border-radius:5px}
    code,pre{font-family:ui-monospace,SFMono-Regular,Consolas,monospace}.code{position:relative}.code button{position:absolute;right:8px;top:8px;border:0;border-radius:6px;padding:5px 8px;cursor:pointer;background:#eef4ef}.code pre{background:var(--code);color:#eaf3eb;padding:16px;padding-top:38px;border-radius:9px;overflow:auto;font-size:12px;line-height:1.5}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;font-size:13px;margin:10px 0}th,td{border:1px solid var(--line);padding:8px 9px;text-align:left;vertical-align:top}th{background:var(--soft)}.notice{border-left:4px solid var(--brand);background:var(--soft);padding:12px 14px;border-radius:6px}.links{display:flex;gap:10px;flex-wrap:wrap}.links a{background:var(--brand);color:#fff;text-decoration:none;border-radius:7px;padding:8px 11px}.hidden{display:none!important}
    @media(max-width:900px){main{grid-template-columns:1fr;padding:14px}aside{position:static;max-height:none}section{padding:17px}.access{margin-left:0}.endpoint-head{align-items:flex-start}}
  </style>
</head>
<body>
<header>
  <h1>Tarla Asistanı API Dokümantasyonu</h1>
  <p>Projenin tüm public API uçları, istek ve yanıt gövdeleri, parametreleri, roller ve model şemaları. Doküman backend kodundaki gerçek OpenAPI tanımından üretilmiştir.</p>
  <div class="stats"><span>__OPERATIONS__ işlem</span><span>__PATHS__ yol</span><span>__SCHEMAS__ şema</span><span>API v__VERSION__</span></div>
</header>
<main>
  <aside>
    <input id="search" type="search" placeholder="Endpoint veya şema ara…" aria-label="Dokümanda ara">
    __NAV__
    <a href="#semalar">Model şemaları <span>__SCHEMAS__</span></a>
  </aside>
  <div>
    <section id="genel">
      <h2>Genel Bakış</h2>
      <div class="links"><a href="openapi.json" download>OpenAPI JSON indir</a><a href="API_DOCUMENTATION.md">Markdown sürümünü aç</a></div>
      <p><strong>Yerel Docker adresi:</strong> <code>http://localhost:8100</code> · <strong>API öneki:</strong> <code>/api/v1</code></p>
      <p><strong>Kimlik doğrulama:</strong> Korunan uçlarda <code>Authorization: Bearer &lt;access_token&gt;</code>. Roller <code>FARMER</code> ve <code>AGRONOMIST</code>.</p>
      <p class="notice"><strong>Çevrimdışı güvenlik:</strong> Faaliyet ve vaka akışlarındaki <code>client_operation_id</code> tekrar gönderilen isteğin yeni kayıt üretmesini engeller.</p>
      <h3>Temel akış</h3>
      <ol><li><code>POST /api/v1/auth/request-otp</code> ile kod iste.</li><li><code>POST /api/v1/auth/verify-otp</code> ile token al.</li><li>Korunan isteklere Bearer token ekle.</li><li>Süre dolunca refresh tokenı bir kez kullanarak oturumu döndür.</li></ol>
    </section>
    __ENDPOINTS__
    <section id="hatalar"><h2>Ortak Hata Cevapları</h2>
      <div class="table-wrap"><table><thead><tr><th>HTTP</th><th>Anlam</th><th>Tipik neden</th></tr></thead><tbody>
      <tr><td><code>400</code></td><td>Bad Request</td><td>Geçersiz OTP veya istek</td></tr><tr><td><code>401</code></td><td>Unauthorized</td><td>Eksik ya da geçersiz oturum</td></tr><tr><td><code>403</code></td><td>Forbidden</td><td>Rol yetkisi yok</td></tr><tr><td><code>404</code></td><td>Not Found</td><td>Kaynak yok veya görünür değil</td></tr><tr><td><code>409</code></td><td>Conflict</td><td>Durum ya da tekilleştirme çakışması</td></tr><tr><td><code>413</code></td><td>Too Large</td><td>Medya boyut sınırı</td></tr><tr><td><code>415</code></td><td>Unsupported Media</td><td>Desteklenmeyen dosya tipi</td></tr><tr><td><code>422</code></td><td>Validation Error</td><td>Alan veya iş kuralı doğrulaması</td></tr><tr><td><code>429</code></td><td>Too Many Requests</td><td>OTP bekleme süresi</td></tr><tr><td><code>500</code></td><td>Server Error</td><td><code>request_id</code> ile izlenir</td></tr><tr><td><code>503</code></td><td>Unavailable</td><td>Hava servisi ve önbellek kullanılamıyor</td></tr>
      </tbody></table></div>
    </section>
    <section id="operasyonel"><h2>Operasyonel Uçlar</h2><p><code>GET /health/live</code> süreç durumunu; <code>GET /health/ready</code> veritabanı ve Redis hazırlığını döndürür. <code>GET /metrics</code> etkin olduğunda Prometheus metni üretir ve public OpenAPI şemasında yer almaz.</p></section>
    <section id="semalar"><div class="section-title"><h2>Model Şemaları</h2><span>__SCHEMAS__ şema</span></div>__SCHEMA_CARDS__</section>
  </div>
</main>
<script>
  const search = document.getElementById('search');
  search.addEventListener('input', () => {
    const query = search.value.toLocaleLowerCase('tr-TR').trim();
    document.querySelectorAll('[data-search]').forEach((item) => {
      item.classList.toggle('hidden', query && !item.dataset.search.includes(query));
    });
  });
  function copyCode(button) {
    const text = button.parentElement.querySelector('pre').innerText;
    navigator.clipboard.writeText(text).then(() => { button.textContent='Kopyalandı'; setTimeout(() => button.textContent='Kopyala', 1200); });
  }
</script>
</body>
</html>
"""
    replacements = {
        "__OPERATIONS__": str(len(operations)),
        "__PATHS__": str(len(spec["paths"])),
        "__SCHEMAS__": str(len(components)),
        "__VERSION__": html.escape(spec["info"].get("version", "0.1.0")),
        "__NAV__": "".join(nav),
        "__ENDPOINTS__": "".join(endpoint_sections),
        "__SCHEMA_CARDS__": "".join(schema_cards),
    }
    for marker, value in replacements.items():
        template = template.replace(marker, value)
    return "\n".join(line.rstrip() for line in template.splitlines()) + "\n"


def main() -> None:
    DOCS.mkdir(parents=True, exist_ok=True)
    spec = app.openapi()
    spec["servers"] = [
        {"url": LOCAL_BASE_URL, "description": "Yerel Docker ortamı"},
        {
            "url": "http://localhost:8000",
            "description": "Doğrudan backend geliştirme ortamı",
        },
    ]
    (DOCS / "openapi.json").write_text(
        json.dumps(spec, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    (DOCS / "API_DOCUMENTATION.md").write_text(render_markdown(spec), encoding="utf-8")
    (DOCS / "api-docs.html").write_text(render_html(spec), encoding="utf-8")
    operations = iter_operations(spec)
    schemas = spec.get("components", {}).get("schemas", {})
    print(
        f"Generated API documentation: {len(operations)} operations, "
        f"{len(spec['paths'])} paths, {len(schemas)} schemas."
    )


if __name__ == "__main__":
    main()
