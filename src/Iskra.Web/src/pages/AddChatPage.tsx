import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { post, postFile } from "../api";
import { useT } from "../session";

export function AddChatPage() {
  const t = useT();
  const nav = useNavigate();
  const [nick, setNick] = useState("");
  const [networkId, setNetworkId] = useState("");
  const [publicKey, setPublicKey] = useState("");
  const [host, setHost] = useState("");
  const [port, setPort] = useState("17501");
  const [err, setErr] = useState("");
  return (
    <div className="screen stack">
      <h2>{t("addchat.title")}</h2>
      <input placeholder={t("addchat.ph_nick")} value={nick} onChange={(e) => setNick(e.target.value)} />
      <input placeholder={t("addchat.ph_id")} value={networkId} onChange={(e) => setNetworkId(e.target.value)} />
      <textarea placeholder={t("addchat.ph_key")} rows={5} value={publicKey} onChange={(e) => setPublicKey(e.target.value)} />
      <input placeholder={t("addchat.host")} value={host} onChange={(e) => setHost(e.target.value)} />
      <input placeholder={t("addchat.port")} value={port} onChange={(e) => setPort(e.target.value)} />
      <label className="btn ghost">
        {t("addchat.scan_img")}
        <input
          type="file"
          accept="image/*"
          hidden
          onChange={async (e) => {
            const f = e.target.files?.[0];
            e.target.value = "";
            if (!f) return;
            try {
              const r = await postFile<{
                id: number;
                nick: string;
                networkId: string;
                publicKey: string;
                host: string;
                port: number;
              }>("/api/chats/qr", f);
              setNick(r.nick);
              setNetworkId(r.networkId);
              setPublicKey(r.publicKey);
              setHost(r.host);
              setPort(String(r.port));
              nav(`/app/chats/${r.id}`);
            } catch (ex) {
              setErr(ex instanceof Error ? ex.message : t("addchat.qr_fail"));
            }
          }}
        />
      </label>
      {err && <div className="err">{err.includes(".") ? t(err) : err}</div>}
      <button
        className="btn"
        onClick={async () => {
          setErr("");
          try {
            const r = await post<{ id: number }>("/api/chats/add", {
              nick,
              networkId,
              publicKey,
              host,
              port: Number(port)
            });
            nav(`/app/chats/${r.id}`);
          } catch (e) {
            setErr(e instanceof Error ? e.message : t("error"));
          }
        }}
      >
        {t("ok")}
      </button>
    </div>
  );
}
