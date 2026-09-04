import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { get, post } from "../api";
import { languages, type Lang } from "../i18n";
import { useSession, useT } from "../session";
import { themes, type ThemeId } from "../theme";
import type { SettingsDto } from "../types";

export function SettingsPage() {
  const t = useT();
  const nav = useNavigate();
  const { setLanguage, setThemeId, logout, lang, theme } = useSession();
  const [s, setS] = useState<SettingsDto | null>(null);
  const load = () => get<SettingsDto>("/api/settings").then(setS);
  useEffect(() => {
    void load();
  }, []);
  if (!s) return <div className="empty">{t("chat.state.loading")}</div>;
  const mb = s.storageBytes / (1024 * 1024);
  const storage = mb >= 1024 ? t("settings.storage_gb", mb / 1024) : t("settings.storage_mb", mb);
  const save = async (patch: { bluetooth?: boolean; lan?: boolean; routing?: boolean; trafficQuality?: string }) => {
    await post("/api/settings", patch);
    await load();
  };
  return (
    <div className="screen wide stack">
      <div className="row">
        <div className="avatar" style={{ background: s.avatar }}>{s.initials}</div>
        <div>
          <strong>{s.nick}</strong>
          <div className="muted">{s.networkId}</div>
        </div>
      </div>
      <div className="card">
        <div className="muted">{t("settings.appearance")}</div>
        <label>
          {t("lang.section")}
          <select
            value={lang}
            onChange={(e) => void setLanguage(e.target.value as Lang)}
            style={{ marginTop: 8 }}
          >
            {languages.map((l) => (
              <option key={l.id} value={l.id}>{l.name}</option>
            ))}
          </select>
        </label>
        <div className="row" style={{ flexWrap: "wrap", marginTop: 12 }}>
          {themes.map((th) => (
            <button
              key={th.id}
              className="btn small"
              style={{ background: th.accent, opacity: theme === th.id ? 1 : 0.55 }}
              onClick={() => void setThemeId(th.id as ThemeId)}
            >
              {t(
                th.id === "DarkFlame" ? "theme.dark"
                  : th.id === "Night" ? "theme.night"
                    : th.id === "LightFlame" ? "theme.light"
                      : th.id === "ColdBlue" ? "theme.cold"
                        : th.id === "Forest" ? "theme.forest" : "theme.mono"
              )}
            </button>
          ))}
        </div>
      </div>
      <div className="card">
        <div className="switch">
          <span>{t("settings.lan")}</span>
          <input type="checkbox" checked={s.lan} onChange={(e) => void save({ lan: e.target.checked })} />
        </div>
        <div className="switch">
          <span>{t("settings.bluetooth")}</span>
          <input type="checkbox" checked={s.bluetooth} onChange={(e) => void save({ bluetooth: e.target.checked })} />
        </div>
        <div className="switch">
          <span>{t("settings.routing")}</span>
          <input type="checkbox" checked={s.routing} onChange={(e) => void save({ routing: e.target.checked })} />
        </div>
        <label>
          {t("settings.economy")}
          <select
            value={s.trafficQuality}
            onChange={(e) => void save({ trafficQuality: e.target.value })}
            style={{ marginTop: 8 }}
          >
            <option value="Normal">{t("economy.mode.normal")}</option>
            <option value="Economy">{t("economy.mode.economy")}</option>
            <option value="UltraEconomy">{t("economy.mode.ultra")}</option>
          </select>
        </label>
      </div>
      <div className="card">
        <div className="linkrow" onClick={() => nav("/app/routing")}>{t("settings.routing_open")}</div>
        <div className="linkrow" onClick={() => nav("/app/servers")}>{t("network.servers")}</div>
        <div className="linkrow" onClick={() => nav("/app/blacklist")}>{t("blacklist.title")}</div>
        <div className="linkrow" onClick={() => nav("/app/logs")}>{t("settings.logs")}</div>
        <div className="muted">{t("settings.storage")}: {storage}</div>
      </div>
      <div className="card">
        <div className="muted">{t("settings.about")}</div>
        <p style={{ whiteSpace: "pre-wrap" }}>{t("settings.about_body")}</p>
      </div>
      <button
        className="btn danger"
        onClick={async () => {
          await logout();
          nav("/login");
        }}
      >
        {t("settings.logout")}
      </button>
    </div>
  );
}
