namespace Iskra.WinForms;

/// <summary>Base for all Iskra WinForms windows: default UI font 12 pt.</summary>
public abstract class AppForm : Form
{
    protected AppForm()
    {
        Font = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);
    }
}
