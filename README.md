# Limita - Digital Banking REST API

A modern, production-grade Digital Banking REST API built with **.NET 9 (net9.0)** and **C# 13** following **Lean Clean Architecture**.

---

## 🏛 Solution Architecture

The solution adheres strictly to Clean Architecture principles with zero external NuGet packages in the Domain layer:

```text
Limita.sln
├── src/
│   ├── Limita.Domain/            # Pure domain layer (Entities, Enums, BaseEntity) - ZERO external packages
│   ├── Limita.Application/       # Core business contracts, ApiResponse wrapper, interfaces, validators
│   ├── Limita.Persistence/       # EF Core 9, SQL Server, IdentityDbContext, Fluent Configurations, Seed
│   ├── Limita.Infrastructure/    # JWT token generator, CurrentUserService (IHttpContextAccessor)
│   └── Limita.API/               # Controllers, Middlewares, DI extensions, Swagger, Program.cs
```

### Dependency Rules:
- `Limita.Domain`: Pure C# (no project or external package dependencies).
- `Limita.Application`: References `Limita.Domain`.
- `Limita.Persistence`: References `Limita.Application`, `Limita.Domain`.
- `Limita.Infrastructure`: References `Limita.Application`, `Limita.Domain`.
- `Limita.API`: References `Limita.Application`, `Limita.Persistence`, `Limita.Infrastructure`.

---

## 🔑 Key Features & Sprint 0 Tasks (FND-01 – FND-10)

| Task ID | Component | Description |
| :--- | :--- | :--- |
| **FND-01** | **Project Scaffolding** | .NET 9 & C# 13 multi-project solution structure conforming to Lean Clean Architecture. |
| **FND-02** | **Domain Entities & Enums** | BaseEntity with Guid primary keys (`Guid.NewGuid()`), audit timestamps, and rich banking domain entities. |
| **FND-03** | **Application Abstractions** | Standard `ApiResponse<T>` wrapper, `IApplicationDbContext`, `ICurrentUserService`, and `IJwtTokenGenerator`. |
| **FND-04** | **Persistence & EF Core 9** | `LimitaDbContext` inheriting from `IdentityDbContext<ApplicationUser>`, entity configurations with decimal precision (`18, 2`), unique indexes, and `DeleteBehavior.Restrict`. |
| **FND-05** | **Database Seeding** | `DbInitializer` for automatic seeding of roles, demo users, currencies/rates, test accounts (100,000 EGP), cards, bill providers, and bills. |
| **FND-06** | **Current User Service** | HTTP context abstraction extracting claims (`NameIdentifier`, `Email`, `Roles`). |
| **FND-07** | **JWT Authentication** | HMAC-SHA256 JWT access token generation and cryptographic 64-byte refresh token management. |
| **FND-08** | **Exception Middleware** | Global exception handler returning standard JSON `ApiResponse` with HTTP 400, 403, 404, 500 status codes. |
| **FND-09** | **Service Registrations** | Modular DI extensions for DbContext, ASP.NET Core Identity, JWT Bearer, CORS, and Swagger with JWT Bearer UI. |
| **FND-10** | **Startup Pipeline** | `Program.cs` configured with middleware pipeline, automatic database migration, and seed execution on startup. |

---

## 👥 Seeded Demo Accounts & Credentials

The database seeder automatically initializes the following accounts upon startup:

| Role | Email | Password | Details |
| :--- | :--- | :--- | :--- |
| **Administrator** | `admin@limita.bank` | `Admin@123456!` | Full administrative capabilities |
| **Demo Customer** | `john.doe@limita.bank` | `User@123456!` | Starting balance: **100,000 EGP** + **$5,000 USD** |

---

## 🚀 Getting Started

### Prerequisites:
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- SQL Server (., SQL Express, or standard SQL Server instance)

### Build the Solution:
```bash
dotnet build Limita.sln
```

### Run the API:
```bash
dotnet run --project src/Limita.API
```

Once running, navigate to the root URL (e.g., `https://localhost:5001/` or `http://localhost:5000/`) to view the interactive **Swagger UI Documentation**.

---

## 🔒 Authenticating via Swagger:
1. Call `POST /api/auth/login` with `john.doe@limita.bank` and `User@123456!`.
2. Copy the `accessToken` from the response.
3. Click the **Authorize** button in Swagger at the top right.
4. Enter `Bearer <token>` and confirm.
5. Test protected endpoints such as `GET /api/auth/me`.
