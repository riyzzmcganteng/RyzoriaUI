using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using RyzoriaUI.Config;
using RyzoriaUI.Core;
using RyzoriaUI.Models;
using RyzoriaUI.Services;

namespace RyzoriaUI.UI;

public sealed class MainForm : Form
{
    private readonly Color Bg = Color.FromArgb(8, 11, 18);
    private readonly Color Panel = Color.FromArgb(16, 23, 34);
    private readonly Color Primary = Color.FromArgb(22, 119, 255);
    private readonly Color Muted = Color.FromArgb(148, 163, 184);

    private readonly AppSettings _settings;
    private readonly EmbeddedToolManager _tools;
    private readonly AdbService _adb;
    private readonly ScrcpyService _scrcpy;
    private readonly FileTransferService _files;
    private readonly DiagnosticsService _diag;
    private readonly MapperService _mapper = new();
    private readonly ResourceStats _stats;

    private readonly Panel _content = new();
    private readonly ComboBox _deviceBox = new();
    private readonly Label _deviceStatus = new();
    private readonly Label _resource = new();
    private readonly Label _clock = new();

    private IReadOnlyList<AndroidDevice> _devices =
        Array.Empty<AndroidDevice>();

    private string _page = "Dashboard";

    private System.Threading.Timer? _deviceTimer;
    private System.Windows.Forms.Timer? _clockTimer;

    public MainForm()
    {
        _settings = SettingsStore.Load();

        Text = "RyzoriaUI v1.7";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(980, 620);
        BackColor = Bg;
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.None;
        WindowState = FormWindowState.Maximized;

        _tools = new EmbeddedToolManager();
        _adb = new AdbService(_tools);
        _scrcpy = new ScrcpyService(_tools);
        _files = new FileTransferService(_tools);
        _diag = new DiagnosticsService(_tools, _adb);
        _stats = new ResourceStats();

        FormClosing += (_, _) =>
        {
            _deviceTimer?.Dispose();

            _clockTimer?.Stop();
            _clockTimer?.Dispose();

            _scrcpy.Dispose();
            _stats.Dispose();
            _tools.Dispose();
        };

        BuildShell();
        ShowDashboard();
        StartPolling();
    }

