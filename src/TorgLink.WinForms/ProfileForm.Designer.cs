using ShortP2P.Auth.Data;

namespace TorgLink.WinForms;

partial class ProfileForm
{
    private System.ComponentModel.IContainer components = null;
    private TableLayoutPanel _root;
    private FlowLayoutPanel _avatarRow;
    private PictureBox _avatarPreview;
    private FlowLayoutPanel _avatarBtns;
    private Button _loadAvatar;
    private Button _clearAvatar;
    private Label _aboutLabel;
    private TextBox _aboutMe;
    private Label _aboutCounter;
    private Label _hint;
    private FlowLayoutPanel _bottom;
    private Button _cancel;
    private Button _save;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_avatarPreview != null)
            {
                var img = _avatarPreview.Image;
                _avatarPreview.Image = null;
                img?.Dispose();
            }

            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        _root = new TableLayoutPanel();
        _avatarRow = new FlowLayoutPanel();
        _avatarPreview = new PictureBox();
        _avatarBtns = new FlowLayoutPanel();
        _loadAvatar = new Button();
        _clearAvatar = new Button();
        _aboutLabel = new Label();
        _aboutMe = new TextBox();
        _aboutCounter = new Label();
        _hint = new Label();
        _bottom = new FlowLayoutPanel();
        _cancel = new Button();
        _save = new Button();
        _root.SuspendLayout();
        _avatarRow.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)_avatarPreview).BeginInit();
        _avatarBtns.SuspendLayout();
        _bottom.SuspendLayout();
        SuspendLayout();

        _avatarPreview.BorderStyle = BorderStyle.FixedSingle;
        _avatarPreview.Height = 96;
        _avatarPreview.Name = "_avatarPreview";
        _avatarPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _avatarPreview.Width = 96;

        _loadAvatar.AutoSize = true;
        _loadAvatar.Name = "_loadAvatar";
        _loadAvatar.Text = "Выбрать аватар…";
        _clearAvatar.AutoSize = true;
        _clearAvatar.Name = "_clearAvatar";
        _clearAvatar.Text = "Убрать аватар";

        _avatarBtns.AutoSize = true;
        _avatarBtns.FlowDirection = FlowDirection.TopDown;
        _avatarBtns.Name = "_avatarBtns";
        _avatarBtns.Padding = new Padding(12, 0, 0, 0);
        _avatarBtns.WrapContents = false;
        _avatarBtns.Controls.Add(_loadAvatar);
        _avatarBtns.Controls.Add(_clearAvatar);

        _avatarRow.AutoSize = true;
        _avatarRow.FlowDirection = FlowDirection.LeftToRight;
        _avatarRow.Name = "_avatarRow";
        _avatarRow.WrapContents = false;
        _avatarRow.Controls.Add(_avatarPreview);
        _avatarRow.Controls.Add(_avatarBtns);

        _aboutLabel.AutoSize = true;
        _aboutLabel.Name = "_aboutLabel";
        _aboutLabel.Text = $"О себе (до {PeerProfileLimits.MaxAboutMeChars} символов):";

        _aboutMe.Height = 100;
        _aboutMe.MaxLength = PeerProfileLimits.MaxAboutMeChars;
        _aboutMe.Multiline = true;
        _aboutMe.Name = "_aboutMe";
        _aboutMe.ScrollBars = ScrollBars.Vertical;
        _aboutMe.Width = 420;

        _aboutCounter.AutoSize = true;
        _aboutCounter.ForeColor = SystemColors.GrayText;
        _aboutCounter.Name = "_aboutCounter";

        _hint.AutoSize = true;
        _hint.ForeColor = SystemColors.GrayText;
        _hint.MaximumSize = new Size(460, 0);
        _hint.Name = "_hint";
        _hint.Text =
            $"Аватар — квадратная обрезка {AvatarDimension}×{AvatarDimension}, до {PeerProfileLimits.MaxAvatarBytes / 1024} КБ. " +
            "Данные хранятся только локально и отдаются пирам при скане сети.";

        _save.AutoSize = true;
        _save.Name = "_save";
        _save.Text = "Сохранить";
        _cancel.AutoSize = true;
        _cancel.DialogResult = DialogResult.Cancel;
        _cancel.Name = "_cancel";
        _cancel.Text = "Отмена";

        _bottom.AutoSize = true;
        _bottom.Dock = DockStyle.Fill;
        _bottom.FlowDirection = FlowDirection.RightToLeft;
        _bottom.Name = "_bottom";
        _bottom.Controls.Add(_cancel);
        _bottom.Controls.Add(_save);

        _root.ColumnCount = 1;
        _root.Dock = DockStyle.Fill;
        _root.Name = "_root";
        _root.Padding = new Padding(12);
        _root.RowCount = 6;
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _root.Controls.Add(_avatarRow, 0, 0);
        _root.Controls.Add(_aboutLabel, 0, 1);
        _root.Controls.Add(_aboutMe, 0, 2);
        _root.Controls.Add(_aboutCounter, 0, 3);
        _root.Controls.Add(_hint, 0, 4);
        _root.Controls.Add(_bottom, 0, 5);

        AcceptButton = _save;
        CancelButton = _cancel;
        Controls.Add(_root);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        Height = 380;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "ProfileForm";
        StartPosition = FormStartPosition.CenterParent;
        Text = "Мой профиль";
        Width = 520;

        ((System.ComponentModel.ISupportInitialize)_avatarPreview).EndInit();
        _root.ResumeLayout(false);
        _root.PerformLayout();
        _avatarRow.ResumeLayout(false);
        _avatarRow.PerformLayout();
        _avatarBtns.ResumeLayout(false);
        _avatarBtns.PerformLayout();
        _bottom.ResumeLayout(false);
        _bottom.PerformLayout();
        ResumeLayout(false);
    }
}
