import { createContext, useContext } from "react";

export const STORAGE_KEY = "arh.currentUser";
export const DEFAULT_USER = "alice@example.local";

export const CurrentUserContext = createContext({
  currentUserEmail: null,
  me: null,
  users: [],
  switchUser: () => {},
});

export function useCurrentUser() {
  return useContext(CurrentUserContext);
}
