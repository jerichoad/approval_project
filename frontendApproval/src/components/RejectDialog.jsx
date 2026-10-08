import { useState } from "react";
import { Button, Dialog, Textarea, Field } from "./ui";

export function RejectDialog({ open, onClose, onConfirm, isPending, serverError }) {
  const [reason, setReason] = useState("");
  const [error, setError] = useState("");

  function handleConfirm() {
    const trimmed = reason.trim();
    if (!trimmed) {
      setError("Alasan wajib diisi.");
      return;
    }
    setError("");
    onConfirm(trimmed);
  }

  function handleClose() {
    setReason("");
    setError("");
    onClose();
  }

  return (
    <Dialog
      open={open}
      onClose={handleClose}
      title="Tolak Request"
      description="Berikan alasan penolakan. Alasan ini akan tercatat di audit trail."
      footer={
        <>
          <Button variant="outline" size="sm" onClick={handleClose} disabled={isPending}>
            Batal
          </Button>
          <Button
            variant="danger"
            size="sm"
            onClick={handleConfirm}
            disabled={isPending || !reason.trim()}
          >
            {isPending ? "Menolak…" : "Tolak"}
          </Button>
        </>
      }
    >
      <Field label="Alasan Penolakan" htmlFor="reject-reason" error={error || serverError}>
        <Textarea
          id="reject-reason"
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          placeholder="Tulis alasan penolakan…"
          maxLength={1000}
          rows={3}
          aria-invalid={!!error || undefined}
        />
      </Field>
    </Dialog>
  );
}
