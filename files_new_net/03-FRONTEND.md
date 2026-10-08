# 04 — Frontend (React + Vite + TypeScript)

Frontend untuk Access Request Hub Phase 1. Tujuannya **fungsional dan jujur terhadap state backend**, bukan pixel-perfect. Semua aturan bisnis tetap ditegakkan di backend; frontend hanya membantu user dan menampilkan hasilnya dengan jelas.

---

## 0. Ringkasan

| Komponen | Pilihan | Alasan |
|---|---|---|
| Build tool | Vite + React + TypeScript | Cepat, setup minimal |
| Routing | `react-router` (v7) | Standar |
| Server state | `@tanstack/react-query` | Loading/error state, refetch, dan invalidasi setelah approve/reject jadi rapi |
| Form | Controlled state biasa | Form-nya kecil, tidak perlu library form |
| Styling | CSS sederhana | Design system tidak diminta |
| Komunikasi API | Vite proxy `/api` → `http://localhost:5080` | Tidak perlu CORS |

---

## 1. Inisialisasi

Vite punya syarat versi Node.js minimum (Vite 7 misalnya butuh 20.19+ atau 22.12+; cek halaman "Getting Started" Vite untuk versi yang kamu install). Tulis versi Node yang kamu pakai di README (bagian Prerequisites) dan tambahkan field `engines` di `package.json` supaya mismatch versi langsung terlihat. Commit `package-lock.json` supaya `npm install` di mesin assessor menghasilkan versi yang sama.

```bash
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
npm install react-router @tanstack/react-query
```

`vite.config.ts`:

```ts
import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": { target: "http://localhost:5080", changeOrigin: true },
    },
  },
});
```

---

## 2. Struktur folder

```
frontend/src/
├── main.tsx
├── App.tsx                      # router + layout + navigasi
├── api/
│   ├── client.ts                # fetch wrapper, header X-User-Email, parsing ProblemDetails
│   ├── types.ts                 # tipe DTO (mirror kontrak backend)
│   └── accessRequests.ts        # fungsi per endpoint
├── auth/
│   └── CurrentUserContext.tsx   # user switcher state
├── components/
│   ├── UserSwitcher.tsx
│   ├── StatusBadge.tsx
│   ├── AuditTimeline.tsx
│   ├── ErrorBanner.tsx
│   ├── EmptyState.tsx
│   └── RejectDialog.tsx
└── pages/
    ├── MyRequestsPage.tsx       # /requests
    ├── NewRequestPage.tsx       # /requests/new
    ├── RequestDetailPage.tsx    # /requests/:id
    ├── InboxPage.tsx            # /inbox
    └── AllRequestsPage.tsx      # /audit (khusus auditor)
```

---

## 3. API client

Tidak memakai *parameter properties* (`constructor(public x)`) karena template Vite terbaru mengaktifkan `erasableSyntaxOnly` di `tsconfig`, yang melarang sintaks tersebut. Template yang sama juga mengaktifkan `verbatimModuleSyntax`, jadi import yang hanya berisi tipe **wajib** ditulis `import type { ... }`. Kalau tidak, build gagal.

```ts
// api/client.ts
export class ApiError extends Error {
  status: number;
  code?: string;
  fieldErrors?: Record<string, string[]>;   // key sudah dinormalisasi: "justification", "environment", ...
  correlationId?: string;
  problem?: Record<string, unknown>;

  constructor(status: number, message: string, opts: {
    code?: string; fieldErrors?: Record<string, string[]>;
    correlationId?: string; problem?: Record<string, unknown>;
  } = {}) {
    super(message);
    this.status = status;
    this.code = opts.code;
    this.fieldErrors = opts.fieldErrors;
    this.correlationId = opts.correlationId;
    this.problem = opts.problem;
  }
}

let currentUserEmail: string | null = null;
export function setApiUser(email: string | null) {
  currentUserEmail = email;
}

// Backend mengirim key camelCase ("justification"), tetapi error format JSON memakai "$.environment".
// Normalisasi supaya form cukup membaca fieldErrors["environment"].
function normalizeFieldErrors(errors: unknown): Record<string, string[]> | undefined {
  if (!errors || typeof errors !== "object") return undefined;
  const out: Record<string, string[]> = {};
  for (const [rawKey, msgs] of Object.entries(errors as Record<string, string[]>)) {
    const key = rawKey.replace(/^\$\./, "");
    const field = key.charAt(0).toLowerCase() + key.slice(1);
    (out[field] ??= []).push(...msgs);
  }
  return out;
}

export async function api<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (init.body) headers.set("Content-Type", "application/json");
  if (currentUserEmail) headers.set("X-User-Email", currentUserEmail);

  let res: Response;
  try {
    res = await fetch(`/api${path}`, { ...init, headers });
  } catch {
    throw new ApiError(0, "Tidak dapat terhubung ke server.", { code: "NETWORK_ERROR" });
  }

  if (res.ok) {
    return (res.status === 204 ? undefined : await res.json()) as T;
  }

  const problem = (await res.json().catch(() => ({}))) as Record<string, any>;
  throw new ApiError(res.status, problem.detail ?? problem.title ?? res.statusText, {
    code: problem.code,
    fieldErrors: normalizeFieldErrors(problem.errors),
    correlationId: res.headers.get("X-Correlation-Id") ?? problem.correlationId,
    problem,
  });
}
```

