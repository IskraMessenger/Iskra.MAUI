namespace TorgLink.WinForms;

partial class ChatForm
{
    private System.ComponentModel.IContainer components = null;
    private SplitContainer _split;
    private ListBox _sidebar;
    private Panel _right;
    private ListBox _messages;
    private TableLayoutPanel _bottom;
    private TextBox _input;
    private Button _attachVoice;
    private Button _attachImage;
    private Button _attachDocument;
    private Button _send;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _split = new SplitContainer();
        _sidebar = new ListBox();
        _right = new Panel();
        _messages = new ListBox();
        _bottom = new TableLayoutPanel();
        _input = new TextBox();
        _attachVoice = new Button();
        _attachImage = new Button();
        _attachDocument = new Button();
        _send = new Button();
        ((System.ComponentModel.ISupportInitialize)_split).BeginInit();
        _split.Panel1.SuspendLayout();
        _split.Panel2.SuspendLayout();
        _split.SuspendLayout();
        _right.SuspendLayout();
        _bottom.SuspendLayout();
        SuspendLayout();

        _sidebar.Dock = DockStyle.Fill;
        _sidebar.DrawMode = DrawMode.OwnerDrawFixed;
        _sidebar.IntegralHeight = false;
        _sidebar.Name = "_sidebar";

        _input.AcceptsReturn = true;
        _input.Dock = DockStyle.Fill;
        _input.Multiline = true;
        _input.Name = "_input";
        _input.ScrollBars = ScrollBars.Vertical;
        _input.WordWrap = true;

        _attachVoice.Anchor = AnchorStyles.None;
        _attachVoice.Font = new Font("Segoe UI Emoji", 11f);
        _attachVoice.Name = "_attachVoice";
        _attachVoice.Size = new Size(36, 32);
        _attachVoice.Text = "🎤";
        _attachImage.Anchor = AnchorStyles.None;
        _attachImage.Font = new Font("Segoe UI Emoji", 11f);
        _attachImage.Name = "_attachImage";
        _attachImage.Size = new Size(36, 32);
        _attachImage.Text = "🖼";
        _attachDocument.Anchor = AnchorStyles.None;
        _attachDocument.Font = new Font("Segoe UI Emoji", 11f);
        _attachDocument.Name = "_attachDocument";
        _attachDocument.Size = new Size(36, 32);
        _attachDocument.Text = "📄";
        _send.Anchor = AnchorStyles.None;
        _send.Name = "_send";
        _send.Size = new Size(120, 32);
        _send.Text = "Отправить";

        _bottom.ColumnCount = 5;
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _bottom.RowCount = 1;
        _bottom.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        _bottom.Controls.Add(_input, 0, 0);
        _bottom.Controls.Add(_attachVoice, 1, 0);
        _bottom.Controls.Add(_attachImage, 2, 0);
        _bottom.Controls.Add(_attachDocument, 3, 0);
        _bottom.Controls.Add(_send, 4, 0);
        _bottom.Dock = DockStyle.Bottom;
        _bottom.Height = 84;
        _bottom.Name = "_bottom";
        _bottom.Padding = new Padding(6);

        _messages.Dock = DockStyle.Fill;
        _messages.DrawMode = DrawMode.OwnerDrawVariable;
        _messages.IntegralHeight = false;
        _messages.Name = "_messages";

        _right.Dock = DockStyle.Fill;
        _right.Name = "_right";
        _right.Controls.Add(_messages);
        _right.Controls.Add(_bottom);

        _split.Dock = DockStyle.Fill;
        _split.Name = "_split";
        _split.Orientation = Orientation.Vertical;
        _split.Panel1MinSize = 0;
        _split.Panel2MinSize = 0;
        _split.Panel1.Controls.Add(_sidebar);
        _split.Panel2.Controls.Add(_right);

        Controls.Add(_split);
        Name = "ChatForm";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(960, 600);
        Text = "Чат";

        _bottom.ResumeLayout(false);
        _bottom.PerformLayout();
        _right.ResumeLayout(false);
        _split.Panel1.ResumeLayout(false);
        _split.Panel2.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)_split).EndInit();
        _split.ResumeLayout(false);
        ResumeLayout(false);
    }
}
