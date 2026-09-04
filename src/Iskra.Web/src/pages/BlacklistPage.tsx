import { useEffect, useState } from "react";
import { del, get } from "../api";
import { useT } from "../session";
import type { BlacklistRow } from "../types";

export function BlacklistPage() {
  const t = useT();
  const [rows, setRows] = useState<BlacklistRow[]>([]);
  const load = () => get<BlacklistRow[]>("/api/blacklist").then(setRows);
  useEffect(() => {
    void load();
  }, []);
  return (
    <div className="screen wide">
      <h2>{t("blacklist.title")}</h2>
      {rows.length === 0 ? (
        <div className="empty">{t("blacklist.empty")}</div>
      ) : (
        rows.map((r) => (
          <div key={r.networkId} className="item">
            <div className="meta">
              <div className="title">{r.nickname}</div>
              <div className="sub">{r.networkId}</div>
            </div>
            <button
              className="btn small"
              onClick={async () => {
                await del(`/api/blacklist/${encodeURIComponent(r.networkId)}`);
                await load();
              }}
            >
              {t("blacklist.unblock")}
            </button>
          </div>
        ))
      )}
    </div>
  );
}
