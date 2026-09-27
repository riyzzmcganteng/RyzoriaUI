using RyzoriaUI.Core;

namespace RyzoriaUI.UI;

public sealed class MainForm : Form
{
    private readonly AdbManager _adb = new();
    private readonly ScrcpyManager _scrcpy = new();
    private readonly ComboBox _devices = new();
    private readonly ComboBox _resolution = new();
    private readonly ComboBox _fps = new();
    private readonly ComboBox _bitrate = new();
    private readonly CheckBox _fullscreen = new();
    private readonly Label _status = new();
    private readonly TextBox _info = new();

    public MainForm()
    {
        Text = "RyzoriaUI v1.0";
        Width = 980; Height = 650; MinimumSize = new Size(820, 560);
        BackColor = Color.FromArgb(8, 12, 24); ForeColor = Color.White;
        Font = new Font("Segoe UI", 10);
        BuildUi();
        _ = ScanAsync();
    }

    private Button Btn(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, Height = 38, FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(18, 31, 55), ForeColor = Color.White, Margin = new Padding(5) };
        b.FlatAppearance.BorderColor = Color.FromArgb(35, 80, 145); b.Click += click; return b;
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, Padding = new Padding(18), BackColor = BackColor };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 70)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        Controls.Add(root);

        var title = new Label { Text = "RyzoriaUI\nAndroid Desktop Experience", Font = new Font("Segoe UI", 19, FontStyle.Bold), Dock = DockStyle.Fill, ForeColor = Color.FromArgb(95, 170, 255) };
        root.Controls.Add(title, 0, 0); root.SetColumnSpan(title, 2);

        var left = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(0, 8, 10, 0) };
        left.Controls.Add(new Label { Text = "DEVICE", AutoSize = true, ForeColor = Color.FromArgb(130, 150, 180) });
        _devices.Width = 270; _devices.DropDownStyle = ComboBoxStyle.DropDownList; left.Controls.Add(_devices);
        left.Controls.Add(Btn("↻ Scan Device", async (_, _) => await ScanAsync()));
        left.Controls.Add(Btn("▶ Start Mirror", StartMirror));
        left.Controls.Add(Btn("■ Stop Mirror", (_, _) => _scrcpy.Stop()));
        left.Controls.Add(Btn("🔒 Lock Screen", async (_, _) => await ActionAsync(_adb.LockScreenAsync)));
        left.Controls.Add(Btn("☀ Wake Screen", async (_, _) => await ActionAsync(_adb.WakeAsync)));
        left.Controls.Add(Btn("⌂ Home", async (_, _) => await ActionAsync(_adb.HomeAsync)));
        left.Controls.Add(Btn("← Back", async (_, _) => await ActionAsync(_adb.BackAsync)));
        left.Controls.Add(Btn("▣ Recent", async (_, _) => await ActionAsync(_adb.RecentAsync)));
        root.Controls.Add(left, 0, 1);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(10) };
        right.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170)); right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        AddSetting(right, "Resolution", _resolution, new object[] { "480p", "720p", "900p", "1080p" }, 0);
        AddSetting(right, "FPS", _fps, new object[] { "15", "20", "30", "45", "60" }, 1);
        AddSetting(right, "Bitrate", _bitrate, new object[] { "2 Mbps", "4 Mbps", "6 Mbps", "8 Mbps" }, 2);
        _fullscreen.Text = "Fullscreen"; _fullscreen.AutoSize = true; right.Controls.Add(_fullscreen, 1, 3);
        _info.Multiline = true; _info.ReadOnly = true; _info.Dock = DockStyle.Fill; _info.BackColor = Color.FromArgb(13, 20, 35); _info.ForeColor = Color.White; right.Controls.Add(_info, 0, 4); right.SetColumnSpan(_info, 2);
        root.Controls.Add(right, 1, 1);

        _status.Text = "Ready"; _status.ForeColor = Color.FromArgb(120, 190, 255); root.Controls.Add(_status, 0, 2); root.SetColumnSpan(_status, 2);
        _resolution.Items.AddRange(new object[] { "480p", "720p", "900p", "1080p" }); _resolution.SelectedIndex = 1;
        _fps.Items.AddRange(new object[] { "15", "20", "30", "45", "60" }); _fps.SelectedIndex = 2;
        _bitrate.Items.AddRange(new object[] { "2 Mbps", "4 Mbps", "6 Mbps", "8 Mbps" }); _bitrate.SelectedIndex = 1;
    }

    private static void AddSetting(TableLayoutPanel p, string label, ComboBox box, object[] values, int row)
    { p.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.Gainsboro }, 0, row); box.Dock = DockStyle.Top; box.Items.AddRange(values); box.DropDownStyle = ComboBoxStyle.DropDownList; p.Controls.Add(box, 1, row); }

    private string Serial => _devices.SelectedItem is AndroidDevice d ? d.Serial : "";

    private async Task ScanAsync()
    {
        _devices.Items.Clear(); _status.Text = "Scanning ADB...";
        if (!await _adb.IsAvailableAsync()) { _status.Text = "ADB tidak ditemukan. Install Android Platform Tools."; return; }
        var list = await _adb.GetDevicesAsync(); foreach (var d in list) _devices.Items.Add(d);
        if (_devices.Items.Count > 0) { _devices.SelectedIndex = 0; await LoadInfoAsync(); _status.Text = $"{_devices.Items.Count} device detected"; }
        else _status.Text = "Tidak ada device. Aktifkan USB debugging.";
    }

    private async Task LoadInfoAsync()
    {
        if (string.IsNullOrWhiteSpace(Serial)) return;
        var s = Serial;
        var manufacturer = await _adb.GetPropAsync(s, "ro.product.manufacturer");
        var model = await _adb.GetPropAsync(s, "ro.product.model");
        var device = await _adb.GetPropAsync(s, "ro.product.device");
        var android = await _adb.GetPropAsync(s, "ro.build.version.release");
        var sdk = await _adb.GetPropAsync(s, "ro.build.version.sdk");
        var resolution = await _adb.GetResolutionAsync(s);
        var battery = await _adb.GetBatteryAsync(s);
        _info.Text = $"Manufacturer : {manufacturer}\r\nModel        : {model}\r\nDevice Code  : {device}\r\nAndroid      : {android}\r\nSDK          : {sdk}\r\nResolution   : {resolution}\r\nBattery      : {battery}";
    }

    private void StartMirror(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Serial)) { _status.Text = "Pilih device dulu."; return; }
        var size = _resolution.SelectedItem?.ToString() switch { "480p" => 480, "900p" => 900, "1080p" => 1080, _ => 720 };
        var fps = int.Parse(_fps.SelectedItem?.ToString() ?? "30");
        var bitrate = int.Parse((_bitrate.SelectedItem?.ToString() ?? "4 Mbps").Split(' ')[0]);
        _status.Text = _scrcpy.Start(Serial, size, fps, bitrate, _fullscreen.Checked) ? "scrcpy started" : "scrcpy gagal dijalankan. Pastikan scrcpy ada di PATH.";
    }

    private async Task ActionAsync(Func<string, Task<string>> action)
    { if (string.IsNullOrWhiteSpace(Serial)) { _status.Text = "Pilih device dulu."; return; } await action(Serial); _status.Text = "Command sent"; }
}
