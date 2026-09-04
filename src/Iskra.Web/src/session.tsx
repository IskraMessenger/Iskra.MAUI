import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from "react";
import { get, post } from "./api";
import { connectHub } from "./hub";
import { getLang, setLang, type Lang, t } from "./i18n";
import { applyTheme, type ThemeId } from "./theme";
import type { User } from "./types";

type Session = {
  ready: boolean;
  languageChosen: boolean;
  lang: Lang;
  theme: ThemeId;
  user: User | null;
  tick: number;
  setLanguage: (lang: Lang) => Promise<void>;
  setThemeId: (theme: ThemeId) => Promise<void>;
  refresh: () => Promise<void>;
  logout: () => Promise<void>;
};

const Ctx = createContext<Session | null>(null);

export function useSession() {
  const s = useContext(Ctx);
  if (!s) throw new Error("session");
  return s;
}

export function SessionProvider({ children }: { children: ReactNode }) {
  const [ready, setReady] = useState(false);
  const [languageChosen, setChosen] = useState(false);
  const [lang, setLangState] = useState<Lang>("Russian");
  const [theme, setThemeState] = useState<ThemeId>("DarkFlame");
  const [user, setUser] = useState<User | null>(null);
  const [tick, setTick] = useState(0);
  const bump = () => setTick((n) => n + 1);

  const refresh = useCallback(async () => {
    const b = await get<{
      language: Lang;
      languageChosen: boolean;
      theme: ThemeId;
      user: User | null;
    }>("/api/bootstrap");
    setLang(b.language);
    applyTheme(b.theme);
    setLangState(b.language);
    setThemeState(b.theme);
    setChosen(b.languageChosen);
    setUser(b.user);
    setReady(true);
    bump();
  }, []);

  useEffect(() => {
    void refresh();
  }, [refresh]);

  useEffect(() => {
    if (!user) return;
    const conn = connectHub({
      chatsChanged: bump,
      messagesChanged: bump,
      presenceChanged: bump,
      keyChanged: (p) =>
        window.alert(t("safety.key_change_body", p.peerNickname, p.previousSafetyNumber, p.newSafetyNumber)),
      trustThreat: (p) =>
        window.alert(t("security.threat_body", p.baseUrl, p.expectedFingerprint, p.actualFingerprint)),
      meshFailover: () => window.alert(t("safety.mesh_body"))
    });
    return () => {
      void conn.stop();
    };
  }, [user?.id]);

  const value = useMemo<Session>(
    () => ({
      ready,
      languageChosen,
      lang,
      theme,
      user,
      tick,
      setLanguage: async (next) => {
        await post("/api/prefs/language", { language: next });
        setLang(next);
        setLangState(next);
        setChosen(true);
        bump();
      },
      setThemeId: async (next) => {
        await post("/api/prefs/theme", { theme: next });
        applyTheme(next);
        setThemeState(next);
        bump();
      },
      refresh,
      logout: async () => {
        await post("/api/auth/logout");
        setUser(null);
        bump();
      }
    }),
    [ready, languageChosen, lang, theme, user, tick, refresh]
  );

  return <Ctx.Provider value={value}>{children}</Ctx.Provider>;
}

export function useT() {
  const { tick } = useSession();
  return useMemo(() => {
    void tick;
    return t;
  }, [tick]);
}

export { getLang };
