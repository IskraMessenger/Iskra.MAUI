import { loadLocales } from "./i18n.js";
import { session, refresh, logoutOnClose } from "./session.js";
import { render } from "./router.js";

function withTimeout(promise, ms, label) {
  return Promise.race([
    promise,
    new Promise((_, reject) =>
      setTimeout(() => reject(new Error(label + " timed out")), ms)
    )
  ]);
}

function showBootError(err) {
  const root = document.getElementById("root");
  if (!root) return;
  const msg = err instanceof Error ? err.message : String(err);
  root.replaceChildren();
  const box = document.createElement("div");
  box.className = "empty err";
  box.textContent = msg;
  root.appendChild(box);
}

async function main() {
  try {
    render();
  } catch (e) {
    showBootError(e);
    return;
  }
  try {
    await withTimeout(loadLocales(), 8000, "i18n");
  } catch { /* fall back to key names */ }
  try {
    await withTimeout(refresh(), 8000, "bootstrap");
  } catch {
    session.ready = true;
  }
  try {
    render();
  } catch (e) {
    showBootError(e);
  }
}

// TEMP: disabled automatic logout on tab close - debugging chats list issue
// window.addEventListener("beforeunload", () => {
//   isClosing = true;
// });
// window.addEventListener("pagehide", (e) => {
//   if (isClosing && !e.persisted) logoutOnClose();
// });

void main();