Key yang tidak cocok dengan field mana pun (misalnya `body` saat JSON rusak) ditampilkan sebagai error umum di atas form.

```ts
// main.tsx: default React Query
export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // Jangan refetch diam-diam saat tab mendapat fokus. User harus bertindak atas data yang ia LIHAT.
      // Tanpa ini, demo "concurrent action" (dua tab) gagal: tab 2 sudah ter-refresh sebelum tombol diklik.
      refetchOnWindowFocus: false,
      // Default React Query me-retry 3x dengan backoff. Untuk 4xx itu hanya menunda pesan 403/404
      // beberapa detik dengan "Memuat…". Retry hanya untuk network error / 5xx.
      retry: (failureCount, error) =>
        !(error instanceof ApiError && error.status >= 400 && error.status < 500) && failureCount < 2,
    },
  },
});
```

Query key yang dipakai: `["me"]`, `["users"]`, `["applications"]`, `["requests", "mine"]`, `["requests", "all"]`, `["request", id]`, `["inbox"]`. Karena list memakai prefix `["requests", ...]`, `invalidateQueries({ queryKey: ["requests"] })` otomatis menyegarkan keduanya.

```ts
// api/accessRequests.ts
export const getUsers      = () => api<UserSummary[]>("/users");
export const getMe         = () => api<Me>("/me");
export const getApps       = () => api<Application[]>("/applications");
export const getMine       = () => api<AccessRequestSummary[]>("/access-requests?scope=mine");
export const getAll        = () => api<AccessRequestSummary[]>("/access-requests?scope=all");
export const getInbox      = () => api<AccessRequestSummary[]>("/approvals/inbox");
export const getRequest    = (id: string) => api<AccessRequestDetail>(`/access-requests/${id}`);

export const createRequest = (body: CreateAccessRequest) =>
  api<AccessRequestDetail>("/access-requests", { method: "POST", body: JSON.stringify(body) });

export const approve = (id: string, expectedVersion: number) =>
  api<AccessRequestDetail>(`/access-requests/${id}/approve`, {
    method: "POST", body: JSON.stringify({ expectedVersion }),
  });

export const reject = (id: string, expectedVersion: number, reason: string) =>
  api<AccessRequestDetail>(`/access-requests/${id}/reject`, {
    method: "POST", body: JSON.stringify({ expectedVersion, reason }),
  });
```

---

## 4. User switcher

- Daftar user diambil dari `GET /api/users` (endpoint ini anonymous).
- User terpilih disimpan di `localStorage` (key `arh.currentUser`) hanya supaya tidak hilang saat refresh. **Ini bukan security boundary**; backend tetap memutuskan semuanya.
- **Saat user berganti, panggil `queryClient.clear()`.** Tanpa ini, data milik user sebelumnya (misalnya inbox Bob) bisa sempat tampil di layar user berikutnya dari cache.
- **Set user API secara sinkron sebelum React render**, jangan di `useEffect` provider. Effect milik child berjalan **lebih dulu** daripada effect parent, jadi query pertama di halaman bisa terkirim tanpa header `X-User-Email` dan mendapat 401 setiap kali halaman di-refresh.

```tsx
// main.tsx
const STORAGE_KEY = "arh.currentUser";
setApiUser(localStorage.getItem(STORAGE_KEY));   // sebelum createRoot(...).render(...)
```

```tsx
function switchUser(email: string) {
  setApiUser(email);
  localStorage.setItem("arh.currentUser", email);
  queryClient.clear();
  setEmail(email);
  navigate("/requests");
}
```

Menu navigasi ditentukan dari `GET /api/me`:

