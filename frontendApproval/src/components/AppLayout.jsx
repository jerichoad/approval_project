import { ClipboardList, FilePlus2, Inbox, KeyRound, ShieldCheck, ScrollText } from "lucide-react";
import { NavLink, Outlet } from "react-router";
import { useCurrentUser } from "../auth/CurrentUserContext";
import { cn } from "../lib/utils";
import { UserSwitcher } from "./UserSwitcher";

function useNavItems() {
  const { me } = useCurrentUser();
  const items = [
    { title: "My Requests", href: "/requests", icon: ClipboardList, end: true },
    { title: "New Request", href: "/requests/new", icon: FilePlus2 },
    { title: "Approval Inbox", href: "/inbox", icon: Inbox },
  ];
  if (me?.isAuditor) items.push({ title: "All Requests", href: "/audit", icon: ScrollText });
  return items;
}

function navCls(isActive, compact) {
  return cn(
    compact
      ? "rounded-md px-2.5 py-1 text-xs font-semibold whitespace-nowrap"
      : "flex items-center gap-2.5 rounded-md px-3 py-2 text-sm font-medium transition-colors",
    isActive ? "bg-navy-50 text-navy-700" : "text-muted-foreground hover:bg-muted hover:text-foreground",
  );
}

function AppSidebar() {
  const items = useNavItems();
  const { me } = useCurrentUser();

  return (
    <aside className="hidden w-60 shrink-0 flex-col border-r bg-card lg:flex">
      <div className="flex h-14 items-center gap-2 border-b px-4">
        <span className="inline-flex size-8 items-center justify-center rounded-lg bg-primary text-primary-foreground">
          <KeyRound aria-hidden="true" className="size-4.5" />
        </span>
        <div className="min-w-0">
          <p className="truncate text-sm font-semibold text-foreground">Access Request Hub</p>
          <p className="truncate text-[11px] text-muted-foreground">Approval Workflow</p>
        </div>
      </div>

      <nav aria-label="Navigasi utama" className="flex flex-1 flex-col gap-1 p-3">
        {items.map((item) => {
          const Icon = item.icon;
          return (
            <NavLink key={item.href} to={item.href} end={item.end} className={({ isActive }) => navCls(isActive)}>
              <Icon aria-hidden="true" className="size-4" />
              {item.title}
            </NavLink>
          );
        })}
      </nav>

      {me ? (
        <div className="border-t p-3">
          <div className="flex flex-col gap-1 rounded-md bg-navy-50 px-3 py-2 text-xs text-navy-700">
            <span className="flex items-center gap-1.5 font-semibold">
              <ShieldCheck aria-hidden="true" className="size-3.5 shrink-0" />
              {me.displayName}
            </span>
            <span className="text-navy-600/80">
              {[
                me.isAuditor && "Auditor",
                me.hasDirectReports && "Manager",
                me.ownedApplications?.length > 0 &&
                  `Owner: ${me.ownedApplications.map((a) => a.name).join(", ")}`,
              ]
                .filter(Boolean)
                .join(" · ") || "Requester"}
            </span>
          </div>
        </div>
      ) : null}
    </aside>
  );
}

function AppHeader() {
  const items = useNavItems();

  return (
    <header className="sticky top-0 z-20 border-b bg-card/90 backdrop-blur">
      <div className="flex h-14 items-center justify-between gap-3 px-3 sm:px-4 md:px-5 lg:px-6 xl:px-8">
        <div className="flex min-w-0 items-center gap-3">
          <span className="inline-flex size-7 shrink-0 items-center justify-center rounded-lg bg-primary text-primary-foreground lg:hidden">
            <KeyRound aria-hidden="true" className="size-4" />
          </span>
          <nav aria-label="Navigasi utama" className="flex gap-1 overflow-x-auto lg:hidden">
            {items.map((item) => (
              <NavLink key={item.href} to={item.href} end={item.end} className={({ isActive }) => navCls(isActive, true)}>
                {item.title}
              </NavLink>
            ))}
          </nav>
        </div>
        <UserSwitcher />
      </div>
    </header>
  );
}

export function AppLayout() {
  return (
    <div className="flex min-h-screen">
      <AppSidebar />
      <div className="flex min-w-0 flex-1 flex-col">
        <AppHeader />
        <main className="flex flex-col gap-6 px-3 py-6 sm:px-4 md:px-5 lg:px-6 xl:px-8">
          <Outlet />
        </main>
      </div>
    </div>
  );
}
