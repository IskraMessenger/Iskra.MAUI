export const themes = [
  { id: "DarkFlame", accent: "#FF6A00" },
  { id: "Night", accent: "#6B8AFF" },
  { id: "LightFlame", accent: "#E39B2B" },
  { id: "ColdBlue", accent: "#00B0FF" },
  { id: "Forest", accent: "#00C853" },
  { id: "Mono", accent: "#E6E6E6" }
];

const vars = {
  DarkFlame: {
    accent: "#FF6A00", "accent-dark": "#E55E00", page: "#121212", surface: "#1E1E20",
    field: "#2A2A2C", text: "#FFFFFF", muted: "#8E8E93", hairline: "#2C2C2E",
    in: "#2A2A2C", out: "#FF6A00", sent: "#FFFFFF", check: "#5AC8FA",
    btn: "#FFFFFF", online: "#34C759", offline: "#636366", danger: "#FF453A"
  },
  Night: {
    accent: "#6B8AFF", "accent-dark": "#5470E0", page: "#000000", surface: "#0E0E12",
    field: "#1A1A22", text: "#E8EAF0", muted: "#6E7380", hairline: "#1C1C24",
    in: "#16161C", out: "#6B8AFF", sent: "#0A0A0E", check: "#9EB0FF",
    btn: "#0A0A0E", online: "#34C759", offline: "#4A4A52", danger: "#FF453A"
  },
  LightFlame: {
    accent: "#E39B2B", "accent-dark": "#C4841F", page: "#FFFFFF", surface: "#F7F7F8",
    field: "#F4F4F6", text: "#1C1C1E", muted: "#8E8E93", hairline: "#E8E8ED",
    in: "#F2F2F2", out: "#FFFFFF", sent: "#2B7DE9", check: "#2B7DE9",
    btn: "#FFFFFF", online: "#34C759", offline: "#C7C7CC", danger: "#D94C4C"
  },
  ColdBlue: {
    accent: "#00B0FF", "accent-dark": "#0091EA", page: "#0B1220", surface: "#151C2C",
    field: "#1C2740", text: "#F2F7FF", muted: "#8AA0B8", hairline: "#243044",
    in: "#1C2740", out: "#00B0FF", sent: "#0B1220", check: "#7FDBFF",
    btn: "#0B1220", online: "#34C759", offline: "#5B6B7A", danger: "#FF6B6B"
  },
  Forest: {
    accent: "#00C853", "accent-dark": "#00A844", page: "#0E1510", surface: "#18231B",
    field: "#223328", text: "#F3FFF6", muted: "#8AA894", hairline: "#2A3A30",
    in: "#223328", out: "#00C853", sent: "#0E1510", check: "#69F0AE",
    btn: "#0E1510", online: "#69F0AE", offline: "#5B6B60", danger: "#FF5252"
  },
  Mono: {
    accent: "#E6E6E6", "accent-dark": "#C8C8C8", page: "#111111", surface: "#1A1A1A",
    field: "#2A2A2A", text: "#F5F5F5", muted: "#9A9A9A", hairline: "#2E2E2E",
    in: "#2A2A2A", out: "#3A3A3A", sent: "#F5F5F5", check: "#F5F5F5",
    btn: "#111111", online: "#34C759", offline: "#666666", danger: "#FF5252"
  }
};

export function applyTheme(id) {
  const v = vars[id] ?? vars.DarkFlame;
  const root = document.documentElement;
  for (const [k, val] of Object.entries(v)) root.style.setProperty(`--${k}`, val);
}
