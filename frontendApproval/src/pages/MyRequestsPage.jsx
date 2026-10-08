import { FileText } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { Link } from "react-router";
import { getMine } from "../api/accessRequests";
import { DashboardPageHeader, Th } from "../components/dashboard";
import { ErrorBanner } from "../components/ErrorBanner";
import { EmptyState } from "../components/states/EmptyState";
import { TableSkeleton } from "../components/states/LoadingState";
import { accessLevelLabel, environmentLabel } from "../lib/constants";
import { HighRiskBadge, StatusBadge } from "../components/StatusBadge";

function formatDate(iso) {
  return new Date(iso).toLocaleString("id-ID", { dateStyle: "medium", timeStyle: "short", timeZone: "Asia/Jakarta" });
}

export function MyRequestsPage() {
  const query = useQuery({ queryKey: ["requests", "mine"], queryFn: getMine });

  return (
    <div className="flex flex-col gap-6">
      <DashboardPageHeader
        eyebrow="Access Request Hub"
        title="My Requests"
        description="Daftar access request yang pernah kamu ajukan beserta status terkininya."
      />

      <div className="overflow-hidden rounded-lg border bg-card shadow-sm">
        {query.isPending ? (
          <TableSkeleton rows={5} cols={5} />
        ) : query.isError ? (
          <div className="p-5">
            <ErrorBanner error={query.error} onRetry={() => query.refetch()} />
          </div>
        ) : query.data.length === 0 ? (
          <div className="p-5">
            <EmptyState
              icon={FileText}
              message="Belum ada request."
              description="Buat request baru untuk mengajukan akses ke sebuah aplikasi."
            />
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full min-w-[640px] border-separate border-spacing-0 text-left text-sm">
              <thead>
                <tr className="text-muted-foreground">
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
                        {r.application.name}
                      </Link>
                    </td>
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
