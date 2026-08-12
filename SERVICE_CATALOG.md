# SERVICE_CATALOG.md â€” Request Service (Ø«Ø¨Øª Ø¯Ø±Ø®ÙˆØ§Ø³Øª Ø³Ø§Ù…Ø§Ù†Ù‡ Û±Û³Û·)

**Version:** Phase 1 (initial: create request) Â· **Stack:** ASP.NET Core 8 / EF Core 8 / SQL Server 2019
**Solution:** `RequestService.sln` Â· **Root:** `src/` (Api Â· Application Â· Domain Â· Infrastructure â€” Clean Architecture, mirrors `files` service)

---

## 1. Purpose

Registers citizen requests (Ø«Ø¨Øª Ø¯Ø±Ø®ÙˆØ§Ø³Øª) for the 137 municipality system (Sabzevar). It is the entry point for the
**citizen apps**, the **operator app/cartable**, the **call center / telephony (IVR)**, and **internal services**.
Identity is resolved via the sibling `sso-login-service`; attachments are referenced (not stored) via the sibling
`files` service.

## 2. Runtime configuration

| Key | Purpose | Dev (appsettings.Development.json) |
|---|---|---|
| `ConnectionStrings:Requests` | SQL Server (shared engine `185.255.91.242,2019`) | `Database=apiweb-137request;User Id=apiweb137requestuser;...` |
| `Sso:BaseUrl` | sso-login-service base URL | `http://127.0.0.1:5001` |
| `Sso:UserInfoPath` | user-info endpoint | `/api/auth/me` |
| `Files:BaseUrl` | files service base URL | `http://127.0.0.1:6000` |
| `Files:GetFilePath` | file-metadata endpoint | `/api/files/{id}` |
| `Files:ServiceToken` | service-to-service JWT (files service) | **not yet provided â€” TODO (Open Item)** |
| `Files:ValidationEnabled` | validate fileIds against files service | `false` in dev (SSO/files unreachable here) |
| `InternalAuth:HeaderName` | API key header for internal channels | `X-Api-Key` |
| `InternalAuth:ApiKeys` | allowed API keys (name + key) | `dev-internal-key-137` (name `TelephonyDev`) |
| `Cors:AllowedOrigins` | browser clients (web apps) | `http://localhost:3000`, `http://localhost:5173` |
| `Logging:File` | Serilog file sink (rolling daily, 14 retained) | `test-api-out.log` (dev) / `/var/log/requestservice/app-.log` (prod) |
| `Swagger:Enabled` | serve swagger UI (also enabled automatically in `Development`) | `true` |

> **Deployment mode:** the service is intentionally deployed in **`ASPNETCORE_ENVIRONMENT=Development`**
> (Dockerfile, docker-compose, deploy/requestservice.service) so **Swagger UI is always served at `/swagger`** and the
> dev connection string / API key from `appsettings.Development.json` are used. Switch to `Production` later by
> uncommenting the `Environment=` overrides in the systemd unit and setting `Swagger:Enabled=true`.

**Port:** `5006` (dev: `http://127.0.0.1:5006`) Â· **Health:** `GET /health` and `GET /api/health` (DB `SELECT 1`).

## 3. Auth model

Three kinds of callers are resolved in `RequestService.CreateAsync`:

| Channel | Allowed auth | Caller kind |
|---|---|---|
| `CitizenMobileApp` / `CitizenWebApp` | SSO bearer (JWT from sso-login-service) | `Citizen` |
| `OperatorApp` | SSO bearer | `Operator` |
| `PhoneCall` (call center / IVR) | `X-Api-Key` **or** SSO bearer | `ExternalService` / `Operator` |
| `InternalService` | `X-Api-Key` (mandatory) | `ExternalService` |

- SSO integration: `GET {Sso:BaseUrl}{Sso:UserInfoPath}` with `Authorization: Bearer <token>` â†’
  `{ success, data: { id, melliCode, phone } }`. Token invalid/expired â†’ 401 `UNAUTHORIZED`; SSO down â†’ 503
  `DEPENDENCY_UNAVAILABLE` (with retry + circuit breaker).
