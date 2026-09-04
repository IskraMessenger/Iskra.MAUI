import type { ReactNode } from "react";
import { Navigate, Route, Routes } from "react-router-dom";
import { useSession } from "./session";
import { LanguagePage } from "./pages/LanguagePage";
import { LoginPage } from "./pages/LoginPage";
import { RegisterPage } from "./pages/RegisterPage";
import { Shell } from "./pages/Shell";
import { ChatsPage } from "./pages/ChatsPage";
import { ChatPage } from "./pages/ChatPage";
import { ContactsPage } from "./pages/ContactsPage";
import { NetworkPage } from "./pages/NetworkPage";
import { SettingsPage } from "./pages/SettingsPage";
import { AddChatPage } from "./pages/AddChatPage";
import { MyQrPage } from "./pages/MyQrPage";
import { ServersPage } from "./pages/ServersPage";
import { RoutingPage } from "./pages/RoutingPage";
import { LanScanPage } from "./pages/LanScanPage";
import { BlacklistPage } from "./pages/BlacklistPage";
import { LogsPage } from "./pages/LogsPage";

function Gate({ children }: { children: ReactNode }) {
  const { ready, languageChosen, user } = useSession();
  if (!ready) return <div className="empty">Iskra…</div>;
  if (!languageChosen) return <Navigate to="/lang" replace />;
  if (!user) return <Navigate to="/login" replace />;
  return <>{children}</>;
}

export function App() {
  const { ready, languageChosen, user } = useSession();
  if (!ready) return <div className="empty">Iskra…</div>;
  return (
    <Routes>
      <Route path="/lang" element={<LanguagePage />} />
      <Route path="/login" element={user ? <Navigate to="/app/chats" replace /> : <LoginPage />} />
      <Route path="/register" element={user ? <Navigate to="/app/chats" replace /> : <RegisterPage />} />
      <Route
        path="/app"
        element={
          <Gate>
            <Shell />
          </Gate>
        }
      >
        <Route path="chats" element={<ChatsPage />} />
        <Route path="chats/:id" element={<ChatPage />} />
        <Route path="contacts" element={<ContactsPage />} />
        <Route path="network" element={<NetworkPage />} />
        <Route path="settings" element={<SettingsPage />} />
        <Route path="add-chat" element={<AddChatPage />} />
        <Route path="my-qr" element={<MyQrPage />} />
        <Route path="servers" element={<ServersPage />} />
        <Route path="routing" element={<RoutingPage />} />
        <Route path="lan" element={<LanScanPage />} />
        <Route path="blacklist" element={<BlacklistPage />} />
        <Route path="logs" element={<LogsPage />} />
      </Route>
      <Route path="*" element={<Navigate to={languageChosen ? (user ? "/app/chats" : "/login") : "/lang"} replace />} />
    </Routes>
  );
}
