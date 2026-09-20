using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;

namespace TorgLink.WinForms;

public sealed class LoginForm : AppForm
{
    private readonly AuthService _auth;
    private readonly IServiceProvider _services;
    private readonly ILogger<LoginForm> _logger;
    private readonly TextBox _nick = new() { Width = 320 };
    private readonly TextBox _pass = new() { Width = 320, UseSystemPasswordChar = true };

    public LoginForm(AuthService auth, IServiceProvider services, ILogger<LoginForm> logger)
    {
        _auth = auth;
        _services = services;
        _logger = logger;
        Text = "TorgLink — вход";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var login = new Button { Text = "Войти" };
        var register = new Button { Text = "Регистрация" };
        var exit = new Button { Text = "Выход", DialogResult = DialogResult.Cancel };
        login.Click += async (_, _) => await OnLoginAsync().ConfigureAwait(true);
        register.Click += OnRegister;
        AcceptButton = login;
        CancelButton = exit;

        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
        layout.Controls.Add(new Label { Text = "Ник", AutoSize = true });
        layout.Controls.Add(_nick);
        layout.Controls.Add(new Label { Text = "Пароль", AutoSize = true });
        layout.Controls.Add(_pass);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.Add(login);
        buttons.Controls.Add(register);
        buttons.Controls.Add(exit);
        layout.Controls.Add(buttons);
        Controls.Add(layout);

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
