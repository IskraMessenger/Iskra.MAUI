namespace TorgLink.WinForms;

partial class AddChatForm
{
    private System.ComponentModel.IContainer components = null;
    private System.Windows.Forms.TableLayoutPanel _layout;
    private System.Windows.Forms.Label _lblNick;
    private System.Windows.Forms.TextBox _nick;
    private System.Windows.Forms.Label _lblId;
    private System.Windows.Forms.TextBox _id;
    private System.Windows.Forms.Label _lblPub;
    private System.Windows.Forms.TextBox _pub;
    private System.Windows.Forms.Label _lblHost;
    private System.Windows.Forms.TextBox _host;
    private System.Windows.Forms.FlowLayoutPanel _buttons;
    private System.Windows.Forms.Button _qrFile;
    private System.Windows.Forms.Button _save;
    private System.Windows.Forms.Button _cancel;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            components?.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>
    /// Required method for Designer support - do not modify
    /// the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        this._layout = new System.Windows.Forms.TableLayoutPanel();
        this._lblNick = new System.Windows.Forms.Label();
        this._nick = new System.Windows.Forms.TextBox();
        this._lblId = new System.Windows.Forms.Label();
        this._id = new System.Windows.Forms.TextBox();
        this._lblPub = new System.Windows.Forms.Label();
        this._pub = new System.Windows.Forms.TextBox();
        this._lblHost = new System.Windows.Forms.Label();
        this._host = new System.Windows.Forms.TextBox();
        this._buttons = new System.Windows.Forms.FlowLayoutPanel();
        this._qrFile = new System.Windows.Forms.Button();
        this._save = new System.Windows.Forms.Button();
        this._cancel = new System.Windows.Forms.Button();
        this._layout.SuspendLayout();
        this._buttons.SuspendLayout();
        this.SuspendLayout();
        // 
        // _layout
        // 
        this._layout.ColumnCount = 1;
        this._layout.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
        this._layout.Controls.Add(this._lblNick, 0, 0);
        this._layout.Controls.Add(this._nick, 0, 1);
        this._layout.Controls.Add(this._lblId, 0, 2);
        this._layout.Controls.Add(this._id, 0, 3);
        this._layout.Controls.Add(this._lblPub, 0, 4);
        this._layout.Controls.Add(this._pub, 0, 5);
        this._layout.Controls.Add(this._lblHost, 0, 6);
        this._layout.Controls.Add(this._host, 0, 7);
        this._layout.Controls.Add(this._buttons, 0, 8);
        this._layout.Dock = System.Windows.Forms.DockStyle.Fill;
        this._layout.GrowStyle = System.Windows.Forms.TableLayoutPanelGrowStyle.FixedSize;
        this._layout.Location = new System.Drawing.Point(12, 12);
        this._layout.Margin = new System.Windows.Forms.Padding(0);
        this._layout.Name = "_layout";
        this._layout.RowCount = 9;
        this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
        this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
        this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 110F));
        this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 35F));
        this._layout.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.AutoSize));
        this._layout.Size = new System.Drawing.Size(496, 364);
        this._layout.TabIndex = 0;
        // 
        // _lblNick
        // 
        this._lblNick.AutoSize = true;
        this._lblNick.Dock = System.Windows.Forms.DockStyle.Fill;
        this._lblNick.Location = new System.Drawing.Point(3, 0);
        this._lblNick.Name = "_lblNick";
        this._lblNick.Size = new System.Drawing.Size(490, 20);
        this._lblNick.TabIndex = 0;
        this._lblNick.Text = "Ник пира";
        this._lblNick.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
        // 
        // _nick
        // 
        this._nick.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right) | System.Windows.Forms.AnchorStyles.Top)));
        this._nick.Location = new System.Drawing.Point(3, 26);
        this._nick.Name = "_nick";
        this._nick.Size = new System.Drawing.Size(490, 29);
        this._nick.TabIndex = 1;
        // 
        // _lblId
        // 
        this._lblId.AutoSize = true;
        this._lblId.Dock = System.Windows.Forms.DockStyle.Fill;
        this._lblId.Location = new System.Drawing.Point(3, 61);
        this._lblId.Name = "_lblId";
        this._lblId.Size = new System.Drawing.Size(490, 20);
        this._lblId.TabIndex = 2;
        this._lblId.Text = "Network id";
        this._lblId.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
        // 
        // _id
        // 
        this._id.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right) | System.Windows.Forms.AnchorStyles.Top)));
        this._id.Location = new System.Drawing.Point(3, 87);
        this._id.Name = "_id";
        this._id.Size = new System.Drawing.Size(490, 29);
        this._id.TabIndex = 3;
        // 
        // _lblPub
        // 
        this._lblPub.AutoSize = true;
        this._lblPub.Dock = System.Windows.Forms.DockStyle.Fill;
        this._lblPub.Location = new System.Drawing.Point(3, 122);
        this._lblPub.Name = "_lblPub";
        this._lblPub.Size = new System.Drawing.Size(490, 20);
        this._lblPub.TabIndex = 4;
        this._lblPub.Text = "RSA public JSON";
        this._lblPub.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
        // 
        // _pub
        // 
        this._pub.Dock = System.Windows.Forms.DockStyle.Fill;
        this._pub.Location = new System.Drawing.Point(3, 148);
        this._pub.MinimumSize = new System.Drawing.Size(0, 100);
        this._pub.Multiline = true;
        this._pub.Name = "_pub";
        this._pub.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
        this._pub.Size = new System.Drawing.Size(490, 104);
        this._pub.TabIndex = 5;
        // 
        // _lblHost
        // 
        this._lblHost.AutoSize = true;
        this._lblHost.Dock = System.Windows.Forms.DockStyle.Fill;
        this._lblHost.Location = new System.Drawing.Point(3, 258);
        this._lblHost.Name = "_lblHost";
        this._lblHost.Size = new System.Drawing.Size(490, 20);
        this._lblHost.TabIndex = 6;
        this._lblHost.Text = "Host / id (необязательно)";
        this._lblHost.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
        // 
        // _host
        // 
        this._host.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right) | System.Windows.Forms.AnchorStyles.Top)));
        this._host.Location = new System.Drawing.Point(3, 284);
        this._host.Name = "_host";
        this._host.Size = new System.Drawing.Size(490, 29);
        this._host.TabIndex = 7;
        // 
        // _buttons
        // 
        this._buttons.AutoSize = true;
        this._buttons.Dock = System.Windows.Forms.DockStyle.Fill;
        this._buttons.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
        this._buttons.Location = new System.Drawing.Point(3, 319);
        this._buttons.Name = "_buttons";
        this._buttons.Size = new System.Drawing.Size(490, 38);
        this._buttons.TabIndex = 8;
        this._buttons.WrapContents = false;
        this._buttons.Controls.Add(this._cancel);
        this._buttons.Controls.Add(this._save);
        this._buttons.Controls.Add(this._qrFile);
        // 
        // _qrFile
        // 
        this._qrFile.AutoSize = true;
        this._qrFile.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        this._qrFile.Location = new System.Drawing.Point(365, 3);
        this._qrFile.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        this._qrFile.MinimumSize = new System.Drawing.Size(90, 31);
        this._qrFile.Name = "_qrFile";
        this._qrFile.Size = new System.Drawing.Size(122, 31);
        this._qrFile.TabIndex = 0;
        this._qrFile.Text = "QR из файла";
        this._qrFile.UseVisualStyleBackColor = true;
        // 
        // _save
        // 
        this._save.AutoSize = true;
        this._save.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        this._save.Location = new System.Drawing.Point(269, 3);
        this._save.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        this._save.MinimumSize = new System.Drawing.Size(90, 31);
        this._save.Name = "_save";
        this._save.Size = new System.Drawing.Size(90, 31);
        this._save.TabIndex = 1;
        this._save.Text = "Сохранить";
        this._save.UseVisualStyleBackColor = true;
        // 
        // _cancel
        // 
        this._cancel.AutoSize = true;
        this._cancel.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
        this._cancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
        this._cancel.Location = new System.Drawing.Point(173, 3);
        this._cancel.Margin = new System.Windows.Forms.Padding(3, 3, 3, 3);
        this._cancel.MinimumSize = new System.Drawing.Size(90, 31);
        this._cancel.Name = "_cancel";
        this._cancel.Size = new System.Drawing.Size(90, 31);
        this._cancel.TabIndex = 2;
        this._cancel.Text = "Отмена";
        this._cancel.UseVisualStyleBackColor = true;
        // 
        // AddChatForm
        // 
        this.AutoSize = false;
        this.CancelButton = this._cancel;
        this.ClientSize = new System.Drawing.Size(520, 388);
        this.Controls.Add(this._layout);
        this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.MinimumSize = new System.Drawing.Size(536, 427);
        this.Name = "AddChatForm";
        this.Padding = new System.Windows.Forms.Padding(12);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
        this.Text = "Добавить чат";
        this._layout.ResumeLayout(false);
        this._layout.PerformLayout();
        this._buttons.ResumeLayout(false);
        this._buttons.PerformLayout();
        this.ResumeLayout(false);
        this.PerformLayout();
    }
}
