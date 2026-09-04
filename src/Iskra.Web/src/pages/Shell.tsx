import { NavLink, Outlet, useLocation, useNavigate } from "react-router-dom";
import { useSession, useT } from "../session";

const tabs = [
  { to: "/app/chats", key: "tab.chats", icon: "/assets/tab_chats.svg" },
  { to: "/app/contacts", key: "tab.contacts", icon: "/assets/tab_contacts.svg" },
  { to: "/app/network", key: "tab.network", icon: "/assets/tab_network.svg" },
  { to: "/app/settings", key: "tab.settings", icon: "/assets/tab_settings.svg" }
];

export function Shell() {
  const t = useT();
  const { user } = useSession();
  const loc = useLocation();
  const nav = useNavigate();
  const hideTabs = loc.pathname.startsWith("/app/chats/");
  return (
    <div className="app">
      <header className="header">
        {hideTabs ? (
          <button className="btn ghost small" onClick={() => nav("/app/chats")}>←</button>
        ) : (
          <div className="row">
            <div className="avatar" style={{ width: 32, height: 32, fontSize: 12, background: user?.avatar }}>
              {user?.initials}
            </div>
            <div>
              <div>{user?.nickname}</div>
              <div className="muted" style={{ fontSize: 12 }}>
                {user?.meshOn ? t("header.mesh_on") : t("header.mesh_off")}
              </div>
            </div>
          </div>
        )}
        <div className="muted" style={{ fontSize: 12 }}>{t("header.port", user?.dataUdpPort ?? "")}</div>
      </header>
      <div className="content">
        <Outlet />
      </div>
      {!hideTabs && (
        <nav className="tabs">
          {tabs.map((tab) => (
            <NavLink key={tab.to} to={tab.to} className={({ isActive }) => "tab" + (isActive ? " active" : "")}>
              <img src={tab.icon} alt="" />
              {t(tab.key)}
            </NavLink>
          ))}
        </nav>
      )}
    </div>
  );
}
