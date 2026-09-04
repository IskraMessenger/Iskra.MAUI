import ru from "./i18n/ru.json";
import en from "./i18n/en.json";
import es from "./i18n/es.json";
import de from "./i18n/de.json";
import fr from "./i18n/fr.json";
import zh from "./i18n/zh.json";

export type Lang = "Russian" | "English" | "Spanish" | "German" | "French" | "ChineseSimplified";

const tables: Record<Lang, Record<string, string>> = {
  Russian: ru as Record<string, string>,
  English: en as Record<string, string>,
  Spanish: es as Record<string, string>,
  German: de as Record<string, string>,
  French: fr as Record<string, string>,
  ChineseSimplified: zh as Record<string, string>
};

let current: Lang = "Russian";

export const languages: { id: Lang; name: string }[] = [
  { id: "Russian", name: "Русский" },
  { id: "English", name: "English" },
  { id: "Spanish", name: "Español" },
  { id: "German", name: "Deutsch" },
  { id: "French", name: "Français" },
  { id: "ChineseSimplified", name: "简体中文" }
];

export function setLang(lang: Lang) {
  current = lang;
}

export function getLang() {
  return current;
}

export function t(key: string, ...args: unknown[]) {
  const table = tables[current] ?? tables.Russian;
  let s = table[key] ?? tables.Russian[key] ?? key;
  if (args.length) {
    s = s.replace(/\{(\d+)(?::[^}]*)?\}/g, (_, i) => String(args[Number(i)] ?? ""));
  }
  return s;
}
