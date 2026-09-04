import { useEffect, useRef, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { del, get, post, postFile } from "../api";
import { useSession, useT } from "../session";
import type { ChatDetail, ChatMessage } from "../types";

export function ChatPage() {
  const t = useT();
  const { id } = useParams();
  const chatId = Number(id);
  const { tick } = useSession();
  const nav = useNavigate();
  const [chat, setChat] = useState<ChatDetail | null>(null);
  const [items, setItems] = useState<ChatMessage[]>([]);
  const [text, setText] = useState("");
  const [err, setErr] = useState("");
  const bottom = useRef<HTMLDivElement>(null);
  const fileRef = useRef<HTMLInputElement>(null);

  const load = async () => {
    try {
      const d = await get<ChatDetail>(`/api/chats/${chatId}`);
      setChat(d);
      const page = await get<{ items: ChatMessage[] }>(`/api/chats/${chatId}/messages?limit=80`);
      setItems(page.items);
    } catch (e) {
      setErr(e instanceof Error ? e.message : t("chats.open_failed"));
    }
  };

  useEffect(() => {
    void load();
  }, [chatId, tick]);

  useEffect(() => {
    bottom.current?.scrollIntoView({ behavior: "smooth" });
  }, [items.length]);

  if (err) return <div className="empty err">{err.includes(".") ? t(err) : err}</div>;
  if (!chat) return <div className="empty">{t("chat.state.loading")}</div>;

  const send = async () => {
    const msg = text.trim();
    if (!msg) return;
    setText("");
    try {
      await post(`/api/chats/${chatId}/messages`, { text: msg });
      await load();
    } catch (e) {
      setErr(e instanceof Error ? e.message : t("error"));
    }
  };

  return (
    <div style={{ display: "flex", flexDirection: "column", minHeight: "100%" }}>
      <div className="header" style={{ position: "sticky", top: 0, zIndex: 1 }}>
        <div className="row">
          <div className="avatar" style={{ width: 36, height: 36, background: chat.avatar }}>
            {chat.initials}
          </div>
          <div>
            <strong>{chat.nick}</strong>
            <div className="muted" style={{ fontSize: 12 }}>
              {chat.online ? t("online") : t("offline")} · {t("chat.node", chat.networkId)}
            </div>
          </div>
        </div>
        <button
          className="btn ghost small"
          onClick={() => {
            const menu = window.prompt(
              `${t("chat.clear_title")}=1\n${t("safety.emergency")}=2\n${t("chat.delete")}=3`,
              ""
            );
            void (async () => {
              if (menu === "1" && window.confirm(t("chat.clear_body"))) await post(`/api/chats/${chatId}/clear`);
              if (menu === "2" && window.confirm(t("safety.untrust_hint"))) await post(`/api/chats/${chatId}/untrust`);
              if (menu === "3" && window.confirm(t("chats.delete_body", chat.nick))) {
                await del(`/api/chats/${chatId}`);
                nav("/app/chats");
              }
              await load();
            })();
          }}
        >
          ⋯
        </button>
      </div>
      {chat.safety && <div className="muted" style={{ padding: "8px 16px", fontSize: 12, whiteSpace: "pre-wrap" }}>{chat.safety}</div>}
      <div className="msgs">
        {items.map((m) => (
          <div key={m.id} className={"bubble " + (m.outgoing ? "out" : "in")}>
            {m.kind === "image" && m.hasBlob ? (
              <img className="att" src={`/api/chats/${chatId}/messages/${m.id}/file`} alt="" />
            ) : m.kind !== "text" ? (
              <div>
                <div>
                  {m.kind === "voice" ? t("chat.caption.voice") : m.kind === "video" ? t("chat.caption.video") : t("chat.caption.file")}
                  {m.fileName ? ` · ${m.fileName}` : ""}
                </div>
                {m.hasBlob ? (
                  <a href={`/api/chats/${chatId}/messages/${m.id}/file`} download>
                    {t("chat.save_doc")}
                  </a>
                ) : (
                  <button
                    className="btn small"
                    onClick={async () => {
                      await post(`/api/chats/${chatId}/messages/${m.id}/download`);
                    }}
                  >
                    {t("chat.state.tap_download")}
                  </button>
                )}
              </div>
            ) : (
              <div>{m.text}</div>
            )}
            <div className="time">
              {m.time} {m.outgoing ? (m.delivery === "failed" ? "!" : m.delivery === "pending" ? "…" : "✓") : ""}
            </div>
            {m.delivery === "failed" && (
              <button className="btn small" onClick={() => post(`/api/chats/${chatId}/messages/${m.id}/retry`)}>
                {t("chat.state.failed")}
              </button>
            )}
          </div>
        ))}
        <div ref={bottom} />
      </div>
      <div className="composer">
        <button className="btn small" onClick={() => fileRef.current?.click()}>
          +
        </button>
        <input
          ref={fileRef}
          type="file"
          hidden
          onChange={async (e) => {
            const f = e.target.files?.[0];
            e.target.value = "";
            if (!f) return;
            try {
              await postFile(`/api/chats/${chatId}/files`, f);
              await load();
            } catch (ex) {
              window.alert(ex instanceof Error ? ex.message : t("error"));
            }
          }}
        />
        <textarea
          placeholder={t("chat.message_ph")}
          value={text}
          rows={1}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey) {
              e.preventDefault();
              void send();
            }
          }}
        />
        <button className="btn small" onClick={() => void send()}>
          →
        </button>
      </div>
    </div>
  );
}
