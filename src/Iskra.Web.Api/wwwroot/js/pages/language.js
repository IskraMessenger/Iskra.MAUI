import { languages } from "../i18n.js";
import { session, setLanguage, t } from "../session.js";
import { nav } from "../router.js";
import { el } from "../dom.js";

const flags = {
  Russian: "/assets/flag_ru.svg",
  English: "/assets/flag_gb.svg",
  Spanish: "/assets/flag_es.svg",
  German: "/assets/flag_de.svg",
  French: "/assets/flag_fr.svg",
  ChineseSimplified: "/assets/flag_cn.svg"
};

export function LanguagePage() {
  let sel = session.lang;
  const items = new Map();

  function select(id) {
    sel = id;
    for (const [k, node] of items) node.classList.toggle("sel", k === sel);
  }

  const root = el(
    "div",
    { class: "screen stack" },
    el(
      "div",
      { class: "brand" },
      el("img", { src: "/assets/logo.png", alt: "" }),
      el("h1", null, "Iskra"),
      el("p", { class: "muted" }, t("lang.choose"))
    ),
    languages.map((l) => {
      const item = el(
        "div",
        {
          class: "lang-item" + (sel === l.id ? " sel" : ""),
          onclick: () => select(l.id)
        },
        el("img", { src: flags[l.id], alt: "" }),
        el("span", null, l.name)
      );
      items.set(l.id, item);
      return item;
    }),
    el(
      "button",
      {
        class: "btn",
        onclick: async () => {
          await setLanguage(sel);
          nav("/login");
        }
      },
      t("lang.continue")
    )
  );
  return root;
}
