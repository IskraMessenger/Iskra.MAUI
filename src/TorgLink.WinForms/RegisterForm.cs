using Microsoft.Extensions.Logging;
using ShortP2P.Auth;

namespace TorgLink.WinForms;

public sealed class RegisterForm : AppForm
{
    private readonly AuthService _auth;
    private readonly ILogger<RegisterForm> _logger;
    private readonly TextBox _nick = new() { Width = 300 };
    private readonly TextBox _pass = new() { Width = 300, UseSystemPasswordChar = true };

    public RegisterForm(AuthService auth, ILogger<RegisterForm> logger)
    {
        _auth = auth;
        _logger = logger;
        Text = "TorgLink — регистрация";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16);

        var ok = new Button { Text = "Создать" };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel };
        ok.Click += async (_, _) => await OnRegisterAsync().ConfigureAwait(true);
        CancelButton = cancel;

        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
        layout.Controls.Add(new Label { Text = "Ник", AutoSize = true });
        layout.Controls.Add(_nick);
        layout.Controls.Add(new Label { Text = "Пароль", AutoSize = true });
        layout.Controls.Add(_pass);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        layout.Controls.Add(buttons);
        Controls.Add(layout);
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
