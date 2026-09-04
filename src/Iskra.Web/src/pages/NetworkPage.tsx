import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { get, post } from "../api";
import { useSession, useT } from "../session";
import type { NetworkNode } from "../types";

export function NetworkPage() {
  const t = useT();
  const { tick } = useSession();
  const nav = useNavigate();
  const [nodes, setNodes] = useState<NetworkNode[]>([]);
  useEffect(() => {
    void get<NetworkNode[]>("/api/network").then(setNodes).catch(() => setNodes([]));
  }, [tick]);
  return (
    <div>
      <div className="menu">
        <button className="btn small" onClick={() => nav("/app/my-qr")}>{t("network.my_qr")}</button>
        <button className="btn small" onClick={() => nav("/app/add-chat")}>{t("network.add_chat")}</button>
        <button
          className="btn small"
          onClick={async () => {
            const r = await get<{ text: string }>("/api/network/addresses");
            await navigator.clipboard.writeText(r.text);
            window.alert(t("copied.addresses"));
          }}
        >
          {t("network.my_addresses")}
        </button>
        <button
          className="btn small"
          onClick={async () => {
            const r = await get<{ text: string }>("/api/network/keys");
            await navigator.clipboard.writeText(r.text);
            window.alert(t("copied.keys"));
          }}
        >
          {t("network.copy_key")}
        </button>
        <button className="btn small" onClick={() => nav("/app/servers")}>{t("network.servers")}</button>
        <button className="btn small" onClick={() => nav("/app/lan")}>{t("lan.title")}</button>
      </div>
      <h3 style={{ padding: "8px 16px" }}>{t("network.nodes_count", nodes.length)}</h3>
      {nodes.map((n) => (
        <div
          key={n.networkId}
          className="item"
          onClick={async () => {
            try {
              const r = await post<{ id: number }>("/api/chats/open-peer", { networkId: n.networkId });
              nav(`/app/chats/${r.id}`);
            } catch (e) {
              window.alert(e instanceof Error ? e.message : t("network.error"));
            }
          }}
        >
          <div className="avatar" style={{ background: n.avatar }}>{n.initials}</div>
          <div className="meta">
            <div className="title">{n.name}</div>
            <div className="sub">{n.networkId} · {n.hops}</div>
          </div>
          <span className={"dot " + (n.online ? "on" : "off")} />
        </div>
      ))}
    </div>
  );
}