| Menu | Tampil untuk |
|---|---|
| My Requests, New Request | Semua user |
| Approval Inbox | Semua user (empty state kalau tidak ada) |
| All Requests (Audit) | `me.isAuditor` |

---

## 5. Halaman

### 5.1 New Request (`/requests/new`)

Field: Application (dropdown dari `/api/applications`), Environment, Access Level, Justification.

Validasi client (mirror dari backend, hanya untuk UX):
- semua field wajib,
- justification tidak boleh kosong atau spasi saja, maksimal 1000 karakter.

Tampilkan juga **indikator high-risk** ("Butuh approval System Owner") saat Environment = Production atau Access Level = Admin. Ini hanya informasi. Penentu sebenarnya tetap backend, dan nilai final terlihat di halaman detail.

**Idempotency di sisi client — bagian terpenting di halaman ini:**

`crypto.randomUUID()` hanya tersedia di *secure context*. `http://localhost:5173` termasuk secure context, tetapi kalau frontend dibuka lewat IP LAN (`http://192.168.x.x:5173`) fungsinya `undefined`. Untuk demo, selalu gunakan `localhost`.

```tsx
const [clientRequestId, setClientRequestId] = useState(() => crypto.randomUUID());

const mutation = useMutation({
  mutationFn: () => createRequest({ clientRequestId, ...form }),
  onSuccess: (created) => {
    setClientRequestId(crypto.randomUUID());   // key baru HANYA setelah sukses
    queryClient.invalidateQueries({ queryKey: ["requests"] });
    navigate(`/requests/${created.id}`);
  },
});
```

| Situasi | Perilaku |
|---|---|
| Klik submit dua kali cepat | Tombol `disabled` selama `mutation.isPending`; kalaupun terkirim dua kali, key-nya sama → backend tetap membuat satu request |
| Network error / timeout lalu klik "Coba lagi" | **Key yang sama** dipakai ulang → kalau request pertama ternyata sudah tersimpan, backend membalas 200 dengan request yang sama |
| 5xx | Key yang sama tetap dipakai saat retry (bisa jadi request sudah tersimpan sebelum error) |
| 400 validation | Tampilkan pesan per field dari `error.fieldErrors` |
| 409 `IDEMPOTENCY_KEY_REUSED` | User mengubah isi form setelah percobaan sebelumnya ternyata sudah tersimpan. Tampilkan pesan + link ke `existingRequestId`, lalu buat key baru |
| 422 `NO_MANAGER` / `NO_ELIGIBLE_APPROVER` | Tampilkan pesan dari backend apa adanya |

### 5.2 My Requests (`/requests`)

Tabel sederhana: aplikasi, environment, access level, status (badge), tanggal dibuat. Klik baris → detail.

### 5.3 Approval Inbox (`/inbox`)

Daftar request yang menunggu keputusan user saat ini. Tampilkan requester, aplikasi, environment/level, tanda high-risk, dan umur request. Klik → detail (approve/reject dilakukan di halaman detail).

### 5.4 Request Detail (`/requests/:id`)

Menampilkan:
- semua field request + `policyVersion` + tanda high-risk,
- status saat ini dan **siapa yang sedang ditunggu** (`currentApprover`),
- rejection reason (kalau Rejected),
- **audit timeline** (event, actor, from → to, reason, waktu),
- tombol Approve / Reject **hanya jika** `allowedActions` dari backend berisi `"approve"` / `"reject"` (tipe: `allowedActions: ("approve" | "reject")[]` di `types.ts`).

Approve / Reject:

```tsx
const decide = useMutation({
  mutationFn: (action: { type: "approve" } | { type: "reject"; reason: string }) =>
    action.type === "approve"
      ? approve(id, data!.version)                  // kirim version yang sedang DILIHAT user
      : reject(id, data!.version, action.reason),
  onSettled: () => {
    queryClient.invalidateQueries({ queryKey: ["request", id] });
    queryClient.invalidateQueries({ queryKey: ["inbox"] });
    queryClient.invalidateQueries({ queryKey: ["requests"] });
  },
});
```

| Response | UI |
|---|---|
| 200 | Data detail ter-refresh, timeline bertambah satu event |
| 409 `STALE_VERSION` | Banner: "Request ini sudah diproses atau berubah oleh user lain. Data terbaru sudah dimuat." Data otomatis di-refetch |
| 409 `INVALID_TRANSITION` | Banner: "Request sudah final dan tidak dapat diproses lagi." |
| 403 | Banner berisi pesan dari backend |
| 400 (reject tanpa reason) | Error di dalam RejectDialog |

