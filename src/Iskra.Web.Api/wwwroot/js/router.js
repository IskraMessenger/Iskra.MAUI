import { session } from "./session.js";
import { el, clear } from "./dom.js";
import { Shell } from "./shell.js";
import { LanguagePage } from "./pages/language.js";
import { LoginPage } from "./pages/login.js";
import { RegisterPage } from "./pages/register.js";
import { ChatsPage } from "./pages/chats.js";
import { ChatPage } from "./pages/chat.js";
import { ContactsPage } from "./pages/contacts.js";
import { NetworkPage } from "./pages/network.js";
import { SettingsPage } from "./pages/settings.js";
import { AddChatPage } from "./pages/addchat.js";
import { MyQrPage } from "./pages/myqr.js";
import { ServersPage } from "./pages/servers.js";
import { RoutingPage } from "./pages/routing.js";
import { LanScanPage } from "./pages/lanscan.js";
import { BlacklistPage } from "./pages/blacklist.js";
import { LogsPage } from "./pages/logs.js";

const rootEl = document.getElementById("root");
let currentDispose = null;

export function nav(path) {
  if (currentPath() === path) return;
  location.hash = "#" + path;
}

export function navReplace(path) {
  if (currentPath() === path) {
    render();
    return;
  }
  location.replace("#" + path); // fires hashchange -> render()
}

function currentPath() {
  const h = location.hash;
  return h.startsWith("#") ? h.slice(1) : "/";
}

const appRoutes = {
  "/app/chats": () => ChatsPage(),
  "/app/contacts": () => ContactsPage(),
  "/app/network": () => NetworkPage(),
  "/app/settings": () => SettingsPage(),
  "/app/add-chat": () => AddChatPage(),
  "/app/my-qr": () => MyQrPage(),
  "/app/servers": () => ServersPage(),
  "/app/routing": () => RoutingPage(),
  "/app/lan": () => LanScanPage(),
  "/app/blacklist": () => BlacklistPage(),
  "/app/logs": () => LogsPage()
};

function mount(node) {
  if (currentDispose) {
    try { currentDispose(); } catch { /* ignore */ }
    currentDispose = null;
  }
  clear(rootEl);
  rootEl.appendChild(node.root ?? node);
  currentDispose = node.dispose ?? null;
}

export function render() {
  try {
    renderRoute();
  } catch (e) {
    const msg = e instanceof Error ? e.message : String(e);
    mount(el("div", { class: "empty err" }, msg));
  }
}

function renderRoute() {
  const path = currentPath();

  if (!session.ready) {
    mount(el("div", { class: "empty" }, "Iskra…"));
    return;
  }
  if (!session.languageChosen && path !== "/lang") return navReplace("/lang");
  if (session.languageChosen && !session.user && path !== "/login" && path !== "/register" && path !== "/lang")
    return navReplace("/login");
  if (session.user && (path === "/login" || path === "/register" || path === "/lang"))
    return navReplace("/app/chats");

  if (path === "/lang") return mount(LanguagePage());
  if (path === "/login") return mount(LoginPage());
  if (path === "/register") return mount(RegisterPage());

  const chatMatch = path.match(/^\/app\/chats\/(\d+)$/);
  if (chatMatch) return mount(Shell(() => ChatPage(Number(chatMatch[1])), true));

  const factory = appRoutes[path];
  if (factory) return mount(Shell(factory, false));

  // "*" fallback
  return navReplace(session.languageChosen ? (session.user ? "/app/chats" : "/login") : "/lang");
}

window.addEventListener("hashchange", render);
