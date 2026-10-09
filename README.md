# TaskTrack — PRN232 Assignment 2 Backend

ASP.NET Core 8, EF Core and PostgreSQL. Forked from Assignment 1 into a separate Assignment 2 repository. AS1 public reads remain available; writes require a valid Staff/Admin JWT. Account management requires Admin.

## Local setup

Use a **separate AS2 database**. Run `database/001_initial.sql` only against an empty database, then `database/002_auth.sql`. The latter can run again without resetting data. Legacy tasks keep a NULL creator.

Configure secrets outside Git (PowerShell, from this repository):

```powershell
$env:ConnectionStrings__DefaultConnection = 'Host=localhost;Port=5432;Database=tasktrack_ass2;Username=postgres;Password=YOUR_DATABASE_PASSWORD'
$env:JWT_SECRET = 'YOUR_RANDOM_SECRET_WITH_AT_LEAST_32_UTF8_BYTES'
$env:ADMIN_EMAIL = 'YOUR_ADMIN_EMAIL'
$env:ADMIN_PASSWORD = 'YOUR_ADMIN_PASSWORD_AT_LEAST_8_CHARACTERS'
dotnet run --project TaskTrack.API -- --seed-admin
dotnet run --project TaskTrack.API
```

Admin seed writes a PBKDF2 hash directly to PostgreSQL, is idempotent, and never promotes an existing Staff account automatically. Register only creates Staff. No real credentials belong in source.

API: `http://localhost:5102/api`; Swagger: `http://localhost:5102/swagger`. The frontend runs on port 3012. Configure `CORS_ORIGINS` for the actual frontend origins. `DATABASE_URL` is supported when a connection string is absent. AS2 has its own User Secrets ID.

## Authentication and permissions

- `POST /api/auth/register`: FullName, Email, Password → 201; normalized duplicate email → 409.
- `POST /api/auth/login`: Email, Password → signed token, expiry and safe account data; bad credentials → 401.
- `GET /api/auth/me`: authenticated session verification.
- JWT: AccountID, Email, Role, FullName and exp; expires after 24 hours. Deleted accounts and changed roles invalidate old tokens.
- All AS1 GET endpoints stay public. POST/PUT/DELETE require authentication.
- `GET /api/accounts`, `GET /api/accounts/{id}`, `PUT /api/accounts/{id}`, `DELETE /api/accounts/{id}` require Admin; Staff receives 403.
- Account update accepts FullName and/or Role. Account deletion returns 409 when the account created any task, including a soft-deleted task.
- Task creator is derived from JWT; the client cannot override it. Tasks use soft deletion. Linked records prevent other entity deletion.

## Verification

```powershell
dotnet restore
dotnet build --configuration Release --no-restore
$env:API_BASE_URL = 'http://127.0.0.1:5102/api'
$env:TEST_DATABASE_URL = 'postgresql://YOUR_USER:YOUR_PASSWORD@127.0.0.1:5432/tasktrack_ass2'
$env:STAFF_EMAIL = 'YOUR_TEST_STAFF_EMAIL'
$env:STAFF_PASSWORD = 'YOUR_TEST_STAFF_PASSWORD'
# ADMIN_EMAIL, ADMIN_PASSWORD and JWT_SECRET must match the running test API.
node --test tests/api.integration.test.mjs
```

Node 24 and PostgreSQL `psql` are required for integration tests. Set `PSQL_EXE` if psql is outside PATH. Tests reject a non-local database or a database name not ending in `_ass2`. They create their own fixtures, exercise an injected database failure, and remove only their fixtures. Use an isolated test instance with no other writers.

Deployment and submission handoff are planned separately in P12. The inherited Dockerfile still builds the .NET 8 projects.
