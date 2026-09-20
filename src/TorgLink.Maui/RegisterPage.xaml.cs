using TorgLink.Maui.Localization;
using TorgLink.Maui.Services;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;

namespace TorgLink.Maui;

public partial class RegisterPage : ContentPage
{
    private readonly AuthService _auth;
    private readonly ILogger<RegisterPage> _logger;

    public RegisterPage(AuthService auth, ILogger<RegisterPage> logger)
    {
        InitializeComponent();
        _auth = auth;
        _logger = logger;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedUi();
        SetPasswordHint(RequirementsHint(), muted: true);
    }

    private static string RequirementsHint() =>
        Loc.Tf("register.pass_hint", UserPasswordPolicy.AllowedSpecialCharacters);

    private void ApplyLocalizedUi()
    {
        Title = Loc.T("register.header");
        TitleLabel.Text = Loc.T("register.title");
        SubtitleLabel.Text = Loc.T("register.subtitle");
        NicknameEntry.Placeholder = Loc.T("login.nick");
        PasswordEntry.Placeholder = Loc.T("login.password");
        RegisterButton.Text = Loc.T("register.button");
    }

    private void OnPasswordTextChanged(object? sender, TextChangedEventArgs e)
    {
        var pass = e.NewTextValue ?? "";
        if (string.IsNullOrEmpty(pass))
        {
            SetPasswordHint(RequirementsHint(), muted: true);
            return;
        }

        if (UserPasswordPolicy.TryValidate(pass, out var error))
        {
            SetPasswordHint(Loc.T("register.pass_ok"), muted: true, ok: true);
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
            await DisplayAlert(Loc.T("register.header"), reason, Loc.T("ok")).ConfigureAwait(true);
            return;
        }

        var (ok, err) = await _auth.RegisterAsync(nick, pass).ConfigureAwait(true);
        if (!ok)
        {
            _logger.LogWarning("Registration failed for {Nickname}: {Reason}", nick, err);
            await DisplayAlert(Loc.T("register.header"), LocalizeRegisterError(err), Loc.T("ok"))
                .ConfigureAwait(true);
            return;
        }

        var id = _auth.CurrentUser?.NetworkIdShort ?? "";
        AppLog.Ui.LogInformation("Registration success for {Nickname} id={Id}", nick, id);
        await DisplayAlert(Loc.T("register.created"), Loc.Tf("register.network_id", id), Loc.T("ok"))
            .ConfigureAwait(true);

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
        UserPasswordPolicyError.Empty => Loc.T("pass.empty"),
        UserPasswordPolicyError.TooShort => Loc.T("pass.too_short"),
        UserPasswordPolicyError.InvalidCharacter =>
            Loc.Tf("pass.invalid_char", UserPasswordPolicy.AllowedSpecialCharacters),
        UserPasswordPolicyError.MissingUppercase => Loc.T("pass.need_upper"),
        UserPasswordPolicyError.MissingLetter => Loc.T("pass.need_letter"),
        UserPasswordPolicyError.MissingDigit => Loc.T("pass.need_digit"),
        _ => Loc.T("pass.invalid")
    };

    private static string LocalizeRegisterError(string? err) => err switch
    {
        "Nickname and password are required." => Loc.T("register.need_nick_pass"),
        "This nickname is already registered." => Loc.T("register.nick_taken"),
        _ => err ?? Loc.T("register.failed")
    };
}
