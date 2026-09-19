using Microsoft.Extensions.Logging;
using ShortP2P.Client.Data;
using ShortP2P.Client.Qr;
using ShortP2P.Client.Services.MessengerServers;

namespace Iskra.WinForms;

public sealed class MessengerServersForm : AppForm
{
    private readonly MessengerServerManager _manager;
    private readonly ILogger<MessengerServersForm> _logger;
    private readonly TextBox _baseUrl = new() { Width = 420 };
    private readonly ListView _list = new()
    {
        View = View.Details,
        FullRowSelect = true,
        HideSelection = false,
        Dock = DockStyle.Fill
    };
    private readonly Label _status = new() { AutoSize = true, ForeColor = SystemColors.GrayText };

    public MessengerServersForm(MessengerServerManager manager, ILogger<MessengerServersForm> logger)
    {
        _manager = manager;
        _logger = logger;
        Text = "Messenger servers";
        Width = 900;
        Height = 420;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        _list.Columns.Add("URL", 280);
        _list.Columns.Add("Active", 60);
        _list.Columns.Add("Trusted", 70);
        _list.Columns.Add("Fingerprint", 280);

        var add = new Button { Text = "Добавить", AutoSize = true };
        var qrFile = new Button { Text = "QR из файла…", AutoSize = true };
        var share = new Button { Text = "Показать QR", AutoSize = true };
        var del = new Button { Text = "Удалить", AutoSize = true };
        var refresh = new Button { Text = "Обновить", AutoSize = true };
        var close = new Button { Text = "Закрыть", DialogResult = DialogResult.OK, AutoSize = true };
        add.Click += async (_, _) => await AddAsync().ConfigureAwait(true);
        qrFile.Click += async (_, _) => await ImportQrAsync().ConfigureAwait(true);
        share.Click += ShareSelected;
        del.Click += async (_, _) => await DeleteAsync().ConfigureAwait(true);
        refresh.Click += async (_, _) => await ReloadAsync().ConfigureAwait(true);

        var addRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(8) };
        addRow.Controls.Add(new Label { Text = "Base URL:", AutoSize = true, Padding = new Padding(0, 6, 0, 0) });
        addRow.Controls.Add(_baseUrl);
        addRow.Controls.Add(add);
        addRow.Controls.Add(qrFile);

        var actions = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Bottom, Padding = new Padding(8) };
        actions.Controls.Add(share);
        actions.Controls.Add(del);
        actions.Controls.Add(refresh);
        actions.Controls.Add(close);
        actions.Controls.Add(_status);

        Controls.Add(_list);
        Controls.Add(actions);
        Controls.Add(addRow);

        Load += async (_, _) => await ReloadAsync().ConfigureAwait(true);
    }

    private async Task ReloadAsync()
    {
        _list.Items.Clear();
        var rows = await _manager.ListAsync().ConfigureAwait(true);
        foreach (var e in rows)
        {
            var item = new ListViewItem(e.BaseUrl);
            item.SubItems.Add(e.Active ? "yes" : "no");
            item.SubItems.Add(e.Trusted ? "yes" : "no");
            item.SubItems.Add(e.FingerprintSha256);
            item.Tag = e;
            _list.Items.Add(item);
        }

        _status.Text = $"Серверов: {rows.Count}";
    }

    private async Task AddAsync()
    {
        var url = _baseUrl.Text.Trim();
        if (url.Length == 0)
        {
            MessageBox.Show(this, "Укажите Base URL.", "Сервер", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var entity = await _manager.AddServerAsync(url).ConfigureAwait(true);
            _baseUrl.Text = "";
            _status.Text = "Добавлен: " + entity.BaseUrl;
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Add server");
            MessageBox.Show(this, ex.Message, "Сервер", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task ImportQrAsync()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "QR-код сервера",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|All files|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;

        try
        {
            var bytes = File.ReadAllBytes(dlg.FileName);
            if (!MessengerServerQrService.TryDecodeImage(bytes, out var payload, out var err) || payload == null)
            {
                MessageBox.Show(this, err ?? "QR не распознан.", "QR сервера", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            var url = MessengerServerQrCodec.ToBaseUrl(payload);
            var entity = await _manager.AddServerAsync(url).ConfigureAwait(true);
            _status.Text = "Импортирован: " + entity.BaseUrl;
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Import server QR");
            MessageBox.Show(this, ex.Message, "QR сервера", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShareSelected(object? sender, EventArgs e)
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not MessengerServerEntity entity)
        {
            MessageBox.Show(this, "Выберите сервер.", "QR", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!MessengerServerQrService.TryBuildPayload(entity.BaseUrl, out var payload, out var err) || payload == null)
        {
            MessageBox.Show(this, err ?? "Не удалось собрать QR.", "QR", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var png = MessengerServerQrService.EncodeQrPng(payload);
        using var preview = new QrPreviewForm("QR сервера", png, entity.BaseUrl);
        preview.ShowDialog(this);
    }

    private async Task DeleteAsync()
    {
        if (_list.SelectedItems.Count == 0 || _list.SelectedItems[0].Tag is not MessengerServerEntity entity)
            return;
        if (MessageBox.Show(this, "Удалить " + entity.BaseUrl + "?", "Сервер", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        try
        {
            await _manager.DeleteServerAsync(entity.Id).ConfigureAwait(true);
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Remove server");
            MessageBox.Show(this, ex.Message, "Сервер", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
