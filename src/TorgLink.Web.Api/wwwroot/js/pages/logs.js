import { get } from "../api.js";
import { t } from "../session.js";
import { el } from "../dom.js";

export function LogsPage() {
  const pathEl = el("p", { class: "muted" }, t("logs.path_none"));
  const pre = el("pre", { class: "log" }, "");

  void get("/api/logs").then((r) => {
    pathEl.textContent = r.path ? t("logs.path", r.path) : t("logs.path_none");
    pre.textContent = r.text;
  });

  return el(
    "div",
    { class: "screen wide stack" },
    el("h2", null, t("logs.title")),
    pathEl,
    el(
      "button",
      {
        class: "btn",
        onclick: async () => {
          await navigator.clipboard.writeText(pre.textContent);
          window.alert(t("logs.copied"));
        }
      },
      t("copy")
    ),
    pre
  );
}
