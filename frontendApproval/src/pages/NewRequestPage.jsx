import { AlertTriangle, Send } from "lucide-react";
import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useNavigate } from "react-router";
import { createRequest, getApps } from "../api/accessRequests";
import { ApiError } from "../api/client";
import { DashboardPageHeader, SectionCard } from "../components/dashboard";
import { ErrorBanner } from "../components/ErrorBanner";
import { Button, Field, Select, SegmentedControl, Textarea } from "../components/ui";
import { LoadingState } from "../components/states/LoadingState";

export function NewRequestPage() {
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const appsQuery = useQuery({ queryKey: ["applications"], queryFn: getApps });

  const [clientRequestId, setClientRequestId] = useState(() => crypto.randomUUID());
  const [applicationId, setApplicationId] = useState("");
  const [environment, setEnvironment] = useState("");
  const [accessLevel, setAccessLevel] = useState("");
  const [justification, setJustification] = useState("");
  const [fieldErrors, setFieldErrors] = useState({});
  const [generalError, setGeneralError] = useState(null);

  const isHighRisk = environment === "Production" || accessLevel === "Admin";

  const mutation = useMutation({
    mutationFn: () =>
      createRequest({
        clientRequestId,
        applicationId,
        environment: environment || undefined,
        accessLevel: accessLevel || undefined,
        justification,
      }),
    onSuccess: (created) => {
      setClientRequestId(crypto.randomUUID());
      queryClient.invalidateQueries({ queryKey: ["requests"] });
      navigate(`/requests/${created.id}`);
    },
    onError: (error) => {
      if (error instanceof ApiError) {
        if (error.fieldErrors) {
          setFieldErrors(error.fieldErrors);
          setGeneralError(null);
        } else if (error.code === "IDEMPOTENCY_KEY_REUSED") {
          setGeneralError(error.message);
          setClientRequestId(crypto.randomUUID());
        } else {
          setFieldErrors({});
          setGeneralError(error.message);
        }
      }
    },
  });

  function handleSubmit(e) {
    e.preventDefault();
    setFieldErrors({});
    setGeneralError(null);

    const errors = {};
    if (!applicationId) errors.applicationId = ["Aplikasi wajib dipilih."];
    if (!environment) errors.environment = ["Environment wajib dipilih."];
    if (!accessLevel) errors.accessLevel = ["Access level wajib dipilih."];
    if (!justification.trim()) errors.justification = ["Justifikasi wajib diisi."];
    else if (justification.length > 1000) errors.justification = ["Maksimal 1000 karakter."];

    if (Object.keys(errors).length > 0) {
      setFieldErrors(errors);
      return;
    }
    mutation.mutate();
  }

  if (appsQuery.isPending) return <LoadingState />;
  if (appsQuery.isError) return <ErrorBanner error={appsQuery.error} onRetry={() => appsQuery.refetch()} />;

  return (
    <div className="flex flex-col gap-6">
      <DashboardPageHeader
        eyebrow="Access Request Hub"
        title="New Request"
        description="Ajukan akses ke sebuah aplikasi. Manager kamu akan menerima request ini untuk di-approve."
      />

      <SectionCard title="Buat Access Request" className="max-w-2xl">
        <form onSubmit={handleSubmit} className="flex flex-col gap-5">
          {generalError ? <ErrorBanner error={new Error(generalError)} title="Gagal membuat request" /> : null}

          <Field label="Aplikasi" htmlFor="applicationId" error={fieldErrors.applicationId?.[0]}>
            <Select
              id="applicationId"
              value={applicationId}
              onChange={(e) => setApplicationId(e.target.value)}
              aria-invalid={!!fieldErrors.applicationId || undefined}
            >
              <option value="" disabled>
                Pilih aplikasi…
              </option>
              {appsQuery.data.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name} (Owner: {a.systemOwner.displayName})
                </option>
              ))}
            </Select>
          </Field>

          <Field label="Environment" htmlFor="environment" error={fieldErrors.environment?.[0]}>
            <SegmentedControl
              label="Environment"
              value={environment}
              onChange={setEnvironment}
              invalid={!!fieldErrors.environment}
              options={[
                { value: "NonProduction", label: "Non-Production" },
                { value: "Production", label: "Production" },
              ]}
            />
          </Field>

          <Field label="Access Level" htmlFor="accessLevel" error={fieldErrors.accessLevel?.[0]}>
            <SegmentedControl
              label="Access Level"
              value={accessLevel}
              onChange={setAccessLevel}
              invalid={!!fieldErrors.accessLevel}
              options={[
                { value: "Read", label: "Read" },
                { value: "Admin", label: "Admin" },
              ]}
            />
          </Field>

          {isHighRisk ? (
            <div className="flex items-center gap-2 rounded-md bg-gold-100/70 px-3 py-2 text-xs font-semibold text-gold-800">
              <AlertTriangle aria-hidden="true" className="size-4 shrink-0" />
              <span>
                Request ini <strong>high-risk</strong> dan membutuhkan approval dari System Owner setelah Manager.
              </span>
            </div>
          ) : null}

          <Field
            label="Justifikasi"
            htmlFor="justification"
            error={fieldErrors.justification?.[0]}
            hint={`${justification.length}/1000`}
          >
            <Textarea
              id="justification"
              value={justification}
              onChange={(e) => setJustification(e.target.value)}
              placeholder="Jelaskan kebutuhan akses…"
              maxLength={1000}
              rows={4}
              aria-invalid={!!fieldErrors.justification || undefined}
            />
          </Field>

          <div className="flex justify-end pt-2">
            <Button type="submit" disabled={mutation.isPending}>
              <Send aria-hidden="true" className="size-3.5" />
              {mutation.isPending ? "Mengirim…" : "Kirim Request"}
            </Button>
          </div>
        </form>
      </SectionCard>
    </div>
  );
}