- For citizen channels the token identity (national code) is authoritative; a conflicting body `nationalCode` is rejected.
- For `PhoneCall`, the national code may come from the body; the source phone is stored in `CreatedBySourcePhone`.

## 4. API â€” Phase 1 (implemented)

### `POST /api/v1/requests` â†’ `201 Created`
Request body (all fields optional except `channel`; FluentValidation is channel-aware):

```json
{
  "channel": "PhoneCall",                      // CitizenMobileApp|CitizenWebApp|OperatorApp|PhoneCall|InternalService
  "citizen": { "nationalCode": "...", "firstName": "...", "lastName": "...", "phoneNumber": "..." },
  "description": "...",
  "location": { "lat": 36.210, "lng": 57.666 },
  "fileIds": ["11111111-1111-1111-1111-111111111111", "22222222-2222-2222-2222-222222222222"]
}
```

Response: `{ "requestId": "<guid>", "trackingCode": "137-14050521-000004" }`

Validation rules (400 `VALIDATION_ERROR`, first error message):
- `CitizenMobileApp`/`CitizenWebApp`: SSO token required (401 if missing).
- `OperatorApp`/`CitizenMobileApp`/`CitizenWebApp`: citizen **required**, `nationalCode` exactly 10 digits.
- `PhoneCall`/`InternalService`: no citizen required (call center may register without identity).
- `location` required for `CitizenMobileApp`; `lat`/`lng` within valid ranges; `description` â‰¤ 2000.
- `fileIds`: each must be a valid GUID if present; duplicates rejected (DB unique index).
- With `Files:ValidationEnabled=true`, each fileId must exist in the files service (else 400 or 503 if service down).

### Reserved (Phase 2+) â€” all return `501 NOT_IMPLEMENTED`
- `GET /api/v1/requests/{id:guid}`
- `GET /api/v1/requests/by-tracking-code/{code}`
- `GET /api/v1/requests` (search / cartable: `status`, `currentGroupId`, `from`, `to`)
- `PUT /api/v1/requests/{id}/status`
- `PUT /api/v1/requests/{id}/refer`

## 5. Error envelope

All errors, including model-binding failures, return `{ "code": "...", "message": "...", "traceId": "..." }`:

| HTTP | code | When |
|---|---|---|
| 400 | `VALIDATION_ERROR` | FluentValidation / model binding / fileId format / nationalCode rule |
| 401 | `UNAUTHORIZED` | missing/invalid/expired token or missing/invalid API key |
| 403 | `FORBIDDEN` | authenticated but not allowed |
| 404 | `NOT_FOUND` | resource not found |
| 501 | `NOT_IMPLEMENTED` | reserved Phase-2 routes |
| 503 | `DEPENDENCY_UNAVAILABLE` | SSO/files unreachable (after retries) |
| 500 | `INTERNAL_ERROR` | unexpected |

## 6. Data model

Tables (all in `apiweb-137request`, enums stored as strings, GUID PKs). Migrations: `20260812061144_InitialCreate`.

**`Requests`** â€” current snapshot only (no redundant PII; only NationalCode).

| column | notes |
|---|---|
| `Id` | GUID PK |
| `TrackingCode` | `137-<yyyyMMdd Jalali>-<6-digit seq>` (e.g. `137-14050521-000004`), **unique index**, from DB sequence |
| `NationalCode` | nullable (unverified telephony) |
| `Description` | â‰¤ 2000 |
| `LocationLat` / `LocationLng` | precision 18,6 |
| `Channel` / `Status` | string enums (`RequestChannel`, `RequestStatus`) |
| `CurrentGroupId` | nullable until referral |
| `CreatedBySourcePhone` | call-center scenario |
| `CreatedAtUtc` / `UpdatedAtUtc` | |

**`RequestFiles`** â€” link to files-service attachments (bytes never stored here).

| column | notes |
|---|---|
| `Id` | GUID PK |
| `RequestId` | FK â†’ Requests (cascade) |
| `FileId` | files-service GUID (string, â‰¤64) |
| `FileType` | derived from extension/mime: `Audio|Image|Video|Other` |
| unique index `(RequestId, FileId)` |

**`RequestLogs`** â€” append-only audit/event log (never updated/deleted).