    private void BuildShell()
    {
        var top = new Panel
        {
            Dock = DockStyle.Top,
            Height = 54,
            BackColor = Panel
        };

        Controls.Add(top);

        var brand = LabelText(
            "RYZORIAUI  v1.7",
            18,
            16,
            FontStyle.Bold,
            16
        );

        brand.Cursor = Cursors.Hand;
        brand.Click += (_, _) => ShowDashboard();

        top.Controls.Add(brand);

        var search = TextBoxStyled(
            "Search RyzoriaUI...",
            250,
            12,
            270
        );

        search.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                ShowSearch(search.Text);
                e.SuppressKeyPress = true;
            }
        };

        top.Controls.Add(search);

        var min = new Button
        {
            Text = "—",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Panel,
            Location = new Point(Width - 150, 8),
            Size = new Size(42, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        min.FlatAppearance.BorderSize = 0;

        min.Click += (_, _) =>
        {
            WindowState = FormWindowState.Minimized;
        };

        top.Controls.Add(min);

        var max = new Button
        {
            Text = "□",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Panel,
            Location = new Point(Width - 102, 8),
            Size = new Size(42, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        max.FlatAppearance.BorderSize = 0;

        max.Click += (_, _) =>
        {
            WindowState =
                WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
        };

        top.Controls.Add(max);

        var close = new Button
        {
            Text = "×",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.White,
            BackColor = Color.FromArgb(120, 20, 30),
            Location = new Point(Width - 54, 8),
            Size = new Size(42, 36),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };

        close.FlatAppearance.BorderSize = 0;

        close.Click += (_, _) =>
        {
            Close();
        };

        top.Controls.Add(close);

        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 190,
            BackColor = Color.FromArgb(10, 15, 24)
        };

        Controls.Add(sidebar);

        string[] nav =
        {
            "⌂  Home",
            "📱 Android Hub",
            "📁 File Manager",
            "🎮 Gaming Center",
            "🎯 Control Mapper",
            "⚡ Performance",
            "🩺 Diagnostics",
            "🧪 Developer",
            "⚙  Settings",
            "ⓘ About"
        };

        int y = 74;

        foreach (var item in nav)
        {
            sidebar.Controls.Add(
                NavButton(item, y)
            );

            y += 48;
        }

        _content.Dock = DockStyle.Fill;
        _content.Padding = new Padding(22);
        _content.BackColor = Bg;

        Controls.Add(_content);

        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 36,
            BackColor = Panel
        };

        Controls.Add(bottom);

        _deviceStatus.Text = "● ADB: checking";
        _deviceStatus.ForeColor = Color.Gold;
        _deviceStatus.Location = new Point(14, 9);
        _deviceStatus.AutoSize = true;

        bottom.Controls.Add(_deviceStatus);

        _resource.Location = new Point(220, 9);
        _resource.AutoSize = true;
        _resource.ForeColor = Muted;

        bottom.Controls.Add(_resource);

        _clock.Location = new Point(Width - 120, 9);
        _clock.AutoSize = true;
        _clock.Anchor =
            AnchorStyles.Right |
            AnchorStyles.Bottom;

        bottom.Controls.Add(_clock);

        _clockTimer = new System.Windows.Forms.Timer
        {
            Interval = 1000
        };

        _clockTimer.Tick += (_, _) =>
        {
            _clock.Text =
                DateTime.Now.ToString(
                    "HH:mm:ss  dd/MM/yyyy"
                );
        };

        _clockTimer.Start();

        _stats.Updated += UpdateResourceLabel;
    }

    private void UpdateResourceLabel()
    {
        if (IsDisposed || !IsHandleCreated)
            return;

        try
        {
            BeginInvoke(new Action(() =>
            {
                if (IsDisposed)
                    return;

                _resource.Text =
                    $"CPU {_stats.CpuPercent:0}%  •  App {_stats.AppMemoryMb:0} MB";
            }));
        }
        catch
        {
            // Form sedang ditutup.
        }
    }

    private Button NavButton(
        string text,
        int y)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(12, y),
            Size = new Size(166, 42),
            TextAlign = ContentAlignment.MiddleLeft,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Padding = new Padding(12, 0, 0, 0),
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderSize = 0;

        button.Click += (_, _) =>
        {
            if (text.Contains(
                    "Home",
                    StringComparison.OrdinalIgnoreCase))
            {
                ShowDashboard();
            }
            else if (text.Contains(
                         "Android",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowAndroid();
            }
            else if (text.Contains(
                         "File",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowFiles();
            }
            else if (text.Contains(
                         "Gaming",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowGaming();
            }
            else if (text.Contains(
                         "Mapper",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowMapper();
            }
            else if (text.Contains(
                         "Performance",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowPerformance();
            }
            else if (text.Contains(
                         "Diagnostics",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowDiagnostics();
            }
            else if (text.Contains(
                         "Developer",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowDeveloper();
            }
            else if (text.Contains(
                         "Settings",
                         StringComparison.OrdinalIgnoreCase))
            {
                ShowSettings();
            }
            else
            {
                ShowAbout();
            }
        };

        return button;
    }

    private Label LabelText(
        string text,
        int x,
        int y,
        FontStyle style = FontStyle.Regular,
        float size = 10)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y),
            AutoSize = true,
            ForeColor = Color.White,
            Font = new Font(
                "Segoe UI",
                size,
                style
            )
        };
    }

    private TextBox TextBoxStyled(
        string placeholder,
        int x,
        int y,
        int width)
    {
        return new TextBox
        {
            Text = "",
            PlaceholderText = placeholder,
            Location = new Point(x, y),
            Width = width,
            BackColor = Color.FromArgb(24, 31, 43),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
    }

    private Panel Card(
        int x,
        int y,
        int width,
        int height)
    {
        return new Panel
        {
            Location = new Point(x, y),
            Size = new Size(width, height),
            BackColor = Panel
        };
    }

    private Button Btn(
        string text,
        int x,
        int y,
        int width = 150)
    {
        var button = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(width, 38),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 33, 55),
            ForeColor = Color.White,
            Cursor = Cursors.Hand
        };

        button.FlatAppearance.BorderColor =
            Primary;

        return button;
    }

    private void ResetContent(string title)
    {
        _content.Controls.Clear();

        _page = title;

        _content.Controls.Add(
            LabelText(
                title,
                10,
                0,
                FontStyle.Bold,
                22
            )
        );
    }

    private void ShowDashboard()
    {
        ResetContent("RyzoriaUI Desktop");

        var hero = Card(
            10,
            48,
            700,
            180
        );

        _content.Controls.Add(hero);

        hero.Controls.Add(
            LabelText(
                "ANDROID DESKTOP EXPERIENCE",
                20,
                20,
                FontStyle.Bold,
                18
            )
        );

        hero.Controls.Add(
            LabelText(
                "Android • ADB • scrcpy • Gaming • Files",
                20,
                55,
                FontStyle.Regular,
                11
            )
        );

        hero.Controls.Add(
            LabelText(
                "Target mode: Celeron N2040 + 4 GB RAM",
                20,
                82,
                FontStyle.Regular,
                10
            )
        );

        var start = Btn(
            "Open Android Hub",
            20,
            118,
            180
        );

        start.Click += (_, _) =>
        {
            ShowAndroid();
        };

        hero.Controls.Add(start);

        var perf = Btn(
            "Ultra Low Mode",
            215,
            118,
            150
        );

        perf.Click += (_, _) =>
        {
            ApplyPreset("Ultra Low");
        };

        hero.Controls.Add(perf);

        var reload = Btn(
            "Test Connection",
            380,
            118,
            150
        );

        reload.Click += async (_, _) =>
        {
            await RefreshDevices();
        };

        hero.Controls.Add(reload);
    }

    private void ShowAndroid()
    {
        ResetContent("Android Hub");

        var card = Card(
            10,
            48,
            780,
            420
        );

        _content.Controls.Add(card);

        card.Controls.Add(
            LabelText(
                "Connected device",
                18,
                18,
                FontStyle.Bold,
                13
            )
        );

        _deviceBox.Location =
            new Point(18, 48);

        _deviceBox.Width = 420;

        _deviceBox.BackColor =
            Color.FromArgb(24, 31, 43);

        _deviceBox.ForeColor =
            Color.White;

        _deviceBox.DropDownStyle =
            ComboBoxStyle.DropDownList;

        card.Controls.Add(_deviceBox);

        var refresh = Btn(
            "↻ Refresh",
            460,
            46,
            120
        );

        refresh.Click += async (_, _) =>
        {
            await RefreshDevices();
        };

        card.Controls.Add(refresh);

        var mirror = Btn(
            "START MIRROR",
            18,
            94,
            160
        );

        mirror.Click += async (_, _) =>
        {
            await StartMirror();
        };

        card.Controls.Add(mirror);

        var stop = Btn(
            "STOP",
            190,
            94,
            100
        );

        stop.Click += (_, _) =>
        {
            _scrcpy.Stop();
        };

        card.Controls.Add(stop);

        var actions =
            new (string Name, Func<string, Task> Action)[]
            {
                ("Home", _adb.HomeAsync),
                ("Back", _adb.BackAsync),
                ("Recent", _adb.RecentAsync),
                ("Vol +", _adb.VolumeUpAsync),
                ("Vol -", _adb.VolumeDownAsync),
                ("Mute", _adb.MuteAsync)
            };

        int actionX = 18;
        int actionY = 145;

        foreach (var action in actions)
        {
            var button = Btn(
                action.Name,
                actionX,
                actionY,
                104
            );

            var currentAction =
                action.Action;

            button.Click += async (_, _) =>
            {
                await DeviceAction(
                    currentAction
                );
            };

            card.Controls.Add(button);

            actionX += 112;

            if (actionX > 470)
            {
                actionX = 18;
                actionY += 52;
            }
        }

        var info = LabelText(
            "No device loaded.",
            18,
            250,
            FontStyle.Regular,
            10
        );

        info.MaximumSize =
            new Size(730, 120);

        card.Controls.Add(info);

        async Task UpdateInfo()
        {
            var device = SelectedDevice();

            if (device is null)
            {
                info.Text =
                    "No authorized Android device.\r\n" +
                    "Enable USB debugging and accept the RSA prompt.";

                return;
            }

            info.Text =
                $"MODEL      {device.Manufacturer} {device.Model}\r\n" +
                $"CODE       {device.DeviceCode}\r\n" +
                $"ANDROID    {device.AndroidVersion} (SDK {device.Sdk})\r\n" +
                $"BATTERY    {device.Battery}\r\n" +
                $"RAM        {device.Ram}\r\n" +
                $"STORAGE    {device.Storage}\r\n" +
                $"DISPLAY    {device.Resolution} / DPI {device.Dpi}\r\n" +
                $"CONNECTION {device.ConnectionType}\r\n" +
                $"ADB        {(device.Authorized ? "Connected" : "Unauthorized")}";
        }

        _ = UpdateInfo();

        var shot = Btn(
            "Screenshot",
            18,
            390,
            130
        );

        shot.Click += async (_, _) =>
        {
            await TakeScreenshot();
        };

        card.Controls.Add(shot);

        var reboot = Btn(
            "Reboot",
            158,
            390,
            110
        );

        /*
         * RebootAsync mengembalikan Task<bool>,
         * sedangkan DeviceAction membutuhkan
         * Func<string, Task>.
         *
         * Jadi hasil bool kita tangani di sini.
         */
        reboot.Click += async (_, _) =>
        {
            await DeviceAction(
                async serial =>
                {
                    var success =
                        await _adb.RebootAsync(
                            serial
                        );

                    if (!success)
                    {
                        throw new Exception(
                            "Android gagal melakukan reboot."
                        );
                    }
                }
            );
        };

        card.Controls.Add(reboot);
    }

    private void ShowFiles()
    {
        ResetContent("Android File Manager");

        var card = Card(
            10,
            48,
            790,
            420
        );

        _content.Controls.Add(card);

        var path = TextBoxStyled(
            "Remote path e.g. /sdcard/Download",
            18,
            18,
            470
        );

        path.Text = "/sdcard";

        card.Controls.Add(path);

        var list = Btn(
            "List",
            500,
            18,
            100
        );

        card.Controls.Add(list);

        var output = new RichTextBox
        {
            Location = new Point(18, 70),
            Size = new Size(745, 260),
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            Font = new Font("Consolas", 9),
            ReadOnly = true
        };

        card.Controls.Add(output);

        list.Click += async (_, _) =>
        {
            var device = SelectedDevice();

            if (device is null)
            {
                output.Text =
                    "No device.";

                return;
            }

            var remote =
                string.IsNullOrWhiteSpace(path.Text)
                    ? "/sdcard"
                    : path.Text.Trim();

            output.Text =
                await _files.ListAsync(
                    device.Serial,
                    remote
                );
        };

        var pull = Btn(
            "Pull file",
            18,
            346,
            120
        );

        pull.Click += async (_, _) =>
        {
            var device = SelectedDevice();

            if (device is null)
                return;

            using var dialog =
                new SaveFileDialog();

            if (dialog.ShowDialog() !=
                DialogResult.OK)
            {
                return;
            }

            var remote =
                path.Text.Trim();

            var success =
                await _files.PullAsync(
                    device.Serial,
                    remote,
                    dialog.FileName
                );

            MessageBox.Show(
                success
                    ? "Transfer complete."
                    : "Transfer failed."
            );
        };

        card.Controls.Add(pull);

        var push = Btn(
            "Push file",
            150,
            346,
            120
        );

        push.Click += async (_, _) =>
        {
            var device = SelectedDevice();

            if (device is null)
                return;

            using var dialog =
                new OpenFileDialog();

            if (dialog.ShowDialog() !=
                DialogResult.OK)
            {
                return;
            }

            var remote =
                path.Text.Trim();

            var success =
                await _files.PushAsync(
                    device.Serial,
                    dialog.FileName,
                    remote
                );

            MessageBox.Show(
                success
                    ? "Upload complete."
                    : "Upload failed."
            );
        };

        card.Controls.Add(push);

        var folder = Btn(
            "Create folder",
            282,
            346,
            120
        );

        folder.Click += async (_, _) =>
        {
            var device = SelectedDevice();

            if (device is null)
                return;

            var name = Prompt(
                "Folder name",
                "NewFolder"
            );

            if (string.IsNullOrWhiteSpace(name))
                return;

            var remotePath =
                $"{path.Text.TrimEnd('/')}/{name}";

            var success =
                await _files.ShellAsync(
                    device.Serial,
                    "mkdir",
                    "-p",
                    remotePath
                );

            MessageBox.Show(
                success
                    ? "Folder created."
                    : "Failed to create folder."
            );
        };

        card.Controls.Add(folder);
    }

    private void ShowGaming()
    {
        ResetContent("Gaming Center");

        var card = Card(
            10,
            48,
            790,
            350
        );

        _content.Controls.Add(card);

        card.Controls.Add(
            LabelText(
                "Low-end gaming profiles",
                18,
                18,
                FontStyle.Bold,
                13
            )
        );

        var presets = new[]
        {
            "Ultra Low • 480p / 20 FPS / 2 Mbps",
            "Low • 576p / 25 FPS / 3 Mbps",
            "Balanced • 720p / 30 FPS / 4 Mbps",
            "Performance • 1080p / 60 FPS / 8 Mbps"
        };

        var combo = new ComboBox
        {
            Location = new Point(18, 52),
            Width = 520,
            DropDownStyle =
                ComboBoxStyle.DropDownList
        };

        combo.Items.AddRange(presets);
        combo.SelectedIndex = 0;

        card.Controls.Add(combo);

        var fullscreen = new CheckBox
        {
            Text = "Fullscreen",
            Location = new Point(18, 96),
            AutoSize = true
        };

        card.Controls.Add(fullscreen);

        var audio = new CheckBox
        {
            Text =
                "No audio (recommended for N2040)",
            Location = new Point(18, 126),
            AutoSize = true,
            Checked = true
        };

        card.Controls.Add(audio);

        var launch = Btn(
            "Launch Mirror",
            18,
            164,
            150
        );

        launch.Click += async (_, _) =>
        {
            var device =
                SelectedDevice();

            if (device is null)
            {
                MessageBox.Show(
                    "Connect Android first."
                );

                return;
            }

            var index =
                Math.Max(
                    0,
                    combo.SelectedIndex
                );

            var preset =
                index switch
                {
                    0 => new PerformancePreset(
                        "Ultra Low",
                        480,
                        20,
                        2,
                        true
                    ),

                    1 => new PerformancePreset(
                        "Low",
                        576,
                        25,
                        3,
                        true
                    ),

                    2 => new PerformancePreset(
                        "Balanced",
                        720,
                        30,
                        4,
                        true
                    ),

                    _ => new PerformancePreset(
                        "Performance",
                        1080,
                        60,
                        8,
                        false
                    )
                };

            try
            {
                await _scrcpy.StartAsync(
                    device,
                    preset,
                    fullscreen.Checked,
                    false,
                    audio.Checked
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "RyzoriaUI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        };

        card.Controls.Add(launch);

        var note = LabelText(
            "FPS counter / game performance depends on the Android device and scrcpy window. RyzoriaUI never fabricates FPS values.",
            18,
            220,
            FontStyle.Regular,
            10
        );

        note.MaximumSize =
            new Size(730, 80);

        card.Controls.Add(note);
    }

    private void ShowMapper()
    {
        ResetContent(
            "Universal Control Mapper"
        );

        var card = Card(
            10,
            48,
            790,
            390
        );

        _content.Controls.Add(card);

        card.Controls.Add(
            LabelText(
                "Profile",
                18,
                18,
                FontStyle.Bold,
                13
            )
        );

        var profiles =
            _settings.MapperProfiles;

        var combo = new ComboBox
        {
            Location = new Point(18, 48),
            Width = 260,
            DropDownStyle =
                ComboBoxStyle.DropDownList
        };

        combo.Items.AddRange(
            profiles.Select(
                p => p.Name
            ).ToArray()
        );

        if (combo.Items.Count > 0)
            combo.SelectedIndex = 0;

        card.Controls.Add(combo);

        var list = new ListBox
        {
            Location = new Point(18, 92),
            Size = new Size(350, 220),
            BackColor =
                Color.FromArgb(10, 15, 24),
            ForeColor = Color.White
        };

        card.Controls.Add(list);

        void LoadProfile()
        {
            list.Items.Clear();

            if (profiles.Count == 0)
                return;

            var index =
                Math.Max(
                    0,
                    combo.SelectedIndex
                );

            if (index >= profiles.Count)
                index = 0;

            var profile =
                profiles[index];

            foreach (var binding
                     in profile.Bindings)
            {
                list.Items.Add(
                    $"{binding.Key}  →  {binding.Value}"
                );
            }
        }

        combo.SelectedIndexChanged +=
            (_, _) => LoadProfile();

        LoadProfile();

        var test = Btn(
            "Test binding",
            390,
            94,
            150
        );

        test.Click += async (_, _) =>
        {
            var device =
                SelectedDevice();

            if (device is null ||
                list.SelectedItem is null ||
                profiles.Count == 0)
            {
                return;
            }

            var key =
                list.SelectedItem
                    .ToString()!
                    .Split('→')[0]
                    .Trim();

            var index =
                Math.Max(
                    0,
                    combo.SelectedIndex
                );

            var profile =
                profiles[index];

            if (profile.Bindings.TryGetValue(
                    key,
                    out var binding))
            {
                var success =
                    await _mapper.SendBindingAsync(
                        _adb,
                        device.Serial,
                        binding
                    );

                if (!success)
                {
                    MessageBox.Show(
                        "Binding gagal dikirim."
                    );
                }
            }
        };

        card.Controls.Add(test);

        var note = LabelText(
            "Mappings use ADB input events where possible. Touch injection is device/game dependent; no universal 100% guarantee.",
            390,
            155,
            FontStyle.Regular,
            10
        );

        note.MaximumSize =
            new Size(330, 100);

        card.Controls.Add(note);

        var save = Btn(
            "Save JSON",
            390,
            275,
            150
        );

        save.Click += (_, _) =>
        {
            SettingsStore.Save(
                _settings
            );

            MessageBox.Show(
                "Mapper profiles saved."
            );
        };

        card.Controls.Add(save);
    }

    private void ShowPerformance()
    {
        ResetContent(
            "Performance Manager"
        );

        var modes =
            new[]
            {
                ("Auto", 480, 20, 2),
                ("Ultra Low", 480, 20, 2),
                ("Low", 576, 25, 3),
                ("Balanced", 720, 30, 4),
                ("Performance", 1080, 60, 8)
            };

        int y = 52;

        foreach (var mode in modes)
        {
            var current = mode;

            var button = Btn(
                $"{current.Item1} • {current.Item2}p / {current.Item3} FPS",
                10,
                y,
                260
            );

            button.Click += (_, _) =>
            {
                _settings.PerformanceMode =
                    current.Item1;

                _settings.ScrcpyMaxSize =
                    current.Item2;

                _settings.ScrcpyMaxFps =
                    current.Item3;

                _settings.ScrcpyBitrateMbps =
                    current.Item4;

                _settings.NoAudio =
                    current.Item1 !=
                    "Performance";

                SettingsStore.Save(
                    _settings
                );

                MessageBox.Show(
                    $"Performance: {current.Item1}"
                );
            };

            _content.Controls.Add(button);

            y += 50;
        }

        var info = LabelText(
            "Auto is intentionally conservative on the N2040 / 4 GB target: 480p, 20 FPS and 2 Mbps with audio off.",
            310,
            62
        );

        info.MaximumSize =
            new Size(480, 80);

        _content.Controls.Add(info);
    }

    private async void ShowDiagnostics()
    {
        ResetContent(
            "Diagnostics Center"
        );

        var card = Card(
            10,
            48,
            790,
            400
        );

        _content.Controls.Add(card);

        var output = new RichTextBox
        {
            Location = new Point(18, 62),
            Size = new Size(750, 300),
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            Font = new Font(
                "Consolas",
                10
            ),
            ReadOnly = true
        };

        card.Controls.Add(output);

        var run = Btn(
            "TEST AGAIN",
            18,
            18,
            150
        );

        run.Click += async (_, _) =>
        {
            await RunDiagnostics();
        };

        card.Controls.Add(run);

        async Task RunDiagnostics()
        {
            output.Text =
                "RYZORIA DIAGNOSTICS\r\n\r\n" +
                "Running...\r\n";

            try
            {
                var result =
                    await _diag.RunAsync();

                output.Text =
                    "RYZORIA DIAGNOSTICS\r\n\r\n" +
                    string.Join(
                        "\r\n",
                        result.Select(
                            kv =>
                                $"{kv.Key,-20} {kv.Value}"
                        )
                    );
            }
            catch (Exception ex)
            {
                output.Text =
                    "RYZORIA DIAGNOSTICS\r\n\r\n" +
                    "ERROR:\r\n" +
                    ex.Message;
            }
        }

        await RunDiagnostics();
    }

    private void ShowDeveloper()
    {
        ResetContent(
            "Developer Console"
        );

        var card = Card(
            10,
            48,
            820,
            430
        );

        _content.Controls.Add(card);

        var input = TextBoxStyled(
            "Enter whitelisted command",
            18,
            18,
            570
        );

        card.Controls.Add(input);

        var output = new RichTextBox
        {
            Location = new Point(18, 68),
            Size = new Size(770, 300),
            BackColor = Color.Black,
            ForeColor = Color.LightGreen,
            Font = new Font(
                "Consolas",
                9
            ),
            ReadOnly = true
        };

        card.Controls.Add(output);

        input.KeyDown += async (_, e) =>
        {
            if (e.KeyCode != Keys.Enter)
                return;

            e.SuppressKeyPress = true;

            var cmd =
                input.Text.Trim();

            input.Clear();

            if (string.IsNullOrWhiteSpace(cmd))
                return;

            output.AppendText(
                $"> {cmd}\r\n"
            );

            try
            {
                switch (cmd.ToLowerInvariant())
                {
                    case "help":
                        output.AppendText(
                            "adb.version\r\n" +
                            "adb.devices\r\n" +
                            "device.info\r\n" +
                            "device.battery\r\n" +
                            "device.resolution\r\n" +
                            "connection.test\r\n" +
                            "diagnostics.run\r\n" +
                            "logs.show\r\n" +
                            "settings.reset\r\n" +
                            "clear\r\n"
                        );
                        break;

                    case "adb.version":
                        output.AppendText(
                            await _adb.VersionAsync() +
                            "\r\n"
                        );
                        break;

                    case "adb.devices":
                    {
                        var devices =
                            await _adb.GetDevicesAsync();

                        foreach (var device
                                 in devices)
                        {
                            output.AppendText(
                                $"{device.Serial}\t" +
                                $"{device.State}\t" +
                                $"{device.Model}\t" +
                                $"{device.ConnectionType}\r\n"
                            );
                        }

                        break;
                    }

                    case "device.info":
                    {
                        var device =
                            SelectedDevice();

                        output.AppendText(
                            device?.ToString()
                            ?? "No device"
                        );

                        output.AppendText(
                            "\r\n"
                        );

                        break;
                    }

                    case "device.battery":
                    {
                        var device =
                            SelectedDevice();

                        output.AppendText(
                            device?.Battery
                            ?? "No device"
                        );

                        output.AppendText(
                            "\r\n"
                        );

                        break;
                    }

                    case "device.resolution":
                    {
                        var device =
                            SelectedDevice();

                        output.AppendText(
                            device?.Resolution
                            ?? "No device"
                        );

                        output.AppendText(
                            "\r\n"
                        );

                        break;
                    }

                    case "connection.test":
                    {
                        var result =
                            await _diag.RunAsync();

                        output.AppendText(
                            result.GetValueOrDefault(
                                "Android Device",
                                "unknown"
                            ) + "\r\n"
                        );

                        break;
                    }

                    case "diagnostics.run":
                    {
                        var result =
                            await _diag.RunAsync();

                        output.AppendText(
                            string.Join(
                                "\r\n",
                                result
                            ) + "\r\n"
                        );

                        break;
                    }

                    case "logs.show":
                    {
                        var logPath =
                            Path.Combine(
                                SettingsStore.LogDirectory,
                                "ryzoria.log"
                            );

                        output.AppendText(
                            File.Exists(logPath)
                                ? File.ReadAllText(logPath)
                                : "No log file.\r\n"
                        );

                        break;
                    }

                    case "settings.reset":
                        SettingsStore.Reset();

                        output.AppendText(
                            "Settings reset. " +
                            "Restart recommended.\r\n"
                        );

                        break;

                    case "clear":
                        output.Clear();
                        break;

                    default:
                        output.AppendText(
                            "Command rejected. " +
                            "Type help.\r\n"
                        );
                        break;
                }
            }
            catch (Exception ex)
            {
                output.AppendText(
                    $"ERROR: {ex.Message}\r\n"
                );
            }
        };
    }

    private void ShowSettings()
    {
        ResetContent("Settings");

        var card = Card(
            10,
            48,
            790,
            390
        );

        _content.Controls.Add(card);

        var developer = new CheckBox
        {
            Text = "Developer Mode",
            Checked = _settings.DeveloperMode,
            Location = new Point(18, 24),
            AutoSize = true
        };

        developer.CheckedChanged += (_, _) =>
        {
            _settings.DeveloperMode =
                developer.Checked;

            SettingsStore.Save(
                _settings
            );
        };

        card.Controls.Add(developer);

        var reconnect = new CheckBox
        {
            Text = "Auto reconnect",
            Checked = _settings.AutoReconnect,
            Location = new Point(18, 55),
            AutoSize = true
        };

        reconnect.CheckedChanged += (_, _) =>
        {
            _settings.AutoReconnect =
                reconnect.Checked;

            SettingsStore.Save(
                _settings
            );
        };

        card.Controls.Add(reconnect);

        var clipboard = new CheckBox
        {
            Text = "Clipboard sync (best effort)",
            Checked = _settings.ClipboardSync,
            Location = new Point(18, 86),
            AutoSize = true
        };

        clipboard.CheckedChanged += (_, _) =>
        {
            _settings.ClipboardSync =
                clipboard.Checked;

            SettingsStore.Save(
                _settings
            );
        };

        card.Controls.Add(clipboard);

        var notifications = new CheckBox
        {
            Text =
                "Android notifications (best effort)",
            Checked = _settings.Notifications,
            Location = new Point(18, 117),
            AutoSize = true
        };

        notifications.CheckedChanged += (_, _) =>
        {
            _settings.Notifications =
                notifications.Checked;

            SettingsStore.Save(
                _settings
            );
        };

        card.Controls.Add(notifications);

        var backup = Btn(
            "Backup settings",
            18,
            165,
            150
        );

        backup.Click += (_, _) =>
        {
            using var dialog =
                new SaveFileDialog
                {
                    Filter = "JSON|*.json",
                    FileName =
                        "ryzoria-settings.json"
                };

            if (dialog.ShowDialog() !=
                DialogResult.OK)
            {
                return;
            }

            if (File.Exists(
                    SettingsStore.SettingsFile))
            {
                File.Copy(
                    SettingsStore.SettingsFile,
                    dialog.FileName,
                    true
                );
            }
        };

        card.Controls.Add(backup);

        var restore = Btn(
            "Restore settings",
            180,
            165,
            150
        );

        restore.Click += (_, _) =>
        {
            using var dialog =
                new OpenFileDialog
                {
                    Filter = "JSON|*.json"
                };

            if (dialog.ShowDialog() !=
                DialogResult.OK)
            {
                return;
            }

            File.Copy(
                dialog.FileName,
                SettingsStore.SettingsFile,
                true
            );

            MessageBox.Show(
                "Restored. Restart RyzoriaUI."
            );
        };

        card.Controls.Add(restore);

        var reset = Btn(
            "Reset all",
            342,
            165,
            130
        );

        reset.Click += (_, _) =>
        {
            SettingsStore.Reset();

            MessageBox.Show(
                "Reset complete. Restart RyzoriaUI."
            );
        };

        card.Controls.Add(reset);

        var info = LabelText(
            "Single-exe distribution: ADB + scrcpy are embedded and extracted only to a temporary session folder while running.",
            18,
            235
        );

        info.MaximumSize =
            new Size(740, 70);

        card.Controls.Add(info);
    }

    private void ShowAbout()
    {
        ResetContent(
            "About RyzoriaUI"
        );

        _content.Controls.Add(
            LabelText(
                "RYZORIAUI",
                10,
                52,
                FontStyle.Bold,
                28
            )
        );

        _content.Controls.Add(
            LabelText(
                "v1.7 • Android Desktop Experience",
                10,
                95,
                FontStyle.Regular,
                13
            )
        );

        _content.Controls.Add(
            LabelText(
                "Developed by Riyzz",
                10,
                135
            )
        );

        _content.Controls.Add(
            LabelText(
                "github.com/riyzzmcganteng",
                10,
                160
            )
        );

        _content.Controls.Add(
            LabelText(
                "Pengembangan Riyzz / Belajar pemula menjadi pro",
                10,
                185
            )
        );

        _content.Controls.Add(
            LabelText(
                "Designed for low-end Windows PCs such as Celeron N2040 + 4 GB RAM.",
                10,
                230
            )
        );
    }

    private void ShowSearch(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            ShowDashboard();
            return;
        }

        var value =
            query.Trim().ToLowerInvariant();

        if (value.Contains("android") ||
            value.Contains("adb"))
        {
            ShowAndroid();
        }
        else if (value.Contains("file"))
        {
            ShowFiles();
        }
        else if (value.Contains("game"))
        {
            ShowGaming();
        }
        else if (value.Contains("map"))
        {
            ShowMapper();
        }
        else if (value.Contains("diag"))
        {
            ShowDiagnostics();
        }
        else if (value.Contains("setting"))
        {
            ShowSettings();
        }
        else
        {
            MessageBox.Show(
                "No RyzoriaUI page matched the search."
            );
        }
    }

    private AndroidDevice? SelectedDevice()
    {
        if (_devices.Count == 0)
            return null;

        if (_deviceBox.SelectedIndex >= 0 &&
            _deviceBox.SelectedIndex <
            _devices.Count)
        {
            return _devices[
                _deviceBox.SelectedIndex
            ];
        }

        return _devices.FirstOrDefault(
            x => x.Authorized
        );
    }

    private async Task RefreshDevices()
    {
        try
        {
            var devices =
                await _adb.GetDevicesAsync();

            _devices = devices;

            _deviceBox.Items.Clear();

            foreach (var device in _devices)
            {
                _deviceBox.Items.Add(
                    $"{device.Model} • " +
                    $"{device.Serial} • " +
                    $"{device.State}"
                );
            }

            if (_deviceBox.Items.Count > 0)
            {
                _deviceBox.SelectedIndex = 0;
            }

            var authorized =
                _devices.Any(
                    x => x.Authorized
                );

            _deviceStatus.Text =
                authorized
                    ? $"● Android: {_devices.Count(x => x.Authorized)} connected"
                    : "● Android: not connected";

            _deviceStatus.ForeColor =
                authorized
                    ? Color.LightGreen
                    : Color.Gold;

            Logger.Info(
                $"Device scan: {_devices.Count}"
            );
        }
        catch (Exception ex)
        {
            _deviceStatus.Text =
                "● ADB: error";

            _deviceStatus.ForeColor =
                Color.Red;

            Logger.Error(
                ex.ToString()
            );
        }
    }

    private async Task DeviceAction(
        Func<string, Task> action)
    {
        var device =
            SelectedDevice();

        if (device is null)
        {
            MessageBox.Show(
                "Connect and authorize an Android device first."
            );

            return;
        }

        if (!device.Authorized)
        {
            MessageBox.Show(
                "Android device belum authorized."
            );

            return;
        }

        try
        {
            await action(
                device.Serial
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "RyzoriaUI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private async Task StartMirror()
    {
        var device =
            SelectedDevice();

        if (device is null)
        {
            MessageBox.Show(
                "Connect and authorize an Android device first."
            );

            return;
        }

        if (!device.Authorized)
        {
            MessageBox.Show(
                "Android device belum authorized."
            );

            return;
        }

        var preset =
            new PerformancePreset(
                "Current",
                _settings.ScrcpyMaxSize,
                _settings.ScrcpyMaxFps,
                _settings.ScrcpyBitrateMbps,
                _settings.NoAudio
            );

        try
        {
            await _scrcpy.StartAsync(
                device,
                preset,
                _settings.Fullscreen,
                _settings.TurnScreenOff,
                _settings.NoAudio
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "RyzoriaUI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void ApplyPreset(string name)
    {
        if (!string.Equals(
                name,
                "Ultra Low",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _settings.PerformanceMode =
            "Ultra Low";

        _settings.ScrcpyMaxSize = 480;
        _settings.ScrcpyMaxFps = 20;
        _settings.ScrcpyBitrateMbps = 2;
        _settings.NoAudio = true;

        SettingsStore.Save(
            _settings
        );

        MessageBox.Show(
            "Ultra Low mode enabled."
        );
    }

    private async Task TakeScreenshot()
    {
        var device =
            SelectedDevice();

        if (device is null)
        {
            MessageBox.Show(
                "No Android device."
            );

            return;
        }

        using var dialog =
            new SaveFileDialog
            {
                Filter = "PNG|*.png",
                FileName =
                    "ryzoria-screenshot.png"
            };

        if (dialog.ShowDialog() !=
            DialogResult.OK)
        {
            return;
        }

        try
        {
            var success =
                await _adb.ScreenshotAsync(
                    device.Serial,
                    dialog.FileName
                );

            MessageBox.Show(
                success
                    ? "Screenshot saved."
                    : "Screenshot failed."
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "RyzoriaUI",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
        }
    }

    private void StartPolling()
    {
        _ = RefreshDevices();

        _deviceTimer =
            new System.Threading.Timer(
                async _ =>
                {
                    if (!_settings.AutoReconnect)
                        return;

                    try
                    {
                        var devices =
                            await _adb.GetDevicesAsync();

                        if (IsDisposed ||
                            !IsHandleCreated)
                        {
                            return;
                        }

                        BeginInvoke(
                            new Action(() =>
                            {
                                if (IsDisposed)
                                    return;

                                _devices = devices;

                                var authorized =
                                    _devices.Any(
                                        x => x.Authorized
                                    );

                                _deviceStatus.Text =
                                    authorized
                                        ? $"● Android: {_devices.Count(x => x.Authorized)} connected"
                                        : "● Android: not connected";

                                _deviceStatus.ForeColor =
                                    authorized
                                        ? Color.LightGreen
                                        : Color.Gold;

                                if (_deviceBox.Items.Count !=
                                    _devices.Count)
                                {
                                    _ = RefreshDevices();
                                }
                            })
                        );
                    }
                    catch
                    {
                        // Polling berjalan di background.
                    }
                },
                null,
                3000,
                8000
            );
    }

    private static string? Prompt(
        string title,
        string defaultValue)
    {
        using var form = new Form
        {
            Text = title,
            StartPosition =
                FormStartPosition.CenterParent,
            Width = 360,
            Height = 150,
            BackColor =
                Color.FromArgb(16, 23, 34),
            ForeColor = Color.White,
            FormBorderStyle =
                FormBorderStyle.FixedDialog
        };

        var text = new TextBox
        {
            Left = 16,
            Top = 18,
            Width = 310,
            Text = defaultValue,
            BackColor =
                Color.FromArgb(24, 31, 43),
            ForeColor = Color.White
        };

        var ok = new Button
        {
            Text = "OK",
            Left = 168,
            Top = 58,
            Width = 75,
            DialogResult =
                DialogResult.OK
        };

        var cancel = new Button
        {
            Text = "Cancel",
            Left = 251,
            Top = 58,
            Width = 75,
            DialogResult =
                DialogResult.Cancel
        };

        form.Controls.AddRange(
            new Control[]
            {
                text,
                ok,
                cancel
            }
        );

        form.AcceptButton = ok;
        form.CancelButton = cancel;

        return form.ShowDialog() ==
               DialogResult.OK
            ? text.Text
            : null;
    }
}