# 02 — Backend Phase 1 (ASP.NET Core Web API + EF Core + PostgreSQL)

Dokumen ini adalah rencana implementasi backend untuk Phase 1. Schema database ada di [`01-DATABASE.md`](./01-DATABASE.md); frontend ada di [`04-FRONTEND.md`](./04-FRONTEND.md).

---

## 0. Ringkasan

| Komponen | Pilihan |
|---|---|
| Runtime | **.NET 8** |
| API | ASP.NET Core Web API dengan **Controllers** |
| ORM | EF Core + `Npgsql.EntityFrameworkCore.PostgreSQL` |
| Database | PostgreSQL 17 via Docker Compose |
| Auth | Simulasi: header `X-User-Email` → dicocokkan ke tabel `users` |
| Error | `ProblemDetails` (RFC 9457) dengan field `code` |
| Test | xUnit + `WebApplicationFactory` + Testcontainers (PostgreSQL asli) |

**Target Definition of Done Phase 1:** tujuh demo scenario di dokumen assessment berjalan dan masing-masing punya automated test.

---

## 1. Inisialisasi project

```bash
mkdir backend && cd backend
dotnet new sln -n AccessRequestHub

dotnet new webapi -n AccessRequestHub.Api -o src/AccessRequestHub.Api --use-controllers
dotnet new xunit  -n AccessRequestHub.Api.Tests -o tests/AccessRequestHub.Api.Tests

dotnet sln add src/AccessRequestHub.Api tests/AccessRequestHub.Api.Tests
dotnet add tests/AccessRequestHub.Api.Tests reference src/AccessRequestHub.Api

# API
dotnet add src/AccessRequestHub.Api package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/AccessRequestHub.Api package Microsoft.EntityFrameworkCore.Design
dotnet add src/AccessRequestHub.Api package EFCore.NamingConventions

# Tests
dotnet add tests/AccessRequestHub.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/AccessRequestHub.Api.Tests package Testcontainers.PostgreSql

# Tool migration sebagai LOCAL tool (di root repo), supaya assessor mendapat versi yang sama
cd ..                                   # kembali ke root repo
dotnet new tool-manifest                # membuat .config/dotnet-tools.json (di-commit)
dotnet tool install dotnet-ef --version <versi sama dengan Microsoft.EntityFrameworkCore.Design di .csproj>
```

Dengan local tool manifest, assessor cukup menjalankan `dotnet tool restore` lalu `dotnet ef ...`. Global tool (`--global`) tidak tercatat di repo, sehingga versi di mesin assessor bisa berbeda dan "reproducible dari README" jadi tidak terjamin.

> Tidak memakai FluentAssertions karena versi 8 ke atas memakai lisensi komersial. `Assert` bawaan xUnit sudah cukup.

---

## 2. Struktur folder

```
backend/
├── AccessRequestHub.sln
├── src/AccessRequestHub.Api/
│   ├── Program.cs
│   ├── appsettings.json
│   ├── appsettings.Development.json
│   ├── AccessRequestHub.Api.http          # skenario manual (curl-like) untuk demo
│   ├── Auth/
│   │   ├── CurrentUser.cs
│   │   └── CurrentUserMiddleware.cs
│   ├── Observability/
│   │   ├── CorrelationContext.cs          # scoped: correlation id request saat ini
│   │   └── CorrelationIdMiddleware.cs
│   ├── Controllers/
│   │   ├── UsersController.cs             # GET /api/users, GET /api/me
│   │   ├── ApplicationsController.cs      # GET /api/applications
│   │   ├── AccessRequestsController.cs    # create, list, detail, approve, reject
│   │   └── ApprovalsController.cs         # GET /api/approvals/inbox
│   ├── Contracts/                         # DTO request/response (BUKAN entity)
│   ├── Domain/
│   │   ├── Entities/                      # User, Application, PolicyVersion, AccessRequest, AuditEvent
│   │   ├── Enums.cs                       # AccessEnvironment, AccessLevel, AccessRequestStatus, AuditEventType
│   │   └── AccessRequestAuthorization.cs  # aturan siapa boleh lihat / approve
│   ├── Policies/
│   │   ├── IApprovalPolicy.cs
│   │   ├── ApprovalPolicyV1.cs
│   │   └── ApprovalPolicyRegistry.cs
│   ├── Services/
│   │   └── AccessRequestService.cs
│   ├── Errors/
│   │   ├── AppExceptions.cs
│   │   ├── AppExceptionHandler.cs
│   │   └── DbUpdateExceptionExtensions.cs
│   └── Data/
│       ├── AppDbContext.cs
│       ├── Configurations/
│       └── Migrations/
└── tests/AccessRequestHub.Api.Tests/
    ├── Infrastructure/ApiFactory.cs
    ├── WorkflowTests.cs
    ├── AuthorizationTests.cs
    ├── IdempotencyTests.cs
    ├── ConcurrencyTests.cs
    └── DatabaseGuardTests.cs
```

Satu project API sudah cukup untuk scope ini. Clean Architecture dengan 4 project terpisah tidak memberi nilai tambah dalam timebox 4–8 jam. Tulis ini sebagai trade-off di PLAN.md.

---

## 3. Konfigurasi lokal

### 3.1 `docker-compose.yml` (di root repo)

```yaml
services:
  db:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: access_request_hub
      POSTGRES_USER: arh
      POSTGRES_PASSWORD: arh_local_dev_only   # nilai dev lokal, bukan credential nyata
    ports:
      - "5432:5432"
    volumes:
      - arh_pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U arh -d access_request_hub"]
      interval: 2s
      timeout: 3s
      retries: 30

volumes:
  arh_pgdata:
```

Jalankan dengan `docker compose up -d --wait db` supaya perintah berikutnya (`dotnet ef database update`) baru jalan setelah Postgres siap.

### 3.2 `appsettings.Development.json`

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=access_request_hub;Username=arh;Password=arh_local_dev_only"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    }
  }
}
```

> Repo tidak boleh berisi credential nyata. Password di atas hanya untuk container lokal. Sebutkan ini di README dan REVIEW.md.

### 3.3 Port

Set `applicationUrl` di `Properties/launchSettings.json` menjadi `http://localhost:5080`. Frontend (Vite) akan mem-proxy `/api` ke port ini, jadi tidak perlu konfigurasi CORS.

---

## 4. Domain model

### 4.1 Enum

```csharp
// "Environment" bentrok dengan System.Environment, jadi namanya AccessEnvironment
public enum AccessEnvironment { NonProduction, Production }
public enum AccessLevel { Read, Admin }

public enum AccessRequestStatus
{
    PendingManagerApproval,
    PendingSystemOwnerApproval,
    Approved,
    Rejected
}

public enum AuditEventType
{
    RequestCreated,
    ManagerApproved,
    ManagerRejected,
    SystemOwnerApproved,
    SystemOwnerRejected
}
```

### 4.2 Entity utama

