import { languages, type Lang } from "../i18n";
import { useSession, useT } from "../session";
import { useNavigate } from "react-router-dom";
import { useState } from "react";

const flags: Record<Lang, string> = {
  Russian: "/assets/flag_ru.svg",
  English: "/assets/flag_gb.svg",
  Spanish: "/assets/flag_es.svg",
  German: "/assets/flag_de.svg",
  French: "/assets/flag_fr.svg",
  ChineseSimplified: "/assets/flag_cn.svg"
};

export function LanguagePage() {
  const t = useT();
  const { lang, setLanguage } = useSession();
  const [sel, setSel] = useState<Lang>(lang);
  const nav = useNavigate();
  return (
    <div className="screen stack">
      <div className="brand">
        <img src="/assets/logo.png" alt="" />
        <h1>Iskra</h1>
        <p className="muted">{t("lang.choose")}</p>
      </div>
      {languages.map((l) => (
        <div key={l.id} className={"lang-item" + (sel === l.id ? " sel" : "")} onClick={() => setSel(l.id)}>
          <img src={flags[l.id]} alt="" />
          <span>{l.name}</span>
        </div>
      ))}
      <button
        className="btn"
        onClick={async () => {
          await setLanguage(sel);
          nav("/login");
        }}
      >
        {t("lang.continue")}
      </button>
    </div>
  );
}
