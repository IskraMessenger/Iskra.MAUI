namespace TorgLink.WinForms;

public sealed partial class QrPreviewForm : AppForm
{
    public QrPreviewForm()
    {
        InitializeComponent();
    }

    public QrPreviewForm(string title, byte[] png, string caption)
        : this()
    {
        Text = title;
        using var ms = new MemoryStream(png);
        _box.Image = new Bitmap(ms);
        _label.Text = caption;
    }
}
