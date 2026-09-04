import { del, get, post, postFile } from "../api.js";
import { t } from "../session.js";
import { el, clear } from "../dom.js";

export function ServersPage() {
  let items = [];
  let max = 32;

  const url = el("input", { placeholder: t("servers.base_url") });
  const statusEl = el("div", { class: "muted", hidden: true });
  const countEl = el("p", { class: "muted" });
  const listEl = el("div", { class: "stack" });

  function setStatus(msg) {
    if (!msg) {
      statusEl.hidden = true;
      return;
    }
    statusEl.hidden = false;
    statusEl.textContent = msg;
  }

  function renderList() {
    countEl.textContent = t("servers.count", items.length, max);
    clear(listEl);
    for (const s of items) {
      const card = el(
        "div",
        { class: "card stack" },
        el("strong", null, s.baseUrl),
        el(
          "div",
          { class: "muted" },
          t(
            "servers.meta",
            s.trustRating,
            s.trusted ? t("servers.meta_trusted") : t("servers.meta_untrusted"),
            s.active ? t("servers.meta_active") : t("servers.meta_off"),
            s.isRegistered ? t("servers.meta_registered") : t("servers.meta_not_registered"),
            s.fingerprintSha256?.slice(0, 12) ?? ""
          )
        )
      );
      const buttons = [
        el(
          "button",
          {
            class: "btn small",
            onclick: async () => {
              try {
                await post(`/api/servers/${s.id}/active`, { active: !s.active });
                await load();
              } catch (e) {
                window.alert(e instanceof Error ? e.message : t("servers.untrusted_body"));
              }
            }
          },
          s.active ? t("servers.meta_off") : t("servers.meta_active")
        ),
        el(
          "button",
          {
            class: "btn small",
            onclick: async () => {
              setStatus(t("servers.checking", s.baseUrl));
              const r = await post(`/api/servers/${s.id}/recheck`);
              if (r.status === "Ok") setStatus(t("servers.recheck_ok"));
              else if (r.status === "Unreachable")
                setStatus(t("servers.recheck_unreachable_detail", r.errorMessage ?? ""));
              else setStatus(t("servers.recheck_fp", r.expectedFingerprint, r.actualFingerprint));
              await load();
            }
          },
          t("servers.check")
        )
      ];
      if (s.canAsk) {
        buttons.push(
          el(
            "button",
            {
              class: "btn small",
              onclick: async () => {
                setStatus(t("servers.asking", s.baseUrl));
                const r = await post(`/api/servers/${s.id}/ask`);
                setStatus(t("servers.ask_result", r.received, r.updated, r.added, s.baseUrl));
                await load();
              }
            },
            t("servers.ask")
          )
        );
      }
      buttons.push(
        el(
          "button",
          {
            class: "btn small",
            onclick: async () => {
              const r = await get(`/api/servers/${s.id}/qr`);
              const w = window.open();
              w?.document.write(`<img src="data:image/png;base64,${r.png}" />`);
            }
          },
          t("servers.share")
        ),
        el(
          "button",
          {
            class: "btn small danger",
            onclick: async () => {
              if (!window.confirm(t("servers.delete_body", s.baseUrl))) return;
              await del(`/api/servers/${s.id}`);
              await load();
            }
          },
          t("servers.delete")
        )
      );
      card.appendChild(el("div", { class: "row", style: { flexWrap: "wrap" } }, buttons));
      listEl.appendChild(card);
    }
  }

  async function load() {
    const r = await get("/api/servers");
    items = r.items;
    max = r.max;
    renderList();
  }
  void load();

  const importInput = el("input", {
    type: "file",
    accept: "image/*",
    hidden: true,
    onchange: async () => {
      const f = importInput.files?.[0];
      importInput.value = "";
      if (!f) return;
      setStatus(t("servers.importing"));
      try {
        const r = await postFile("/api/servers/import-qr", f);
        setStatus(r.already ? t("servers.already_status", r.baseUrl) : t("servers.imported", r.baseUrl));
        await load();
      } catch (ex) {
        setStatus(ex instanceof Error ? ex.message : t("servers.import_read_fail"));
      }
    }
  });

  return el(
    "div",
    { class: "screen wide stack" },
    el("h2", null, t("servers.title")),
    el("p", { class: "muted" }, t("servers.intro")),
    countEl,
    url,
    el(
      "div",
      { class: "row" },
      el(
        "button",
        {
          class: "btn",
          onclick: async () => {
            setStatus(t("servers.connecting"));
            try {
              const r = await post("/api/servers", { baseUrl: url.value });
              url.value = "";
              setStatus(t("servers.added", r.baseUrl));
              await load();
            } catch (e) {
              setStatus(e instanceof Error ? e.message : t("error"));
            }
          }
        },
        t("servers.add")
      ),
      el("label", { class: "btn ghost" }, t("servers.import"), importInput)
    ),
    statusEl,
    listEl
  );
}
