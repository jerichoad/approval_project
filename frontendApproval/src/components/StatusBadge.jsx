import { ShieldAlert } from "lucide-react";
import { cn } from "../lib/utils";

const statusMeta = {
  PendingManagerApproval: { label: "Menunggu Manager", dot: "bg-warning", cls: "bg-warning-tint text-warning" },
  PendingSystemOwnerApproval: { label: "Menunggu System Owner", dot: "bg-info", cls: "bg-info-tint text-info" },
  Approved: { label: "Approved", dot: "bg-success", cls: "bg-success-tint text-success" },
  Rejected: { label: "Rejected", dot: "bg-error", cls: "bg-error-tint text-error" },
};

export function StatusBadge({ status }) {
  const st = statusMeta[status] ?? { label: status, dot: "bg-muted-foreground", cls: "bg-muted text-foreground" };
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap",
        st.cls,
      )}
    >
      <span aria-hidden="true" className={cn("size-1.5 rounded-full", st.dot)} />
      {st.label}
    </span>
  );
}

export function HighRiskBadge({ isHighRisk }) {
  if (!isHighRisk) {
    return (
      <span className="inline-flex items-center rounded-full bg-navy-50 px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap text-navy-600">
        Standard
      </span>
    );
  }
  return (
    <span className="inline-flex items-center gap-1 rounded-full bg-gold-100/70 px-2.5 py-0.5 text-xs font-semibold whitespace-nowrap text-gold-800">
      <ShieldAlert aria-hidden="true" className="size-3" />
      High-risk
    </span>
  );
}
