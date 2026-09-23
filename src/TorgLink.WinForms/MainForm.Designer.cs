namespace TorgLink.WinForms;

partial class MainForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel _root;
    private FlowLayoutPanel _toolbar;
    private Button _btnAdd;
    private Button _btnLan;
    private Button _btnServers;
    private Button _btnMyQr;
    private Button _btnSettings;
    private Button _btnProfile;
    private Button _btnLogout;
    private ListBox _list;
    private Label _status;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _root = new TableLayoutPanel();
        _toolbar = new FlowLayoutPanel();
        _btnAdd = new Button();
        _btnLan = new Button();
        _btnServers = new Button();
        _btnMyQr = new Button();
        _btnSettings = new Button();
        _btnProfile = new Button();
        _btnLogout = new Button();
        _list = new ListBox();
        _status = new Label();
        _root.SuspendLayout();
        _toolbar.SuspendLayout();
        SuspendLayout();

        _btnAdd.AutoSize = true;
        _btnAdd.Name = "_btnAdd";
        _btnAdd.Text = "Добавить чат";
        _btnLan.AutoSize = true;
        _btnLan.Name = "_btnLan";
        _btnLan.Text = "Контакты";
        _btnServers.AutoSize = true;
        _btnServers.Name = "_btnServers";
        _btnServers.Text = "Серверы";
        _btnMyQr.AutoSize = true;
        _btnMyQr.Name = "_btnMyQr";
        _btnMyQr.Text = "Мой QR";
        _btnSettings.AutoSize = true;
        _btnSettings.Name = "_btnSettings";
        _btnSettings.Text = "Настройки";
        _btnProfile.AutoSize = true;
        _btnProfile.Name = "_btnProfile";
        _btnProfile.Text = "Мой профиль";
        _btnLogout.AutoSize = true;
        _btnLogout.Name = "_btnLogout";
        _btnLogout.Text = "Выйти";

        _toolbar.AutoSize = true;
        _toolbar.Dock = DockStyle.Fill;
        _toolbar.Name = "_toolbar";
        _toolbar.Padding = new Padding(8);
        _toolbar.WrapContents = true;
        _toolbar.Controls.Add(_btnAdd);
        _toolbar.Controls.Add(_btnLan);
        _toolbar.Controls.Add(_btnServers);
        _toolbar.Controls.Add(_btnMyQr);
        _toolbar.Controls.Add(_btnSettings);
        _toolbar.Controls.Add(_btnProfile);
        _toolbar.Controls.Add(_btnLogout);

        _list.Dock = DockStyle.Fill;
        _list.DrawMode = DrawMode.OwnerDrawFixed;
        _list.IntegralHeight = false;
        _list.Name = "_list";

        _status.AutoSize = true;
        _status.Dock = DockStyle.Top;
        _status.MinimumSize = new Size(0, 56);
        _status.Name = "_status";
        _status.Padding = new Padding(10, 12, 10, 12);
        _status.TextAlign = ContentAlignment.MiddleLeft;

        _root.ColumnCount = 1;
        _root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _root.Dock = DockStyle.Fill;
        _root.Name = "_root";
        _root.RowCount = 3;
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.Controls.Add(_toolbar, 0, 0);
        _root.Controls.Add(_list, 0, 1);
        _root.Controls.Add(_status, 0, 2);

        Controls.Add(_root);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "TorgLink";
        // Width is set after measuring the typical status line (not a blind scale).
        ClientSize = new Size(640, 480);

        _root.ResumeLayout(false);
        _root.PerformLayout();
        _toolbar.ResumeLayout(false);
        _toolbar.PerformLayout();
        ResumeLayout(false);
    }
}
