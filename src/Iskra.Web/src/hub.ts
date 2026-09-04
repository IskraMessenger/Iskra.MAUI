import * as signalR from "@microsoft/signalr";

export type HubHandlers = {
  chatsChanged?: () => void;
  messagesChanged?: (chatId: number) => void;
  presenceChanged?: () => void;
  keyChanged?: (p: {
    chatId: number;
    peerNickname: string;
    previousSafetyNumber: string;
    newSafetyNumber: string;
  }) => void;
  trustThreat?: (p: { baseUrl: string; expectedFingerprint: string; actualFingerprint: string }) => void;
  meshFailover?: () => void;
};

export function connectHub(handlers: HubHandlers): signalR.HubConnection {
  const conn = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/iskra")
    .withAutomaticReconnect()
    .build();
  conn.on("chatsChanged", () => handlers.chatsChanged?.());
  conn.on("messagesChanged", (chatId: number) => handlers.messagesChanged?.(chatId));
  conn.on("presenceChanged", () => handlers.presenceChanged?.());
  conn.on("keyChanged", (p) => handlers.keyChanged?.(p));
  conn.on("trustThreat", (p) => handlers.trustThreat?.(p));
  conn.on("meshFailover", () => handlers.meshFailover?.());
  void conn.start();
  return conn;
}
