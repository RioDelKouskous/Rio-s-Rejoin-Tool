using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Forms;
using System.ComponentModel;

namespace RejoinTool
{
    public class MainForm : Form
    {
        private readonly Label urlLabel;
        private readonly TextBox urlTextBox;
        private readonly Button toggleButton;
        private readonly Button selectAreaButton;
        private readonly Label areaLabel;
        private readonly LogViewer logViewer;
        private readonly System.Windows.Forms.Timer pollTimer;
        private readonly System.Windows.Forms.Timer clickTimer;
        private bool monitoring;
        private bool pendingClick;
        private int clickStage = -1;
        private DateTime clickDueTime;
        private Rectangle clickArea;
        private readonly string settingsPath;

        public MainForm()
        {
            Text = "Rio's Rejoin Tool";
            ClientSize = new Size(560, 450);
            MinimumSize = new Size(560, 450);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            StartPosition = FormStartPosition.CenterScreen;
            Icon = LoadStartupIcon();
            BackColor = Color.Black;
            ForeColor = Color.White;
            DoubleBuffered = true;

            // Load background
            try
            {
                var bgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Background.jpg");
                if (File.Exists(bgPath))
                {
                    BackgroundImage = Image.FromFile(bgPath);
                    BackgroundImageLayout = ImageLayout.Zoom;
                }
            }
            catch { }

            urlLabel = new Label
            {
                Text = "Private server URL / Roblox game link:",
                AutoSize = true,
                Location = new Point(15, 15),
                BackColor = Color.Transparent,
                ForeColor = Color.FromArgb(220, 220, 220),
                Font = new Font("Segoe UI", 10, FontStyle.Regular)
            };

            urlTextBox = new TextBox
            {
                Location = new Point(15, 40),
                Width = 530,
                Height = 32,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                BackColor = Color.FromArgb(55, 65, 80),
                ForeColor = Color.White,
                BorderStyle = BorderStyle.None,
                Font = new Font("Segoe UI", 10),
                Padding = new Padding(8)
            };
            urlTextBox.TextChanged += UrlTextBox_TextChanged;

            selectAreaButton = new Button
            {
                Text = "Select Area",
                Location = new Point(15, 85),
                Size = new Size(155, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                BackColor = Color.FromArgb(100, 150, 200),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            selectAreaButton.MouseEnter += (s, e) => selectAreaButton.BackColor = Color.FromArgb(130, 180, 230);
            selectAreaButton.MouseLeave += (s, e) => selectAreaButton.BackColor = Color.FromArgb(100, 150, 200);
            selectAreaButton.Click += SelectAreaButton_Click;

            toggleButton = new Button
            {
                Text = "Start",
                Location = new Point(390, 85),
                Size = new Size(155, 38),
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                BackColor = Color.FromArgb(50, 200, 100),
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Cursor = Cursors.Hand,
            };
            toggleButton.MouseEnter += (s, e) => toggleButton.BackColor = Color.FromArgb(80, 230, 130);
            toggleButton.MouseLeave += (s, e) => toggleButton.BackColor = Color.FromArgb(50, 200, 100);
            toggleButton.Click += ToggleButton_Click;

            areaLabel = new Label
            {
                Text = "Click area: not selected",
                AutoSize = true,
                Location = new Point(15, 135),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                            
                ForeColor = Color.FromArgb(150, 180, 200),
                Font = new Font("Segoe UI", 9, FontStyle.Italic)
            };

            logViewer = new LogViewer
            {
                Location = new Point(15, 165),
                Size = new Size(530, 260),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                Font = new Font("Consolas", 9),
                ForeColor = Color.FromArgb(180, 180, 180),
                Padding = new Padding(8),
            };

            // Panels with soft blurred background behind inputs
            var topPanel = new BlurredPanel
            {
                Location = new Point(10, 34),
                Size = new Size(540, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                // let BlurredPanel read the form background itself for correct sampling
                SourceImage = null,
                Overlay = Color.FromArgb(160, 20, 25, 35),
                BlurDownscale = 20
            };
            urlTextBox.Parent = topPanel;
            urlTextBox.Location = new Point(8, 6);
            urlTextBox.Width = topPanel.Width - 16;
            urlTextBox.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;

            var logPanel = new BlurredPanel
            {
                Location = new Point(10, 160),
                Size = new Size(540, 260),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                SourceImage = null,
                Overlay = Color.FromArgb(140, 18, 22, 30),
                BlurDownscale = 30
            };
            logViewer.Parent = logPanel;
            logViewer.Location = new Point(8, 8);
            logViewer.Size = new Size(logPanel.Width - 16, logPanel.Height - 16);
            logViewer.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

            Controls.Add(urlLabel);
            Controls.Add(topPanel);
            Controls.Add(selectAreaButton);
            Controls.Add(toggleButton);
            Controls.Add(areaLabel);
            Controls.Add(logPanel);

            settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            LoadSettings();

            pollTimer = new System.Windows.Forms.Timer { Interval = 3000 };
            pollTimer.Tick += PollTimer_Tick;

            clickTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            clickTimer.Tick += ClickTimer_Tick;

            AddLog("Ready. Enter your private server link, select a click area, and click Start.");
            AddLog("This tool checks Roblox every 3 seconds, launches your link if Roblox is not running, and clicks the selected area about 45 seconds after launch.");
        }

        private void SelectAreaButton_Click(object? sender, EventArgs e)
        {
            SelectClickArea();
        }

        private static Icon LoadStartupIcon()
        {
            try
            {
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var iconPath = Path.Combine(baseDir, "icon.ico");
                if (File.Exists(iconPath))
                {
                    using var iconFile = new Icon(iconPath);
                    return (Icon)iconFile.Clone();
                }

                iconPath = Path.Combine(baseDir, "icon.png");
                if (File.Exists(iconPath))
                {
                    using var bitmap = new Bitmap(iconPath);
                    var hIcon = bitmap.GetHicon();
                    try
                    {
                        using var loadedIcon = Icon.FromHandle(hIcon);
                        return (Icon)loadedIcon.Clone();
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }

                return SystemIcons.Application;
            }
            catch
            {
                return SystemIcons.Application;
            }
        }

        private void ToggleButton_Click(object? sender, EventArgs e)
        {
            if (monitoring)
            {
                StopMonitoring();
            }
            else
            {
                StartMonitoring();
            }
        }

        private void StartMonitoring()
        {
            if (string.IsNullOrWhiteSpace(urlTextBox.Text))
            {
                AddLog("Please enter a Roblox private server link before starting.");
                return;
            }

            if (clickArea.IsEmpty)
            {
                AddLog("Please select the Roblox 'Start' click area before starting.");
                return;
            }

            monitoring = true;
            urlTextBox.Enabled = false;
            selectAreaButton.Enabled = false;
            toggleButton.Text = "Stop";
            AddLog("Monitoring started.");
            TryLaunchIfNeeded();
            pollTimer.Start();
        }

        private void StopMonitoring()
        {
            monitoring = false;
            pendingClick = false;
            clickStage = -1;
            clickTimer.Stop();
            pollTimer.Stop();
            urlTextBox.Enabled = true;
            selectAreaButton.Enabled = true;
            toggleButton.Text = "Start";
            AddLog("Monitoring stopped.");
        }

        private void PollTimer_Tick(object? sender, EventArgs e)
        {
            if (monitoring)
                TryLaunchIfNeeded();
        }

        private void ClickTimer_Tick(object? sender, EventArgs e)
        {
            if (!pendingClick)
                return;

            if (DateTime.Now < clickDueTime)
                return;

            if (!IsRobloxRunning())
            {
                AddLog("Click delay elapsed, but Roblox is not running yet. Waiting for Roblox to open.");
                return;
            }

            PerformClickSequence();
        }

        private void TryLaunchIfNeeded()
        {
            if (pendingClick)
            {
                if (!IsRobloxRunning())
                {
                    AddLog("Roblox launch is in progress. Waiting for the game to start.");
                    return;
                }

                if (DateTime.Now < clickDueTime)
                {
                    var remaining = (int)(clickDueTime - DateTime.Now).TotalSeconds;
                    AddLog($"Roblox is running. Click scheduled in {remaining} seconds.");
                    return;
                }

                return;
            }

            if (IsRobloxRunning())
            {
                AddLog("Roblox is running. Waiting.");
                return;
            }

            AddLog("Roblox is not running. Launching the link.");
            LaunchRoblox();
        }

        private void LaunchRoblox()
        {
            var rawUrl = urlTextBox.Text.Trim();
            if (string.IsNullOrEmpty(rawUrl))
            {
                AddLog("The URL is empty. Stopping monitoring.");
                StopMonitoring();
                return;
            }

            if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out var uri))
            {
                if (!Uri.TryCreate("https://" + rawUrl, UriKind.Absolute, out uri))
                {
                    AddLog("Invalid URL format. Enter a full Roblox private server link.");
                    StopMonitoring();
                    return;
                }

                rawUrl = "https://" + rawUrl;
            }

            try
            {
                var startInfo = new ProcessStartInfo(rawUrl)
                {
                    UseShellExecute = true
                };
                Process.Start(startInfo);
                pendingClick = true;
                clickStage = 0;
                clickDueTime = DateTime.Now.AddSeconds(45);
                clickTimer.Start();
                AddLog("Launch request sent. Waiting for Roblox process and delayed click.");
            }
            catch (Exception ex)
            {
                AddLog($"Failed to launch Roblox: {ex.Message}");
                StopMonitoring();
            }
        }

        private void PerformClickSequence()
        {
            var target = new Point(clickArea.Left + clickArea.Width / 2, clickArea.Top + clickArea.Height / 2);

            if (clickStage == 0)
            {
                AddLog($"Moving cursor to selected area at {target.X}, {target.Y}.");
                SmoothMoveCursor(target, 20, 10);
                clickStage = 1;
                clickDueTime = DateTime.Now.AddSeconds(1);
                return;
            }

            if (clickStage >= 1 && clickStage <= 3)
            {
                AddLog($"Performing click {clickStage} at {target.X}, {target.Y}.");
                MouseClickAt(target);
                clickStage++;
                if (clickStage <= 3)
                {
                    clickDueTime = DateTime.Now.AddSeconds(1);
                    return;
                }
            }

            AddLog("Triple click sequence complete. Resuming Roblox monitoring.");
            pendingClick = false;
            clickStage = -1;
            clickTimer.Stop();
        }

        private void SmoothMoveCursor(Point target, int steps, int delayMs)
        {
            if (!GetCursorPos(out var current))
                return;

            var startX = current.X;
            var startY = current.Y;
            int prevX = startX;
            int prevY = startY;

            for (int i = 1; i <= steps; i++)
            {
                var progress = i / (double)steps;
                var x = (int)Math.Round(startX + (target.X - startX) * progress);
                var y = (int)Math.Round(startY + (target.Y - startY) * progress);

                var dx = x - prevX;
                var dy = y - prevY;
                if (dx != 0 || dy != 0)
                {
                    mouse_event(MouseEventMove, (uint)dx, (uint)dy, 0, UIntPtr.Zero);
                    prevX = x;
                    prevY = y;
                }

                System.Threading.Thread.Sleep(delayMs);
            }

            // ensure final position accurate
            SetCursorPos(target.X, target.Y);
        }

        private static void MouseClickAt(Point point)
        {
            SetCursorPos(point.X, point.Y);
            mouse_event(MouseEventLeftDown | MouseEventLeftUp, 0, 0, 0, UIntPtr.Zero);
        }

        private void SelectClickArea()
        {
            using var selector = new ClickAreaSelectorForm();
            if (selector.ShowDialog(this) != DialogResult.OK)
                return;

            clickArea = selector.SelectedArea;
            areaLabel.Text = $"Click area: {clickArea.Width}x{clickArea.Height} at {clickArea.Left},{clickArea.Top}";
            SaveSettings();
            AddLog("Click area selected. The tool will click the center of this area after launching Roblox.");
        }

        private void UrlTextBox_TextChanged(object? sender, EventArgs e)
        {
            SaveSettings();
        }

        private void LoadSettings()
        {
            try
            {
                if (!File.Exists(settingsPath))
                    return;

                var json = File.ReadAllText(settingsPath);
                var settings = JsonSerializer.Deserialize<SettingsData>(json);
                if (settings == null)
                    return;

                urlTextBox.Text = settings.Url ?? string.Empty;
                clickArea = settings.ClickArea;
                if (!clickArea.IsEmpty)
                {
                    areaLabel.Text = $"Click area: {clickArea.Width}x{clickArea.Height} at {clickArea.Left},{clickArea.Top}";
                }
            }
            catch
            {
                // ignore invalid or corrupted settings
            }
        }

        private void SaveSettings()
        {
            try
            {
                var settings = new SettingsData
                {
                    Url = urlTextBox.Text ?? string.Empty,
                    ClickArea = clickArea
                };

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsPath, json);
            }
            catch
            {
                // ignore save failures
            }
        }

        private static bool IsRobloxRunning()
        {
            try
            {
                return Process.GetProcessesByName("RobloxPlayerBeta").Any() ||
                       Process.GetProcessesByName("RobloxStudioBeta").Any() ||
                       Process.GetProcessesByName("RobloxBrowserBeta").Any();
            }
            catch
            {
                return false;
            }
        }

        private void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            logViewer.AppendText($"{timestamp} - {message}{Environment.NewLine}");
        }

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, uint dx, uint dy, uint dwData, UIntPtr dwExtraInfo);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr handle);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        private const uint MouseEventLeftDown = 0x0002;
        private const uint MouseEventLeftUp = 0x0004;
        private const uint MouseEventMove = 0x0001;
    }

    internal class LogViewer : Panel
    {
        private readonly System.Collections.Generic.List<string> lines = new System.Collections.Generic.List<string>();
        private int lineHeight;

        public LogViewer()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            DoubleBuffered = true;
            AutoScroll = true;
            BackColor = Color.Transparent;
            ForeColor = Color.FromArgb(180, 180, 180);
            Padding = new Padding(8);
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RecalculateLineHeight();
        }

        protected override void OnCreateControl()
        {
            base.OnCreateControl();
            RecalculateLineHeight();
        }

        private void RecalculateLineHeight()
        {
            if (Font != null)
            {
                var size = TextRenderer.MeasureText("Mg", Font);
                lineHeight = Math.Max(14, size.Height + 2);
            }
            else
            {
                lineHeight = 16;
            }
            UpdateScrollSize();
        }

        public void AppendText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            var parts = text.Replace("\r", "").Split('\n');
            foreach (var p in parts)
            {
                if (p.Length == 0) continue;
                lines.Add(p);
            }
            UpdateScrollSize();
            ScrollToBottom();
            Invalidate();
        }

        private void UpdateScrollSize()
        {
            var h = lines.Count * lineHeight + Padding.Top + Padding.Bottom;
            AutoScrollMinSize = new Size(0, h);
        }

        private void ScrollToBottom()
        {
            var y = Math.Max(0, AutoScrollMinSize.Height - ClientSize.Height);
            AutoScrollPosition = new Point(0, y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var clip = e.ClipRectangle;
            e.Graphics.TranslateTransform(AutoScrollPosition.X, AutoScrollPosition.Y);

            var y = Padding.Top;
            var textFormat = TextFormatFlags.WordBreak | TextFormatFlags.Left | TextFormatFlags.Top;
            var font = Font ?? new Font("Consolas", 9);
            var brush = new SolidBrush(ForeColor);
            var lineRectWidth = ClientSize.Width - Padding.Left - Padding.Right;

            foreach (var line in lines)
            {
                var rect = new Rectangle(Padding.Left, y, lineRectWidth, lineHeight * 3);
                TextRenderer.DrawText(e.Graphics, line, font, rect, ForeColor, textFormat);
                y += lineHeight;
            }
            brush.Dispose();
            e.Graphics.ResetTransform();
        }
    }

    internal class RoundedButton : Button
    {
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
        public int CornerRadius { get; set; } = 8;

        public RoundedButton()
        {
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            FlatAppearance.BorderColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent?.BackColor ?? Color.White);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

            var path = GetRoundedRectanglePath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius);
            using (var brush = new SolidBrush(BackColor))
            {
                e.Graphics.FillPath(brush, path);
            }

            var textSize = e.Graphics.MeasureString(Text, Font);
            var textX = (Width - textSize.Width) / 2;
            var textY = (Height - textSize.Height) / 2;

            using (var textBrush = new SolidBrush(ForeColor))
            {
                e.Graphics.DrawString(Text, Font, textBrush, textX, textY);
            }

            using (var pen = new Pen(BackColor))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        private static GraphicsPath GetRoundedRectanglePath(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            var diameter = radius * 2;

            path.AddArc(rect.X, rect.Y, diameter, diameter, 180, 90);
            path.AddArc(rect.Right - diameter, rect.Y, diameter, diameter, 270, 90);
            path.AddArc(rect.Right - diameter, rect.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rect.X, rect.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();

            return path;
        }
    }

    internal class BlurredPanel : Panel
    {
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Image? SourceImage { get; set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Color Overlay { get; set; } = Color.FromArgb(140, 20, 25, 35);

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int BlurDownscale { get; set; } = 10;

        private Form? ownerForm;

        public BlurredPanel()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }

        protected override void OnParentChanged(EventArgs e)
        {
            base.OnParentChanged(e);
            HookOwnerForm();
        }

        private void HookOwnerForm()
        {
            if (ownerForm != null)
            {
                ownerForm.Resize -= OwnerForm_Invalidate;
                ownerForm.BackgroundImageChanged -= OwnerForm_Invalidate;
            }

            ownerForm = FindForm();
            if (ownerForm != null)
            {
                ownerForm.Resize += OwnerForm_Invalidate;
                ownerForm.BackgroundImageChanged += OwnerForm_Invalidate;
            }
            Invalidate();
        }

        private void OwnerForm_Invalidate(object? s, EventArgs e) => Invalidate();

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            Invalidate();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            try
            {
                Image? img = SourceImage ?? FindForm()?.BackgroundImage;
                if (img == null)
                {
                    using var b = new SolidBrush(Overlay);
                    e.Graphics.FillRectangle(b, ClientRectangle);
                    return;
                }

                var form = FindForm();
                Rectangle srcRect = new Rectangle(0, 0, img.Width, img.Height);

                if (form != null && form.ClientSize.Width > 0 && form.ClientSize.Height > 0)
                {
                    // compute this control's location relative to form client area
                    var controlScreen = RectangleToScreen(ClientRectangle);
                    var formScreen = form.RectangleToScreen(form.ClientRectangle);
                    var offset = new Point(controlScreen.Left - formScreen.Left, controlScreen.Top - formScreen.Top);

                    // compute how the background image is displayed in the form (respecting ImageLayout)
                    Rectangle imgDisplay;
                    var layout = form.BackgroundImageLayout;
                    if (layout == ImageLayout.Stretch || layout == ImageLayout.None || layout == ImageLayout.Tile)
                    {
                        imgDisplay = new Rectangle(0, 0, form.ClientSize.Width, form.ClientSize.Height);
                    }
                    else if (layout == ImageLayout.Center)
                    {
                        int x = (form.ClientSize.Width - img.Width) / 2;
                        int y = (form.ClientSize.Height - img.Height) / 2;
                        imgDisplay = new Rectangle(x, y, img.Width, img.Height);
                    }
                    else // Zoom
                    {
                        double scale = Math.Min(form.ClientSize.Width / (double)img.Width, form.ClientSize.Height / (double)img.Height);
                        int w = (int)Math.Round(img.Width * scale);
                        int h = (int)Math.Round(img.Height * scale);
                        int x = (form.ClientSize.Width - w) / 2;
                        int y = (form.ClientSize.Height - h) / 2;
                        imgDisplay = new Rectangle(x, y, w, h);
                    }

                    // find intersection of our control with the displayed image area
                    var relative = new Rectangle(offset.X - imgDisplay.X, offset.Y - imgDisplay.Y, Width, Height);
                    var intersect = Rectangle.Intersect(new Rectangle(0, 0, imgDisplay.Width, imgDisplay.Height), relative);
                    if (intersect.Width > 0 && intersect.Height > 0)
                    {
                        double scaleX = img.Width / (double)imgDisplay.Width;
                        double scaleY = img.Height / (double)imgDisplay.Height;

                        int sx = (int)Math.Round((intersect.X) * scaleX);
                        int sy = (int)Math.Round((intersect.Y) * scaleY);
                        int sw = (int)Math.Round(intersect.Width * scaleX);
                        int sh = (int)Math.Round(intersect.Height * scaleY);

                        sx = Math.Max(0, Math.Min(sx, img.Width - 1));
                        sy = Math.Max(0, Math.Min(sy, img.Height - 1));
                        sw = Math.Max(1, Math.Min(sw, img.Width - sx));
                        sh = Math.Max(1, Math.Min(sh, img.Height - sy));

                        srcRect = new Rectangle(sx, sy, sw, sh);
                    }
                    else
                    {
                        // nothing overlaps the image -- use a tiny sample
                        srcRect = new Rectangle(0, 0, Math.Min(8, img.Width), Math.Min(8, img.Height));
                    }
                }

                int smallW = Math.Max(1, Width / BlurDownscale);
                int smallH = Math.Max(1, Height / BlurDownscale);

                using var small = new Bitmap(smallW, smallH);
                using (var g = Graphics.FromImage(small))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.DrawImage(img, new Rectangle(0, 0, smallW, smallH), srcRect, GraphicsUnit.Pixel);
                }

                e.Graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                e.Graphics.DrawImage(small, ClientRectangle);

                using var overlay = new SolidBrush(Overlay);
                e.Graphics.FillRectangle(overlay, ClientRectangle);
            }
            catch
            {
                base.OnPaintBackground(e);
            }
        }
    }

    internal class SettingsData
    {
        public string Url { get; set; } = string.Empty;
        public Rectangle ClickArea { get; set; }
    }

    internal class ClickAreaSelectorForm : Form
    {
        private bool isDragging;
        private Point dragStart;
        private Point dragEnd;
        public Rectangle SelectedArea { get; private set; }

        public ClickAreaSelectorForm()
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
            BackColor = Color.Black;
            Opacity = 0.25;
            TopMost = true;
            DoubleBuffered = true;
            ShowInTaskbar = false;
            Cursor = Cursors.Cross;
            Text = "Select the Roblox Start button area and release.";
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            AddInstructionLabel();
        }

        private void AddInstructionLabel()
        {
            var label = new Label
            {
                Text = "Drag a rectangle around the Roblox Start button, then release the mouse.",
                AutoSize = true,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(120, 0, 0, 0),
                Font = new Font(Font.FontFamily, 12, FontStyle.Bold),
                Location = new Point(20, 20)
            };
            Controls.Add(label);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left)
                return;

            isDragging = true;
            dragStart = e.Location;
            dragEnd = e.Location;
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!isDragging)
                return;

            dragEnd = e.Location;
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!isDragging)
                return;

            isDragging = false;
            dragEnd = e.Location;
            SelectedArea = GetSelectionRectangle(dragStart, dragEnd);

            if (SelectedArea.Width > 0 && SelectedArea.Height > 0)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!isDragging)
                return;

            var rect = GetSelectionRectangle(dragStart, dragEnd);
            using var pen = new Pen(Color.Lime, 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
            using var brush = new SolidBrush(Color.FromArgb(80, Color.Lime));
            e.Graphics.FillRectangle(brush, rect);
            e.Graphics.DrawRectangle(pen, rect);
        }

        private static Rectangle GetSelectionRectangle(Point p1, Point p2)
        {
            return new Rectangle(
                Math.Min(p1.X, p2.X),
                Math.Min(p1.Y, p2.Y),
                Math.Abs(p1.X - p2.X),
                Math.Abs(p1.Y - p2.Y));
        }
    }
}
