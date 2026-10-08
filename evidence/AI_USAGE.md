# AI_USAGE.md

Tool AI: opencode (CLI agent), model `9router/api.jerichoad` (gabungan Claude Opus 5.5 dan Gemini 3.8 Flash High). AI membaca dokumen rancangan saya di `files_new_net/`, menulis kode, menjalankan build dan test, lalu saya review hasilnya.

## Interaksi yang paling berpengaruh

### 1. Implementasi backend dari plan, di .NET 8

- Ask: implementasikan `02-BACKEND-PHASE-1.md` dan `01-DATABASE.md` di project `backendApproval`, memakai .NET 8.
- Suggestion: AI mencari versi package yang cocok dengan .NET 8, lalu memakai EF Core 8.0.11, Npgsql EF 8.0.11, `EFCore.NamingConventions` 8.0.3, dan `dotnet-ef` 8.0.11. Kode domain, service, middleware, migration, dan trigger mengikuti plan. AI menambahkan 27 test dan menjalankannya terhadap PostgreSQL di Testcontainers.
- Status: accepted.
- Why: struktur kode dan aturan bisnis tetap sesuai plan. Yang berubah hanya versi package dan nama project, yang memang harus mengikuti project yang sudah ada.

### 2. Menjalankan tanpa Docker, lewat Visual Studio

- Ask: cara menjalankan di lokal tanpa Docker, memakai Visual Studio.
- Suggestion: buat database `access_request_hub` di PostgreSQL lokal, ubah connection string (atau simpan di User Secrets supaya password tidak masuk git), jalankan migration lewat terminal (`dotnet ef database update`) atau Package Manager Console (`Update-Database`, butuh package `Microsoft.EntityFrameworkCore.Tools`). AI juga memberi tahu bahwa test tetap butuh Docker karena Testcontainers.
- Status: accepted sebagai panduan setup. Langkahnya dicatat di README.
- Why: kode tidak perlu diubah; hanya konfigurasi.

### 3. Frontend dengan tema dari project lain

- Ask: implementasikan `03-FRONTEND.md` di `frontendApproval` dengan tema dashboard dari `E:\Herd\luwes-weather-station\frontend`.
- Suggestion: AI memindahkan token warna (navy, gold, tint status), font, pola `cn()`, komponen `Button`/`Select`/`Field`/`Dialog`, `DashboardPageHeader`, `SectionCard`, `MetricCard`, tabel, dan state loading/empty/error. Karena project sumbernya Next.js + TypeScript dan project tujuan Vite + JavaScript, AI menulis ulang komponen itu ke JSX tanpa API Next.js.
- Status: changed. Spec frontend meminta TypeScript; implementasi memakai JavaScript mengikuti template project yang sudah ada.
- Why: mengganti bahasa project di tengah jalan menambah pekerjaan setup tanpa mengubah perilaku yang diuji assessment.

### 4. Error NO_MANAGER saat membuat request

- Ask: muncul pesan "Requester tidak memiliki manager untuk approval."
- Suggestion: AI menjelaskan bahwa ini perilaku yang benar (`422 NO_MANAGER`), karena di seed hanya Alice yang punya manager. AI menawarkan field `hasManager` di `/api/me` supaya halaman New Request bisa memberi tahu sebelum form diisi.
- Status: rejected untuk sekarang (belum dikerjakan). Dicatat sebagai deferred work di REVIEW.md.
- Why: aturan backend sudah benar; tambahan ini hanya perbaikan UX.

### 5. Standardisasi Response Envelope (Status & Data) serta Try-Catch Error Handling

- Ask: user meminta response API memiliki field `Status` (S atau E) dan `Data` (payload response). Pemanggilan API di controller diberi `try-catch` untuk menangkap error, dan saat ada exception, `Status` diberi tanda `E`. Dokumen evidence dan koleksi Bruno disesuaikan.
- Suggestion: implementasikan `ApiResponse<T>` dan `ApiErrorResponse` dengan PascalCase `Status` dan `Data`. Buat `BaseApiController` dengan helper `ExecuteAsync(Func<Task<IActionResult>>)` yang memiliki blok `try-catch` terpusat untuk menangkap `AppException` (4xx) dan unhandled exceptions (500), membungkusnya menjadi `{ Status: "E", Data: { code, message, correlationId, fieldErrors, extensions } }`. Model validation failure (400) dibungkus via `InvalidModelStateResponseFactory`. Update frontend client (`api/client.js`), test xUnit (`TestFixtures.cs`), file `.http`, koleksi Bruno, dan dokumen evidence.
- Status: accepted.
- Why: standardisasi envelope konsisten di seluruh lapisan (backend, frontend, automated test, dan dokumen evidence) tanpa merusak semantik status code HTTP.

## Three things AI got wrong

Bagian ini bukan tentang plan yang salah. Plan di `files_new_net/` saya pakai apa adanya. Yang dicatat di sini adalah kesalahan pada output AI saat menerjemahkan plan menjadi kode dan instruksi, yang saya temukan saat review atau saat menjalankan build/test.

1. **Test yang tidak menguji aturannya.** Versi pertama `System_owner_requesting_own_high_risk_app_gets_422` hanya membuat request Alice ke CRM Production dan mengharapkan 201. Nama test menjanjikan 422 `NO_ELIGIBLE_APPROVER`, tetapi isinya tidak pernah memicu aturan itu, karena tidak ada user seed yang punya manager sekaligus menjadi System Owner. Test ini akan selalu hijau walaupun aturannya dihapus. Perbaikan: test sekarang membuat user dengan manager Bob dan aplikasi milik user itu, lalu memastikan request high-risk mendapat 422 dan request low-risk tetap 201.

2. **Error reject tertutup dialog.** Di halaman detail, error dari mutation reject (misalnya 400 atau 403) awalnya ditampilkan sebagai banner di halaman. Saat itu `RejectDialog` masih terbuka sebagai modal, jadi user tidak bisa melihat banner tersebut. Perbaikan: error ditampilkan di dalam dialog, dan dialog ditutup otomatis kalau responsnya 409 supaya banner konflik terlihat.

3. **Perintah test yang salah dan log 4xx yang tidak sesuai klaim.** Di ringkasan backend, AI menulis `dotnet test --project backendApproval/backendApproval.Tests`. Opsi `--project` itu bukan cara menjalankan project test VSTest seperti di repo ini; perintah yang benar adalah `dotnet test` dari folder `backendApproval` atau `dotnet test backendApproval/backendApproval.slnx`. Selain itu, plan menyebut AppException dicatat sebagai warning. Output test menunjukkan bahwa di .NET 8, `ExceptionHandlerMiddleware` tetap menulis log level Error dengan stack trace untuk setiap 4xx (misalnya `NO_ELIGIBLE_APPROVER`) sebelum `AppExceptionHandler` menulis warning. Perintah test sudah diperbaiki di README. Masalah log masih terbuka dan dicatat di REVIEW.md.
