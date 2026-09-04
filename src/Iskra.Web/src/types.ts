export type User = {
  id: number;
  nickname: string;
  networkIdShort: string;
  dataUdpPort: number;
  initials: string;
  avatar: string;
  meshOn: boolean;
  bluetoothOn: boolean;
};

export type ChatRow = {
  id: number;
  nick: string;
  networkId: string;
  initials: string;
  avatar: string;
  preview: string;
  time: string;
  online: boolean;
  delivery: string;
};

export type ChatDetail = {
  id: number;
  nick: string;
  networkId: string;
  initials: string;
  avatar: string;
  online: boolean;
  safety: string;
  keySource: string;
};

export type ChatMessage = {
  id: number;
  outgoing: boolean;
  text: string;
  kind: string;
  mime: string | null;
  fileName: string | null;
  size: number | null;
  time: string;
  delivery: string;
  transferState: string | null;
  hasBlob: boolean;
};

export type ContactRow = {
  chatId: number | null;
  name: string;
  detail: string;
  networkId: string;
  initials: string;
  avatar: string;
  online: boolean;
};

export type NetworkNode = {
  name: string;
  networkId: string;
  initials: string;
  avatar: string;
  online: boolean;
  hops: string;
};

export type ServerRow = {
  id: number;
  baseUrl: string;
  trusted: boolean;
  active: boolean;
  isRegistered: boolean;
  trustRating: number;
  fingerprintSha256: string;
  isLowRating: boolean;
  canAsk: boolean;
};

export type SettingsDto = {
  nick: string;
  networkId: string;
  udpPort: number;
  initials: string;
  avatar: string;
  bluetooth: boolean;
  lan: boolean;
  routing: boolean;
  trafficQuality: string;
  language: string;
  theme: string;
  storageBytes: number;
};

export type RoutingDto = {
  maxSearchHops: number;
  sendFailureSearchAttempts: number;
  delayMs: number;
  timeoutMs: number;
  linkTechnology: string;
  enableUdpTransport: boolean;
  enableBluetoothTransport: boolean;
  suggestBluetoothPairing: boolean;
  advertisePeerSearch: boolean;
  presets: { value: string; label: string }[];
};

export type LanRow = {
  nickname: string;
  networkId: string;
  online: boolean;
  transport: string;
  lastSeen: string;
};

export type BlacklistRow = {
  networkId: string;
  nickname: string;
};
