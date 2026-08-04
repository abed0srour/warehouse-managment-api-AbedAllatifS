# Warehouse Management API — Reference

> Generated from the code in `src/` as it exists on branch `session-10-generative-ai-backend`.
> Every route below was read out of a controller; nothing here is aspirational.

---

## 1. Overview

### What it does

A warehouse inventory REST API. It tracks **products** (SKU, price, stock level, expiry date,
optional supplier) and **suppliers**, exposes an **inventory dashboard** for low-stock reporting,
and stores **file attachments** in object storage. Authentication is delegated to Firebase; the
API reads a `role` claim from the Firebase JWT to separate admin writes from authenticated reads.

Two behaviours are worth knowing before you integrate:

- **Deletes are soft.** `DELETE /api/products/{id}` archives the row (`isArchived = true`). The
  product is still returned by `GET /api/products/{id}` with a `200` afterwards.
- **Suppliers are linked by name, not by ID, on create.** `POST /api/products` copies
  `supplierName` as free text and leaves `supplierId` null. Use
  `POST /api/products/{id}/assign-supplier/{supplierId}` to establish the actual relation.

### Tech stack

| Concern | Technology |
|---|---|
| Runtime | .NET 8 (ASP.NET Core, controller-based) |
| Mediation | MediatR — CQRS commands & queries |
| Persistence | EF Core 8 + Npgsql → PostgreSQL |
| Mapping | AutoMapper (`MappingProfile`, `EfMappingProfile`) |
| Cache | Redis via `IDistributedCache` (`StackExchangeRedisCache`) |
| Object storage | MinIO (S3-compatible) via `IFileStorageService` |
| Auth | Firebase JWT (`JwtBearer`, keys fetched from Google JWKS) |
| Background jobs | Hangfire (in-memory storage) |
| Logging | Serilog → console + rolling daily file in `Logs/` |
| Health | `AspNetCore.HealthChecks` + Health Checks UI |
| API docs | Swashbuckle / Swagger (Development only) |

### Running it

**Prerequisites**

- .NET 8 SDK
- PostgreSQL on `localhost:5433`, database `WarehouseDbCodeFirst`, user/password `postgres`/`postgres`
- Redis on `localhost:6379`
- MinIO on `localhost:9000` (`docker compose up -d` starts it — the compose file provides MinIO only,
  not Postgres or Redis)
- A Firebase service-account JSON at the path in `Firebase:ServiceAccountPath`
  (default `firebase-adminsdk.json`, resolved relative to the Presentation project)

All connection details live in `src/Warehouse.Presentation/appsettings.json`.

```bash
docker compose up -d                              # MinIO
dotnet run --project src/Warehouse.Presentation   # API
```

| Surface | URL |
|---|---|
| HTTP | `http://localhost:5205` |
| HTTPS | `https://localhost:7168` |
| Swagger UI | `http://localhost:5205/swagger` *(Development only)* |
| Health JSON | `http://localhost:5205/health` |
| Health dashboard | `http://localhost:5205/health-ui` |
| Hangfire dashboard | `http://localhost:5205/hangfire` |

**Tests**

```bash
dotnet test    # 292 tests: 206 unit, 81 integration, 5 domain
```

> Note: the integration tests run against the EF Core **InMemory** provider, not PostgreSQL, so
> they exercise handler logic but not SQL translation.

---

## 2. Conventions

### Authentication

Every request to a protected endpoint needs a Firebase ID token:

```http
Authorization: Bearer <firebase-id-token>
```

The token is validated against issuer `https://securetoken.google.com/{Firebase:ProjectId}` and
audience `{Firebase:ProjectId}`. `MapInboundClaims` is off, so claim names are passed through
unmapped, and the role claim type is `role`.

Two policies exist:

| Policy | Requirement | Used by |
|---|---|---|
| `AuthenticatedUser` | any valid token | all reads |
| `AdminOnly` | `role` claim == `admin` | all writes |

