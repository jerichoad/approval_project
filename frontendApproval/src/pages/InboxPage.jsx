import { Inbox as InboxIcon } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";
import { getInbox } from "../api/accessRequests";
import { DashboardPageHeader, Th } from "../components/dashboard";
import { ErrorBanner } from "../components/ErrorBanner";
import { EmptyState } from "../components/states/EmptyState";
import { TableSkeleton } from "../components/states/LoadingState";
import { accessLevelLabel, environmentLabel } from "../lib/constants";
import { HighRiskBadge } from "../components/StatusBadge";

function ageLabel(iso) {
  const ms = Date.now() - new Date(iso).getTime();
  const hours = Math.floor(ms / 3_600_000);
  if (hours < 1) return "< 1 jam";
  if (hours < 24) return `${hours} jam lalu`;
  return `${Math.floor(hours / 24)} hari lalu`;
}

export function InboxPage() {
  const query = useQuery({ queryKey: ["inbox"], queryFn: getInbox });

  return (
    <div className="flex flex-col gap-6">
      <DashboardPageHeader
        eyebrow="Access Request Hub"
        title="Approval Inbox"
        description="Request yang sedang menunggu keputusanmu. Klik baris untuk approve atau reject."
      />

      <div className="overflow-hidden rounded-lg border bg-card shadow-sm">
        {query.isPending ? (
          <TableSkeleton rows={4} cols={6} />
        ) : query.isError ? (
          <div className="p-5">
            <ErrorBanner error={query.error} onRetry={() => query.refetch()} />
          </div>
        ) : query.data.length === 0 ? (
          <div className="p-5">
            <EmptyState icon={InboxIcon} message="Tidak ada request yang menunggu." />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[720px] border-separate border-spacing-0 text-left text-sm">
              <thead>
                <tr className="text-muted-foreground">
                  <Th>Requester</Th>
                  <Th>Aplikasi</Th>
                  <Th>Environment</Th>
                  <Th>Access Level</Th>
                  <Th>Risk</Th>
                  <Th>Umur</Th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/60">
                {query.data.map((r) => (
                  <tr key={r.id} className="cursor-pointer transition-colors hover:bg-muted/50">
                    <td className="px-4 py-3 font-medium text-foreground">
                      <Link to={`/requests/${r.id}`} className="hover:underline">
                        {r.requester.displayName}
                      </Link>
                    </td>
                    <td className="px-3 py-3 text-muted-foreground">{r.application.name}</td>
                    <td className="px-3 py-3 text-muted-foreground">{environmentLabel[r.environment]}</td>
                    <td className="px-3 py-3 text-muted-foreground">{accessLevelLabel[r.accessLevel]}</td>
                    <td className="px-3 py-3">
                      <HighRiskBadge isHighRisk={r.isHighRisk} />
                    </td>
                    <td className="px-3 py-3 text-xs text-muted-foreground">{ageLabel(r.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
