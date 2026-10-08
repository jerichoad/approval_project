import { ScrollText } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";
import { getAll } from "../api/accessRequests";
import { DashboardPageHeader, Th } from "../components/dashboard";
import { ErrorBanner } from "../components/ErrorBanner";
import { EmptyState } from "../components/states/EmptyState";
import { TableSkeleton } from "../components/states/LoadingState";
import { accessLevelLabel, environmentLabel } from "../lib/constants";
import { HighRiskBadge, StatusBadge } from "../components/StatusBadge";

function formatDate(iso) {
  return new Date(iso).toLocaleString("id-ID", { dateStyle: "medium", timeStyle: "short", timeZone: "Asia/Jakarta" });
}

export function AllRequestsPage() {
  const query = useQuery({ queryKey: ["requests", "all"], queryFn: getAll });

  return (
    <div className="flex flex-col gap-6">
      <DashboardPageHeader
        eyebrow="Access Request Hub"
        title="All Requests"
        description="Tampilan audit: seluruh access request di semua aplikasi, untuk peninjauan compliance."
      />

      <div className="overflow-hidden rounded-lg border bg-card shadow-sm">
        {query.isPending ? (
          <TableSkeleton rows={6} cols={6} />
        ) : query.isError ? (
          <div className="p-5">
            <ErrorBanner error={query.error} onRetry={() => query.refetch()} title="Gagal memuat data audit" />
          </div>
        ) : query.data.length === 0 ? (
          <div className="p-5">
            <EmptyState icon={ScrollText} message="Belum ada request di sistem." />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[760px] border-separate border-spacing-0 text-left text-sm">
              <thead>
                <tr className="text-muted-foreground">
                  <Th>Requester</Th>
                  <Th>Aplikasi</Th>
                  <Th>Environment</Th>
                  <Th>Access Level</Th>
                  <Th>Risk</Th>
                  <Th>Status</Th>
                  <Th>Dibuat</Th>
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
                    <td className="px-3 py-3">
                      <StatusBadge status={r.status} />
                    </td>
                    <td className="px-3 py-3 text-xs text-muted-foreground">{formatDate(r.createdAt)}</td>
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
