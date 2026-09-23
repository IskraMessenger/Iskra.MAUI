namespace TorgLink.WinForms;

partial class QrPreviewForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel _layout;
    private PictureBox _box;
    private Label _label;
    private Button _close;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _layout = new TableLayoutPanel();
        _box = new PictureBox();
        _label = new Label();
        _close = new Button();
        _layout.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_box).BeginInit();
        SuspendLayout();

        _box.Name = "_box";
        _box.SizeMode = PictureBoxSizeMode.AutoSize;

        _label.AutoSize = true;
        _label.MaximumSize = new Size(360, 0);
        _label.Name = "_label";

        _close.DialogResult = DialogResult.OK;
        _close.Name = "_close";
        _close.Text = "Закрыть";

        _layout.AutoSize = true;
        _layout.ColumnCount = 1;
        _layout.Name = "_layout";
        _layout.Controls.Add(_box);
        _layout.Controls.Add(_label);
        _layout.Controls.Add(_close);

        AcceptButton = _close;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Controls.Add(_layout);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "QrPreviewForm";
        Padding = new Padding(12);
        StartPosition = FormStartPosition.CenterParent;

        ((System.ComponentModel.ISupportInitialize)_box).EndInit();
        _layout.ResumeLayout(false);
        _layout.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }
}
