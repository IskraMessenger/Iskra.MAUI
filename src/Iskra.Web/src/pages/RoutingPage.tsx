import { useEffect, useState } from "react";
import { get, post } from "../api";
import { useT } from "../session";
import type { RoutingDto } from "../types";

export function RoutingPage() {
  const t = useT();
  const [s, setS] = useState<RoutingDto | null>(null);
  const [err, setErr] = useState("");
  useEffect(() => {
    void get<RoutingDto>("/api/routing").then(setS);
  }, []);
  if (!s) return <div className="empty">{t("chat.state.loading")}</div>;
  const num = (k: "maxSearchHops" | "sendFailureSearchAttempts" | "delayMs" | "timeoutMs", label: string) => (
    <label>
      {label}
      <input
        type="number"
        value={s[k]}
        onChange={(e) => setS({ ...s, [k]: Number(e.target.value) })}
        style={{ marginTop: 6 }}
      />
    </label>
  );
  return (
    <div className="screen stack">
      <h2>{t("routing.title")}</h2>
      {num("maxSearchHops", t("routing.max_depth"))}
      {num("sendFailureSearchAttempts", t("routing.attempts"))}
      {num("delayMs", t("routing.delay"))}
      {num("timeoutMs", t("routing.timeout"))}
      <label>
        {t("routing.speed")}
        <select
          value={s.linkTechnology}
          onChange={(e) => setS({ ...s, linkTechnology: e.target.value })}
          style={{ marginTop: 6 }}
        >
          {(s.presets ?? []).map((p) => (
            <option key={p.value} value={p.value}>{p.label}</option>
          ))}
        </select>
      </label>
      <div className="switch">
        <span>{t("routing.udp")}</span>
        <input type="checkbox" checked={s.enableUdpTransport} onChange={(e) => setS({ ...s, enableUdpTransport: e.target.checked })} />
      </div>
      <div className="switch">
        <span>{t("routing.bt")}</span>
        <input type="checkbox" checked={s.enableBluetoothTransport} onChange={(e) => setS({ ...s, enableBluetoothTransport: e.target.checked })} />
      </div>
      <div className="switch">
        <span>{t("routing.bt_pair")}</span>
        <input type="checkbox" checked={s.suggestBluetoothPairing} onChange={(e) => setS({ ...s, suggestBluetoothPairing: e.target.checked })} />
      </div>
      <div className="switch">
        <span>{t("routing.share_routes")}</span>
        <input type="checkbox" checked={s.advertisePeerSearch} onChange={(e) => setS({ ...s, advertisePeerSearch: e.target.checked })} />
      </div>
      {err && <div className="err">{err.includes(".") ? t(err) : err}</div>}
      <button
        className="btn"
        onClick={async () => {
          setErr("");
          try {
            await post("/api/routing", {
              maxSearchHops: s.maxSearchHops,
              sendFailureSearchAttempts: s.sendFailureSearchAttempts,
              delayMs: s.delayMs,
              timeoutMs: s.timeoutMs,
              linkTechnology: s.linkTechnology,
              enableUdpTransport: s.enableUdpTransport,
              enableBluetoothTransport: s.enableBluetoothTransport,
              suggestBluetoothPairing: s.suggestBluetoothPairing,
              advertisePeerSearch: s.advertisePeerSearch
            });
          } catch (e) {
            setErr(e instanceof Error ? e.message : t("error"));
          }
        }}
      >
        {t("save")}
      </button>
    </div>
  );
}
