// SignalR hub client (uses the vendored global `signalR` from /lib/signalr.min.js).
export function connectHub(handlers) {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/iskra")
    .withAutomaticReconnect()
    .build();
  conn.on("chatsChanged", () => handlers.chatsChanged?.());
  conn.on("messagesChanged", (chatId) => handlers.messagesChanged?.(chatId));
  conn.on("incomingMessage", (p) => handlers.incomingMessage?.(p));
  conn.on("chatCreated", (p) => handlers.chatCreated?.(p));
  conn.on("presenceChanged", () => handlers.presenceChanged?.());
  conn.on("keyChanged", (p) => handlers.keyChanged?.(p));
  conn.on("trustThreat", (p) => handlers.trustThreat?.(p));
  conn.on("meshFailover", () => handlers.meshFailover?.());
  conn.start().catch(() => { /* reconnect handled by withAutomaticReconnect */ });
  return conn;
}