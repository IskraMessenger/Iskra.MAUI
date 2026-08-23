using Microsoft.Extensions.Logging;
using ShortP2P.Auth;


namespace Iskra.Maui;

public partial class RegisterPage : ContentPage
{
    private const string RequirementsHint =
        "Не менее 8 символов, латиница, минимум одна заглавная буква и цифра. Можно спецсимволы: " +
        UserPasswordPolicy.AllowedSpecialCharacters;

    private readonly AuthService _auth;
    private readonly ILogger<RegisterPage> _logger;

    public RegisterPage(AuthService auth, ILogger<RegisterPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _logger = logger;
        PasswordHintLabel.Text = RequirementsHint;
    }

    private void OnPasswordTextChanged(object? sender, TextChangedEventArgs e)
    {
        var pass = e.NewTextValue ?? "";
        if (string.IsNullOrEmpty(pass))
        {
            SetPasswordHint(RequirementsHint, muted: true);
            return;
        }

        if (UserPasswordPolicy.TryValidate(pass, out var error))
        {
            SetPasswordHint("Пароль соответствует требованиям.", muted: true, ok: true);
            return;
        }

        SetPasswordHint(DescribePasswordError(error!.Value), muted: false);
    }

    private async void OnRegisterClicked(object? sender, EventArgs e)
    {
        var nick = NicknameEntry.Text?.Trim() ?? "";
        var pass = PasswordEntry.Text ?? "";

        if (!UserPasswordPolicy.TryValidate(pass, out var policyError))
        {
            var reason = DescribePasswordError(policyError!.Value);
            _logger.LogWarning("Registration failed for {Nickname}: {Reason}", nick, reason);
            await DisplayAlert("Регистрация", reason, "OK").ConfigureAwait(true);
            return;
        }

        var (ok, err) = await _auth.RegisterAsync(nick, pass).ConfigureAwait(true);
        if (!ok)
        {
            _logger.LogWarning("Registration failed for {Nickname}: {Reason}", nick, err);
            await DisplayAlert("Регистрация", LocalizeRegisterError(err), "OK").ConfigureAwait(true);
            return;
        }

        var id = _auth.CurrentUser?.NetworkIdShort ?? "";
        await DisplayAlert("Аккаунт создан", $"Сетевой идентификатор:\n{id}", "OK").ConfigureAwait(true);

        Application.Current!.MainPage = MauiProgram.Services.GetRequiredService<AppShell>();
    }

    private void SetPasswordHint(string text, bool muted, bool ok = false)
    {
        PasswordHintLabel.Text = text;
        var resources = Application.Current?.Resources;
        if (ok && resources?["OnlineGreen"] is Color okColor)
            PasswordHintLabel.TextColor = okColor;
        else if (!muted && resources?["Danger"] is Color danger)
            PasswordHintLabel.TextColor = danger;
        else if (resources?["MutedText"] is Color mutedColor)
            PasswordHintLabel.TextColor = mutedColor;
    }

    private static string DescribePasswordError(UserPasswordPolicyError error) => error switch
    {
        UserPasswordPolicyError.Empty => "Введите пароль.",
        UserPasswordPolicyError.TooShort => "Пароль должен быть не короче 8 символов.",
        UserPasswordPolicyError.InvalidCharacter =>
            $"Только латиница, цифры и спецсимволы: {UserPasswordPolicy.AllowedSpecialCharacters}",
        UserPasswordPolicyError.MissingUppercase => "Добавьте хотя бы одну заглавную латинскую букву.",
        UserPasswordPolicyError.MissingLetter => "Добавьте хотя бы одну латинскую букву.",
        UserPasswordPolicyError.MissingDigit => "Добавьте хотя бы одну цифру.",
        _ => "Пароль не соответствует требованиям."
    };

    private static string LocalizeRegisterError(string? err) => err switch
    {
        "Nickname and password are required." => "Укажите ник и пароль.",
        "This nickname is already registered." => "Этот ник уже зарегистрирован.",
        _ => err ?? "Не удалось зарегистрироваться."
    };
}