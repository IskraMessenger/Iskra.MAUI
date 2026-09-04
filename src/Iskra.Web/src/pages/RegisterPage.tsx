import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { post } from "../api";
import { passwordErrorKey } from "../password";
import { useSession, useT } from "../session";

const SPECIALS = "~!@#$%^&*()\\|/,.<>";

export function RegisterPage() {
  const t = useT();
  const { refresh } = useSession();
  const nav = useNavigate();
  const [nick, setNick] = useState("");
  const [password, setPassword] = useState("");
  const [err, setErr] = useState("");
  const [busy, setBusy] = useState(false);
  const hint = passwordErrorKey(password);
  return (
    <div className="screen stack">
      <div className="brand">
        <img src="/assets/logo.png" alt="" />
        <h1>{t("register.title")}</h1>
        <p className="muted">{t("register.subtitle")}</p>
      </div>
      <input placeholder={t("login.nick")} value={nick} onChange={(e) => setNick(e.target.value)} />
      <input
        placeholder={t("login.password")}
        type="password"
        value={password}
        onChange={(e) => setPassword(e.target.value)}
      />
      <p className="muted">{hint ? t(hint, SPECIALS) : t("register.pass_ok")}</p>
      <p className="muted">{t("register.pass_hint", SPECIALS)}</p>
      {err && <div className="err">{err.includes(".") ? t(err, SPECIALS) : err}</div>}
      <button
        className="btn"
        disabled={busy}
        onClick={async () => {
          if (!nick.trim() || !password) {
            setErr("register.need_nick_pass");
            return;
          }
          const pe = passwordErrorKey(password);
          if (pe) {
            setErr(pe);
            return;
          }
          setBusy(true);
          setErr("");
          try {
            const u = await post<{ networkIdShort: string }>("/api/auth/register", { nickname: nick, password });
            await refresh();
            window.alert(t("register.network_id", u.networkIdShort));
            nav("/app/chats");
          } catch (e) {
            setErr(e instanceof Error ? e.message : t("register.failed"));
          } finally {
            setBusy(false);
          }
        }}
      >
        {t("register.button")}
      </button>
      <Link to="/login" className="btn ghost">
        {t("login.sign_in")}
      </Link>
    </div>
  );
}
