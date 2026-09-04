import { get, post } from "../api.js";
import { t } from "../session.js";
import { el, clear } from "../dom.js";

export function RoutingPage() {
  let s = null;
  const root = el("div", { class: "screen stack" }, el("div", { class: "empty" }, t("chat.state.loading")));

  function renderPage() {
    clear(root);
    root.appendChild(el("h2", null, t("routing.title")));

    const num = (k, label) => {
      const input = el("input", { type: "number", value: s[k], style: { marginTop: "6px" } });
      input.addEventListener("input", () => {
        s[k] = Number(input.value);
      });
      return el("label", null, label, input);
    };

    root.appendChild(num("maxSearchHops", t("routing.max_depth")));
    root.appendChild(num("sendFailureSearchAttempts", t("routing.attempts")));
    root.appendChild(num("delayMs", t("routing.delay")));
    root.appendChild(num("timeoutMs", t("routing.timeout")));

    const speed = el(
      "select",
      {
        style: { marginTop: "6px" },
        onchange: (e) => {
          s.linkTechnology = e.target.value;
        }
      },
      (s.presets ?? []).map((p) => {
        const o = el("option", { value: p.value }, p.label);
        if (p.value === s.linkTechnology) o.selected = true;
        return o;
      })
    );
    root.appendChild(el("label", null, t("routing.speed"), speed));

    const sw = (label, key) =>
      el(
        "div",
        { class: "switch" },
        el("span", null, label),
        el("input", {
          type: "checkbox",
          checked: s[key],
          onchange: (e) => {
            s[key] = e.target.checked;
          }
        })
      );
    root.appendChild(sw(t("routing.udp"), "enableUdpTransport"));
    root.appendChild(sw(t("routing.bt"), "enableBluetoothTransport"));
    root.appendChild(sw(t("routing.bt_pair"), "suggestBluetoothPairing"));
    root.appendChild(sw(t("routing.share_routes"), "advertisePeerSearch"));

    const errEl = el("div", { class: "err", hidden: true });
    root.appendChild(errEl);

    root.appendChild(
      el(
        "button",
        {
          class: "btn",
          onclick: async () => {
            errEl.hidden = true;
            try {
              await post("/api/routing", {
                maxSearchHops: s.maxSearchHops,
                sendFailureSearchAttempts: s.sendFailureSearchAttempts,
                delayMs: s.delayMs,
                timeoutMs: s.timeoutMs,
                linkTechnology: s.linkTechnology,
                enableUdpTransport: s.enableUdpTransport,
                enableBluetoothTransport: s.enableBluetoothTransport,
                suggestBluetoothPairing: s.suggestBluetoothPairing,
                advertisePeerSearch: s.advertisePeerSearch
              });
            } catch (e) {
              const msg = e instanceof Error ? e.message : "error";
              errEl.hidden = false;
              errEl.textContent = msg.includes(".") ? t(msg) : msg;
            }
          }
        },
        t("save")
      )
    );
  }

  void get("/api/routing").then((r) => {
    s = r;
    renderPage();
  });

  return root;
}
