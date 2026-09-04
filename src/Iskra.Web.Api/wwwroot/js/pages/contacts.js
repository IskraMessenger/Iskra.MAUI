import { get, post } from "../api.js";
import { onTick, t } from "../session.js";
import { nav } from "../router.js";
import { el, clear } from "../dom.js";

export function ContactsPage() {
  let rows = [];
  let q = "";
  let busy = false;

  const listEl = el("div");
  const scanBtn = el("button", { class: "btn small" }, t("contacts.scan"));

  function renderList() {
    clear(listEl);
    const filtered = rows.filter(
      (c) =>
        !q ||
        c.name.toLowerCase().includes(q.toLowerCase()) ||
        c.networkId.toLowerCase().includes(q.toLowerCase())
    );
    if (filtered.length === 0) {
      listEl.appendChild(el("div", { class: "empty" }, t("contacts.empty")));
      return;
    }
    for (const c of filtered) {
      listEl.appendChild(
        el(
          "div",
          {
            class: "item",
            onclick: async () => {
              if (c.chatId) {
                nav(`/app/chats/${c.chatId}`);
                return;
              }
              try {
                const r = await post("/api/chats/open-peer", { networkId: c.networkId });
                nav(`/app/chats/${r.id}`);
              } catch (e) {
                window.alert(e instanceof Error ? e.message : t("network.error"));
              }
            }
          },
          el("div", { class: "avatar", style: { background: c.avatar } }, c.initials),
          el(
            "div",
            { class: "meta" },
            el("div", { class: "title" }, c.name),
            el("div", { class: "sub" }, c.detail)
          ),
          el("span", { class: "dot " + (c.online ? "on" : "off") })
        )
      );
    }
  }

  async function load() {
    try {
      rows = await get("/api/contacts");
    } catch {
      rows = [];
    }
    renderList();
  }
  void load();
  const off = onTick(() => void load());

  scanBtn.addEventListener("click", async () => {
    busy = true;
    scanBtn.disabled = true;
    scanBtn.textContent = t("contacts.scanning");
    try {
      await post("/api/contacts/scan");
      await load();
    } finally {
      busy = false;
      scanBtn.disabled = false;
      scanBtn.textContent = t("contacts.scan");
    }
  });

  const root = el(
    "div",
    null,
    el(
      "div",
      { class: "search row" },
      el("input", {
        placeholder: t("contacts.search"),
        value: q,
        oninput: (e) => {
          q = e.target.value;
          renderList();
        }
      }),
      scanBtn
    ),
    listEl
  );
  return { root, dispose: off };
}
