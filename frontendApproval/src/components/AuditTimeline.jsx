import { cn } from "../lib/utils";

const eventLabels = {
  RequestCreated: "Request dibuat",
  ManagerApproved: "Manager menyetujui",
  ManagerRejected: "Manager menolak",
  SystemOwnerApproved: "System Owner menyetujui",
  SystemOwnerRejected: "System Owner menolak",
};

const eventColors = {
  RequestCreated: "bg-navy-50 border-navy-200",
  ManagerApproved: "bg-success-tint border-success/30",
  ManagerRejected: "bg-error-tint border-error/30",
  SystemOwnerApproved: "bg-success-tint border-success/30",
  SystemOwnerRejected: "bg-error-tint border-error/30",
};

function formatDate(iso) {
  return new Date(iso).toLocaleString("id-ID", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone: "Asia/Jakarta",
  });
}

export function AuditTimeline({ events }) {
  if (!events || events.length === 0) return null;

  return (
    <div className="flex flex-col gap-3">
      {events.map((e, i) => (
        <div
          key={i}
          className={cn(
            "rounded-md border px-4 py-3 text-sm",
            eventColors[e.eventType] ?? "bg-muted border-border",
          )}
        >
          <div className="flex items-center justify-between gap-2">
            <p className="font-semibold text-foreground">
              {eventLabels[e.eventType] ?? e.eventType}
            </p>
            <span className="rounded bg-card px-1.5 py-0.5 font-mono text-[11px] text-muted-foreground">
              v{e.requestVersion}
            </span>
          </div>
          <div className="mt-1.5 flex flex-wrap items-center gap-x-4 gap-y-1 text-xs text-muted-foreground">
            <span>oleh {e.actor}</span>
            {e.fromStatus ? (
              <span>
                {e.fromStatus} → {e.toStatus}
              </span>
            ) : (
              <span>→ {e.toStatus}</span>
            )}
            <span>{formatDate(e.occurredAt)}</span>
          </div>
          {e.reason ? (
            <p className="mt-2 rounded bg-card/60 px-3 py-2 text-xs text-foreground italic">"{e.reason}"</p>
          ) : null}
        </div>
      ))}
    </div>
  );
}