```csharp
public sealed class AccessRequest
{
    public Guid Id { get; set; }
    public string ClientRequestId { get; set; } = default!;

    public Guid RequesterId { get; set; }
    public User Requester { get; set; } = default!;
    public Guid ApplicationId { get; set; }
    public Application Application { get; set; } = default!;

    public AccessEnvironment Environment { get; set; }
    public AccessLevel AccessLevel { get; set; }
    public string Justification { get; set; } = default!;

    public AccessRequestStatus Status { get; set; }
    public string PolicyVersion { get; set; } = default!;
    public bool IsHighRisk { get; set; }

    // Snapshot approver saat request dibuat
    public Guid ManagerApproverId { get; set; }
    public Guid? SystemOwnerApproverId { get; set; }

    public string? RejectionReason { get; set; }
    public int Version { get; set; }               // concurrency token

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DecidedAt { get; set; }

    public List<AuditEvent> AuditEvents { get; set; } = new();

    public bool IsTerminal =>
        Status is AccessRequestStatus.Approved or AccessRequestStatus.Rejected;
}

public sealed class AuditEvent
{
    public long Id { get; set; }
    public Guid AccessRequestId { get; set; }
    public AuditEventType EventType { get; set; }
    public Guid ActorId { get; set; }
    public AccessRequestStatus? FromStatus { get; set; }
    public AccessRequestStatus ToStatus { get; set; }
    public string? Reason { get; set; }
    public string PolicyVersion { get; set; } = default!;
    public int RequestVersion { get; set; }        // version request SETELAH event
    public string? CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
```

> Npgsql mewajibkan `DateTimeOffset` ber-offset 0 (UTC) untuk kolom `timestamptz`. Selalu gunakan `TimeProvider.GetUtcNow()` (inject `TimeProvider.System`), jangan `DateTimeOffset.Now`.

---

## 5. Policy (persiapan Phase 2)

Semua aturan "high-risk" dan "status berikutnya setelah approve" berada di satu tempat. Request selalu diproses dengan policy **sesuai `PolicyVersion` miliknya**, bukan policy yang sedang aktif.

```csharp
public interface IApprovalPolicy
{
    string Version { get; }
    bool IsHighRisk(AccessEnvironment environment, AccessLevel accessLevel);
    AccessRequestStatus NextStatusOnApprove(AccessRequest request);
}

public sealed class ApprovalPolicyV1 : IApprovalPolicy
{
    public string Version => "v1";

    public bool IsHighRisk(AccessEnvironment environment, AccessLevel accessLevel) =>
        environment == AccessEnvironment.Production || accessLevel == AccessLevel.Admin;

    public AccessRequestStatus NextStatusOnApprove(AccessRequest request) => request.Status switch
    {
        AccessRequestStatus.PendingManagerApproval => request.IsHighRisk
            ? AccessRequestStatus.PendingSystemOwnerApproval
            : AccessRequestStatus.Approved,
        AccessRequestStatus.PendingSystemOwnerApproval => AccessRequestStatus.Approved,
        _ => throw new InvalidOperationException($"No approve transition from {request.Status}.")
    };
}

public sealed class ApprovalPolicyRegistry
{
    private readonly Dictionary<string, IApprovalPolicy> _byVersion;

    public ApprovalPolicyRegistry(IEnumerable<IApprovalPolicy> policies) =>
        _byVersion = policies.ToDictionary(p => p.Version);

    public IApprovalPolicy Get(string version) =>
        _byVersion.TryGetValue(version, out var policy)
            ? policy
            : throw new InvalidOperationException($"Unknown policy version '{version}'.");
}
```

Policy **aktif** dibaca dari tabel `policy_versions` (`is_active = true`) saat create, supaya hanya ada satu sumber kebenaran.

---

## 6. Autentikasi simulasi

```csharp
public sealed class CurrentUser
{
    private User? _user;
    public User User => _user ?? throw new InvalidOperationException("No current user.");
    public bool IsAuthenticated => _user is not null;
    public void Set(User user) => _user = user;
}

public sealed class CurrentUserMiddleware(RequestDelegate next)
{
    // Endpoint yang boleh diakses tanpa user (daftar user untuk user switcher)
    private static readonly PathString[] AnonymousPaths = ["/api/users"];

    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, CurrentUser currentUser,
                                  ILogger<CurrentUserMiddleware> logger)
    {
        var path = ctx.Request.Path;
        if (!path.StartsWithSegments("/api") || AnonymousPaths.Any(p => path.StartsWithSegments(p)))
        {
            await next(ctx);
            return;
        }

        var email = ctx.Request.Headers["X-User-Email"].ToString().Trim().ToLowerInvariant();
        var user = email.Length == 0
            ? null
            : await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Email == email);

        if (user is null)
        {
            logger.LogWarning("Unauthenticated request to {Path}", path);
            await Results.Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unknown or missing user",
                detail: "Kirim header X-User-Email dengan salah satu demo user.",
                extensions: new Dictionary<string, object?> { ["code"] = "UNAUTHENTICATED" })
                .ExecuteAsync(ctx);
            return;
        }

        currentUser.Set(user);
        using (logger.BeginScope(new Dictionary<string, object> { ["UserEmail"] = user.Email }))
        {
            await next(ctx);
        }
    }
}
```

**Penting:** `RequesterId` selalu diambil dari `CurrentUser`, **tidak pernah** dari body request. DTO create memang tidak punya field requester.

> **Catat di REVIEW.md:** header ini bisa dipalsukan siapa pun. Ini sesuai izin assessment ("authentication boleh disimulasikan"), tetapi di production harus diganti OIDC/SSO. Authorization tetap ditegakkan di server berdasarkan user hasil resolusi header.

---

## 7. Aturan authorization (satu fungsi, dipakai di mana-mana)

```csharp
public static class AccessRequestAuthorization
{
    public static bool CanView(User actor, AccessRequest r) =>
        actor.IsAuditor
        || r.RequesterId == actor.Id
        || r.ManagerApproverId == actor.Id
        || r.SystemOwnerApproverId == actor.Id;

    public static Guid? AssignedApprover(AccessRequest r) => r.Status switch
    {
        AccessRequestStatus.PendingManagerApproval     => r.ManagerApproverId,
        AccessRequestStatus.PendingSystemOwnerApproval => r.SystemOwnerApproverId,
        _ => null
    };

    public static bool CanDecide(User actor, AccessRequest r) =>
        !r.IsTerminal
        && r.RequesterId != actor.Id
        && AssignedApprover(r) == actor.Id;
}
```

Fungsi yang sama dipakai untuk (a) menegakkan aturan di service dan (b) menghitung `allowedActions` di response. Dengan begitu, tombol di UI dan aturan backend tidak bisa "berbeda pendapat".

Catatan: Erin (auditor) bisa **melihat** semua request, tetapi `CanDecide` tidak memberi hak apa pun kepada auditor, sesuai aturan "bukan approver otomatis".

---

## 8. API contract

### 8.1 Endpoint

| Method | Path | Akses | Response |
|---|---|---|---|
| GET | `/api/users` | anonymous | Daftar demo user (untuk user switcher) |
| GET | `/api/me` | semua user | User saat ini + info peran: `isAuditor`, `hasDirectReports`, `ownedApplications` |
| GET | `/api/applications` | semua user | Daftar aplikasi + System Owner |
| POST | `/api/access-requests` | user yang punya manager | `201` baru · `200` replay idempotent · `400` · `409` · `422` |
| GET | `/api/access-requests?scope=mine` | semua user | Request milik sendiri. `scope` kosong = `mine`; nilai selain `mine`/`all` → `400` |
| GET | `/api/access-requests?scope=all` | auditor | Semua request · `403` jika bukan auditor |
| GET | `/api/access-requests/{id}` | `CanView` | Detail + audit timeline · `403` · `404` |
| GET | `/api/approvals/inbox` | semua user | Request yang sedang menunggu keputusan user ini |
| POST | `/api/access-requests/{id}/approve` | assigned approver | `200` · `403` · `404` · `409` |
| POST | `/api/access-requests/{id}/reject` | assigned approver | `200` · `400` · `403` · `404` · `409` |

