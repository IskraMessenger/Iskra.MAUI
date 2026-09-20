import { post, postFile } from "../api.js";
import { t } from "../session.js";
import { nav } from "../router.js";
import { el } from "../dom.js";

export function AddChatPage() {
  const nick = el("input", { placeholder: t("addchat.ph_nick") });
  const networkId = el("input", { placeholder: t("addchat.ph_id") });
  const publicKey = el("textarea", { placeholder: t("addchat.ph_key"), rows: "5" });
  const host = el("input", { placeholder: t("addchat.host") });
  const port = el("input", { placeholder: t("addchat.port"), value: "17501" });
  const errEl = el("div", { class: "err", hidden: true });

  function setErr(msg) {
    if (!msg) {
      errEl.hidden = true;
      return;
    }
    errEl.hidden = false;
    errEl.textContent = msg.includes(".") ? t(msg) : msg;
  }

  const fileInput = el("input", {
    type: "file",
    accept: "image/*",
    hidden: true,
    onchange: async () => {
      const f = fileInput.files?.[0];
      fileInput.value = "";
      if (!f) return;
      try {
        const r = await postFile("/api/chats/qr", f);
        nick.value = r.nick;
        networkId.value = r.networkId;
        publicKey.value = r.publicKey;
        host.value = r.host;
        port.value = String(r.port);
        nav(`/app/chats/${r.id}`);
      } catch (ex) {
        setErr(ex instanceof Error ? ex.message : "addchat.qr_fail");
      }
    }
  });

  return el(
    "div",
    { class: "screen stack" },
    el("h2", null, t("addchat.title")),
    nick,
    networkId,
    publicKey,
    host,
    port,
    el("label", { class: "btn ghost" }, t("addchat.scan_img"), fileInput),
    errEl,
    el(
      "button",
      {
        class: "btn",
        onclick: async () => {
          setErr("");
          try {
            const r = await post("/api/chats/add", {
              nick: nick.value,
              networkId: networkId.value,
              publicKey: publicKey.value,
              host: host.value,
              port: Number(port.value)
            });
            nav(`/app/chats/${r.id}`);
          } catch (e) {
            setErr(e instanceof Error ? e.message : "error");
          }
        }
      },
      t("ok")
    )
  );
}
