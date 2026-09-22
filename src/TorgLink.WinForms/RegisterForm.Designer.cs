namespace TorgLink.WinForms;

partial class RegisterForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel _layout;
    private Label _lblNick;
    private TextBox _nick;
    private Label _lblPass;
    private TextBox _pass;
    private FlowLayoutPanel _buttons;
    private Button _ok;
    private Button _cancel;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _layout = new TableLayoutPanel();
        _lblNick = new Label();
        _nick = new TextBox();
        _lblPass = new Label();
        _pass = new TextBox();
        _buttons = new FlowLayoutPanel();
        _ok = new Button();
        _cancel = new Button();
        _layout.SuspendLayout();
        _buttons.SuspendLayout();
        SuspendLayout();

        _layout.AutoSize = true;
        _layout.ColumnCount = 1;
        _layout.Dock = DockStyle.Fill;
        _layout.Name = "_layout";

        _lblNick.AutoSize = true;
        _lblNick.Name = "_lblNick";
        _lblNick.Text = "Ник";
        _layout.Controls.Add(_lblNick);

        _nick.Name = "_nick";
        _nick.Width = 300;
        _layout.Controls.Add(_nick);

        _lblPass.AutoSize = true;
        _lblPass.Name = "_lblPass";
        _lblPass.Text = "Пароль";
        _layout.Controls.Add(_lblPass);

        _pass.Name = "_pass";
        _pass.UseSystemPasswordChar = true;
        _pass.Width = 300;
        _layout.Controls.Add(_pass);

        _ok.Name = "_ok";
        _ok.Text = "Создать";
        _cancel.DialogResult = DialogResult.Cancel;
        _cancel.Name = "_cancel";
        _cancel.Text = "Отмена";

        _buttons.AutoSize = true;
        _buttons.Name = "_buttons";
        _buttons.Controls.Add(_ok);
        _buttons.Controls.Add(_cancel);
        _layout.Controls.Add(_buttons);

        CancelButton = _cancel;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(_layout);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "RegisterForm";
        Padding = new Padding(16);
        StartPosition = FormStartPosition.CenterParent;
        Text = "TorgLink — регистрация";

        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        _buttons.ResumeLayout(false);
        _buttons.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