### 8.2 DTO

```csharp
public sealed class CreateAccessRequestRequest
{
    [Required, StringLength(64, MinimumLength = 1)]
    public string? ClientRequestId { get; init; }

    [Required] public Guid? ApplicationId { get; init; }

    // Nullable + [Required] supaya field yang tidak dikirim tidak diam-diam jadi nilai default enum (0)
    [Required] public AccessEnvironment? Environment { get; init; }
    [Required] public AccessLevel? AccessLevel { get; init; }

    [Required, StringLength(1000)]   // [Required] juga menolak string berisi spasi saja
    public string? Justification { get; init; }
}

// Sengaja class, BUKAN positional record. Pada record `([property: Required] int? ExpectedVersion)`,
// ASP.NET Core MVC melempar InvalidOperationException saat validasi
// ("Record type ... has validation metadata defined on property ... that will be ignored").
public sealed class ApproveRequest
{
    [Required] public int? ExpectedVersion { get; init; }
}

public sealed class RejectRequest
{
    [Required] public int? ExpectedVersion { get; init; }
    [Required, StringLength(1000)] public string? Reason { get; init; }
}
```

Enum dikirim sebagai string, dan **angka ditolak**:

```csharp
builder.Services.AddControllers(o =>
        // Key error validasi memakai nama JSON (camelCase: "justification"), bukan nama C# ("Justification")
        o.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)));
```

Kenapa `allowIntegerValues: false`: default `JsonStringEnumConverter` menerima angka. Body `"environment": 7` akan lolos deserialisasi sebagai `(AccessEnvironment)7`, `IsHighRisk` menghitungnya sebagai bukan Production, lalu insert gagal di CHECK constraint dan user mendapat **500**, padahal seharusnya **400**. Dengan opsi ini, angka langsung ditolak di model binding sebagai 400.

Contoh body create:

```json
{
  "clientRequestId": "6f1c2a0e-9b8d-4a77-8a35-0c1f9d1e2b11",
  "applicationId": "10000000-0000-0000-0000-000000000001",
  "environment": "Production",
  "accessLevel": "Read",
  "justification": "Investigasi tiket support #123"
}
```

Contoh response detail:

```json
{
  "id": "7b0c0e2a-...",
  "clientRequestId": "6f1c2a0e-...",
  "requester": { "id": "...", "email": "alice@example.local", "displayName": "Alice" },
  "application": { "id": "...", "code": "CRM", "name": "CRM" },
  "environment": "Production",
  "accessLevel": "Read",
  "justification": "Investigasi tiket support #123",
  "status": "PendingSystemOwnerApproval",
  "policyVersion": "v1",
  "isHighRisk": true,
  "currentApprover": { "email": "carol@example.local", "displayName": "Carol" },
  "rejectionReason": null,
  "version": 2,
  "createdAt": "2026-10-08T03:00:00Z",
  "updatedAt": "2026-10-08T03:05:00Z",
  "decidedAt": null,
  "allowedActions": [],
  "auditTrail": [
    { "eventType": "RequestCreated",  "actor": "alice@example.local", "fromStatus": null, "toStatus": "PendingManagerApproval", "reason": null, "requestVersion": 1, "occurredAt": "..." },
    { "eventType": "ManagerApproved", "actor": "bob@example.local", "fromStatus": "PendingManagerApproval", "toStatus": "PendingSystemOwnerApproval", "reason": null, "requestVersion": 2, "occurredAt": "..." }
  ]
}
```

`allowedActions` dihitung untuk **user yang sedang memanggil** dan isinya hanya `"approve"` dan/atau `"reject"`. Contoh di atas dilihat oleh Alice, jadi kosong. Kalau Carol membuka request yang sama, isinya `["approve", "reject"]`. Frontend hanya membaca nilai ini dan tidak menghitung ulang aturan.

### 8.3 Error code

Semua error berbentuk `ProblemDetails` dengan field tambahan `code` dan `correlationId` (lihat bagian 11).

| HTTP | `code` | Kapan |
|---|---|---|
| 400 | (ValidationProblemDetails, field `errors`) | Input tidak valid: field kosong, enum salah/berupa angka, reason kosong. Key `errors` camelCase (`justification`); error format JSON memakai key `$.environment` |
| 401 | `UNAUTHENTICATED` | Header `X-User-Email` kosong atau tidak dikenal |
| 403 | `NOT_ASSIGNED_APPROVER` | User bukan approver untuk tahap saat ini (termasuk user yang sama sekali tidak terkait dengan request) |
| 403 | `SELF_APPROVAL_NOT_ALLOWED` | Requester mencoba memproses request sendiri |
| 403 | `FORBIDDEN` | Melihat request yang bukan haknya, atau `scope=all` oleh non-auditor |
| 404 | `ACCESS_REQUEST_NOT_FOUND` | ID request tidak ada |
| 409 | `STALE_VERSION` | `expectedVersion` tidak sama dengan version sekarang, atau kalah race. Extension: `currentVersion`, `currentStatus` |
| 409 | `INVALID_TRANSITION` | Request sudah terminal (Approved/Rejected) |
| 409 | `IDEMPOTENCY_KEY_REUSED` | `ClientRequestId` sama tetapi isi payload berbeda. Extension: `existingRequestId` |
| 422 | `NO_MANAGER` | Requester tidak punya manager |
| 422 | `NO_ELIGIBLE_APPROVER` | Request high-risk dan System Owner = requester |
| 422 | `UNKNOWN_APPLICATION` | `applicationId` tidak ada |

---

## 9. Logic service

### 9.1 Create (idempotent)

Strategi: **database unique index adalah penjaga utama**. Pengecekan "sudah ada belum" di awal hanya *fast path*; race tetap ditangani oleh unique violation.

