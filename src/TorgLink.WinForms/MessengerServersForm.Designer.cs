namespace TorgLink.WinForms;

partial class MessengerServersForm
{
    private System.ComponentModel.IContainer components = null;
    private FlowLayoutPanel _addRow;
    private Label _lblBaseUrl;
    private TextBox _baseUrl;
    private Button _add;
    private Button _qrFile;
    private ListView _list;
    private ColumnHeader _colUrl;
    private ColumnHeader _colActive;
    private ColumnHeader _colTrusted;
    private ColumnHeader _colFingerprint;
    private FlowLayoutPanel _actions;
    private Button _share;
    private Button _del;
    private Button _refresh;
    private Button _close;
    private Label _status;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _addRow = new FlowLayoutPanel();
        _lblBaseUrl = new Label();
        _baseUrl = new TextBox();
        _add = new Button();
        _qrFile = new Button();
        _list = new ListView();
        _colUrl = new ColumnHeader();
        _colActive = new ColumnHeader();
        _colTrusted = new ColumnHeader();
        _colFingerprint = new ColumnHeader();
        _actions = new FlowLayoutPanel();
        _share = new Button();
        _del = new Button();
        _refresh = new Button();
        _close = new Button();
        _status = new Label();
        _addRow.SuspendLayout();
        _actions.SuspendLayout();
        SuspendLayout();

        _lblBaseUrl.AutoSize = true;
        _lblBaseUrl.Name = "_lblBaseUrl";
        _lblBaseUrl.Padding = new Padding(0, 6, 0, 0);
        _lblBaseUrl.Text = "Base URL:";

        _baseUrl.Name = "_baseUrl";
        _baseUrl.Width = 420;

        _add.AutoSize = true;
        _add.Name = "_add";
        _add.Text = "Добавить";
        _qrFile.AutoSize = true;
        _qrFile.Name = "_qrFile";
        _qrFile.Text = "QR из файла…";

        _addRow.AutoSize = true;
        _addRow.Dock = DockStyle.Top;
        _addRow.Name = "_addRow";
        _addRow.Padding = new Padding(8);
        _addRow.Controls.Add(_lblBaseUrl);
        _addRow.Controls.Add(_baseUrl);
        _addRow.Controls.Add(_add);
        _addRow.Controls.Add(_qrFile);

        _colUrl.Text = "URL";
        _colUrl.Width = 280;
        _colActive.Text = "Active";
        _colActive.Width = 60;
        _colTrusted.Text = "Trusted";
        _colTrusted.Width = 70;
        _colFingerprint.Text = "Fingerprint";
        _colFingerprint.Width = 280;

        _list.Columns.AddRange(new[] { _colUrl, _colActive, _colTrusted, _colFingerprint });
        _list.Dock = DockStyle.Fill;
        _list.FullRowSelect = true;
        _list.HideSelection = false;
        _list.Name = "_list";
        _list.View = View.Details;

        _share.AutoSize = true;
        _share.Name = "_share";
        _share.Text = "Показать QR";
        _del.AutoSize = true;
        _del.Name = "_del";
        _del.Text = "Удалить";
        _refresh.AutoSize = true;
        _refresh.Name = "_refresh";
        _refresh.Text = "Обновить";
        _close.AutoSize = true;
        _close.DialogResult = DialogResult.OK;
        _close.Name = "_close";
        _close.Text = "Закрыть";

        _status.AutoSize = true;
        _status.ForeColor = SystemColors.GrayText;
        _status.Name = "_status";

        _actions.AutoSize = true;
        _actions.Dock = DockStyle.Bottom;
        _actions.Name = "_actions";
        _actions.Padding = new Padding(8);
        _actions.Controls.Add(_share);
        _actions.Controls.Add(_del);
        _actions.Controls.Add(_refresh);
        _actions.Controls.Add(_close);
        _actions.Controls.Add(_status);

        Controls.Add(_list);
        Controls.Add(_actions);
        Controls.Add(_addRow);
        Height = 420;
        MinimizeBox = false;
        Name = "MessengerServersForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Messenger servers";
        Width = 900;

        _addRow.ResumeLayout(false);
        _addRow.PerformLayout();
        _actions.ResumeLayout(false);
        _actions.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
