namespace TorgLink.WinForms;

partial class SettingsForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel _root;
    private Label _lblProfileHeader;
    private Label _profile;
    private Button _editProfile;
    private Label _lblUdpCaption;
    private Label _udpPort;
    private Label _lblNoBle;
    private Label _lblNetworkHeader;
    private CheckBox _lan;
    private CheckBox _shareRoutes;
    private Label _lblEconomy;
    private ComboBox _economy;
    private Label _economyHint;
    private Label _lblRoutingHeader;
    private FlowLayoutPanel _rowHops;
    private Label _lblHops;
    private NumericUpDown _hops;
    private FlowLayoutPanel _rowAttempts;
    private Label _lblAttempts;
    private NumericUpDown _attempts;
    private FlowLayoutPanel _rowDelay;
    private Label _lblDelay;
    private NumericUpDown _delayMs;
    private FlowLayoutPanel _rowTimeout;
    private Label _lblTimeout;
    private NumericUpDown _timeoutMs;
    private Label _lblLink;
    private ComboBox _link;
    private Label _lblStorageHeader;
    private Label _storage;
    private FlowLayoutPanel _buttons;
    private Button _save;
    private Button _keys;
    private Button _about;
    private Button _close;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _root = new TableLayoutPanel();
        _lblProfileHeader = new Label();
        _profile = new Label();
        _editProfile = new Button();
        _lblUdpCaption = new Label();
        _udpPort = new Label();
        _lblNoBle = new Label();
        _lblNetworkHeader = new Label();
        _lan = new CheckBox();
        _shareRoutes = new CheckBox();
        _lblEconomy = new Label();
        _economy = new ComboBox();
        _economyHint = new Label();
        _lblRoutingHeader = new Label();
        _rowHops = new FlowLayoutPanel();
        _lblHops = new Label();
        _hops = new NumericUpDown();
        _rowAttempts = new FlowLayoutPanel();
        _lblAttempts = new Label();
        _attempts = new NumericUpDown();
        _rowDelay = new FlowLayoutPanel();
        _lblDelay = new Label();
        _delayMs = new NumericUpDown();
        _rowTimeout = new FlowLayoutPanel();
        _lblTimeout = new Label();
        _timeoutMs = new NumericUpDown();
        _lblLink = new Label();
        _link = new ComboBox();
        _lblStorageHeader = new Label();
        _storage = new Label();
        _buttons = new FlowLayoutPanel();
        _save = new Button();
        _keys = new Button();
        _about = new Button();
        _close = new Button();
        _root.SuspendLayout();
        _rowHops.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_hops).BeginInit();
        _rowAttempts.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_attempts).BeginInit();
        _rowDelay.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_delayMs).BeginInit();
        _rowTimeout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_timeoutMs).BeginInit();
        _buttons.SuspendLayout();
        SuspendLayout();

        _lblProfileHeader.AutoSize = true;
        _lblProfileHeader.Font = new Font(Font, FontStyle.Bold);
        _lblProfileHeader.Name = "_lblProfileHeader";
        _lblProfileHeader.Text = "Профиль";

        _profile.AutoSize = true;
        _profile.Name = "_profile";

        _editProfile.AutoSize = true;
        _editProfile.Name = "_editProfile";
        _editProfile.Text = "Мой профиль…";

        _lblUdpCaption.AutoSize = true;
        _lblUdpCaption.Name = "_lblUdpCaption";
        _lblUdpCaption.Text = "UDP-порт данных (только просмотр)";

        _udpPort.AutoSize = true;
        _udpPort.Name = "_udpPort";

        _lblNoBle.AutoSize = true;
        _lblNoBle.ForeColor = SystemColors.GrayText;
        _lblNoBle.MaximumSize = new Size(520, 0);
        _lblNoBle.Name = "_lblNoBle";
        _lblNoBle.Text = "Bluetooth в этом клиенте недоступен. Язык/тема Maui не перенесены (интерфейс на русском).";

        _lblNetworkHeader.AutoSize = true;
        _lblNetworkHeader.Font = new Font(Font, FontStyle.Bold);
        _lblNetworkHeader.Name = "_lblNetworkHeader";
        _lblNetworkHeader.Text = "Сеть";

        _lan.AutoSize = true;
        _lan.Name = "_lan";
        _lan.Text = "LAN (UDP)";

        _shareRoutes.AutoSize = true;
        _shareRoutes.Name = "_shareRoutes";
        _shareRoutes.Text = "Делиться маршрутами (PeerSearch)";

        _lblEconomy.AutoSize = true;
        _lblEconomy.Name = "_lblEconomy";
        _lblEconomy.Text = "Экономия трафика";

        _economy.DropDownStyle = ComboBoxStyle.DropDownList;
        _economy.Name = "_economy";
        _economy.Width = 360;

        _economyHint.AutoSize = true;
        _economyHint.ForeColor = SystemColors.GrayText;
        _economyHint.MaximumSize = new Size(520, 0);
        _economyHint.Name = "_economyHint";

        _lblRoutingHeader.AutoSize = true;
        _lblRoutingHeader.Font = new Font(Font, FontStyle.Bold);
        _lblRoutingHeader.Name = "_lblRoutingHeader";
        _lblRoutingHeader.Text = "Маршрутизация";

        _lblHops.AutoSize = true;
        _lblHops.Name = "_lblHops";
        _lblHops.Padding = new Padding(0, 6, 8, 0);
        _lblHops.Text = "Макс. глубина поиска (1–3)";
        _hops.Maximum = 3;
        _hops.Minimum = 1;
        _hops.Name = "_hops";
        _hops.Width = 80;
        _rowHops.AutoSize = true;
        _rowHops.FlowDirection = FlowDirection.LeftToRight;
        _rowHops.Name = "_rowHops";
        _rowHops.Controls.Add(_lblHops);
        _rowHops.Controls.Add(_hops);

        _lblAttempts.AutoSize = true;
        _lblAttempts.Name = "_lblAttempts";
        _lblAttempts.Padding = new Padding(0, 6, 8, 0);
        _lblAttempts.Text = "Повторы поиска при ошибке";
        _attempts.Maximum = 20;
        _attempts.Minimum = 1;
        _attempts.Name = "_attempts";
        _attempts.Width = 80;
        _rowAttempts.AutoSize = true;
        _rowAttempts.FlowDirection = FlowDirection.LeftToRight;
        _rowAttempts.Name = "_rowAttempts";
        _rowAttempts.Controls.Add(_lblAttempts);
        _rowAttempts.Controls.Add(_attempts);

        _lblDelay.AutoSize = true;
        _lblDelay.Name = "_lblDelay";
        _lblDelay.Padding = new Padding(0, 6, 8, 0);
        _lblDelay.Text = "Пауза между попытками, мс";
        _delayMs.Increment = 1000;
        _delayMs.Maximum = 3_600_000;
        _delayMs.Minimum = 0;
        _delayMs.Name = "_delayMs";
        _delayMs.Width = 120;
        _rowDelay.AutoSize = true;
        _rowDelay.FlowDirection = FlowDirection.LeftToRight;
        _rowDelay.Name = "_rowDelay";
        _rowDelay.Controls.Add(_lblDelay);
        _rowDelay.Controls.Add(_delayMs);

        _lblTimeout.AutoSize = true;
        _lblTimeout.Name = "_lblTimeout";
        _lblTimeout.Padding = new Padding(0, 6, 8, 0);
        _lblTimeout.Text = "Таймаут FIND, мс";
        _timeoutMs.Increment = 500;
        _timeoutMs.Maximum = 120_000;
        _timeoutMs.Minimum = 500;
        _timeoutMs.Name = "_timeoutMs";
        _timeoutMs.Width = 120;
        _rowTimeout.AutoSize = true;
        _rowTimeout.FlowDirection = FlowDirection.LeftToRight;
        _rowTimeout.Name = "_rowTimeout";
        _rowTimeout.Controls.Add(_lblTimeout);
        _rowTimeout.Controls.Add(_timeoutMs);

        _lblLink.AutoSize = true;
        _lblLink.Name = "_lblLink";
        _lblLink.Text = "Пресет скорости канала";

        _link.DropDownStyle = ComboBoxStyle.DropDownList;
        _link.Name = "_link";
        _link.Width = 360;

        _lblStorageHeader.AutoSize = true;
        _lblStorageHeader.Font = new Font(Font, FontStyle.Bold);
        _lblStorageHeader.Name = "_lblStorageHeader";
        _lblStorageHeader.Text = "Хранилище";

        _storage.AutoSize = true;
        _storage.Name = "_storage";

        _save.AutoSize = true;
        _save.Name = "_save";
        _save.Text = "Сохранить";
        _keys.AutoSize = true;
        _keys.Name = "_keys";
        _keys.Text = "Копировать ключи";
        _about.AutoSize = true;
        _about.Name = "_about";
        _about.Text = "О программе";
        _close.AutoSize = true;
        _close.DialogResult = DialogResult.OK;
        _close.Name = "_close";
        _close.Text = "Закрыть";

        _buttons.AutoSize = true;
        _buttons.FlowDirection = FlowDirection.LeftToRight;
        _buttons.Name = "_buttons";
        _buttons.Controls.Add(_save);
        _buttons.Controls.Add(_keys);
        _buttons.Controls.Add(_about);
        _buttons.Controls.Add(_close);

        _root.AutoScroll = true;
        _root.AutoSize = true;
        _root.ColumnCount = 1;
        _root.Dock = DockStyle.Fill;
        _root.Name = "_root";
        _root.Padding = new Padding(12);
        _root.Controls.Add(_lblProfileHeader);
        _root.Controls.Add(_profile);
        _root.Controls.Add(_editProfile);
        _root.Controls.Add(_lblUdpCaption);
        _root.Controls.Add(_udpPort);
        _root.Controls.Add(_lblNoBle);
        _root.Controls.Add(_lblNetworkHeader);
        _root.Controls.Add(_lan);
        _root.Controls.Add(_shareRoutes);
        _root.Controls.Add(_lblEconomy);
        _root.Controls.Add(_economy);
        _root.Controls.Add(_economyHint);
        _root.Controls.Add(_lblRoutingHeader);
        _root.Controls.Add(_rowHops);
        _root.Controls.Add(_rowAttempts);
        _root.Controls.Add(_rowDelay);
        _root.Controls.Add(_rowTimeout);
        _root.Controls.Add(_lblLink);
        _root.Controls.Add(_link);
        _root.Controls.Add(_lblStorageHeader);
        _root.Controls.Add(_storage);
        _root.Controls.Add(_buttons);

        AcceptButton = _close;
        Controls.Add(_root);
        Height = 640;
        MinimizeBox = false;
        Name = "SettingsForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Настройки";
        Width = 580;

        ((System.ComponentModel.ISupportInitialize)_hops).EndInit();
        ((System.ComponentModel.ISupportInitialize)_attempts).EndInit();
        ((System.ComponentModel.ISupportInitialize)_delayMs).EndInit();
        ((System.ComponentModel.ISupportInitialize)_timeoutMs).EndInit();
        _root.ResumeLayout(false);
        _root.PerformLayout();
        _rowHops.ResumeLayout(false);
        _rowHops.PerformLayout();
        _rowAttempts.ResumeLayout(false);
        _rowAttempts.PerformLayout();
        _rowDelay.ResumeLayout(false);
        _rowDelay.PerformLayout();
        _rowTimeout.ResumeLayout(false);
        _rowTimeout.PerformLayout();
        _buttons.ResumeLayout(false);
        _buttons.PerformLayout();
        ResumeLayout(false);
    }
}