```csharp
public async Task<CreateResult> CreateAsync(CreateAccessRequestRequest input, CancellationToken ct)
{
    var actor = _currentUser.User;
    var clientRequestId = input.ClientRequestId!.Trim();

    // 1) Fast path: retry dengan key yang sama
    var existing = await FindByClientRequestIdAsync(actor.Id, clientRequestId, ct);
    if (existing is not null) return Replay(existing, input);

    // 2) Validasi bisnis
    var app = await _db.Applications.SingleOrDefaultAsync(a => a.Id == input.ApplicationId, ct)
        ?? throw new BusinessRuleException("UNKNOWN_APPLICATION", "Aplikasi tidak dikenal.");
    if (actor.ManagerId is null)
        throw new BusinessRuleException("NO_MANAGER", "Requester tidak memiliki manager untuk approval.");

    var activeCode = await _db.PolicyVersions.Where(p => p.IsActive).Select(p => p.Code).SingleAsync(ct);
    var policy = _policies.Get(activeCode);
    var isHighRisk = policy.IsHighRisk(input.Environment!.Value, input.AccessLevel!.Value);

    if (isHighRisk && app.SystemOwnerId == actor.Id)
        throw new BusinessRuleException("NO_ELIGIBLE_APPROVER",
            "Requester adalah System Owner aplikasi ini, sehingga tidak ada approver yang sah.");

    // 3) Bangun entity + audit event dalam satu unit of work
    var now = _clock.GetUtcNow();
    var request = new AccessRequest
    {
        Id = Guid.NewGuid(),
        ClientRequestId = clientRequestId,
        RequesterId = actor.Id,
        ApplicationId = app.Id,
        Environment = input.Environment!.Value,
        AccessLevel = input.AccessLevel!.Value,
        Justification = input.Justification!.Trim(),
        Status = AccessRequestStatus.PendingManagerApproval,
        PolicyVersion = policy.Version,
        IsHighRisk = isHighRisk,
        ManagerApproverId = actor.ManagerId.Value,
        SystemOwnerApproverId = isHighRisk ? app.SystemOwnerId : null,
        Version = 1,
        CreatedAt = now,
        UpdatedAt = now
    };
    request.AuditEvents.Add(NewAudit(request, AuditEventType.RequestCreated, actor.Id,
        from: null, to: request.Status, reason: null, now));

    _db.AccessRequests.Add(request);

    try
    {
        await _db.SaveChangesAsync(ct);          // 1 transaksi: insert request + insert audit
        _logger.LogInformation("Access request {RequestId} created (clientRequestId {ClientRequestId})",
            request.Id, clientRequestId);
        return CreateResult.Created(request);
    }
    catch (DbUpdateException ex) when (ex.IsUniqueViolation("ux_access_requests_requester_client_request_id"))
    {
        // 4) Kalah race dengan request kembar: ambil pemenangnya
        _db.ChangeTracker.Clear();
        var winner = await FindByClientRequestIdAsync(actor.Id, clientRequestId, ct)
            ?? throw new InvalidOperationException("Unique violation but no existing row found.", ex);
        _logger.LogInformation("Idempotent replay after race for {ClientRequestId}", clientRequestId);
        return Replay(winner, input);
    }
}

private static CreateResult Replay(AccessRequest existing, CreateAccessRequestRequest input)
{
    var samePayload =
        existing.ApplicationId == input.ApplicationId &&
        existing.Environment == input.Environment &&
        existing.AccessLevel == input.AccessLevel &&
        existing.Justification == input.Justification!.Trim();

    if (!samePayload)
        throw new ConflictException("IDEMPOTENCY_KEY_REUSED",
            "ClientRequestId sudah dipakai untuk request dengan isi berbeda.",
            new() { ["existingRequestId"] = existing.Id });

    return CreateResult.Replayed(existing);      // controller membalas 200 dengan body yang sama
}
```

Helper audit (dipakai di create dan approve/reject). `RequestVersion` diambil dari `request.Version` **setelah** transisi, jadi helper ini harus dipanggil setelah `Version` dinaikkan. `CorrelationContext` adalah service scoped yang diisi `CorrelationIdMiddleware` (bagian 11), sehingga service tidak perlu `IHttpContextAccessor`.

```csharp
private AuditEvent NewAudit(AccessRequest r, AuditEventType type, Guid actorId,
                            AccessRequestStatus? from, AccessRequestStatus to,
                            string? reason, DateTimeOffset now) => new()
{
    AccessRequestId = r.Id,
    EventType       = type,
    ActorId         = actorId,
    FromStatus      = from,
    ToStatus        = to,
    Reason          = reason,
    PolicyVersion   = r.PolicyVersion,
    RequestVersion  = r.Version,
    CorrelationId   = _correlation.Id,
    OccurredAt      = now
};
```

Helper unique violation:

```csharp
public static class DbUpdateExceptionExtensions
{
    public static bool IsUniqueViolation(this DbUpdateException ex, string constraintName) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg
        && pg.ConstraintName == constraintName;
}
```

Controller:

```csharp
[HttpPost]
public async Task<IActionResult> Create(CreateAccessRequestRequest body, CancellationToken ct)
{
    var result = await _service.CreateAsync(body, ct);
    var dto = await _service.GetDetailAsync(result.Request.Id, ct);
    return result.IsNew
        ? CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto)
        : Ok(dto);
}
```

### 9.2 Approve / Reject

**Urutan pengecekan itu penting**, dan alasannya perlu ditulis di PLAN.md:

| Urutan | Cek | Hasil | Kenapa di posisi ini |
|---|---|---|---|
| 1 | Request ada? | 404 | — |
| 2 | Actor terkait dengan request (`CanView`)? | 403 `NOT_ASSIGNED_APPROVER` | User yang sama sekali tidak terkait (misalnya Dana untuk request CRM) langsung ditolak **sebelum** menerima informasi apa pun. Tanpa langkah ini, response 409 `STALE_VERSION` membocorkan `currentVersion` dan `currentStatus` request orang lain |
| 3 | `version == expectedVersion`? | 409 `STALE_VERSION` | Kalau Bob double-click, klik kedua harus dapat 409 yang jelas. Kalau cek approver duluan, klik kedua akan melihat status sudah `PendingSystemOwnerApproval` dan Bob malah dapat 403, yang menyesatkan. Bob lolos langkah 2 karena ia `manager_approver_id` |
| 4 | Sudah terminal? | 409 `INVALID_TRANSITION` | Request Approved/Rejected tidak boleh diproses lagi, siapa pun pelakunya |
| 5 | Actor = requester? | 403 `SELF_APPROVAL_NOT_ALLOWED` | — |
| 6 | Actor = assigned approver tahap ini? | 403 `NOT_ASSIGNED_APPROVER` | — |
| 7 | Simpan dengan concurrency token | 409 kalau kalah race | Pengaman sebenarnya. Langkah 3 hanya fast-fail |

> Trade-off yang dicatat di REVIEW.md: user yang **boleh melihat** request tetapi bukan approver tahap ini (requester, auditor, System Owner sebelum tahapnya) dan mengirim version lama akan menerima 409, bukan 403. Ini diterima karena user tersebut memang sudah berhak melihat status dan version request itu.

