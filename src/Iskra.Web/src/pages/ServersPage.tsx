import { useEffect, useState } from "react";
import { del, get, post, postFile } from "../api";
import { useT } from "../session";
import type { ServerRow } from "../types";

export function ServersPage() {
  const t = useT();
  const [items, setItems] = useState<ServerRow[]>([]);
  const [max, setMax] = useState(32);
  const [url, setUrl] = useState("");
  const [status, setStatus] = useState("");
  const load = () =>
    get<{ items: ServerRow[]; max: number }>("/api/servers").then((r) => {
      setItems(r.items);
      setMax(r.max);
    });
  useEffect(() => {
    void load();
  }, []);
  return (
    <div className="screen wide stack">
      <h2>{t("servers.title")}</h2>
      <p className="muted">{t("servers.intro")}</p>
      <p className="muted">{t("servers.count", items.length, max)}</p>
      <input placeholder={t("servers.base_url")} value={url} onChange={(e) => setUrl(e.target.value)} />
      <div className="row">
        <button
          className="btn"
          onClick={async () => {
            setStatus(t("servers.connecting"));
            try {
              const r = await post<{ baseUrl: string }>("/api/servers", { baseUrl: url });
              setUrl("");
              setStatus(t("servers.added", r.baseUrl));
              await load();
            } catch (e) {
              setStatus(e instanceof Error ? e.message : t("error"));
            }
          }}
        >
          {t("servers.add")}
        </button>
        <label className="btn ghost">
          {t("servers.import")}
          <input
            type="file"
            accept="image/*"
            hidden
            onChange={async (e) => {
              const f = e.target.files?.[0];
              e.target.value = "";
              if (!f) return;
              setStatus(t("servers.importing"));
              try {
                const r = await postFile<{ already?: boolean; baseUrl: string }>("/api/servers/import-qr", f);
                setStatus(r.already ? t("servers.already_status", r.baseUrl) : t("servers.imported", r.baseUrl));
                await load();
              } catch (ex) {
                setStatus(ex instanceof Error ? ex.message : t("servers.import_read_fail"));
              }
            }}
          />
        </label>
      </div>
      {status && <div className="muted">{status.includes(".") ? status : status}</div>}
      {items.map((s) => (
        <div key={s.id} className="card stack">
          <strong>{s.baseUrl}</strong>
          <div className="muted">
            {t(
              "servers.meta",
              s.trustRating,
              s.trusted ? t("servers.meta_trusted") : t("servers.meta_untrusted"),
              s.active ? t("servers.meta_active") : t("servers.meta_off"),
              s.isRegistered ? t("servers.meta_registered") : t("servers.meta_not_registered"),
              s.fingerprintSha256?.slice(0, 12) ?? ""
            )}
          </div>
          <div className="row" style={{ flexWrap: "wrap" }}>
            <button
              className="btn small"
              onClick={async () => {
                try {
                  await post(`/api/servers/${s.id}/active`, { active: !s.active });
                  await load();
                } catch (e) {
                  window.alert(e instanceof Error ? e.message : t("servers.untrusted_body"));
                }
              }}
            >
              {s.active ? t("servers.meta_off") : t("servers.meta_active")}
            </button>
            <button
              className="btn small"
              onClick={async () => {
                setStatus(t("servers.checking", s.baseUrl));
                const r = await post<{
                  status: string;
                  errorMessage?: string;
                  expectedFingerprint?: string;
                  actualFingerprint?: string;
                }>(`/api/servers/${s.id}/recheck`);
                if (r.status === "Ok") setStatus(t("servers.recheck_ok"));
                else if (r.status === "Unreachable")
                  setStatus(t("servers.recheck_unreachable_detail", r.errorMessage ?? ""));
                else setStatus(t("servers.recheck_fp", r.expectedFingerprint, r.actualFingerprint));
                await load();
              }}
            >
              {t("servers.check")}
            </button>
            {s.canAsk && (
              <button
                className="btn small"
                onClick={async () => {
                  setStatus(t("servers.asking", s.baseUrl));
                  const r = await post<{ received: number; updated: number; added: number }>(`/api/servers/${s.id}/ask`);
                  setStatus(t("servers.ask_result", r.received, r.updated, r.added, s.baseUrl));
                  await load();
                }}
              >
                {t("servers.ask")}
              </button>
            )}
            <button
              className="btn small"
              onClick={async () => {
                const r = await get<{ png: string }>(`/api/servers/${s.id}/qr`);
                const w = window.open();
                w?.document.write(`<img src="data:image/png;base64,${r.png}" />`);
              }}
            >
              {t("servers.share")}
            </button>
            <button
              className="btn small danger"
              onClick={async () => {
                if (!window.confirm(t("servers.delete_body", s.baseUrl))) return;
                await del(`/api/servers/${s.id}`);
                await load();
              }}
            >
              {t("servers.delete")}
            </button>
          </div>
        </div>
      ))}
    </div>
  );
}
