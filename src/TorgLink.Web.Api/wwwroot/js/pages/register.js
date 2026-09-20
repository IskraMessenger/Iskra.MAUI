import { post } from "../api.js";
import { passwordErrorKey } from "../password.js";
import { refresh, t } from "../session.js";
import { nav } from "../router.js";
import { el } from "../dom.js";

const SPECIALS = "~!@#$%^&*()\\|/,.<>";

export function RegisterPage() {
  let busy = false;
  const errEl = el("div", { class: "err", hidden: true });
  const hintEl = el("p", { class: "muted" });
  const nick = el("input", { placeholder: t("login.nick") });
  const password = el("input", { placeholder: t("login.password"), type: "password" });
  const btn = el("button", { class: "btn" }, t("register.button"));

  function updateHint() {
    const hint = passwordErrorKey(password.value);
    hintEl.textContent = hint ? t(hint, SPECIALS) : t("register.pass_ok");
  }
  updateHint();
  password.addEventListener("input", updateHint);

  function setErr(msg) {
    if (!msg) {
      errEl.hidden = true;
      return;
    }
    errEl.hidden = false;
    errEl.textContent = msg.includes(".") ? t(msg, SPECIALS) : msg;
  }

  btn.addEventListener("click", async () => {
    if (!nick.value.trim() || !password.value) {
      setErr("register.need_nick_pass");
      return;
    }
    const pe = passwordErrorKey(password.value);
    if (pe) {
      setErr(pe);
      return;
    }
    busy = true;
    btn.disabled = true;
    setErr("");
    try {
      const u = await post("/api/auth/register", { nickname: nick.value, password: password.value });
      await refresh();
      window.alert(t("register.network_id", u.networkIdShort));
      nav("/app/chats");
    } catch (e) {
      setErr(e instanceof Error ? e.message : t("register.failed"));
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
      el("img", { src: "/assets/logo_wordmark_256.png", alt: "TorgLink" }),
      el("h1", null, t("register.title")),
      el("p", { class: "muted" }, t("register.subtitle"))
    ),
    nick,
    password,
    hintEl,
    el("p", { class: "muted" }, t("register.pass_hint", SPECIALS)),
    errEl,
    btn,
    el("a", { class: "btn ghost", href: "#/login" }, t("login.sign_in"))
  );
}
