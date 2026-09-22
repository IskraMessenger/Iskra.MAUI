using Microsoft.Extensions.Logging;
using ShortP2P.Auth;

namespace TorgLink.WinForms;

public sealed partial class RegisterForm : AppForm
{
    private readonly AuthService _auth = null!;
    private readonly ILogger<RegisterForm> _logger = null!;

    public RegisterForm()
    {
        InitializeComponent();
    }

    public RegisterForm(AuthService auth, ILogger<RegisterForm> logger)
        : this()
    {
        _auth = auth;
        _logger = logger;
        _ok.Click += async (_, _) => await OnRegisterAsync().ConfigureAwait(true);
    }

    private async Task OnRegisterAsync()
    {
        var (ok, err) = await _auth.RegisterAsync(_nick.Text.Trim(), _pass.Text ?? "").ConfigureAwait(true);
        if (!ok)
        {
            _logger.LogWarning("Register failed: {Reason}", err);
            MessageBox.Show(this, err ?? "Регистрация не удалась.", "Регистрация", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var id = _auth.CurrentUser?.NetworkIdShort ?? "";
        MessageBox.Show(this, "Network id:\n" + id, "Аккаунт создан", MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
