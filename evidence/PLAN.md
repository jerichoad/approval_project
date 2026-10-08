# PLAN.md

Access Request Hub, Phase 1. Dokumen rancangan awal ada di `files_new_net/` (`01-DATABASE.md`, `02-BACKEND-PHASE-1.md`, `03-FRONTEND.md`). File ini merangkum rencana itu dan mencatat apa saja yang berubah saat implementasi.

## 1. Problem understanding

Karyawan mengajukan akses ke sebuah aplikasi (CRM, Finance Portal) untuk environment dan access level tertentu. Aturan bisnis yang harus dijaga backend:

1. Semua request harus di-approve Manager requester lebih dulu.
2. Request high-risk (Production atau Admin) lanjut ke System Owner aplikasi setelah Manager.
3. Requester tidak boleh memproses request miliknya sendiri.
4. Hanya approver yang ditugaskan untuk tahap saat ini yang boleh approve atau reject.
5. Reject wajib punya alasan.
6. Approved dan Rejected adalah status final.
7. Setiap perubahan status punya tepat satu audit event, disimpan dalam transaksi yang sama.
8. Submit ganda (double click, retry setelah timeout) tidak boleh membuat request ganda.
9. Dua approver yang menekan tombol bersamaan tidak boleh menghasilkan dua transisi.
10. Auditor boleh melihat semua request, tetapi tidak otomatis menjadi approver.

Autentikasi boleh disimulasikan. Saya memakai header `X-User-Email` yang dicocokkan ke tabel `users`.

## 2. Arsitektur

| Lapisan  | Pilihan                                                                |
| -------- | ---------------------------------------------------------------------- |
| Backend  | ASP.NET Core Web API, .NET 8, Controllers                              |
| ORM      | EF Core 8 + Npgsql, naming snake_case lewat `EFCore.NamingConventions` |
| Database | PostgreSQL 17 (Docker Compose atau instalasi lokal)                    |
| Error    | `ProblemDetails` dengan field `code` dan `correlationId`               |
| Frontend | React 19 + Vite, React Router, TanStack Query, Tailwind CSS v4         |
| Test     | xUnit + `WebApplicationFactory` + Testcontainers (PostgreSQL asli)     |

Satu project API saja (`backendApproval/backendApproval`), dengan folder `Domain`, `Policies`, `Services`, `Data`, `Auth`, `Errors`, `Observability`, `Controllers`, `Contracts`.

Alur request: `CorrelationIdMiddleware` (paling luar) -> `UseExceptionHandler` -> `CurrentUserMiddleware` (401 kalau header tidak dikenal) -> controller -> `AccessRequestService`.

## 3. Data model ringkas

| Tabel             | Isi penting                                                                                                                                                                   |
| ----------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `users`           | email unik lowercase, `manager_id`, `is_auditor`                                                                                                                              |
| `applications`    | `code` unik, `system_owner_id`                                                                                                                                                |
| `policy_versions` | `code` (PK), `is_active` dengan partial unique index (hanya satu yang aktif)                                                                                                  |
| `access_requests` | snapshot `policy_version`, `is_high_risk`, `manager_approver_id`, `system_owner_approver_id`; `version` sebagai concurrency token; unique `(requester_id, client_request_id)` |
| `audit_events`    | append-only, unique `(access_request_id, request_version)`                                                                                                                    |

Enum disimpan sebagai text dan dijaga CHECK constraint. Tiga trigger dipasang lewat migration `AddDatabaseGuards`: guard update (terminal state, transisi legal, version +1, kolom identitas tidak berubah), guard insert (wajib mulai di `PendingManagerApproval` versi 1), dan audit append-only (tolak UPDATE, DELETE, TRUNCATE).

State machine:

```
PendingManagerApproval --approve low-risk--> Approved
PendingManagerApproval --approve high-risk--> PendingSystemOwnerApproval --approve--> Approved
PendingManagerApproval / PendingSystemOwnerApproval --reject--> Rejected
```

## 4. Implementation order

1. Setup repo: `.gitignore`, `docker-compose.yml`, tool manifest `dotnet-ef`.
2. Domain: enum, entity, `AccessRequestAuthorization` (`CanView`, `AssignedApprover`, `CanDecide`).
3. Data: `AppDbContext`, entity configuration, seed `HasData`, migration `Initial`.
4. Migration `AddDatabaseGuards` (trigger).
5. Policy v1 + registry, middleware user dan correlation, exception handler.
6. Service: create idempotent, approve/reject dengan optimistic concurrency, endpoint baca.
7. Controller dan `Program.cs`.
8. Test project dan 27 test.
9. Frontend: theme, layout dashboard, user switcher, lima halaman.
10. Dokumen evidence (folder ini).

