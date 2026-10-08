import { api } from "./client";

export const getUsers = () => api("/users");
export const getMe = () => api("/me");
export const getApps = () => api("/applications");
export const getMine = () => api("/access-requests?scope=mine");
export const getAll = () => api("/access-requests?scope=all");
export const getInbox = () => api("/approvals/inbox");
export const getRequest = (id) => api(`/access-requests/${id}`);

export const createRequest = (body) =>
  api("/access-requests", { method: "POST", body: JSON.stringify(body) });

export const approve = (id, expectedVersion) =>
  api(`/access-requests/${id}/approve`, {
    method: "POST",
    body: JSON.stringify({ expectedVersion }),
  });

export const reject = (id, expectedVersion, reason) =>
  api(`/access-requests/${id}/reject`, {
    method: "POST",
    body: JSON.stringify({ expectedVersion, reason }),
  });
