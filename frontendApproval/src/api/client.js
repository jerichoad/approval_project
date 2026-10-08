export class ApiError extends Error {
  constructor(status, message, opts = {}) {
    super(message);
    this.status = status;
    this.code = opts.code;
    this.fieldErrors = opts.fieldErrors;
    this.correlationId = opts.correlationId;
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

  if (res.ok) {
    return res.status === 204 ? undefined : await res.json();
  }

  const problem = await res.json().catch(() => ({}));
  throw new ApiError(res.status, problem.detail ?? problem.title ?? res.statusText, {
    code: problem.code,
    fieldErrors: normalizeFieldErrors(problem.errors),
    correlationId: res.headers.get("X-Correlation-Id") ?? problem.correlationId,
    problem,
  });
}
