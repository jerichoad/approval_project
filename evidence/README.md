# README.md

Access Request Hub, Phase 1. Backend ASP.NET Core 8 + PostgreSQL 17, frontend React + Vite.

## 1. Prerequisites

| Tool       | Versi yang dipakai saat development                                          |
| ---------- | ---------------------------------------------------------------------------- |
| .NET SDK   | 8.0 (atau lebih baru yang masih mendukung target `net8.0`)                   |
| Node.js    | 24.x                                                                         |
| npm        | 11.x                                                                         |
| PostgreSQL | 17 (lewat Docker, atau instalasi lokal)                                      |
| Docker     | Opsional, hanya untuk `docker-compose.yml` dan test backend (Testcontainers) |

## 2. Struktur repo

```
approval_project/
├── backendApproval/
│   ├── backendApproval/            API (.NET 8)
│   └── backendApproval.Tests/      xUnit + Testcontainers
├── frontendApproval/                React + Vite
├── files_new_net/                   Dokumen rancangan (01-DATABASE, 02-BACKEND-PHASE-1, 03-FRONTEND)
├── evidence/                        Dokumen ini (PLAN, AI_USAGE, REVIEW, INTEGRITY, README)
├── docker-compose.yml
└── dotnet-tools.json
```

## 3. Setup database

### Opsi A: Docker

```bash
docker compose up -d --wait db
```

### Opsi B: PostgreSQL lokal (tanpa Docker)

Buat database secara manual:

```sql
CREATE DATABASE access_request_hub;
```

Lalu sesuaikan connection string di `backendApproval/backendApproval/appsettings.Development.json`, atau simpan lewat User Secrets supaya password tidak ikut ter-commit:

```bash
cd backendApproval/backendApproval
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=access_request_hub;Username=postgres;Password=GANTI_DENGAN_PASSWORD_ANDA"
```

## 4. Migrate dan seed

```bash
cd approval_project
dotnet tool restore
dotnet ef database update --project backendApproval/backendApproval
```

Perintah ini membuat schema, trigger (`AddDatabaseGuards`), dan seed data (lihat bagian 6) sekaligus. Kalau pakai Visual Studio: jadikan `backendApproval` sebagai project default di Package Manager Console, install `Microsoft.EntityFrameworkCore.Tools`, lalu jalankan `Update-Database`.

## 5. Menjalankan

### Backend

```bash
dotnet run --project backendApproval/backendApproval
```

API jalan di `http://localhost:5080`. Swagger tersedia di `http://localhost:5080/swagger` saat environment Development.

### Frontend

```bash
cd frontendApproval
npm install
npm run dev
```

Buka `http://localhost:5173`. Vite proxy `/api` ke `http://localhost:5080`, jadi tidak perlu konfigurasi CORS.

## 6. Demo users

Seed data deterministik, cocok untuk setiap demo:

| User  | Email               | Manager | System Owner dari | Auditor |
| ----- | ------------------- | ------- | ----------------- | ------- |
| Alice | alice@example.local | Bob     | -                 | Tidak   |
| Bob   | bob@example.local   | -       | -                 | Tidak   |
| Carol | carol@example.local | -       | CRM               | Tidak   |
| Dana  | dana@example.local  | -       | Finance Portal    | Tidak   |
| Erin  | erin@example.local  | -       | -                 | Ya      |

Hanya Alice yang punya manager, jadi hanya Alice yang bisa membuat access request baru lewat UI. User lain bisa approve, reject, atau melihat (Erin bisa melihat semua request).

## 7. Test

Butuh Docker berjalan (Testcontainers menjalankan PostgreSQL asli per test class).

```bash
cd backendApproval
dotnet test
```

27 test tersebar di lima file: `WorkflowTests`, `AuthorizationTests`, `IdempotencyTests`, `ConcurrencyTests`, `DatabaseGuardTests`. Kalau backend sedang berjalan dari `dotnet run` di proses lain, hentikan dulu sebelum `dotnet test` supaya build tidak gagal mengunci file `.exe`.

Frontend belum punya automated test (lihat REVIEW.md).

## 8. Demo flow

Tujuh skenario ini bisa didemokan lewat UI (`http://localhost:5173`) kecuali disebutkan sebaliknya.

| Skenario              | Langkah                                                                                                                                                                                                                    | Hasil                                                                        |
| --------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| Standard request      | Alice: New Request, CRM, Non-Production, Read. Bob: Inbox, buka detail, Approve.                                                                                                                                           | Approved (version 2)                                                         |
| Production request    | Alice: CRM, Production, Read. Bob approve. Carol approve.                                                                                                                                                                  | Setelah Bob: menunggu Carol. Setelah Carol: Approved                         |
| Admin access          | Alice: Finance Portal, Non-Production, Admin. Bob approve.                                                                                                                                                                 | Menunggu Dana. Dana approve: Approved                                        |
| Rejected request      | Alice buat request. Bob: Reject dengan alasan.                                                                                                                                                                             | Rejected, alasan dan audit tampil, tombol aksi hilang                        |
| Concurrent action     | Bob membuka detail yang sama di dua tab. Approve di tab 1, lalu Approve di tab 2 tanpa refresh.                                                                                                                            | Tab 2 menampilkan banner konflik 409 `STALE_VERSION` dan memuat data terbaru |
| Unauthorized approval | Tombol tidak tampil untuk user yang tidak berhak, jadi pakai `backendApproval/backendApproval/backendApproval.http` (Visual Studio 17.12+ atau VS Code REST Client). Carol atau Dana approve request CRM di tahap Manager. | 403 `NOT_ASSIGNED_APPROVER`                                                  |
| Duplicate submit      | Kirim `POST /api/access-requests` dua kali dengan `clientRequestId` sama, lewat file `.http`.                                                                                                                              | 201 lalu 200, `id` sama                                                      |

File `.http` memakai `clientRequestId` tetap (`demo-std-001`, `demo-prod-001`, dan seterusnya). Kalau dijalankan ulang di database yang sama, request pertama mendapat 200 (replay), bukan 201. Untuk demo bersih, buat ulang database lalu jalankan `dotnet ef database update`.

## 9. Error handling

Semua error 4xx dan 5xx berbentuk `ProblemDetails` dengan field tambahan `code` dan `correlationId`. Header `X-Correlation-Id` juga dikirim di setiap response, termasuk yang sukses, untuk ditelusuri di log server.

## 10. Dokumen lain

- `evidence/PLAN.md`: pemahaman masalah, arsitektur, urutan implementasi, strategi test, trade-off.
- `evidence/AI_USAGE.md`: interaksi AI yang berpengaruh dan kesalahan AI yang ditemukan.
- `evidence/REVIEW.md`: self-review kesiapan produksi, known limitations, deferred work.
- `evidence/INTEGRITY.md`: deklarasi ownership dan sumber yang diadaptasi.