**RejectDialog:** textarea reason wajib; tombol konfirmasi disabled selama reason kosong. Backend tetap memvalidasi ulang.

### 5.5 All Requests (`/audit`)

Khusus auditor (Erin): daftar semua request, klik → detail (tanpa tombol aksi karena `allowedActions` kosong). Kalau non-auditor membuka URL ini secara langsung, backend membalas 403, dan halaman menampilkan pesan "Tidak memiliki akses".

---

## 6. Loading, empty, dan error state

Setiap halaman yang mengambil data menangani tiga keadaan:

```tsx
if (query.isPending) return <p>Memuat…</p>;
if (query.isError)   return <ErrorBanner error={query.error} onRetry={() => query.refetch()} />;
if (query.data.length === 0) return <EmptyState message="Belum ada request." />;
```

`ErrorBanner` membedakan pesan berdasarkan `status`/`code`:

| Kondisi | Pesan |
|---|---|
| `NETWORK_ERROR` | "Server tidak dapat dihubungi. Pastikan backend berjalan." + tombol coba lagi |
| 401 | "User tidak dikenal. Pilih user dari switcher." |
| 403 | Pesan dari backend |
| 404 | "Request tidak ditemukan." |
| 409 | Pesan konflik sesuai `code` |
| 5xx | "Terjadi kesalahan di server." + tampilkan `error.correlationId` (dari header `X-Correlation-Id` atau field `correlationId` di body), supaya mudah dicari di log |

---

## 7. Skrip demo lewat UI

| Scenario | Langkah |
|---|---|
| Standard request | Alice → New Request: CRM / NonProduction / Read → Bob → Inbox → Approve → status **Approved** |
| Production request | Alice → CRM / Production / Read → Bob approve → status menunggu **Carol** → Carol approve → **Approved** |
| Admin access | Alice → Finance Portal / NonProduction / Admin → Bob approve → menunggu **Dana** |
| Rejected request | Alice buat request → Bob → Reject dengan reason → detail menampilkan reason + audit; tidak ada tombol aksi lagi |
| Concurrent action | Buka detail request yang sama di dua tab sebagai Bob → Approve di tab 1 → pindah ke tab 2 **tanpa refresh** → Approve → tab 2 menampilkan banner konflik. Ini bergantung pada `refetchOnWindowFocus: false` (bagian 3); bukti race yang sebenarnya ada di test `Two_concurrent_approvals_on_same_version_only_one_succeeds` |
| Unauthorized approval | Tombol tidak tampil untuk user yang tidak berhak, jadi demokan lewat file `.http` backend (lihat 02-BACKEND-PHASE-1.md bagian 14) |
| Duplicate submit | Demokan lewat `.http` (kirim key yang sama dua kali) atau test otomatis |

> Skenario "unauthorized" dan "duplicate" sengaja didemokan lewat API langsung karena justru membuktikan poin assessment: **menyembunyikan tombol bukan security boundary**.

---

## 8. Test frontend (opsional, nilai tambah)

Dokumen assessment menyebut test frontend tidak wajib bila perilaku bisnis sudah dibuktikan di backend. Kalau ada waktu sisa:

```bash
npm install -D vitest @testing-library/react @testing-library/jest-dom jsdom
```

Kandidat test:
- `NewRequestPage` menampilkan error validasi saat justification kosong.
- `NewRequestPage` memakai **clientRequestId yang sama** saat retry setelah network error.
- `RequestDetailPage` menampilkan banner konflik saat API membalas 409 `STALE_VERSION`.

---

## 9. Menjalankan

```bash
cd frontend
npm install
npm run dev        # http://localhost:5173 (backend harus jalan di :5080)
```

## 10. Checklist frontend

- [ ] User switcher dengan 5 demo user; cache dibersihkan saat ganti user
- [ ] Form create dengan validasi + `clientRequestId` yang stabil saat retry
- [ ] My Requests, Inbox, Detail dengan audit timeline
- [ ] Tombol aksi berdasarkan `allowedActions` dari backend
- [ ] Penanganan 409 yang jelas + auto refetch
- [ ] Loading, empty, dan error state di setiap halaman yang mengambil data
- [ ] `refetchOnWindowFocus: false` dan tidak ada retry untuk 4xx
- [ ] User API di-set sinkron sebelum render (refresh halaman tidak menghasilkan 401)
- [ ] Banner 5xx menampilkan correlation ID
