import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { get, post } from "../api";
import { useSession, useT } from "../session";
import type { ContactRow } from "../types";

export function ContactsPage() {
  const t = useT();
  const { tick } = useSession();
  const nav = useNavigate();
  const [rows, setRows] = useState<ContactRow[]>([]);
  const [q, setQ] = useState("");
  const [busy, setBusy] = useState(false);
  const load = () => get<ContactRow[]>("/api/contacts").then(setRows).catch(() => setRows([]));
  useEffect(() => {
    void load();
  }, [tick]);
  const filtered = rows.filter(
    (c) => !q || c.name.toLowerCase().includes(q.toLowerCase()) || c.networkId.toLowerCase().includes(q.toLowerCase())
  );
  return (
    <div>
      <div className="search row">
        <input placeholder={t("contacts.search")} value={q} onChange={(e) => setQ(e.target.value)} />
        <button
          className="btn small"
          disabled={busy}
          onClick={async () => {
            setBusy(true);
            try {
              await post("/api/contacts/scan");
              await load();
            } finally {
              setBusy(false);
            }
          }}
        >
          {busy ? t("contacts.scanning") : t("contacts.scan")}
        </button>
      </div>
      {filtered.length === 0 ? (
        <div className="empty">{t("contacts.empty")}</div>
      ) : (
        filtered.map((c) => (
          <div
            key={c.networkId}
            className="item"
            onClick={async () => {
              if (c.chatId) {
                nav(`/app/chats/${c.chatId}`);
                return;
              }
              try {
                const r = await post<{ id: number }>("/api/chats/open-peer", { networkId: c.networkId });
                nav(`/app/chats/${r.id}`);
              } catch (e) {
                window.alert(e instanceof Error ? e.message : t("network.error"));
              }
            }}
          >
            <div className="avatar" style={{ background: c.avatar }}>
              {c.initials}
            </div>
            <div className="meta">
              <div className="title">{c.name}</div>
              <div className="sub">{c.detail}</div>
            </div>
            <span className={"dot " + (c.online ? "on" : "off")} />
          </div>
        ))
      )}
    </div>
  );
}
