import { get } from "../api.js";
import { t } from "../session.js";
import { el, clear } from "../dom.js";

export function MyQrPage() {
  const imgWrap = el("div", null, el("p", null, t("qr.not_ready")));

  void get("/api/network/qr").then((r) => {
    if (r.png) {
      clear(imgWrap);
      imgWrap.appendChild(el("img", { class: "qr", src: `data:image/png;base64,${r.png}`, alt: "QR" }));
    }
  });

  return el(
    "div",
    { class: "screen" },
    el("h2", null, t("myqr.title")),
    el("p", { class: "muted" }, t("myqr.hint")),
    imgWrap
  );
}
