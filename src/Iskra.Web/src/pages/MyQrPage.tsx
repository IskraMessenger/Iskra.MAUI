import { useEffect, useState } from "react";
import { get } from "../api";
import { useT } from "../session";

export function MyQrPage() {
  const t = useT();
  const [png, setPng] = useState("");
  useEffect(() => {
    void get<{ png: string }>("/api/network/qr").then((r) => setPng(r.png));
  }, []);
  return (
    <div className="screen">
      <h2>{t("myqr.title")}</h2>
      <p className="muted">{t("myqr.hint")}</p>
      {png ? <img className="qr" src={`data:image/png;base64,${png}`} alt="QR" /> : <p>{t("qr.not_ready")}</p>}
    </div>
  );
}
