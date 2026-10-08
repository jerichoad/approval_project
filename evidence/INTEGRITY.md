# INTEGRITY.md

Deklarasi integritas untuk Enterprise Fullstack Engineering Assessment, Access Request Hub Phase 1.

## 1. Ownership

Saya menyatakan bahwa:

- Rancangan di `files_new_net/` (`01-DATABASE.md`, `02-BACKEND-PHASE-1.md`, `03-FRONTEND.md`) adalah dasar implementasi.
- Kode di `backendApproval/` dan `frontendApproval/` ditulis dengan bantuan AI agent (lihat bagian 2), lalu direview, dijalankan, dan diuji oleh saya.
- Saya bertanggung jawab atas semua keputusan teknis, dependensi, dan isi dokumen yang diserahkan, dan bisa menjelaskan setiap bagiannya.

## 2. Tools AI utama

| Tool                                                                                                     | Dipakai untuk                                                                                                                                                         |
| -------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| opencode (CLI agent), model `9router/api.jerichoad` (gabungan Claude Opus 5.5 dan Gemini 3.8 Flash High) | Menulis kode backend dan frontend dari rancangan, memilih versi package untuk .NET 8, membuat migration, menulis dan menjalankan test, menyusun draf dokumen evidence |

Detail interaksi dan kesalahan AI yang saya temukan ada di `AI_USAGE.md`.

## 3. External human assistance

Tidak ada

## 4. Template, repository, dan snippet yang diadaptasi

| Sumber                                                                    | Yang diambil                                                                                                                                                                                                                                                                                                                                       | Lokasi di repo ini                                                                                                                                                                    |
| ------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Template `dotnet new webapi` (.NET 8, controllers)                        | Scaffold project API. Contoh WeatherForecast dihapus.                                                                                                                                                                                                                                                                                              | `backendApproval/backendApproval`                                                                                                                                                     |
| Template `dotnet new xunit` (.NET 8)                                      | Scaffold project test                                                                                                                                                                                                                                                                                                                              | `backendApproval/backendApproval.Tests`                                                                                                                                               |
| Template Vite React (JavaScript)                                          | Scaffold project frontend                                                                                                                                                                                                                                                                                                                          | `frontendApproval`                                                                                                                                                                    |
| `E:\Herd\luwes-weather-station\frontend` (project milik Jericho)          | Token theme Tailwind v4 (skala navy dan gold, warna status dan tint), helper `cn()`, komponen `Button`, `Input`, `Select`, `Textarea`, `Field`, `SegmentedControl`, `Dialog`, `DashboardPageHeader`, `MetricCard`, `SectionCard`, pola tabel, `EmptyState`, `ErrorState`, skeleton loading. Ditulis ulang dari Next.js + TypeScript ke Vite + JSX. | `frontendApproval/src/index.css`, `src/components/ui.jsx`, `src/components/dashboard.jsx`, `src/components/states/`, `src/components/ErrorBanner.jsx`, `src/components/AppLayout.jsx` |
| Contoh kode di `files_new_net/02-BACKEND-PHASE-1.md` dan `01-DATABASE.md` | Middleware, exception handler, policy, service, konfigurasi EF, SQL trigger                                                                                                                                                                                                                                                                        | `backendApproval/backendApproval/`                                                                                                                                                    |
| Contoh kode di `files_new_net/03-FRONTEND.md`                             | `api/client.js`, fungsi endpoint, konfigurasi React Query                                                                                                                                                                                                                                                                                          | `frontendApproval/src/api/`, `src/main.jsx`                                                                                                                                           |

Package pihak ketiga tercatat di `backendApproval/backendApproval/backendApproval.csproj`, `backendApproval/backendApproval.Tests/backendApproval.Tests.csproj`, dan `frontendApproval/package.json`.
