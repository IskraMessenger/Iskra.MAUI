import { useEffect, useState } from "react";
import { get } from "../api";
import { useT } from "../session";

export function LogsPage() {
  const t = useT();
  const [path, setPath] = useState("");
  const [text, setText] = useState("");
  useEffect(() => {
    void get<{ path: string; text: string }>("/api/logs").then((r) => {
      setPath(r.path);
      setText(r.text);
    });
  }, []);
  return (
    <div className="screen wide stack">
      <h2>{t("logs.title")}</h2>
      <p className="muted">{path ? t("logs.path", path) : t("logs.path_none")}</p>
      <button
        className="btn"
        onClick={async () => {
          await navigator.clipboard.writeText(text);
          window.alert(t("logs.copied"));
        }}
      >
        {t("copy")}
      </button>
      <pre className="log">{text}</pre>
    </div>
  );
}