```csharp
public async Task<AccessRequest> DecideAsync(Guid id, Decision decision, int expectedVersion,
                                             string? reason, CancellationToken ct)
{
    var actor = _currentUser.User;
    var request = await _db.AccessRequests.SingleOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException("ACCESS_REQUEST_NOT_FOUND", $"Access request {id} tidak ditemukan.");

    if (!AccessRequestAuthorization.CanView(actor, request))
        throw new ForbiddenException("NOT_ASSIGNED_APPROVER", "Anda bukan approver untuk request ini.");

    if (request.Version != expectedVersion)
        throw Stale(request);

    if (request.IsTerminal)
        throw new ConflictException("INVALID_TRANSITION",
            $"Request sudah {request.Status} dan tidak dapat diproses lagi.");

    if (request.RequesterId == actor.Id)
        throw new ForbiddenException("SELF_APPROVAL_NOT_ALLOWED", "Requester tidak boleh memproses request miliknya sendiri.");

    if (AccessRequestAuthorization.AssignedApprover(request) != actor.Id)
        throw new ForbiddenException("NOT_ASSIGNED_APPROVER", "Anda bukan approver untuk tahap ini.");

    var policy = _policies.Get(request.PolicyVersion);     // policy MILIK request, bukan yang aktif
    var from = request.Status;
    var isManagerStage = from == AccessRequestStatus.PendingManagerApproval;
    var now = _clock.GetUtcNow();

    AuditEventType eventType;
    if (decision == Decision.Approve)
    {
        request.Status = policy.NextStatusOnApprove(request);
        eventType = isManagerStage ? AuditEventType.ManagerApproved : AuditEventType.SystemOwnerApproved;
    }
    else
    {
        request.Status = AccessRequestStatus.Rejected;
        request.RejectionReason = reason!.Trim();
        eventType = isManagerStage ? AuditEventType.ManagerRejected : AuditEventType.SystemOwnerRejected;
    }

    if (request.IsTerminal) request.DecidedAt = now;
    request.UpdatedAt = now;

    // Optimistic concurrency: UPDATE ... WHERE id = @id AND version = @expectedVersion
    _db.Entry(request).Property(r => r.Version).OriginalValue = expectedVersion;
    request.Version = expectedVersion + 1;

    _db.AuditEvents.Add(NewAudit(request, eventType, actor.Id, from, request.Status,
        decision == Decision.Reject ? request.RejectionReason : null, now));

    try
    {
        await _db.SaveChangesAsync(ct);          // 1 transaksi: update state + insert audit
    }
    catch (DbUpdateConcurrencyException)
    {
        _logger.LogWarning("Concurrency conflict on {RequestId} at version {Version}", id, expectedVersion);
        throw await StaleFromDbAsync(id, ct);
    }
    catch (DbUpdateException ex) when (ex.IsUniqueViolation("ux_audit_events_request_version"))
    {
        _logger.LogWarning("Duplicate audit version on {RequestId} at version {Version}", id, expectedVersion);
        throw await StaleFromDbAsync(id, ct);
    }

    _logger.LogInformation("Access request {RequestId}: {From} -> {To} by {Actor}",
        id, from, request.Status, actor.Email);
    return request;
}
```

`Stale(request)` membuat exception dari entity yang sudah dimuat. `StaleFromDbAsync` membersihkan change tracker, membaca ulang version dan status terbaru dari database, lalu membuat `ConflictException("STALE_VERSION", ...)` dengan extension `currentVersion` dan `currentStatus`, supaya frontend bisa langsung menampilkan status terbaru.

### 9.3 Read endpoint

- **My requests:** `WHERE requester_id = me ORDER BY created_at DESC`, dengan `AsNoTracking()`.
- **Inbox:** query di bagian 7.3 dokumen database. Karena memakai snapshot approver, inbox Bob hanya berisi request direct report-nya (aturan "Manager hanya boleh memproses request direct report").
- **Detail:** cek `CanView`, lalu include audit events terurut berdasarkan `request_version`, dan hitung `allowedActions` dengan `CanDecide`.
- **All (auditor):** cek `actor.IsAuditor`, kalau tidak → 403.

---

## 10. Error handling

```csharp
public abstract class AppException(int statusCode, string code, string message,
                                   Dictionary<string, object?>? extensions = null) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public Dictionary<string, object?> Extensions { get; } = extensions ?? new();
}

public sealed class NotFoundException(string code, string message)
    : AppException(StatusCodes.Status404NotFound, code, message);
public sealed class ForbiddenException(string code, string message)
    : AppException(StatusCodes.Status403Forbidden, code, message);
public sealed class ConflictException(string code, string message, Dictionary<string, object?>? ext = null)
    : AppException(StatusCodes.Status409Conflict, code, message, ext);
public sealed class BusinessRuleException(string code, string message)
    : AppException(StatusCodes.Status422UnprocessableEntity, code, message);

public sealed class AppExceptionHandler(IProblemDetailsService problemDetails,
                                        ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception exception, CancellationToken ct)
    {
        if (exception is not AppException app)
        {
            logger.LogError(exception, "Unhandled exception on {Path}", ctx.Request.Path);
            return false;                         // biarkan default handler membalas 500 ProblemDetails
        }

        logger.LogWarning("{Code} ({Status}) on {Path}: {Message}",
            app.Code, app.StatusCode, ctx.Request.Path, app.Message);

        var problem = new ProblemDetails
        {
            Status = app.StatusCode,
            Title = app.Code,
            Detail = app.Message,
            Instance = ctx.Request.Path
        };
        problem.Extensions["code"] = app.Code;
        foreach (var (key, value) in app.Extensions) problem.Extensions[key] = value;

        ctx.Response.StatusCode = app.StatusCode;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = ctx,
            ProblemDetails = problem,
            Exception = exception
        });
    }
}
```

Error dari trigger database (`check_violation` dari guard) **sengaja tidak di-map** ke 4xx. Kalau trigger terpicu, berarti ada bug di aplikasi, jadi response 500 + log error adalah perilaku yang benar.

---

## 11. Logging & correlation ID

```csharp
public sealed class CorrelationContext
{
    public string? Id { get; set; }
}

public sealed class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-Id";

    public async Task InvokeAsync(HttpContext ctx, CorrelationContext correlation)
    {
        var incoming = ctx.Request.Headers[Header].ToString();
        var correlationId = incoming.Length is > 0 and <= 64 ? incoming : Guid.NewGuid().ToString("N");

        correlation.Id = correlationId;
        ctx.Items[Header] = correlationId;

        // Header dipasang lewat OnStarting, BUKAN langsung. ExceptionHandlerMiddleware memanggil
        // Response.Clear() sebelum menulis ProblemDetails, dan Clear() ikut menghapus header.
        // Kalau dipasang langsung, header hilang tepat pada response error, padahal di situ paling dibutuhkan.
        ctx.Response.OnStarting(() =>
        {
            ctx.Response.Headers[Header] = correlationId;
            return Task.CompletedTask;
        });

        var sw = Stopwatch.StartNew();
        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            try
            {
                await next(ctx);
            }
            finally
            {
                // Satu baris log per request: cukup untuk menelusuri request/error (requirement 3.2)
                logger.LogInformation("HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs} ms",
                    ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
            }
        }
    }
}
```

Middleware ini harus menjadi middleware **paling luar**, yaitu dipasang **sebelum** `UseExceptionHandler()` (lihat bagian 12). Dengan begitu log dari `AppExceptionHandler` dan error 500 tetap berada di dalam scope `CorrelationId`.

Correlation ID juga:
- disimpan di `audit_events.correlation_id` (lewat `CorrelationContext`), sehingga satu baris audit bisa ditelusuri ke baris log yang sama;
- ditambahkan ke setiap `ProblemDetails` sebagai `correlationId`, termasuk 400 otomatis dari `[ApiController]` dan 500. Frontend menampilkannya di banner error.

```csharp
builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
{
    if (c.HttpContext.Items[CorrelationIdMiddleware.Header] is string id)
        c.ProblemDetails.Extensions["correlationId"] = id;
});
```

---

## 12. `Program.cs`

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Logging.AddJsonConsole(o => o.IncludeScopes = true);

builder.Services.AddControllers(o =>
        o.ModelMetadataDetailsProviders.Add(new SystemTextJsonValidationMetadataProvider()))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(
        new JsonStringEnumConverter(allowIntegerValues: false)));

builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = c =>
{
    if (c.HttpContext.Items[CorrelationIdMiddleware.Header] is string id)
        c.ProblemDetails.Extensions["correlationId"] = id;
});
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default"))
     .UseSnakeCaseNamingConvention());

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<CorrelationContext>();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<IApprovalPolicy, ApprovalPolicyV1>();
builder.Services.AddSingleton<ApprovalPolicyRegistry>();
builder.Services.AddScoped<AccessRequestService>();

var app = builder.Build();

// Urutan penting:
// 1) Correlation paling luar  -> log & ProblemDetails dari exception handler tetap punya CorrelationId
// 2) Exception handler        -> mengubah AppException menjadi ProblemDetails 4xx, sisanya 500
// 3) Current user             -> 401 untuk header yang tidak dikenal
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseMiddleware<CurrentUserMiddleware>();
app.MapControllers();

app.Run();

public partial class Program { }   // agar bisa dipakai WebApplicationFactory<Program>
```

---

## 13. Automated tests

### 13.1 Infrastruktur

Test memakai **PostgreSQL asli** lewat Testcontainers (butuh Docker berjalan). EF Core InMemory provider **tidak dipakai** karena tidak menegakkan unique index, FK, CHECK, trigger, maupun row locking, padahal justru itu yang ingin dibuktikan.

```csharp
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:Default", _db.GetConnectionString());

    // xUnit v2: IAsyncLifetime memakai Task. (xUnit v3 memakai ValueTask; sesuaikan.)
    async Task IAsyncLifetime.InitializeAsync()
    {
        await _db.StartAsync();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();   // hentikan host/TestServer dulu (koneksi DB ditutup dengan rapi)
        await _db.DisposeAsync();    // baru hentikan container
    }

    public HttpClient ClientAs(string email)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Email", email);
        return client;
    }
}
```

Setiap test class memakai `IClassFixture<ApiFactory>`, sehingga mendapat database baru. Di dalam satu class, setiap test memakai `ClientRequestId = Guid.NewGuid()` sehingga data tidak saling mengganggu.

### 13.2 Daftar test, dipetakan ke demo scenario

| Demo scenario / requirement | Test |
|---|---|
| Standard request | `Standard_request_is_approved_after_manager_approval` |
| Production request | `Production_request_requires_system_owner_then_approved` |
| Admin access | `Admin_access_on_finance_portal_waits_for_dana` |
| Unauthorized approval | `Carol_cannot_approve_at_manager_stage` (403), `Dana_cannot_approve_crm_request` (403, termasuk saat mengirim version yang salah: tetap 403, bukan 409), `Erin_auditor_cannot_approve` (403), `Unknown_user_header_returns_401` |
| Duplicate submit (sequential) | `Same_client_request_id_returns_same_request` (201 lalu 200, ID sama) |
| Duplicate submit (concurrent) | `Concurrent_duplicate_submits_create_exactly_one_row` (10 POST paralel → satu 201, sisanya 200, `COUNT(*) = 1`) |
| Key dipakai ulang dengan payload beda | `Reused_client_request_id_with_different_payload_returns_409` |
| Concurrent action | `Two_concurrent_approvals_on_same_version_only_one_succeeds` (satu 200, satu 409, hanya satu audit `ManagerApproved`) |
| Stale version deterministik | `Approve_with_stale_version_returns_409_with_current_version` |
| Concurrency di level DB (deterministik) | `Second_save_with_same_original_version_throws_concurrency` (dua `DbContext` memuat request yang sama, keduanya simpan) |
| Rejected request | `Reject_stores_reason_and_audit_and_blocks_further_actions` |
| Reject tanpa reason | `Reject_without_reason_returns_400` |
| Self-approval | Unit test `CanDecide_is_false_for_requester` + DB test constraint `ck_access_requests_manager_not_requester` |
| Audit append-only | `Updating_audit_event_is_rejected_by_database` |
| Business rule create | `User_without_manager_gets_422`, `System_owner_requesting_own_high_risk_app_gets_422` |
| Inbox | `Bob_inbox_contains_only_pending_direct_reports` |
| Validasi input | `Integer_enum_value_returns_400` (bukan 500), `Blank_justification_returns_400` |
| Audit atomic dengan state | Di setiap test workflow, assert `detail.AuditTrail.Count == detail.Version` dan `auditTrail.Last().ToStatus == detail.Status`. Invariant ini murah dan langsung membuktikan "setiap transisi punya tepat satu audit event" |
| Correlation ID di error | `Error_response_contains_correlation_id` (header `X-Correlation-Id` + `correlationId` di body pada 403/409) |

> **Self-approval dengan seed data:** dengan seed bawaan, kasus self-approval tidak bisa terjadi lewat alur normal. Alice bukan approver; Carol/Dana tidak bisa membuat request karena tidak punya manager; dan System Owner yang meminta aksesnya sendiri sudah ditolak saat create. Aturan ini tetap ditegakkan di tiga lapis: service, `CanDecide`, dan CHECK constraint. Bukti test-nya ada di unit test dan DB test. Tulis penjelasan ini di REVIEW.md.

Contoh test concurrency:

```csharp
[Fact]
public async Task Two_concurrent_approvals_on_same_version_only_one_succeeds()
{
    var created = await CreateAsync(Users.Alice, Apps.Crm, "Production", "Read");

    var body = new { expectedVersion = created.Version };
    var responses = await Task.WhenAll(
        _factory.ClientAs(Users.Bob).PostAsJsonAsync($"/api/access-requests/{created.Id}/approve", body),
        _factory.ClientAs(Users.Bob).PostAsJsonAsync($"/api/access-requests/{created.Id}/approve", body));

    Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
    Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));

    var detail = await GetDetailAsync(Users.Alice, created.Id);
    Assert.Equal("PendingSystemOwnerApproval", detail.Status);
    Assert.Equal(2, detail.Version);
    Assert.Single(detail.AuditTrail, e => e.EventType == "ManagerApproved");
}
```

Test ini benar apa pun urutan eksekusinya. Kalau dua request benar-benar bersamaan, yang kalah gagal di concurrency token. Kalau ternyata berurutan, yang kedua gagal di cek version awal. Hasil akhirnya tetap satu 200 dan satu 409.

### 13.3 Menjalankan test

```bash
# Docker harus berjalan (Testcontainers)
cd backend
dotnet test
```

---

## 14. File `.http` untuk demo manual

Simpan di `AccessRequestHub.Api.http`. Sintaks *named request* (`# @name` + `{{nama.response.body.$.id}}`) didukung VS Code REST Client dan Visual Studio 2022 17.12+. File ini berguna untuk mendemokan scenario yang **tidak bisa** dipicu dari UI (karena tombolnya disembunyikan), misalnya unauthorized approval dan duplicate submit.

> `clientRequestId` di file ini sengaja tetap supaya demo duplicate submit jelas. Kalau file dijalankan ulang di database yang sama, request pertama akan mendapat 200 (replay), bukan 201. Untuk demo bersih: reset database (01-DATABASE.md bagian 10) atau ganti suffix key.

