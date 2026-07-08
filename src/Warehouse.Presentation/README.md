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

## Session 03 — DDD Refactor
Implemented architecture improvements:
- Separated the solution into Domain, Application, Infrastructure, and API layers
- Moved product and supplier rules into the domain model
- Added repository abstractions and MediatR-based use cases
- Kept controllers thin and focused on HTTP concerns
- Added basic domain tests for validation and business rules

## Layer Responsibilities
- Domain: business entities and core validation rules
- Application: CQRS-style commands and queries
- Infrastructure: in-memory repositories and persistence implementation details
- API: controllers, contracts, and HTTP responses

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

## Verification
- `dotnet build WarehouseManagement.sln`
- `dotnet test Warehouse.Domain.Tests/Warehouse.Domain.Tests.csproj`


