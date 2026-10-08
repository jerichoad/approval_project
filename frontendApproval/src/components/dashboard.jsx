import { cn } from "../lib/utils";

export function DashboardPageHeader({ eyebrow, title, description, meta }) {
  return (
    <section className="relative overflow-hidden rounded-xl bg-navy-700 px-4 py-5 text-white sm:px-6 sm:py-7">
      <div
        aria-hidden="true"
        className="pointer-events-none absolute inset-y-0 right-0 w-1/3 opacity-40"
        style={{
          background:
            "linear-gradient(115deg, transparent 0%, rgba(203,157,81,0.28) 55%, rgba(228,195,135,0.35) 100%)",
        }}
      />
      <div className="relative flex flex-col gap-5 lg:flex-row lg:items-end lg:justify-between">
        <div className="max-w-3xl">
          <p className="text-[11px] font-bold tracking-[0.17em] text-gold-300 uppercase">{eyebrow}</p>
          <h1 className="mt-2 text-xl font-semibold tracking-tight text-balance sm:text-2xl">{title}</h1>
          {description ? (
            <p className="mt-2 max-w-2xl text-[13px] leading-relaxed text-white/75">{description}</p>
          ) : null}
        </div>
        {meta ? <div className="shrink-0">{meta}</div> : null}
      </div>
    </section>
  );
}

const toneMap = {
  neutral: "bg-navy-50 text-navy-600",
  success: "bg-success-tint text-success",
  warning: "bg-warning-tint text-warning",
  error: "bg-error-tint text-error",
  info: "bg-info-tint text-info",
  gold: "bg-gold-100/70 text-gold-800",
};

export function MetricCard({ label, value, helper, icon: Icon, tone = "neutral" }) {
  return (
    <div className="flex flex-col justify-between gap-4 rounded-lg border bg-card p-5 shadow-sm">
      <div className="flex items-start justify-between gap-4">
        <div className="min-w-0">
          <p className="truncate text-xs font-semibold tracking-wide text-muted-foreground uppercase">{label}</p>
          <p className="mt-2 font-mono text-2xl font-medium text-foreground tabular-nums">{value}</p>
        </div>
        <span
          aria-hidden="true"
          className={cn("inline-flex size-10 shrink-0 items-center justify-center rounded-xl", toneMap[tone])}
        >
          <Icon aria-hidden="true" className="size-5" />
        </span>
      </div>
      {helper ? <p className="text-xs leading-relaxed text-muted-foreground">{helper}</p> : null}
    </div>
  );
}

export function SectionCard({ title, description, action, children, className, contentClassName }) {
  return (
    <section aria-label={title} className={cn("rounded-lg border bg-card shadow-sm", className)}>
      <header className="flex items-start justify-between gap-4 border-b px-5 py-4">
        <div className="min-w-0">
          <h2 className="text-base font-semibold text-foreground">{title}</h2>
          {description ? <p className="mt-0.5 text-xs text-muted-foreground">{description}</p> : null}
        </div>
        {action ? <div className="shrink-0">{action}</div> : null}
      </header>
      <div className={cn("px-5 py-4", contentClassName)}>{children}</div>
    </section>
  );
}

export function Th({ children, className }) {
  return (
    <th
      scope="col"
      className={cn(
        "sticky top-0 border-b bg-muted px-4 py-2.5 text-xs font-semibold tracking-wide uppercase",
        className,
      )}
    >
      {children}
    </th>
  );
}
