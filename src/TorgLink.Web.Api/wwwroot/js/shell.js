import { session, onTick, t } from "./session.js";
import { el, clear } from "./dom.js";
import { nav } from "./router.js";

const tabs = [
  { to: "/app/chats", key: "tab.chats", icon: "/assets/tab_chats.svg" },
  { to: "/app/contacts", key: "tab.contacts", icon: "/assets/tab_contacts.svg" },
  { to: "/app/network", key: "tab.network", icon: "/assets/tab_network.svg" },
  { to: "/app/settings", key: "tab.settings", icon: "/assets/tab_settings.svg" }
];

// Shell wraps an /app/* page with header + tab bar. hideTabs = chat detail view.
export function Shell(pageFactory, hideTabs) {
  const page = pageFactory();
  const pageEl = page.root ?? page;

  const headerLeft = el("div");
  const portEl = el("div", { class: "muted", style: { fontSize: "12px" } });
  const content = el("div", { class: "content" }, pageEl);

  const tabEls = tabs.map((tab) => {
    const a = el(
      "a",
      { class: "tab", href: "#" + tab.to, style: { textDecoration: "none" } },
      el("img", { src: tab.icon, alt: "" }),
      el("span", {}, t(tab.key))
    );
    const cur = location.hash.startsWith("#") ? location.hash.slice(1) : "/";
    if (cur === tab.to || (tab.to === "/app/chats" && cur.startsWith("/app/chats"))) a.classList.add("active");
    return a;
  });

  function renderHeaderLeft() {
    clear(headerLeft);
    if (hideTabs) {
      headerLeft.appendChild(
        el("button", { class: "btn ghost small", onclick: () => nav("/app/chats") }, "←")
      );
    } else {
      headerLeft.appendChild(
        el(
          "div",
          { class: "row" },
          el(
            "div",
            {
              class: "avatar",
              style: { width: "32px", height: "32px", fontSize: "12px", background: session.user?.avatar ?? "" }
            },
            session.user?.initials ?? ""
          ),
          el(
            "div",
            null,
            el("div", null, session.user?.nickname ?? ""),
            el(
              "div",
              { class: "muted", style: { fontSize: "12px" } },
              session.user?.meshOn ? t("header.mesh_on") : t("header.mesh_off")
            )
          )
        )
      );
    }
    portEl.textContent = t("header.port", session.user?.dataUdpPort ?? "");
    if (!hideTabs) {
      tabEls.forEach((a, i) => {
        a.lastChild.textContent = t(tabs[i].key);
      });
    }
  }
  renderHeaderLeft();

  const header = el("header", { class: "header" }, headerLeft, portEl);
  const children = [header, content];
  if (!hideTabs) children.push(el("nav", { class: "tabs" }, tabEls));
  const root = el("div", { class: "app" }, children);

  const off = onTick(renderHeaderLeft);
  return {
    root,
    dispose: () => {
      off();
      page.dispose?.();
    }
  };
}
