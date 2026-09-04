import { loadLocales } from "./i18n.js";
import { session, refresh, logoutOnClose } from "./session.js";
import { render } from "./router.js";

async function main() {
  try {
    await loadLocales();
  } catch { /* fall back to key names */ }
  try {
    await refresh();
  } catch {
    session.ready = true;
  }
  render();
}

// TEMP: disabled automatic logout on tab close - debugging chats list issue
// window.addEventListener("beforeunload", () => {
//   isClosing = true;
// });
// window.addEventListener("pagehide", (e) => {
//   if (isClosing && !e.persisted) logoutOnClose();
// });

void main();
