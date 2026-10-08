import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Navigate, Route, Routes, useNavigate } from "react-router";
import { getMe, getUsers } from "./api/accessRequests";
import { setApiUser } from "./api/client";
import { CurrentUserContext, DEFAULT_USER, STORAGE_KEY } from "./auth/CurrentUserContext";
import { AppLayout } from "./components/AppLayout";
import { AllRequestsPage } from "./pages/AllRequestsPage";
import { InboxPage } from "./pages/InboxPage";
import { MyRequestsPage } from "./pages/MyRequestsPage";
import { NewRequestPage } from "./pages/NewRequestPage";
import { RequestDetailPage } from "./pages/RequestDetailPage";

export function App() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();
  const [currentUserEmail, setCurrentUserEmail] = useState(
    () => localStorage.getItem(STORAGE_KEY) || DEFAULT_USER,
  );

  const usersQuery = useQuery({ queryKey: ["users"], queryFn: getUsers });
  const meQuery = useQuery({ queryKey: ["me"], queryFn: getMe });

  function switchUser(email) {
    setApiUser(email);
    localStorage.setItem(STORAGE_KEY, email);
    queryClient.clear();
    setCurrentUserEmail(email);
    navigate("/requests");
  }

  const contextValue = {
    currentUserEmail,
    me: meQuery.data ?? null,
    users: usersQuery.data ?? [],
    switchUser,
  };

  return (
    <CurrentUserContext.Provider value={contextValue}>
      <Routes>
        <Route element={<AppLayout />}>
          <Route index element={<Navigate to="/requests" replace />} />
          <Route path="requests" element={<MyRequestsPage />} />
          <Route path="requests/new" element={<NewRequestPage />} />
          <Route path="requests/:id" element={<RequestDetailPage />} />
          <Route path="inbox" element={<InboxPage />} />
          <Route path="audit" element={<AllRequestsPage />} />
          <Route path="*" element={<Navigate to="/requests" replace />} />
        </Route>
      </Routes>
    </CurrentUserContext.Provider>
  );
}
