import { CheckCircle2, Clock, User2, XCircle } from "lucide-react";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "react-router";
import { approve, getRequest, reject } from "../api/accessRequests";
import { ApiError } from "../api/client";
import { AuditTimeline } from "../components/AuditTimeline";
import { DashboardPageHeader, MetricCard, SectionCard } from "../components/dashboard";
import { ConflictBanner, ErrorBanner } from "../components/ErrorBanner";
import { RejectDialog } from "../components/RejectDialog";
import { accessLevelLabel, environmentLabel } from "../lib/constants";
import { HighRiskBadge, StatusBadge } from "../components/StatusBadge";
import { LoadingState } from "../components/states/LoadingState";
import { Button } from "../components/ui";

function formatDate(iso) {
  if (!iso) return "—";
  return new Date(iso).toLocaleString("id-ID", { dateStyle: "medium", timeStyle: "short", timeZone: "Asia/Jakarta" });
}

export function RequestDetailPage() {
  const { id } = useParams();
  const queryClient = useQueryClient();
  const [conflict, setConflict] = useState(null);
  const [rejectOpen, setRejectOpen] = useState(false);

  const query = useQuery({ queryKey: ["request", id], queryFn: () => getRequest(id) });

  function invalidateAll() {
    queryClient.invalidateQueries({ queryKey: ["request", id] });
    queryClient.invalidateQueries({ queryKey: ["inbox"] });
    queryClient.invalidateQueries({ queryKey: ["requests"] });
  }

  const decideMutation = useMutation({
    mutationFn: (action) =>
      action.type === "approve" ? approve(id, query.data.version) : reject(id, query.data.version, action.reason),
    onSuccess: () => {
      setConflict(null);
      setRejectOpen(false);
      invalidateAll();
    },
    onError: (error) => {
      if (error instanceof ApiError && (error.code === "STALE_VERSION" || error.code === "INVALID_TRANSITION")) {
        const msg =
          error.code === "STALE_VERSION"
            ? "Request ini sudah diproses atau berubah oleh user lain. Data terbaru sudah dimuat."
            : "Request sudah final dan tidak dapat diproses lagi.";
        setConflict({ code: error.code, message: msg });
        setRejectOpen(false);
        invalidateAll();
      } else {
        setConflict(null);
      }
    },
  });

  if (query.isPending) return <LoadingState />;
  if (query.isError) return <ErrorBanner error={query.error} onRetry={() => query.refetch()} />;

  const data = query.data;
  const canApprove = data.allowedActions?.includes("approve");
  const canReject = data.allowedActions?.includes("reject");

  return (
    <div className="flex flex-col gap-6">
      <DashboardPageHeader
        eyebrow="Request Detail"
        title={`${data.application.name} — ${environmentLabel[data.environment]} / ${accessLevelLabel[data.accessLevel]}`}
        description={`Diajukan oleh ${data.requester.displayName} (${data.requester.email})`}
        meta={
          <div className="flex items-center gap-2">
            <StatusBadge status={data.status} />
            <HighRiskBadge isHighRisk={data.isHighRisk} />
          </div>
        }
      />

      {conflict ? <ConflictBanner code={conflict.code} message={conflict.message} /> : null}
      {decideMutation.isError && !conflict && !rejectOpen ? (
        <ErrorBanner error={decideMutation.error} title="Gagal memproses request" />
      ) : null}

      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
        <MetricCard label="Version" value={data.version} icon={Clock} tone="neutral" />
        <MetricCard label="Policy" value={data.policyVersion} icon={Clock} tone="info" />
        <MetricCard label="Dibuat" value={formatDate(data.createdAt)} icon={Clock} tone="neutral" />
        <MetricCard
          label="Selesai"
          value={data.decidedAt ? formatDate(data.decidedAt) : "Belum"}
          icon={data.status === "Approved" ? CheckCircle2 : data.status === "Rejected" ? XCircle : Clock}
          tone={data.status === "Approved" ? "success" : data.status === "Rejected" ? "error" : "neutral"}
        />
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <SectionCard title="Informasi Request" className="lg:col-span-1">
          <dl className="flex flex-col gap-3 text-sm">
            <div>
              <dt className="text-xs text-muted-foreground">Requester</dt>
              <dd className="mt-0.5 font-medium">{data.requester.displayName}</dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">Aplikasi</dt>
              <dd className="mt-0.5 font-medium">{data.application.name}</dd>
            </div>
            <div>
              <dt className="text-xs text-muted-foreground">Justifikasi</dt>
              <dd className="mt-0.5 text-foreground">{data.justification}</dd>
            </div>
            {data.currentApprover ? (
              <div className="flex items-center gap-2">
                <User2 aria-hidden="true" className="size-4 text-muted-foreground" />
                <div>
                  <dt className="text-xs text-muted-foreground">Menunggu approval dari</dt>
                  <dd className="mt-0.5 font-semibold text-navy-700">{data.currentApprover.displayName}</dd>
                </div>
              </div>
            ) : null}
            {data.rejectionReason ? (
              <div className="rounded-md bg-error-tint px-3 py-2 text-sm text-error">
                <dt className="text-xs font-semibold">Alasan penolakan</dt>
                <dd className="mt-0.5">{data.rejectionReason}</dd>
              </div>
            ) : null}
          </dl>

          {canApprove || canReject ? (
            <div className="mt-6 flex gap-2 border-t pt-4">
              {canApprove ? (
                <Button
                  variant="success"
                  size="sm"
                  disabled={decideMutation.isPending}
                  onClick={() => {
                    setConflict(null);
                    decideMutation.mutate({ type: "approve" });
                  }}
                >
                  <CheckCircle2 aria-hidden="true" className="size-3.5" />
                  {decideMutation.isPending ? "Memproses…" : "Approve"}
                </Button>
              ) : null}
              {canReject ? (
                <Button variant="danger" size="sm" disabled={decideMutation.isPending} onClick={() => setRejectOpen(true)}>
                  <XCircle aria-hidden="true" className="size-3.5" />
                  Reject
                </Button>
              ) : null}
            </div>
          ) : null}
        </SectionCard>

        <SectionCard title="Audit Trail" className="lg:col-span-2">
          <AuditTimeline events={data.auditTrail} />
        </SectionCard>
      </div>

      <RejectDialog
        open={rejectOpen}
        onClose={() => {
          setRejectOpen(false);
          decideMutation.reset();
        }}
        onConfirm={(reason) => {
          setConflict(null);
          decideMutation.mutate({ type: "reject", reason });
        }}
        isPending={decideMutation.isPending}
        serverError={
          rejectOpen && decideMutation.isError
            ? decideMutation.error.fieldErrors?.reason?.[0] ?? decideMutation.error.message
            : undefined
        }
      />
    </div>
  );
}
