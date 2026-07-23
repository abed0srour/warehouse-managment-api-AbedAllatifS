# Warehouse Management API

This is an ASP.NET Core Web API built for tracking products and managing inventory data.

## Getting Started
1. Clone the repository.
2. Run `dotnet build` to restore dependencies.
3. Run `dotnet run` to launch the development server.
4. View OpenAPI documentation at `/openapi/v1.json` or use tools like Swagger UI, Postman, or VS Code REST Client to interact with the API.

## Session 02 — Inventory API Implementation
Implemented features:
- Product CRUD operations with in-memory data storage
- Search products by name and supplier (partial, case-insensitive)
- Optional available-only product filtering
- Soft delete products via `IsArchived`
- Product image upload with JPG/PNG and 2 MB limit
- Supplier endpoints and assign supplier to a product
- Server time endpoint with language-aware formatting
- Swagger UI for interactive API documentation

## API Endpoints
- `GET /api/products`
- `GET /api/products?onlyAvailable=true`
- `GET /api/products/{id}`
- `GET /api/products/search?name=...&supplier=...`
- `POST /api/products`
- `POST /api/products/{id}/quantity`
- `POST /api/products/{id}/price`
- `POST /api/products/{id}/image`
- `DELETE /api/products/{id}`
- `GET /api/products/server-time`
- `POST /api/products/{id}/assign-supplier/{supplierId}`
- `GET /api/suppliers`
- `GET /api/suppliers/{id}`
- `POST /api/suppliers`
- `DELETE /api/suppliers/{id}`

## Session 07 — Firebase Auth & MinIO Storage
- MinIO object storage for warehouse file attachments (`docker-compose.yml` runs a local MinIO
  instance on ports 9000/9001).
- `POST /api/files/upload`, `GET /api/files/{id}/download`, `DELETE /api/files/{id}`.
- **TODO before any non-local deployment**: `MinIO:AccessKey` and `MinIO:SecretKey` in
  `appsettings.json` are placeholder local-dev credentials. Move them to user-secrets
  (`dotnet user-secrets set "MinIO:AccessKey" "..."`) or environment variables before this goes
  anywhere near a shared or production environment — do not commit real credentials.
