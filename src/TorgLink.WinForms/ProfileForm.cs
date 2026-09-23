using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using ShortP2P.Auth;
using ShortP2P.Auth.Data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using Img = SixLabors.ImageSharp.Image;
using Rectangle = SixLabors.ImageSharp.Rectangle;

namespace TorgLink.WinForms;

/// <summary>Редактирование своего Avatar и AboutMe (только локально). Аватар: квадратная обрезка 512×512.</summary>
public sealed partial class ProfileForm : AppForm
{
    private const int AvatarDimension = 512;

    private readonly AuthService _auth = null!;
    private readonly ILogger<ProfileForm> _logger = null!;

    private byte[]? _avatarBytes;

    public ProfileForm()
    {
        InitializeComponent();
    }

    public ProfileForm(AuthService auth, ILogger<ProfileForm> logger)
        : this()
    {
        _auth = auth;
        _logger = logger;

        _loadAvatar.Click += (_, _) => OnLoadAvatar();
        _clearAvatar.Click += (_, _) =>
        {
            _avatarBytes = null;
            ApplyAvatarPreview(null);
        };
        _aboutMe.TextChanged += (_, _) => UpdateAboutCounter();
        _save.Click += async (_, _) => await OnSaveAsync().ConfigureAwait(true);

        Shown += (_, _) => LoadCurrentProfileBestEffort();
    }

    private void LoadCurrentProfileBestEffort()
    {
        try
        {
            var user = _auth.CurrentUser;
            if (user == null)
                return;
            _aboutMe.Text = user.AboutMe ?? "";
            _avatarBytes = user.Avatar;
            ApplyAvatarPreview(_avatarBytes);
            UpdateAboutCounter();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load own profile into UI (best-effort); continuing with empty fields");
            _aboutMe.Text = "";
            _avatarBytes = null;
            ApplyAvatarPreview(null);
            UpdateAboutCounter();
        }
    }

    private void UpdateAboutCounter()
    {
        _aboutCounter.Text = $"{_aboutMe.Text.Length} / {PeerProfileLimits.MaxAboutMeChars}";
    }

    private void OnLoadAvatar()
    {
        using var dlg = new OpenFileDialog
        {
            Title = "Выбор аватара",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.gif;*.bmp;*.webp|All files|*.*"
        };
        if (dlg.ShowDialog(this) != DialogResult.OK)
            return;
        try
        {
            var raw = File.ReadAllBytes(dlg.FileName);
            if (!TryCropTo512(raw, out var cropped, out var err))
            {
                MessageBox.Show(this, err ?? "Не удалось подготовить изображение.", "Аватар",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            byte[]? prepared = cropped;
            if (cropped!.Length > PeerProfileLimits.MaxAvatarBytes)
            {
                var sizeKb = Math.Max(1, (cropped.Length + 1023) / 1024);
                var limitKb = PeerProfileLimits.MaxAvatarBytes / 1024;
                var answer = MessageBox.Show(this,
                    $"После обрезки изображение ≈ {sizeKb} КБ (лимит {limitKb} КБ).\nСжать, чтобы уложиться?",
                    "Сжать аватар?",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    MessageBox.Show(this,
                        $"Аватар должен быть ≤ {limitKb} КБ после обрезки.",
                        "Аватар", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!TryCompressToLimit(cropped, out prepared, out err))
                {
                    MessageBox.Show(this, err ?? $"Не удалось уложить аватар в {limitKb} КБ.", "Аватар",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            _avatarBytes = prepared;
            ApplyAvatarPreview(prepared);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load avatar file (best-effort)");
            MessageBox.Show(this, "Не удалось загрузить изображение.", "Аватар", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }

    private static bool TryCropTo512(ReadOnlySpan<byte> source,
        [NotNullWhen(true)] out byte[]? output,
        [NotNullWhen(false)] out string? error)
    {
        output = null;
        error = null;
        try
        {
            using var loaded = Img.Load(source);
            var side = Math.Min(loaded.Width, loaded.Height);
            if (side < 1)
            {
                error = "Некорректное изображение.";
                return false;
            }

            var cropX = (loaded.Width - side) / 2;
            var cropY = (loaded.Height - side) / 2;
            loaded.Mutate(x =>
            {
                x.Crop(new Rectangle(cropX, cropY, side, side));
                x.Resize(AvatarDimension, AvatarDimension);
            });

            using var ms = new MemoryStream();
            loaded.SaveAsJpeg(ms, new JpegEncoder { Quality = 90 });
            output = ms.ToArray();
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool TryCompressToLimit(ReadOnlySpan<byte> source,
        [NotNullWhen(true)] out byte[]? output,
        [NotNullWhen(false)] out string? error)
    {
        output = null;
        error = null;
        try
        {
            using var loaded = Img.Load(source);
            for (var attempt = 0; attempt < 45; attempt++)
            {
                var q = Math.Max(22, Math.Min(88, 88 - attempt));
                var scale = attempt < 12 ? 1.0f : (float)Math.Pow(0.92, attempt - 11);
                using var work = loaded.Clone(x =>
                {
                    if (scale < 0.999f)
                    {
                        var w = Math.Max(48, (int)(AvatarDimension * scale));
                        var h = Math.Max(48, (int)(AvatarDimension * scale));
                        x.Resize(w, h);
                    }
                });
                using var ms = new MemoryStream();
                work.SaveAsJpeg(ms, new JpegEncoder { Quality = q });
                var bytes = ms.ToArray();
                if (bytes.Length <= PeerProfileLimits.MaxAvatarBytes)
                {
                    output = bytes;
                    return true;
                }
            }

            error = $"Не удалось уложить аватар в {PeerProfileLimits.MaxAvatarBytes / 1024} КБ.";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private void ApplyAvatarPreview(byte[]? bytes)
    {
        var previous = _avatarPreview.Image;
        _avatarPreview.Image = null;
        previous?.Dispose();
        if (bytes == null || bytes.Length == 0)
            return;
        try
        {
            using var ms = new MemoryStream(bytes, false);
            using var img = System.Drawing.Image.FromStream(ms);
            _avatarPreview.Image = new Bitmap(img);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to preview avatar (best-effort)");
        }
    }

    private async Task OnSaveAsync()
    {
        try
        {
            var (ok, error) = await _auth.UpdateProfileAsync(_aboutMe.Text, _avatarBytes).ConfigureAwait(true);
            if (!ok)
            {
                MessageBox.Show(this, error ?? "Ошибка сохранения", "Профиль", MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save own profile");
            MessageBox.Show(this, "Не удалось сохранить профиль.", "Профиль", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
