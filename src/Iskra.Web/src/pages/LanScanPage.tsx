import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { get, post } from "../api";
import { useSession, useT } from "../session";
import type { LanRow } from "../types";

export function LanScanPage() {
  const t = useT();
  const { tick } = useSession();
  const nav = useNavigate();
  const [rows, setRows] = useState<LanRow[]>([]);
  const [busy, setBusy] = useState(false);
  const load = () => get<LanRow[]>("/api/lan").then(setRows).catch(() => setRows([]));
  useEffect(() => {
    void load();
  }, [tick]);
  return (
    <div>
      <div className="search row">
        <p className="muted" style={{ flex: 1 }}>{t("lan.hint")}</p>
        <button
          className="btn small"
          disabled={busy}
          onClick={async () => {
            setBusy(true);
            try {
              await post("/api/lan/scan");
              await load();
            } finally {
              setBusy(false);
            }
          }}
        >
          {t("lan.scan")}
        </button>
      </div>
      {rows.map((r) => (
        <div
          key={r.networkId}
          className="item"
          onDoubleClick={async () => {
            try {
              const chat = await post<{ id: number }>("/api/chats/open-peer", { networkId: r.networkId });
              nav(`/app/chats/${chat.id}`);
            } catch (e) {
              window.alert(e instanceof Error ? e.message : t("network.error"));
            }
          }}
        >
          <div className="meta">
            <div className="title">
              {r.nickname}
              <span className={"dot " + (r.online ? "on" : "off")} />
            </div>
            <div className="sub">{t("lan.detail", r.networkId, r.transport, t("lan.last", r.lastSeen))}</div>
          </div>
        </div>
      ))}
    </div>
  );
}
