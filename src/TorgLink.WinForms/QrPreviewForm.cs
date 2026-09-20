namespace TorgLink.WinForms;

public sealed class QrPreviewForm : AppForm
{
    public QrPreviewForm(string title, byte[] png, string caption)
    {
        Text = title;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        using var ms = new MemoryStream(png);
        var image = new Bitmap(ms);
        var box = new PictureBox
        {
            Image = image,
            SizeMode = PictureBoxSizeMode.AutoSize
        };
        var label = new Label
        {
            Text = caption,
            AutoSize = true,
            MaximumSize = new Size(360, 0)
        };
        var close = new Button { Text = "Закрыть", DialogResult = DialogResult.OK };
        AcceptButton = close;

        var layout = new TableLayoutPanel { ColumnCount = 1, AutoSize = true };
        layout.Controls.Add(box);
        layout.Controls.Add(label);
        layout.Controls.Add(close);
        Controls.Add(layout);
    }
}