```http
@base = http://localhost:5080
@crm = 10000000-0000-0000-0000-000000000001
@finance = 10000000-0000-0000-0000-000000000002

### [Standard] Alice -> CRM / NonProduction / Read
# @name standard
POST {{base}}/api/access-requests
X-User-Email: alice@example.local
Content-Type: application/json

{ "clientRequestId": "demo-std-001", "applicationId": "{{crm}}",
  "environment": "NonProduction", "accessLevel": "Read", "justification": "Akses baca CRM untuk onboarding" }

### [Standard] Bob approve -> Approved
POST {{base}}/api/access-requests/{{standard.response.body.$.id}}/approve
X-User-Email: bob@example.local
Content-Type: application/json

{ "expectedVersion": 1 }

### [Production] Alice -> CRM / Production / Read
# @name prod
POST {{base}}/api/access-requests
X-User-Email: alice@example.local
Content-Type: application/json

{ "clientRequestId": "demo-prod-001", "applicationId": "{{crm}}",
  "environment": "Production", "accessLevel": "Read", "justification": "Investigasi tiket support" }

### [Duplicate submit] Key yang sama dikirim ulang -> 200, ID sama dengan request di atas
POST {{base}}/api/access-requests
X-User-Email: alice@example.local
Content-Type: application/json

{ "clientRequestId": "demo-prod-001", "applicationId": "{{crm}}",
  "environment": "Production", "accessLevel": "Read", "justification": "Investigasi tiket support" }

### [Unauthorized] Carol mencoba approve di tahap Manager -> 403 NOT_ASSIGNED_APPROVER
POST {{base}}/api/access-requests/{{prod.response.body.$.id}}/approve
X-User-Email: carol@example.local
Content-Type: application/json

{ "expectedVersion": 1 }

### [Unauthorized] Dana (bukan owner CRM) mencoba approve -> 403
POST {{base}}/api/access-requests/{{prod.response.body.$.id}}/approve
X-User-Email: dana@example.local
Content-Type: application/json

{ "expectedVersion": 1 }

### [Production] Bob approve -> PendingSystemOwnerApproval (version 2)
POST {{base}}/api/access-requests/{{prod.response.body.$.id}}/approve
X-User-Email: bob@example.local
Content-Type: application/json

{ "expectedVersion": 1 }

### [Concurrent/stale] Bob approve lagi dengan version lama -> 409 STALE_VERSION
POST {{base}}/api/access-requests/{{prod.response.body.$.id}}/approve
X-User-Email: bob@example.local
Content-Type: application/json

{ "expectedVersion": 1 }

### [Production] Carol approve -> Approved
POST {{base}}/api/access-requests/{{prod.response.body.$.id}}/approve
X-User-Email: carol@example.local
Content-Type: application/json

{ "expectedVersion": 2 }

### [Admin access] Alice -> Finance Portal / NonProduction / Admin
# @name admin
POST {{base}}/api/access-requests
X-User-Email: alice@example.local
Content-Type: application/json

{ "clientRequestId": "demo-admin-001", "applicationId": "{{finance}}",
  "environment": "NonProduction", "accessLevel": "Admin", "justification": "Rekonsiliasi akhir bulan" }

### [Admin access] Bob approve -> menunggu Dana
POST {{base}}/api/access-requests/{{admin.response.body.$.id}}/approve
X-User-Email: bob@example.local
Content-Type: application/json

{ "expectedVersion": 1 }

### [Rejected] Alice membuat request
# @name rej
POST {{base}}/api/access-requests
X-User-Email: alice@example.local
Content-Type: application/json

{ "clientRequestId": "demo-rej-001", "applicationId": "{{crm}}",
  "environment": "NonProduction", "accessLevel": "Read", "justification": "Coba-coba" }

### [Rejected] Reject tanpa reason -> 400
POST {{base}}/api/access-requests/{{rej.response.body.$.id}}/reject
X-User-Email: bob@example.local
Content-Type: application/json

{ "expectedVersion": 1, "reason": "   " }

### [Rejected] Reject dengan reason -> Rejected
POST {{base}}/api/access-requests/{{rej.response.body.$.id}}/reject
X-User-Email: bob@example.local
Content-Type: application/json

{ "expectedVersion": 1, "reason": "Justifikasi tidak cukup jelas" }

### [Rejected] Approve setelah Rejected (version terbaru) -> 409 INVALID_TRANSITION
POST {{base}}/api/access-requests/{{rej.response.body.$.id}}/approve
X-User-Email: bob@example.local
Content-Type: application/json

{ "expectedVersion": 2 }

### [Audit] Erin melihat detail + audit timeline
GET {{base}}/api/access-requests/{{rej.response.body.$.id}}
X-User-Email: erin@example.local
```

---

## 15. Urutan implementasi & rencana commit

| # | Langkah | Estimasi | Commit / tag |
|---|---|---|---|
| 1 | Repo, `.gitignore`, PLAN.md awal (lihat bagian 18.1), kerangka dokumen | 20 m | `docs: initial plan` → **tag `assessment-start`** |
| 2 | Solution, docker-compose, tool manifest, DbContext, entity, configuration, seed, migration `Initial` | 60 m | `feat(db): schema, constraints, seed` |
| 3 | Migration `AddDatabaseGuards` (trigger) | 15 m | `feat(db): terminal, insert & append-only guards` |
| 4 | Middleware user + correlation, exception handler, policy v1 | 30 m | `feat(api): auth simulation, problem details, policy v1` |
| 5 | Create + idempotency + test-nya | 45 m | `feat(api): idempotent create` |
| 6 | Approve/Reject + concurrency + test-nya | 60 m | `feat(api): approval workflow with optimistic concurrency` |
| 7 | List, detail, inbox, `me` + test authorization | 30 m | `feat(api): read endpoints` |
| 8 | Frontend (lihat 04-FRONTEND.md) | 75 m | beberapa commit kecil |
| 9 | README, AI_USAGE, REVIEW, INTEGRITY (bagian 18); jalankan semua test dari clone bersih | 40 m | `docs: review & integrity` → **tag `phase-1-complete`** |

Total sekitar **6 jam**, menyisakan ruang untuk Phase 2 dalam batas 8 jam. Kalau mulai molor, potong UI dulu (bukan test atau backend), dan catat di REVIEW.md.

Dua kebiasaan kecil yang menghemat waktu di langkah 9:
- **Catat interaksi AI saat terjadi** (satu-dua baris di draft AI_USAGE.md), jangan merekonstruksi di akhir. Hasilnya lebih jujur dan lebih spesifik.
- **Catat setiap perubahan plan** di section "Perubahan plan" PLAN.md begitu terjadi, lengkap dengan alasannya. Dokumen assessment meminta ini secara eksplisit.

```bash
git tag assessment-start
git push origin main --tags     # push segera, supaya batas awal assessment sudah tercatat di remote
# ... kerja ...
dotnet test                     # harus hijau sebelum tag
git tag phase-1-complete
git push origin main --tags
git ls-remote --tags origin     # verifikasi kedua tag benar-benar ada di remote
```

---

## 16. Checklist Definition of Done Phase 1

