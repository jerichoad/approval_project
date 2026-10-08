import { AlertTriangle, Check, Copy, RotateCw } from "lucide-react";
import { useState } from "react";
import { ApiError } from "../api/client";
import { cn } from "../lib/utils";

function CorrelationChip({ correlationId }) {
  const [copied, setCopied] = useState(false);

  async function copy() {
    try {
      await navigator.clipboard.writeText(correlationId);
      setCopied(true);
      window.setTimeout(() => setCopied(false), 1500);
    } catch {
      setCopied(false);
    }
  }

  return (
    <button
      type="button"
      onClick={copy}
      title="Salin correlation ID untuk dicari di log backend"
      className="inline-flex items-center gap-1.5 rounded bg-white/70 px-2 py-0.5 font-mono text-[11px] text-error ring-1 ring-error/20 hover:bg-white focus-visible:outline-2 focus-visible:outline-ring"
    >
      {copied ? <Check aria-hidden="true" className="size-3" /> : <Copy aria-hidden="true" className="size-3" />}
      <span className="sr-only">Salin correlation ID</span>
      {correlationId}
    </button>
  );
}

function describe(error) {
  if (error instanceof ApiError) {
    let message = error.message;
    if (error.code === "NETWORK_ERROR") message = "Server tidak dapat dihubungi. Pastikan backend berjalan.";
    else if (error.status === 401) message = "User tidak dikenal. Pilih user dari switcher.";
    else if (error.status === 404) message = error.message || "Request tidak ditemukan.";
    else if (error.status >= 500) message = "Terjadi kesalahan di server.";
    return { message, code: error.code, correlationId: error.correlationId };
  }
  if (error instanceof Error) return { message: error.message, code: null, correlationId: null };
  return { message: "Terjadi kesalahan.", code: null, correlationId: null };
}

export function ErrorBanner({ error, onRetry, title = "Gagal memuat data", className }) {
  const { message, code, correlationId } = describe(error);

  return (
    <div
      role="alert"
      className={cn(
        "flex flex-col gap-3 rounded-md border border-error/20 bg-error-tint/60 px-5 py-5 text-sm text-error",
        className,
      )}
    >
      <div className="flex items-start gap-2.5">
        <AlertTriangle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
        <div className="min-w-0 space-y-1">
          <p className="font-semibold">{title}</p>
          <p className="text-error/90">{message}</p>
          <div className="flex flex-wrap items-center gap-2 pt-1">
            {code ? (
              <span className="rounded bg-error/10 px-1.5 py-0.5 font-mono text-[11px] font-semibold">{code}</span>
            ) : null}
            {correlationId ? <CorrelationChip correlationId={correlationId} /> : null}
          </div>
        </div>
      </div>
      {onRetry ? (
        <div>
          <button
            type="button"
            onClick={onRetry}
            className="inline-flex items-center gap-1.5 rounded-md bg-error px-3 py-1.5 text-xs font-semibold text-white hover:bg-error/90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-error"
          >
            <RotateCw aria-hidden="true" className="size-3.5" />
            Coba lagi
          </button>
        </div>
      ) : null}
    </div>
  );
}

export function ConflictBanner({ code, message }) {
  return (
    <div
      role="alert"
      className="flex items-start gap-2.5 rounded-md border border-warning/20 bg-warning-tint/60 px-4 py-3 text-sm text-warning"
    >
      <AlertTriangle aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
      <div className="min-w-0 space-y-1">
        <p className="font-medium">{message}</p>
        {code ? (
          <span className="rounded bg-warning/10 px-1.5 py-0.5 font-mono text-[11px] font-semibold">{code}</span>
        ) : null}
      </div>
    </div>
  );
}
