using System.Globalization;
using Iskra.Maui.Services;

namespace Iskra.Maui.Localization;

internal static class LanguageService
{
    private const string PrefLanguage = "iskra_language";
    private const string PrefChosen = "iskra_language_chosen";

    public static AppLanguage Current { get; private set; } = AppLanguage.Russian;

    public static bool HasChosen => Preferences.Default.Get(PrefChosen, false);

    /// <summary>Only Simplified Chinese shows the translation-inaccuracy disclaimer.</summary>
    public static bool ShowTranslationWarning => Current == AppLanguage.ChineseSimplified;

    public static event EventHandler? Changed;

    public static void Load()
    {
        var raw = Preferences.Default.Get(PrefLanguage, nameof(AppLanguage.Russian));
        if (!Enum.TryParse(raw, out AppLanguage lang))
            lang = AppLanguage.Russian;
        Apply(lang, save: false, markChosen: false);
    }

    public static void Set(AppLanguage language, bool markChosen = true)
    {
        Apply(language, save: true, markChosen);
        AppLog.SettingChanged("Language", language);
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static string NativeName(AppLanguage language) => language switch
    {
        AppLanguage.Russian => "Русский",
        AppLanguage.English => "English",
        AppLanguage.Spanish => "Español",
        AppLanguage.ChineseSimplified => "简体中文",
        _ => language.ToString()
    };

    /// <summary>
    /// Chinese only: bilingual disclaimer (Chinese + English). Empty for other languages.
    /// </summary>
    public static string TranslationWarning(AppLanguage language) => language switch
    {
        AppLanguage.ChineseSimplified =>
            "警告：中文翻译可能不准确。\nWarning: the Chinese translation may be inaccurate.",
        _ => ""
    };

    private static void Apply(AppLanguage language, bool save, bool markChosen)
    {
        Current = language;
        var culture = ToCulture(language);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;

        if (save)
            Preferences.Default.Set(PrefLanguage, language.ToString());
        if (markChosen)
            Preferences.Default.Set(PrefChosen, true);
    }

    private static CultureInfo ToCulture(AppLanguage language) => language switch
    {
        AppLanguage.English => new CultureInfo("en"),
        AppLanguage.Spanish => new CultureInfo("es"),
        AppLanguage.ChineseSimplified => new CultureInfo("zh-Hans"),
        _ => new CultureInfo("ru")
    };
}