| column | notes |
|---|---|
| `Id` | GUID PK |
| `RequestId` | FK â†’ Requests (cascade) |
| `ActionType` | `Created`, `ReviewedByOperator`, `Confirmed`, `Referred`, `GroupChanged`, `Rejected`, `StatusChanged`, `Updated`, `Closed`, `Canceled` |
| `ActorType` / `ActorId` | `System|Operator|Citizen|ExternalService` + id |
| `PreviousStatus`/`NewStatus`, `PreviousGroupId`/`NewGroupId` | status/group transitions |
| `Description` | free-form JSON metadata (see Open Item) |

**Sequence:** `dbo.RequestTrackingCodeSeq` (`START 1, NO CYCLE, CACHE 50`) consumed via ADO.NET `NEXT VALUE FOR`
(EF forbids it in subqueries). Race-safe: sequence + unique index; proven with 5 parallel POSTs â†’ 5 distinct codes.

## 7. Transactionality

`RequestRepository.CreateAsync` runs inside `SqlServerRetryingExecutionStrategy` (transient-fault retry).
Every retry clears the change tracker and re-runs the whole work (sequence read â†’ inserts â†’ commit) so a
retry never double-inserts. The 500 seen mid-development ("FK_RequestFiles_Requests_RequestId" violation) was caused
by EF not ordering separately-added children before the parent insert â€” fixed by setting `file.Request = entity` /
`createdLog.Request = entity` navigations.

## 8. Integration dependencies

| Dependency | How | Failure behavior |
|---|---|---|
| `sso-login-service` | `GET /api/auth/me` (Bearer) | 401 invalid token; 503 unreachable |
| `files` service | `GET /api/files/{id}` (needs `Files:ServiceToken`) | 400 unknown fileId; 503 unreachable |
| SQL Server `185.255.91.242,2019` (`apiweb-137request`) | EF Core | health check `database` reports Down |

## 9. Open Items (blockers / to confirm)

1. **PII contradiction:** `Citizen.FirstName/LastName/PhoneNumber` are accepted by the API (spec section asked for
   them) but the architecture decision says "no duplicate PII". Resolution: they are persisted only inside
   `RequestLogs.Description` as JSON (temporary metadata), never in `Requests`. **Confirm** this is acceptable, or
   drop the fields.
2. **SSO profile forwarding:** there is no sso-login-service endpoint to register/forward citizen name/phone.
   Until it exists, telephony-collected PII lives only in the request audit log. A forward/register endpoint is
   needed to complete citizen profiles.
3. **Files `ServiceToken`:** the files service authenticates service-to-service callers via a JWT
   (`Files:ServiceToken`, see files `Jwt` issuer `storage.sabzevar.ir`). The token must be issued and supplied;
   until then real fileId validation cannot run (`ValidationEnabled=false` in dev).
4. **DB confirmed:** engine SQL Server 2019 at `185.255.91.242,2019`; DB `apiweb-137request` (user is db_owner).
5. **CORS origins:** final web-app origins must be confirmed and set in `Cors:AllowedOrigins` for prod.
6. **Domain + nginx:** target domain `apiweb-137request.sabzevar.ir`, port 5006 â€” nginx/systemd/Docker configs are
   included in the repo (see `deploy/`, `Dockerfile`, `docker-compose.yml`) but the server-side install is not done.
   The service runs in **Development** mode so Swagger is reachable at `/swagger`.

## 10. Run / test (dev)

```
dotnet run --project src/RequestService.Api   # â†’ http://127.0.0.1:5006, Swagger at /swagger
curl -X POST http://127.0.0.1:5006/api/v1/requests -H "Content-Type: application/json" \
     -H "X-Api-Key: dev-internal-key-137" --data @body.json
```

Verified (live DB): `/health` Healthy; PhoneCall+key â†’ 201 + tracking code; missing key â†’ 401; missing location
(citizen app) â†’ 400; invalid nationalCode â†’ 400; invalid fileId format â†’ 400; citizen app without token â†’ 401;
citizen app with fake token (SSO down) â†’ 503; request with 2 fileIds â†’ 201 (FK ordering fixed);
5 parallel POSTs â†’ 5 unique tracking codes; reserved routes â†’ 501.
