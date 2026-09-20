import { del, get } from "../api.js";
import { t } from "../session.js";
import { el, clear } from "../dom.js";

export function BlacklistPage() {
  const listEl = el("div");

  async function load() {
    const rows = await get("/api/blacklist");
    clear(listEl);
    if (rows.length === 0) {
      listEl.appendChild(el("div", { class: "empty" }, t("blacklist.empty")));
      return;
    }
    for (const r of rows) {
      listEl.appendChild(
        el(
          "div",
          { class: "item" },
          el(
            "div",
            { class: "meta" },
            el("div", { class: "title" }, r.nickname),
            el("div", { class: "sub" }, r.networkId)
          ),
          el(
            "button",
            {
              class: "btn small",
              onclick: async () => {
                await del(`/api/blacklist/${encodeURIComponent(r.networkId)}`);
                await load();
              }
            },
            t("blacklist.unblock")
          )
        )
      );
    }
  }
  void load();

  return el("div", { class: "screen wide" }, el("h2", null, t("blacklist.title")), listEl);
}
