// i18n: 6 locales loaded from /i18n/*.json at startup.
export const LANG_FILES = {
  Russian: "ru",
  English: "en",
  Spanish: "es",
  German: "de",
  French: "fr",
  ChineseSimplified: "zh"
};

export const languages = [
  { id: "Russian", name: "Русский" },
  { id: "English", name: "English" },
  { id: "Spanish", name: "Español" },
  { id: "German", name: "Deutsch" },
  { id: "French", name: "Français" },
  { id: "ChineseSimplified", name: "简体中文" }
];

const tables = {};
let current = "Russian";

export async function loadLocales() {
  await Promise.all(Object.entries(LANG_FILES).map(async ([lang, file]) => {
    const res = await fetch(`/i18n/${file}.json`);
    tables[lang] = res.ok ? await res.json() : {};
  }));
}

export function setLang(lang) {
  current = lang;
}

export function getLang() {
  return current;
}

export function t(key, ...args) {
  const table = tables[current] ?? tables.Russian;
  let s = table[key] ?? tables.Russian?.[key] ?? key;
  if (args.length) {
    s = s.replace(/\{(\d+)(?::[^}]*)?\}/g, (_, i) => String(args[Number(i)] ?? ""));
  }
  return s;
}
