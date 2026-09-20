import { post } from "../api.js";
import { refresh, t } from "../session.js";
import { nav } from "../router.js";
import { el, clear } from "../dom.js";

export function LoginPage() {
  let busy = false;
  const errEl = el("div", { class: "err", hidden: true });
  const btn = el("button", { class: "btn" }, t("login.sign_in"));
  const nick = el("input", { placeholder: t("login.nick"), autocomplete: "username" });
  const password = el("input", {
    placeholder: t("login.password"),
    type: "password",
    autocomplete: "current-password"
  });

  function setErr(msg) {
    if (!msg) {
      errEl.hidden = true;
      return;
    }
    errEl.hidden = false;
    errEl.textContent = msg.startsWith("login.") || msg.startsWith("pass.") ? t(msg) : msg;
  }

  btn.addEventListener("click", async () => {
    busy = true;
    btn.disabled = true;
    setErr("");
    try {
      await post("/api/auth/login", { nickname: nick.value, password: password.value });
      await refresh();
      nav("/app/chats");
    } catch (e) {
      setErr(e instanceof Error ? e.message : t("login.failed"));
    } finally {
      busy = false;
      btn.disabled = false;
    }
  });

  return el(
    "div",
    { class: "screen stack" },
    el(
      "div",
      { class: "brand" },
      el("img", { src: "/assets/logo.png", alt: "" }),
      el("h1", null, "TorgLink"),
      el("p", { class: "muted" }, t("login.subtitle"))
    ),
    nick,
    password,
    errEl,
    btn,
    el("a", { class: "btn ghost", href: "#/register" }, t("login.create_account"))
  );
}
