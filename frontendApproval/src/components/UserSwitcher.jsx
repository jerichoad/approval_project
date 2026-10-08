import { UserCheck } from "lucide-react";
import { useCurrentUser } from "../auth/CurrentUserContext";
import { Select } from "./ui";

export function UserSwitcher() {
  const { currentUserEmail, users, switchUser } = useCurrentUser();

  return (
    <div className="flex items-center gap-2">
      <span className="inline-flex size-7 items-center justify-center rounded-lg bg-navy-50 text-navy-700">
        <UserCheck aria-hidden="true" className="size-3.5" />
      </span>
      <Select
        value={currentUserEmail ?? ""}
        onChange={(e) => switchUser(e.target.value)}
        aria-label="Pilih user demo"
        className="h-8 text-xs font-semibold"
      >
        <option value="" disabled>
          Pilih User
        </option>
        {users.map((u) => (
          <option key={u.id} value={u.email}>
            {u.displayName} ({u.email})
          </option>
        ))}
      </Select>
    </div>
  );
}
