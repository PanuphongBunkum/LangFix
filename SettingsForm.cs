namespace LangFix;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings settings;
    private readonly CheckBox enabled = new() { Text = "เปิดใช้งาน LangFix", AutoSize = true };
    private readonly CheckBox automatic = new() { Text = "แก้คำที่พิมพ์ผิดภาษาโดยอัตโนมัติ", AutoSize = true };
    private readonly CheckBox startup = new() { Text = "เปิด LangFix เมื่อเข้าสู่ Windows", AutoSize = true };
    private readonly CheckBox notifications = new() { Text = "แสดงการแจ้งเตือนเมื่อแก้คำ", AutoSize = true };
    private readonly CheckBox switchLayout = new() { Text = "เปลี่ยนภาษาแป้นพิมพ์ตามข้อความที่แก้แล้ว", AutoSize = true };
    private readonly CheckBox checkUpdates = new() { Text = "ตรวจสอบการอัปเดตโดยอัตโนมัติ", AutoSize = true };
    private readonly TextBox shortcutBox = new() { ReadOnly = true, Width = 230, TextAlign = HorizontalAlignment.Center, Cursor = Cursors.Hand };
    private readonly Label shortcutHint = new() { AutoSize = true, ForeColor = Color.DimGray, Text = "คลิกช่อง แล้วกดปุ่มร่วมกับ Ctrl, Alt หรือ Shift" };
    private ShortcutDefinition pendingShortcut = ShortcutDefinition.Default;

    public event Action? SettingsChanged;

    public SettingsForm(AppSettings settings)
    {
        this.settings = settings;
        Text = "LangFix — ตั้งค่า";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(520, 460);
        Font = new Font("Segoe UI", 10);
        Icon = TryLoadIcon();

        var title = new Label { Text = "LangFix", Font = new Font("Segoe UI Semibold", 22), AutoSize = true };
        var subtitle = new Label { Text = "แก้การพิมพ์ผิดภาษาระหว่างไทยและอังกฤษ", ForeColor = Color.DimGray, AutoSize = true };
        var developer = new Label { Text = "พัฒนาโดย Panuphong Bunkum", ForeColor = Color.FromArgb(23, 105, 255), AutoSize = true, Font = new Font("Segoe UI Semibold", 9) };
        var privacy = new Label
        {
            Text = "กดคีย์ลัดเพื่อแก้คำล่าสุด และกดซ้ำเพื่อย้อนกลับการแก้ครั้งล่าสุด\nข้อมูลทั้งหมดประมวลผลในเครื่องและไม่ถูกบันทึก",
            AutoSize = true,
            Padding = new Padding(10),
            BackColor = Color.FromArgb(238, 246, 255)
        };
        var dictionary = new Label
        {
            Text = $"พจนานุกรมอัตโนมัติ: {WordDictionaryService.SourceDescription}",
            ForeColor = Color.DimGray,
            AutoSize = true
        };
        var save = new Button { Text = "บันทึก", AutoSize = true, Padding = new Padding(18, 5, 18, 5) };
        save.Click += (_, _) => SaveAndHide();
        shortcutBox.KeyDown += CaptureShortcut;
        shortcutBox.PreviewKeyDown += (_, e) => e.IsInputKey = true;
        shortcutBox.Enter += (_, _) => shortcutBox.BackColor = Color.FromArgb(255, 249, 218);
        shortcutBox.Leave += (_, _) => shortcutBox.BackColor = SystemColors.Window;

        var shortcutPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0)
        };
        shortcutPanel.Controls.Add(new Label { Text = "คีย์ลัดแก้คำ / ย้อนกลับ", AutoSize = true, Font = new Font("Segoe UI Semibold", 10) });
        shortcutPanel.Controls.Add(shortcutBox);
        shortcutPanel.Controls.Add(shortcutHint);

        var layout = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            AutoScroll = true
        };
        layout.Controls.AddRange([title, subtitle, developer, dictionary, Spacer(8), enabled, automatic, switchLayout, startup, notifications, checkUpdates, Spacer(8), shortcutPanel, Spacer(8), privacy, Spacer(8), save]);
        Controls.Add(layout);
        LoadValues();

        FormClosing += (_, e) => { if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
    }

    private static Control Spacer(int height) => new Panel { Height = height, Width = 1 };

    private void LoadValues()
    {
        enabled.Checked = settings.Enabled;
        automatic.Checked = settings.AutomaticMode;
        startup.Checked = settings.StartWithWindows;
        notifications.Checked = settings.ShowNotifications;
        switchLayout.Checked = settings.SwitchLayoutAfterCorrection;
        checkUpdates.Checked = settings.CheckForUpdatesAutomatically;
        pendingShortcut = settings.GetShortcut();
        shortcutBox.Text = pendingShortcut.DisplayText;
    }

    private void CaptureShortcut(object? sender, KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;
        if (ShortcutDefinition.IsModifierKey(e.KeyCode)) return;

        var modifiers = e.Modifiers & (Keys.Control | Keys.Alt | Keys.Shift);
        var candidate = new ShortcutDefinition(e.KeyCode, modifiers);
        if (!candidate.IsValid)
        {
            shortcutHint.Text = "ต้องใช้ร่วมกับ Ctrl, Alt หรือ Shift อย่างน้อยหนึ่งปุ่ม";
            shortcutHint.ForeColor = Color.Firebrick;
            return;
        }

        pendingShortcut = candidate;
        shortcutBox.Text = candidate.DisplayText;
        shortcutHint.Text = "บันทึกแล้วคีย์ลัดใหม่จะเริ่มทำงานทันที";
        shortcutHint.ForeColor = Color.DarkGreen;
    }

    private void SaveAndHide()
    {
        settings.Enabled = enabled.Checked;
        settings.AutomaticMode = automatic.Checked;
        settings.StartWithWindows = startup.Checked;
        settings.ShowNotifications = notifications.Checked;
        settings.SwitchLayoutAfterCorrection = switchLayout.Checked;
        settings.ShortcutKey = (int)pendingShortcut.Key;
        settings.ShortcutModifiers = (int)pendingShortcut.Modifiers;
        settings.CheckForUpdatesAutomatically = checkUpdates.Checked;
        settings.Save();
        StartupManager.SetEnabled(settings.StartWithWindows);
        SettingsChanged?.Invoke();
        Hide();
    }

    private static Icon? TryLoadIcon()
    {
        try { return Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
        catch { return null; }
    }
}
