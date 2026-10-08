export class ApiError extends Error {
  constructor(status, message, opts = {}) {
    super(message);
    this.status = status;
    this.code = opts.code;
    this.fieldErrors = opts.fieldErrors;
    this.correlationId = opts.correlationId;
    this.extensions = opts.extensions ?? {};
    this.problem = opts.problem;
  }
}

let currentUserEmail = null;
export function setApiUser(email) {
  currentUserEmail = email;
}

function normalizeFieldErrors(errors) {
  if (!errors || typeof errors !== "object") return undefined;
  const out = {};
  for (const [rawKey, msgs] of Object.entries(errors)) {
    const key = rawKey.replace(/^\$\./, "");
    const field = key.charAt(0).toLowerCase() + key.slice(1);
    (out[field] ??= []).push(...msgs);
  }
  return out;
}

export async function api(path, init = {}) {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (init.body) headers.set("Content-Type", "application/json");
  if (currentUserEmail) headers.set("X-User-Email", currentUserEmail);

  let res;
  try {
    res = await fetch(`/api${path}`, { ...init, headers });
  } catch {
    throw new ApiError(0, "Tidak dapat terhubung ke server.", { code: "NETWORK_ERROR" });
  }

  const payload = await res.json().catch(() => null);

  if (res.ok) {
    if (res.status === 204 || payload === null) return undefined;
    // Format baru: { Status: "S", Data: ... }
    if (payload && typeof payload === "object" && "Status" in payload && "Data" in payload) {
      if (payload.Status === "S") return payload.Data;
      // Jika status HTTP ok tapi Status === "E"
      const errData = payload.Data ?? {};
      throw new ApiError(res.status, errData.message ?? "Terjadi kesalahan.", {
        code: errData.code,
        fieldErrors: normalizeFieldErrors(errData.fieldErrors),
        correlationId: res.headers.get("X-Correlation-Id") ?? errData.correlationId,
        extensions: errData.extensions,
        problem: payload,
      });
    }
    return payload;
  }

  // Error HTTP: cek format envelope { Status: "E", Data: { code, message, ... } }
  // atau fallback format lama / ProblemDetails
  const errData = payload && typeof payload === "object" && payload.Status === "E" && payload.Data
    ? payload.Data
    : (payload ?? {});

  const code = errData.code;
  const message = errData.message ?? errData.detail ?? errData.title ?? res.statusText;
  const fieldErrors = normalizeFieldErrors(errData.fieldErrors ?? errData.errors);
  const correlationId = res.headers.get("X-Correlation-Id") ?? errData.correlationId;
  const extensions = errData.extensions ?? {};

  throw new ApiError(res.status, message, {
    code,
    fieldErrors,
    correlationId,
    extensions,
    problem: payload,
  });
}
