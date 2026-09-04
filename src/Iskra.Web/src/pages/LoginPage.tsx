import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { post } from "../api";
import { useSession, useT } from "../session";

export function LoginPage() {
  const t = useT();
  const { refresh } = useSession();
  const nav = useNavigate();
  const [nick, setNick] = useState("");
  const [password, setPassword] = useState("");
  const [err, setErr] = useState("");
  const [busy, setBusy] = useState(false);
  return (
    <div className="screen stack">
      <div className="brand">
        <img src="/assets/logo.png" alt="" />
        <h1>Iskra</h1>
        <p className="muted">{t("login.subtitle")}</p>
      </div>
      <input placeholder={t("login.nick")} value={nick} onChange={(e) => setNick(e.target.value)} autoComplete="username" />
      <input
        placeholder={t("login.password")}
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
        autoComplete="current-password"
      />
      {err && <div className="err">{err.startsWith("login.") || err.startsWith("pass.") ? t(err) : err}</div>}
      <button
        className="btn"
        disabled={busy}
        onClick={async () => {
          setBusy(true);
          setErr("");
          try {
            await post("/api/auth/login", { nickname: nick, password });
            await refresh();
            nav("/app/chats");
          } catch (e) {
            setErr(e instanceof Error ? e.message : t("login.failed"));
          } finally {
            setBusy(false);
          }
        }}
      >
        {t("login.sign_in")}
      </button>
      <Link to="/register" className="btn ghost">
        {t("login.create_account")}
      </Link>
    </div>
  );
}
