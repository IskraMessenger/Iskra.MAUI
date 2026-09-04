import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { del, get, post } from "../api";
import { useSession, useT } from "../session";
import type { ChatRow } from "../types";

export function ChatsPage() {
  const t = useT();
  const { tick } = useSession();
  const nav = useNavigate();
  const [rows, setRows] = useState<ChatRow[]>([]);
  const [q, setQ] = useState("");
  useEffect(() => {
    void get<ChatRow[]>("/api/chats").then(setRows).catch(() => setRows([]));
  }, [tick]);
  const filtered = rows.filter(
    (c) => !q || c.nick.toLowerCase().includes(q.toLowerCase()) || c.networkId.toLowerCase().includes(q.toLowerCase())
  );
  return (
    <div>
      <div className="search">
        <input placeholder={t("search")} value={q} onChange={(e) => setQ(e.target.value)} />
      </div>
      {filtered.length === 0 ? (
        <div className="empty">{t("chats.empty")}</div>
      ) : (
        filtered.map((c) => (
          <div
            key={c.id}
            className="item"
            onClick={() => nav(`/app/chats/${c.id}`)}
            onContextMenu={async (e) => {
              e.preventDefault();
              const choice = window.prompt(`${c.nick}\n1 = ${t("delete")}\n2 = ${t("blacklist.add")}`, "1");
              if (choice === "1" && window.confirm(t("chats.delete_body", c.nick))) {
                await del(`/api/chats/${c.id}`);
                setRows((r) => r.filter((x) => x.id !== c.id));
              }
              if (choice === "2" && window.confirm(t("blacklist.add_body", c.nick))) {
                await post(`/api/chats/${c.id}/block`);
                setRows((r) => r.filter((x) => x.id !== c.id));
              }
            }}
          >
            <div className="avatar" style={{ background: c.avatar }}>
              {c.initials}
            </div>
            <div className="meta">
              <div className="title">
                <span>{c.nick}</span>
                <span className="muted">{c.time}</span>
              </div>
              <div className="sub">{c.preview || t("preview.none")}</div>
            </div>
            <span className={"dot " + (c.online ? "on" : "off")} />
          </div>
        ))
      )}
      <button className="fab" onClick={() => nav("/app/add-chat")} title={t("network.add_chat")}>
        +
      </button>
    </div>
  );
}