## 5. Test strategy

- Semua test backend jalan terhadap PostgreSQL asli lewat Testcontainers. EF InMemory tidak dipakai karena tidak menegakkan unique index, CHECK, trigger, maupun row lock.
- Satu container per test class (`IClassFixture<ApiFactory>`). Data tidak dibersihkan karena audit append-only memblokir DELETE dan TRUNCATE; setiap test memakai `clientRequestId` baru.
- Test dibagi lima class: `WorkflowTests` (12), `AuthorizationTests` (8), `IdempotencyTests` (3), `ConcurrencyTests` (2), `DatabaseGuardTests` (2).
- Invariant audit dicek di test workflow: jumlah audit event = `version`, event terakhir = status sekarang, `requestVersion` berurutan 1..n.
- Race condition diuji dua cara: lewat HTTP paralel (`Task.WhenAll`) dan deterministik lewat dua `DbContext` yang memuat row yang sama.
- Frontend tidak punya test otomatis. Perilaku bisnisnya sudah dibuktikan di backend, dan `03-FRONTEND.md` menyebut test frontend opsional.

## 6. Trade-off penting

**Snapshot approver di row request.** `manager_approver_id` dan `system_owner_approver_id` disimpan saat create. Keuntungan: request lama tetap jelas siapa approver-nya walaupun struktur organisasi berubah, dan Phase 2 bisa mengubah policy tanpa mengubah arti request lama. Kerugian: kalau manager keluar dari perusahaan, request yang masih pending akan macet sampai ada fitur reassign (belum ada).

**Urutan pengecekan approve/reject.** Urutannya: 404, lalu `CanView` (403), lalu version (409), lalu terminal (409), lalu self-approval (403), lalu assigned approver (403). Cek version sengaja diletakkan sebelum cek approver supaya klik kedua Bob mendapat 409 `STALE_VERSION`, bukan 403 yang menyesatkan. Cek `CanView` di depan mencegah user yang tidak terkait (misalnya Dana untuk request CRM) membaca `currentVersion` dan `currentStatus` dari response 409. Konsekuensinya, user yang boleh melihat tetapi bukan approver tahap ini akan mendapat 409 kalau mengirim version lama.

**Guard di database, bukan hanya di C#.** Unique index, CHECK, dan trigger menjaga invariant walaupun ada bug di service. Biayanya: test tidak bisa membersihkan data (append-only), jadi setiap test class butuh database baru, dan suite test butuh sekitar satu menit.

## 7. Perubahan plan selama implementasi

| Rencana awal                                                           | Implementasi                                                                                    | Alasan                                                                                                                                            |
| ---------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------- |
| Folder `backend/src/AccessRequestHub.Api` dan `tests/...`              | `backendApproval/backendApproval` dan `backendApproval/backendApproval.Tests`, solution `.slnx` | Project sudah dibuat user dengan nama dan struktur itu                                                                                            |
| Tool manifest di `.config/dotnet-tools.json`                           | `dotnet-tools.json` di root repo                                                                | Lokasi default `dotnet new tool-manifest` di SDK yang terpasang                                                                                   |
| Test `System_owner_requesting_own_high_risk_app_gets_422` memakai seed | Test membuat user dan aplikasi sendiri                                                          | Seed tidak punya user yang punya manager sekaligus menjadi System Owner, jadi aturan `NO_ELIGIBLE_APPROVER` tidak bisa dipicu dari seed           |
| Inbox: status + approver                                               | Ditambah filter `requester_id <> me`                                                            | Lapisan tambahan supaya request milik sendiri tidak pernah muncul di inbox                                                                        |
| Hanya Docker untuk database                                            | Docker atau PostgreSQL lokal                                                                    | User menjalankan tanpa Docker lewat Visual Studio                                                                                                 |
| Frontend TypeScript (`react-ts`)                                       | JavaScript (JSX)                                                                                | Project `frontendApproval` sudah dibuat dengan template JavaScript                                                                                |
| React Router v7                                                        | React Router 8.4                                                                                | Versi terbaru saat `npm install`                                                                                                                  |
| Styling "CSS sederhana"                                                | Tailwind CSS v4 dengan theme dari `luwes-weather-station/frontend`                              | Permintaan user untuk memakai tema dashboard tersebut                                                                                             |
| Error reject ditampilkan di banner halaman                             | Error reject ditampilkan di dalam `RejectDialog`                                                | Banner tertutup modal dialog yang masih terbuka                                                                                                   |