Missing or invalid token → `401 Unauthorized`. Valid token without the admin role on an
`AdminOnly` route → `403 Forbidden`.

### Request/response headers

| Header | Direction | Meaning |
|---|---|---|
| `X-Correlation-Id` | in / out | Echoed if supplied, generated otherwise. Appears as `traceId` in middleware errors. |
| `X-Response-Time-ms` | out | Server-side handling duration. |
| `Accept-Language` | in | `en` and `fr` are configured; `GET /api/products/server-time` additionally special-cases `fr-FR`, `ar-LB`, `en-US`. |

### JSON casing

Responses use ASP.NET Core's default **camelCase**. Note that request bodies are bound
case-insensitively, so `POST /api/products` accepts both `sku` and `SKU`.

### Error shapes — there are three

This is the most important thing to know when writing a client. The API does **not** emit one
consistent error envelope.

**(a) Controller-returned errors** — most `400`/`404`/`409` responses. A bare message object:

```json
{ "message": "Product with ID 3f2504e0-4f89-11d3-9a0c-0305e82c3301 was not found." }
```

**(b) Model-validation failures** — produced automatically by `[ApiController]` when a
`[Required]` / `[Range]` / `[EmailAddress]` annotation fails. RFC 7807 ProblemDetails:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "Price": ["Price must be greater than zero."]
  }
}
```

**(c) Unhandled exceptions** — caught by `ExceptionHandlingMiddleware`:

```json
{
  "errorCode": "ServerError",
  "message": "An unexpected error occurred.",
  "traceId": "8f1c2b7a-3e5d-4a19-9d2f-6b0c4e7a1f83"
}
```

`errorCode` is `NotFound` (404) for `NotFoundException`, `Conflict` (409) for
`BusinessRuleException`, and `ServerError` (500) otherwise. Only the 500 case masks the message;
the other two pass the exception text through.

---

## 3. Endpoint summary

### Products — `/api/products`

| Method | Route | Policy | Purpose |
|---|---|---|---|
| GET | `/api/products` | AuthenticatedUser | List products, optionally in-stock only |
| GET | `/api/products/{id}` | AuthenticatedUser | Fetch one product |
| GET | `/api/products/search` | AuthenticatedUser | Search by name and/or supplier |
| POST | `/api/products` | AdminOnly | Create a product |
| POST | `/api/products/{id}/quantity` | AdminOnly | Set stock level |
| POST | `/api/products/{id}/price` | AdminOnly | Set price |
| POST | `/api/products/{id}/image` | AdminOnly | Upload a product image to local disk |
| POST | `/api/products/{id}/assign-supplier/{supplierId}` | AdminOnly | Link product to a supplier |
| DELETE | `/api/products/{id}` | AdminOnly | Archive (soft delete) |
| GET | `/api/products/server-time` | AuthenticatedUser | Culture-formatted server clock |

### Suppliers — `/api/suppliers`

| Method | Route | Policy | Purpose |
|---|---|---|---|
| GET | `/api/suppliers` | AuthenticatedUser | List all suppliers |
| GET | `/api/suppliers/{id}` | AuthenticatedUser | Fetch one supplier |
| POST | `/api/suppliers` | AdminOnly | Create a supplier |
| DELETE | `/api/suppliers/{id}` | AdminOnly | Deactivate (`isActive = false`) |

### Inventory — `/api/inventory`

| Method | Route | Policy | Purpose |
|---|---|---|---|
| GET | `/api/inventory/dashboard` | AuthenticatedUser | Product count, supplier count, low-stock list |

### Files — `/api/files`

| Method | Route | Policy | Purpose |
|---|---|---|---|
| POST | `/api/files/upload` | AdminOnly | Upload any attachment to MinIO |
| GET | `/api/files/{id}/download` | AuthenticatedUser | Stream a stored file back |
| DELETE | `/api/files/{id}` | AdminOnly | Hard-delete from MinIO and the database |

### Admin — `/api/admin/users`

| Method | Route | Policy | Purpose |
|---|---|---|---|
| POST | `/api/admin/users/{uid}/role` | *(none — see note)* | Set a Firebase custom `role` claim |

> **This endpoint carries no `[Authorize]` attribute.** It guards itself by returning `404` when
> the host environment is not `Development`. In Development it is reachable **unauthenticated**,
> and it grants Firebase roles. Treat it as a local bootstrap tool, and never run this API in
> Development mode on a reachable host.

### Operational (not controllers)

| Route | Purpose |
|---|---|
| `GET /health` | Postgres + Redis health as JSON |
| `/health-ui` | Health dashboard |
| `/hangfire` | Hangfire job dashboard |
| `/swagger` | Swagger UI (Development only) |

---

## 4. Endpoint detail

### 4.1 `GET /api/products`

List products. `onlyAvailable=true` filters to non-archived items with `quantityInStock > 0`.
Results are ordered by `createdAt` descending and cached in Redis for 5 minutes under
`products:all:{onlyAvailable}`.

**Request**

```http
GET /api/products?onlyAvailable=true HTTP/1.1
Host: localhost:5205
Authorization: Bearer <firebase-id-token>
```

**200 OK**

```json
[
  {
    "id": "9c8f2b41-6a3e-4d15-b7c2-1e0a5f9d3e88",
    "name": "Wireless Mouse",
    "sku": "SKU-001",
    "description": "2.4 GHz optical mouse",
    "price": 19.99,
    "quantityInStock": 50,
    "supplierName": "Acme Corp",
    "expiryDate": null,
    "isArchived": false,
    "createdAt": "2026-01-15T09:30:00",
    "lastUpdatedAt": "0001-01-01T00:00:00"
  }
]
```

> `lastUpdatedAt` is `DateTime` (non-nullable) on the view model but nullable on the entity, so a
> never-updated product serialises as `0001-01-01T00:00:00` rather than `null`.

**401 Unauthorized** — token missing or invalid (empty body).

---

### 4.2 `GET /api/products/{id}`

**Request**

```http
GET /api/products/9c8f2b41-6a3e-4d15-b7c2-1e0a5f9d3e88 HTTP/1.1
Authorization: Bearer <firebase-id-token>
```

**200 OK** — a single `ProductViewModel` (shape as above).

**404 Not Found** — message is localised via `IStringLocalizer`:

```json
{ "message": "Product not found." }
```

---

### 4.3 `GET /api/products/search`

Case-insensitive substring match. `name` and `supplier` are ANDed when both are supplied. At
least one must be present.

**Request**

```http
GET /api/products/search?name=mouse&supplier=Acme HTTP/1.1
Authorization: Bearer <firebase-id-token>
```

**200 OK** — array of `ProductViewModel`; `[]` when nothing matches.

**400 Bad Request** — both parameters blank:

```json
{ "message": "At least one of 'name' or 'supplier' must be provided." }
```

> Matching uses `StringComparison.OrdinalIgnoreCase`, which performs no Unicode normalisation —
> searching `Cafe` will not find `Café`. Archived products **are** included; there is no filter
> to exclude them.

---

### 4.4 `POST /api/products`

Creates a product. Rejects a duplicate SKU (case-insensitive) and invalidates both
`products:all:*` cache entries.

**Request**

```http
POST /api/products HTTP/1.1
Authorization: Bearer <admin-firebase-id-token>
Content-Type: application/json

