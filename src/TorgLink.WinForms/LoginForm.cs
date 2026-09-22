using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;

namespace TorgLink.WinForms;

public sealed partial class LoginForm : AppForm
{
    private readonly AuthService _auth = null!;
    private readonly IServiceProvider _services = null!;
    private readonly ILogger<LoginForm> _logger = null!;

    public LoginForm()
    {
        InitializeComponent();
    }

    public LoginForm(AuthService auth, IServiceProvider services, ILogger<LoginForm> logger)
        : this()
    {
        _auth = auth;
        _services = services;
        _logger = logger;
        _login.Click += async (_, _) => await OnLoginAsync().ConfigureAwait(true);
        _register.Click += OnRegister;
        Load += async (_, _) =>
        {
            if (await _auth.TryRestoreSessionAsync().ConfigureAwait(true) && _auth.CurrentUser != null)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        };
    }

    private async Task OnLoginAsync()
    {
        var (ok, err) = await _auth.LoginAsync(_nick.Text.Trim(), _pass.Text ?? "").ConfigureAwait(true);
        if (!ok)
        {
            _logger.LogWarning("Login failed: {Reason}", err);
            MessageBox.Show(this, err ?? "Не удалось войти.", "Вход", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void OnRegister(object? sender, EventArgs e)
    {
        using var form = _services.GetRequiredService<RegisterForm>();
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
