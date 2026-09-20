namespace TorgLink.WinForms;

internal static class Branding
{
    public static PictureBox? CreateLogoPicture(int size)
    {
        var bmp = LoadLogoBitmap(size);
        if (bmp is null)
            return null;

        return new PictureBox
        {
            Image = bmp,
            SizeMode = PictureBoxSizeMode.Zoom,
            Width = size,
            Height = size,
            Margin = new Padding(0),
            Anchor = AnchorStyles.None
        };
    }

    public static Bitmap? LoadLogoBitmap(int preferredSize)
    {
        var asm = typeof(Branding).Assembly;
        var name = preferredSize switch
        {
            <= 40 => "TorgLink.WinForms.Resources.logo_32.png",
            <= 128 => "TorgLink.WinForms.Resources.logo_64.png",
            _ => "TorgLink.WinForms.Resources.logo_256.png"
        };

        using var stream = asm.GetManifestResourceStream(name)
            ?? asm.GetManifestResourceStream("TorgLink.WinForms.Resources.logo_64.png");
        return stream is null ? null : new Bitmap(stream);
    }

    public static void ApplyFormIcon(Form form)
    {
        try
        {
            var icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "app.ico");
            if (File.Exists(icoPath))
                form.Icon = new Icon(icoPath);
        }
        catch
        {
            // optional
        }
    }
}