{
  "name": "Thermal Label Roll",
  "sku": "SKU-2291",
  "description": "4x6 direct thermal labels, 500/roll",
  "price": 24.50,
  "quantityInStock": 120,
  "supplierName": "Globex Supplies",
  "expiryDate": "2027-03-01T00:00:00"
}
```

**201 Created**

```http
Location: http://localhost:5205/api/Products/1d4e9a77-2c65-4b8f-a0d3-77b1e6c4f902
```

The body is the **domain `Product` entity**, not `ProductViewModel` — note the extra
`supplierId` and `supplier` fields and the nullable `lastUpdatedAt`:

```json
{
  "id": "1d4e9a77-2c65-4b8f-a0d3-77b1e6c4f902",
  "name": "Thermal Label Roll",
  "sku": "SKU-2291",
  "description": "4x6 direct thermal labels, 500/roll",
  "price": 24.50,
  "quantityInStock": 120,
  "supplierName": "Globex Supplies",
  "supplierId": null,
  "expiryDate": "2027-03-01T00:00:00",
  "isArchived": false,
  "createdAt": "2026-08-04T11:42:07.331",
  "lastUpdatedAt": null,
  "supplier": null
}
```

> Two quirks worth pinning: the `Location` header uses the `[controller]` route token and so
> reads `/api/**P**roducts/...` with a capital P, unlike every hand-written lowercase URL in this
> codebase; and `supplierId` stays `null` even when a supplier named `Globex Supplies` exists.

**409 Conflict** — duplicate SKU. Raised as `BusinessRuleException`, so this one comes from the
middleware and uses the three-field envelope:

```json
{
  "errorCode": "Conflict",
  "message": "A product with SKU 'SKU-2291' already exists.",
  "traceId": "8f1c2b7a-3e5d-4a19-9d2f-6b0c4e7a1f83"
}
```

**400 Bad Request** — annotation failure (ProblemDetails), or a domain rule rejection from
`Product.Create` such as `{ "message": "Price must be greater than zero." }`.

---

### 4.5 `POST /api/products/{id}/quantity`

**Request**

```http
POST /api/products/1d4e9a77-2c65-4b8f-a0d3-77b1e6c4f902/quantity HTTP/1.1
Authorization: Bearer <admin-firebase-id-token>
Content-Type: application/json

{ "quantityInStock": 85 }
```

**200 OK** — the updated domain `Product`.

**409 Conflict** — the product is archived:

```json
{ "message": "Archived products cannot be updated." }
```

**404 Not Found** — no such product. **400 Bad Request** — negative quantity.

---

### 4.6 `POST /api/products/{id}/price`

Identical in shape to the quantity endpoint. Logs an information-level entry recording the old
and new price.

**Request**

```http
POST /api/products/1d4e9a77-2c65-4b8f-a0d3-77b1e6c4f902/price HTTP/1.1
Authorization: Bearer <admin-firebase-id-token>
Content-Type: application/json

{ "price": 27.95 }
```

**200 OK** — the updated domain `Product`.

**400 Bad Request** — `price` of `0` or less fails the `[Range(0.01, …)]` annotation:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "Price": ["Price must be greater than zero."] }
}
```

**409 Conflict** — archived product. **404 Not Found** — no such product.

---

### 4.7 `POST /api/products/{id}/image`

`multipart/form-data`, form field name `file`. Limits: **2 MB**, extensions `.jpg`, `.jpeg`,
`.png`. The file is written to `wwwroot/uploads/{productId}{ext}` on the **API server's local
disk** — this endpoint does not use MinIO, and it writes no database row.

**Request**

```http
POST /api/products/1d4e9a77-2c65-4b8f-a0d3-77b1e6c4f902/image HTTP/1.1
Authorization: Bearer <admin-firebase-id-token>
Content-Type: multipart/form-data; boundary=----Boundary7MA4YWxkTrZu0gW

------Boundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="file"; filename="label-roll.png"
Content-Type: image/png

<binary png bytes>
------Boundary7MA4YWxkTrZu0gW--
```

**200 OK**

```json
{
  "message": "Image uploaded successfully.",
  "path": "/uploads/1d4e9a77-2c65-4b8f-a0d3-77b1e6c4f902.png"
}
```

The file is then served statically from that path.

**400 Bad Request** — one of:

```json
{ "message": "Only .jpg and .png files are allowed." }
```
```json
{ "message": "File size cannot exceed 2 MB." }
```
```json
{ "message": "A file is required." }
```

**404 Not Found** — product does not exist. Note the existence check does **not** consider
`isArchived`, so images can be attached to archived products.

> Validation is by **file extension only**. The multipart `Content-Type` is never inspected and
> the bytes are never decoded, so any file renamed to `.jpg` is accepted and stored verbatim.

---

### 4.8 `POST /api/products/{id}/assign-supplier/{supplierId}`

Sets `supplierId`, `supplierName` and the `supplier` navigation from an existing supplier row.

**Request**

```http
POST /api/products/1d4e9a77-2c65-4b8f-a0d3-77b1e6c4f902/assign-supplier/4b7d1e93-8c02-4a56-9f31-2d6e8b0a7c14 HTTP/1.1
Authorization: Bearer <admin-firebase-id-token>
```

**200 OK** — the updated domain `Product`, now with `supplierId` populated.

**409 Conflict** — the domain refuses the assignment:

```json
{ "message": "Inactive suppliers cannot be assigned to products." }
```

The other domain rejection is `"Archived products cannot be updated."`.

**404 Not Found** — either the product or the supplier is missing. Raised as `NotFoundException`,
so this one uses the middleware envelope:

```json
{
  "errorCode": "NotFound",
  "message": "Supplier with ID 4b7d1e93-8c02-4a56-9f31-2d6e8b0a7c14 was not found.",
  "traceId": "8f1c2b7a-3e5d-4a19-9d2f-6b0c4e7a1f83"
}
```

---

### 4.9 `DELETE /api/products/{id}`

**Soft delete.** Sets `isArchived = true` and stamps `lastUpdatedAt`; no row is removed.

**Request**

```http
DELETE /api/products/1d4e9a77-2c65-4b8f-a0d3-77b1e6c4f902 HTTP/1.1
Authorization: Bearer <admin-firebase-id-token>
```

**204 No Content** — empty body.

**404 Not Found**

```json
{ "message": "Product with ID 1d4e9a77-2c65-4b8f-a0d3-77b1e6c4f902 was not found." }
```

> Nothing cascades. Any uploaded image file, and any related `Productimages` row, outlives the
> archived product and is never cleaned up. The call is also idempotent-but-silent: archiving an
> already-archived product returns `204` again and overwrites `lastUpdatedAt`, so that timestamp
> reflects the most recent call rather than when the product was first archived.

---

### 4.10 `GET /api/products/server-time`

Returns the UTC clock formatted for a culture chosen from the first `Accept-Language` entry.
Recognised values are `fr-FR`, `ar-LB` and `en-US`; anything else falls back to `en-US`.

**Request**

```http
GET /api/products/server-time HTTP/1.1
Authorization: Bearer <firebase-id-token>
Accept-Language: fr-FR
```

**200 OK**

```json
{
  "serverTimeUtc": "mardi 4 août 2026 11:42:07",
  "culture": "fr-FR"
}
```

---

### 4.11 `GET /api/suppliers`

**Request**

```http
GET /api/suppliers HTTP/1.1
Authorization: Bearer <firebase-id-token>
```

**200 OK**

```json
[
  {
    "id": "4b7d1e93-8c02-4a56-9f31-2d6e8b0a7c14",
    "name": "Acme Corp",
    "country": "USA",
    "contactEmail": "contact@acme.com",
    "phoneNumber": "555-0100",
    "isActive": true
  }
]
```

Deactivated suppliers are included, distinguished by `isActive: false`.

---

### 4.12 `GET /api/suppliers/{id}`

**200 OK** — a single `SupplierViewModel`.

**404 Not Found**

```json
{ "message": "Supplier with ID 4b7d1e93-8c02-4a56-9f31-2d6e8b0a7c14 was not found." }
```

---

### 4.13 `POST /api/suppliers`

**Request**

```http
POST /api/suppliers HTTP/1.1
Authorization: Bearer <admin-firebase-id-token>
Content-Type: application/json

{
  "name": "Initech Logistics",
  "country": "Canada",
  "contactEmail": "orders@initech.example",
  "phoneNumber": "+1-555-0142"
}
```

**201 Created**

```http
Location: http://localhost:5205/api/Suppliers/6e2a0c58-9b41-4d73-8e5a-3f9c1b2d4e60
```
```json
{
  "id": "6e2a0c58-9b41-4d73-8e5a-3f9c1b2d4e60",
  "name": "Initech Logistics",
  "country": "Canada",
  "contactEmail": "orders@initech.example",
  "phoneNumber": "+1-555-0142",
  "isActive": true
}
```

**400 Bad Request** — invalid email or phone fails annotations (ProblemDetails):

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": { "ContactEmail": ["Invalid email address format."] }
}
```

A blank `name` that survives binding is rejected by the handler as
`{ "message": "Supplier name is required." }`.

---

### 4.14 `DELETE /api/suppliers/{id}`

**Deactivation, not deletion.** Sets `isActive = false`; the row remains and is still returned
by both supplier reads.

**204 No Content** on success.

**404 Not Found**

```json
{ "message": "Supplier with ID 6e2a0c58-9b41-4d73-8e5a-3f9c1b2d4e60 was not found." }
```

> Products already referencing the supplier keep their `supplierId`. Only *new* assignments are
> blocked, by the domain rule in `Product.AssignSupplier`.

---

### 4.15 `GET /api/inventory/dashboard`

Product count, supplier count, and every non-archived product below the low-stock threshold
(default `10`).

**Request**

```http
GET /api/inventory/dashboard HTTP/1.1
Authorization: Bearer <firebase-id-token>
```

**200 OK**

```json
{
  "totalProducts": 214,
  "lowStockProducts": [
    {
      "id": "9c8f2b41-6a3e-4d15-b7c2-1e0a5f9d3e88",
      "name": "Wireless Mouse",
      "quantityInStock": 3
    }
  ],
  "totalSuppliers": 12
}
```

> `totalProducts` counts **all** products including archived ones, while `lowStockProducts`
> excludes archived. The two figures are drawn from different filters.
>
> `GetInventoryDashboardQuery` accepts a `LowStockThreshold` parameter, but the controller always
> constructs it with no arguments, so the threshold is fixed at `10` over HTTP.

---

### 4.16 `POST /api/files/upload`

`multipart/form-data`. Stores the payload in MinIO and records a row. Limits: **10 MB**, and the
`Content-Type` must be one of image (`jpeg`, `png`, `gif`, `webp`), PDF, Word, Excel, `text/plain`
or `text/csv`.

Form fields: `file`, `relatedEntityType` (string), `relatedEntityId` (int).

**Request**

```http
POST /api/files/upload HTTP/1.1
Authorization: Bearer <admin-firebase-id-token>
Content-Type: multipart/form-data; boundary=----Boundary7MA4YWxkTrZu0gW

------Boundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="file"; filename="packing-list.pdf"
Content-Type: application/pdf

<binary pdf bytes>
------Boundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="relatedEntityType"

Product
------Boundary7MA4YWxkTrZu0gW
Content-Disposition: form-data; name="relatedEntityId"

42
------Boundary7MA4YWxkTrZu0gW--
```

**201 Created**

```json
{
  "id": "b5d3f1a9-7c48-4e26-9a03-5f2e8c1d6b47",
  "objectKey": "e07c4a12-9d38-4b6f-8c25-1a9e3f7d0b64-packing-list.pdf",
  "fileName": "packing-list.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 184320,
  "uploadedByUid": "kR3nQ9xT2bYc5vM8hJ1wZ0pL4aE2",
  "relatedEntityType": "Product",
  "relatedEntityId": 42,
  "uploadedAtUtc": "2026-08-04T11:42:07.331Z"
}
```

**400 Bad Request**

```json
{ "message": "Only image and common document file types are allowed." }
```

**401 Unauthorized** — token valid but carries neither a `user_id` nor a `sub` claim:

```json
{ "message": "Unable to determine the authenticated user's UID." }
```

> The stored `objectKey` is `{new Guid}-{original filename}` — the GUID is generated by the
> storage service and is **not** the file's `id`, so the two differ.
>
> `relatedEntityId` is an `int`, but every entity in this API is keyed by `Guid`. The association
> it records cannot actually address a product or supplier.

---

### 4.17 `GET /api/files/{id}/download`

**Request**

```http
GET /api/files/b5d3f1a9-7c48-4e26-9a03-5f2e8c1d6b47/download HTTP/1.1
Authorization: Bearer <firebase-id-token>
```

**200 OK** — the raw bytes, with the stored content type and original filename:

```http
Content-Type: application/pdf
Content-Disposition: attachment; filename=packing-list.pdf
```

**404 Not Found**

```json
{ "message": "File with ID b5d3f1a9-7c48-4e26-9a03-5f2e8c1d6b47 was not found." }
```

---

### 4.18 `DELETE /api/files/{id}`

A genuine hard delete — removes the MinIO object *and* the database row. Unlike products and
suppliers, nothing is preserved.

**204 No Content** on success.

**404 Not Found** — same shape as the download endpoint.

---

### 4.19 `POST /api/admin/users/{uid}/role`

Sets a Firebase custom claim so the user's next token carries a role. **Development only** — in
any other environment this returns `404`. See the warning in §3.

**Request**

```http
POST /api/admin/users/kR3nQ9xT2bYc5vM8hJ1wZ0pL4aE2/role HTTP/1.1
Content-Type: application/json

{ "role": "admin" }
```

**200 OK**

```json
{ "uid": "kR3nQ9xT2bYc5vM8hJ1wZ0pL4aE2", "role": "admin" }
```

**400 Bad Request** — unknown UID, or a blank role:

```json
{ "message": "Unable to set role for user 'kR3nQ9xT2bYc5vM8hJ1wZ0pL4aE2'. Verify the UID is correct." }
```

> The claim only takes effect once the client obtains a **fresh** ID token. Existing tokens keep
> the old role until they expire.

---

## 5. Architecture notes

### Project structure

```
src/
├── Warehouse.Domain/          entities + invariants; no dependencies
├── Warehouse.Application/     CQRS commands/queries, DTOs, AutoMapper, background jobs
├── Warehouse.Infrastructure/  EF Core, repositories, MinIO, DB-backed query handlers
└── Warehouse.Presentation/    controllers, contracts, middleware, filters, composition root
tests/
├── Warehouse.Domain.Tests/          5 tests
├── Warehouse.Api.UnitTests/       206 tests
└── Warehouse.Api.IntegrationTests/ 81 tests (WebApplicationFactory + InMemory EF)
```

Dependencies point inward: Presentation → Infrastructure → Application → Domain.

### CQRS with MediatR

There is **no service layer**. Controllers depend only on `IMediator` and translate the result
into an HTTP response; all logic lives in handlers.

```
Controller → IMediator.Send(Query|Command) → Handler → Repository|DbContext → Domain
```

MediatR scans **two** assemblies, because handlers live in two places:

```csharp
cfg.RegisterServicesFromAssembly(typeof(CreateProductCommand).Assembly);   // Application
cfg.RegisterServicesFromAssembly(typeof(WarehouseDbContext).Assembly);     // Infrastructure
```

**Application-layer handlers** depend on `IProductRepository` / `ISupplierRepository`, so they are
mockable and unit-tested with Moq.

**Infrastructure-layer handlers** (`src/Warehouse.Infrastructure/Queries/`) take `WarehouseDbContext`
directly to use LINQ features the repository interface does not expose — grouping, paging,
`ProjectTo`. They cannot be mocked, so they are tested against the EF InMemory provider.

### Key design decisions

**Domain invariants live in the entity.** `Product.Create` validates name, SKU, price and
quantity; `UpdatePrice`, `UpdateQuantity` and `AssignSupplier` all refuse to mutate an archived
product. Handlers orchestrate, the entity enforces.

**Two result conventions coexist.** Newer handlers return `Result<T>` with an `ErrorType`
(`NotFound` / `Validation` / `Conflict`) that the controller maps to a status code. Older ones
throw `NotFoundException` / `BusinessRuleException` and let the middleware map them, or simply
return `null`. This is the direct cause of the three error shapes documented in §2.

**Deletion is archival.** Products archive, suppliers deactivate. Only files hard-delete.

**Caching is hand-rolled and manually invalidated.** `GetAllProductsQuery` and
`GetProductByIdQuery` read/write Redis with a 5-minute TTL under `products:all:{bool}` and
`products:{id}`. Five command handlers then invalidate those keys with hardcoded string literals
— 16 occurrences across 7 files. There is no cache abstraction and no pipeline behaviour, so
changing the key scheme means editing seven files, and a cache outage fails the request rather
than falling through to the database.

**Cross-cutting concerns are middleware and filters**, ordered in `Program.cs` as
`CorrelationId → ExceptionHandling → RequestTiming → RequestLocalization → HttpsRedirection →
StaticFiles → Authentication → Authorization`.

**Two projection strategies exist for the same view model.** Most query handlers use AutoMapper
(`ProjectTo<ProductViewModel>`), but the two grouping handlers share a hand-written projection in
`ProductProjections`. They disagree deliberately: the hand-written one maps a null `lastUpdatedAt`
to `createdAt` and a null `description` to `""`, where AutoMapper yields `0001-01-01` and `null`.
Both behaviours are pinned by tests.

**A daily Hangfire job** (`ProductExpiryCheckJob`, `Cron.Daily`) logs products already expired or
expiring within 30 days. It only writes to the log — it exposes no endpoint and changes no data.

### Five query handlers are registered but unreachable

These are wired into MediatR and fully unit-tested, but **no controller sends them**, so they
have no HTTP surface:

| Handler | Would provide |
|---|---|
| `GetPagedProductsQueryHandler` | server-side pagination |
| `GetProductsBySupplierQueryHandler` | filter by supplier with sort order |
| `GetProductsGroupedByExpiryYearQueryHandler` | products grouped by expiry year |
| `GetProductsGroupedByExpiryAndCountryQueryHandler` | grouped by expiry year + supplier country |
| `GetTotalProductCountQueryHandler` | total product count |

Adding a controller action that sends the query is all that is needed to expose any of them.

### Known issues

Documented here because a client will observe them; each is pinned by a passing test.

| Issue | Effect |
|---|---|
| Paging arithmetic overflows | `Skip((PageNumber - 1) * PageSize)` wraps at large page numbers; `int.MaxValue` silently returns page 1. Currently unreachable over HTTP. |
| Search crashes on a NULL product name | The name predicate dereferences `p.Name` without the null guard the supplier predicate has. |
| Zero-length Redis entry is fatal | Only a `null` byte array counts as a cache miss; an empty entry reaches the deserialiser and returns 500 instead of falling through. |
| `lastUpdatedAt` = `0001-01-01` | Non-nullable on the view model. A client east of Greenwich that applies its own UTC offset to that value throws rather than rendering. |
| Cancellation not observed | Handlers pass the token to the repository, then filter and map to completion regardless. |
| Product image uploads leak | Files are never removed, including when the product is archived. |

---

*Generated 2026-08-04 against branch `session-10-generative-ai-backend`.*