- [ ] Dari clone bersih: `docker compose up -d --wait db` + `dotnet tool restore` + `dotnet ef database update` membangun schema dan seed dari nol
- [ ] Tujuh demo scenario lolos (otomatis + bisa didemokan manual lewat UI / `.http`)
- [ ] Semua aturan bisnis ditegakkan di backend, bukan hanya di UI
- [ ] Error membedakan 400 / 403 / 404 / 409 (+ 401, 422), dan input tidak valid tidak pernah menghasilkan 500
- [ ] Setiap transisi punya audit event dalam transaksi yang sama
- [ ] `dotnet test` hijau, perintahnya (dan syarat Docker) tertulis di README
- [ ] Tidak ada credential, token, atau data perusahaan nyata di repo
- [ ] PLAN.md, AI_USAGE.md, REVIEW.md, INTEGRITY.md, README.md tersedia dan isinya memenuhi bagian 18
- [ ] Tag `assessment-start` dan `phase-1-complete` ada di remote
- [ ] Phase 2 **belum** dikerjakan sebelum dokumen CR diterima

## 17. Kandidat isi REVIEW.md

Format tabel mengikuti dokumen assessment: **finding, severity, action, status, evidence**. Kolom evidence menunjuk ke sesuatu yang bisa dicek assessor (nama test, commit, file, atau output perintah).

| Finding | Severity | Action | Status | Evidence |
|---|---|---|---|---|
| Auth via header `X-User-Email` bisa dipalsukan | High (di production) | Ganti OIDC/SSO di production; authorization tetap server-side | Accepted (by design assessment) | `CurrentUserMiddleware.cs`, test `Unknown_user_header_returns_401` |
| User yang boleh melihat request tetapi bukan approver tahap ini mendapat 409 (bukan 403) saat mengirim version lama | Low | User tak terkait sudah ditolak lebih dulu dengan 403 | Accepted | Bagian 9.2, test `Dana_cannot_approve_crm_request` |
| Superuser DB bisa men-drop trigger audit | Medium | Di production: role DB aplikasi tanpa hak `UPDATE`/`DELETE`/`DDL` di `audit_events` | Deferred | `AddDatabaseGuards` migration, test `Updating_audit_event_is_rejected_by_database` |
| Password Postgres lokal ada di `appsettings.Development.json` | Low | Nilai khusus container lokal, bukan credential nyata | Accepted | `docker-compose.yml`, README |
| Tidak ada pagination di list | Low | Tambah paging kalau data bertambah | Deferred | — |
| Tidak ada fitur cancel/withdraw oleh requester | Low | Di luar scope Phase 1 | Deferred | — |
| Rate limiting / anti-abuse tidak ada | Medium | `AddRateLimiter` di production | Deferred | — |

Tambahkan juga finding yang **benar-benar kamu temukan** saat review kode hasil AI (bug yang sudah diperbaiki dengan status `Fixed` + commit/test sebagai evidence). Finding seperti ini adalah bukti review terkuat.

---

## 18. Dokumen evidence wajib

Bagian ini memetakan bagian 4–6 dokumen assessment ke isi minimum setiap file. Semua file berada di root repo.

### 18.1 PLAN.md

Dibuat **sebelum** coding besar (commit pertama, sebelum tag `assessment-start`), lalu terus diperbarui.

| Section | Isi minimum | Sumber |
|---|---|---|
| Problem understanding | Masalah bisnis dengan kata-kata sendiri, scope Phase 1, apa yang sengaja tidak dibangun | PDF bagian 2 & 3.5 |
| Architecture & data model ringkas | Diagram/daftar komponen, ERD ringkas, state model | 01 bagian 2–3, 02 bagian 2 |
| Implementation order | Tabel urutan + estimasi | 02 bagian 15 |
| Test strategy | Kenapa Testcontainers, peta demo scenario → test | 02 bagian 13 |
| Trade-off penting | **Pilih 2–3 saja** yang paling berdampak, misalnya: (1) snapshot approver + `is_high_risk`, (2) urutan cek di approve/reject, (3) satu project API vs Clean Architecture. Sisanya cukup satu baris atau pindah ke REVIEW.md | 01 bagian 1, 02 bagian 2 & 9.2 |
| Asumsi | `NO_MANAGER`, `NO_ELIGIBLE_APPROVER`, idempotency key per requester | 01 bagian 1 |
| **Perubahan plan** | Log bertanggal: apa yang berubah dari plan awal dan kenapa | Diisi selama implementasi |

### 18.2 AI_USAGE.md

- **2–5 interaksi AI yang paling berpengaruh**, masing-masing dengan format: *ask → suggestion → accepted / changed / rejected → why*. Pilih yang benar-benar mengubah arah desain atau menemukan bug, bukan yang sepele.
- Section **"Three things AI got wrong"**: tiga kesalahan nyata dari output AI, cara kamu mendeteksinya (test gagal, error runtime, baca dokumentasi), dan perbaikannya. Sertakan evidence (nama test atau commit).
- Sebutkan tools yang dipakai (misalnya Claude, Copilot) dan untuk bagian apa.

### 18.3 REVIEW.md

- Tabel self-review production readiness: **finding, severity, action, status, evidence** (lihat bagian 17).
- **Known limitations.**
- **Deferred work** beserta alasan prioritasnya. Kalau timebox habis, tulis juga apa yang belum selesai dan state terakhir yang stabil.

### 18.4 INTEGRITY.md

Gunakan teks deklarasi minimum dari dokumen assessment apa adanya, lalu isi field-nya:

```markdown
I confirm that this submission represents my own engineering work.
AI-assisted tools were used as documented in AI_USAGE.md.
I reviewed the submitted code and can explain its architecture, behavior,
known limitations, security implications, and trade-offs.

Date:
Primary AI tools used:
External human assistance: None / describe
Starter/template code used: (mis. `dotnet new webapi`, `npm create vite` react-ts template)
External repositories/snippets copied or adapted:
```

### 18.5 README.md

| Section | Isi |
|---|---|
| Prerequisites | .NET SDK 10, Docker (wajib, juga untuk test), Node.js versi yang didukung Vite (lihat 04 bagian 1) |
| Setup | `git clone`, `dotnet tool restore`, `npm install` di `frontend/` |
| Migrate / Seed | Perintah dari 01-DATABASE.md bagian 10, termasuk cara reset database |
| Run | Backend (`dotnet run --project backend/src/AccessRequestHub.Api`, port 5080) dan frontend (`npm run dev`, port 5173) |
| Test | `cd backend && dotnet test` + catatan bahwa Docker harus berjalan |
| Demo users | Tabel 5 user beserta perannya |
| Demo flow | Tujuh scenario: lewat UI (04 bagian 7) dan lewat `AccessRequestHub.Api.http` (02 bagian 14) |
| Struktur repo & dokumen | Link ke PLAN.md, AI_USAGE.md, REVIEW.md, INTEGRITY.md |

Sebelum tag `phase-1-complete`, **ikuti README dari clone baru** (`git clone` ke folder lain) sampai test hijau dan demo jalan. Ini cara paling murah untuk membuktikan reproducibility.

### 18.6 Submission

- Repository URL lebih disukai. Pastikan tag ikut ter-push (`git ls-remote --tags origin`).
- Kalau harus ZIP, history Git wajib ikut: sertakan folder `.git`, atau buat bundle:
  ```bash
  git bundle create access-request-hub.bundle --all
  git bundle verify access-request-hub.bundle
  ```
- Pastikan `bin/`, `obj/`, `node_modules/`, dan file `.env` lokal tidak ikut ter-commit.
