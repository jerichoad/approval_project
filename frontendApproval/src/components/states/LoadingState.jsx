import { cn } from "../../lib/utils";

export function Skeleton({ className }) {
  return <div aria-hidden="true" className={cn("animate-pulse rounded-md bg-muted", className)} />;
}

export function LoadingState({ className, label = "Memuat data…" }) {
  return (
    <div role="status" aria-live="polite" className={cn("flex flex-col gap-3", className)}>
      <span className="sr-only">{label}</span>
      <Skeleton className="h-4 w-1/3" />
      <Skeleton className="h-full min-h-24 w-full flex-1" />
    </div>
  );
}

export function TableSkeleton({ rows = 5, cols = 6 }) {
  return (
    <div role="status" aria-live="polite" className="flex flex-col gap-2 p-5">
      <span className="sr-only">Memuat tabel…</span>
      {Array.from({ length: rows }).map((_, r) => (
        <div key={r} className="grid gap-3" style={{ gridTemplateColumns: `repeat(${cols}, minmax(0, 1fr))` }}>
          {Array.from({ length: cols }).map((__, c) => (
            <Skeleton key={c} className="h-5" />
          ))}
        </div>
      ))}
    </div>
  );
}

export function CardGridSkeleton({ count = 4 }) {
  return (
    <div role="status" aria-live="polite" className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
      <span className="sr-only">Memuat…</span>
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="flex flex-col gap-4 rounded-lg border bg-card p-5 shadow-sm">
          <Skeleton className="h-4 w-2/3" />
          <Skeleton className="h-3 w-1/3" />
          <Skeleton className="h-12" />
          <Skeleton className="h-3 w-1/2" />
        </div>
      ))}
    </div>
  );
}
