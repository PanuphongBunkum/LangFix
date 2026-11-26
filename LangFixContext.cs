namespace LangFix;

internal sealed class LangFixContext : ApplicationContext
{
    private readonly AppSettings settings;
    private readonly KeyboardService keyboard;
    private readonly NotifyIcon tray;
    private readonly SettingsForm window;
    private readonly ToolStripMenuItem enabledItem;
    private readonly ToolStripMenuItem automaticItem;
    private readonly ToolStripMenuItem updateItem;
    private readonly EventWaitHandle shutdownEvent;

    public LangFixContext()
    {
        settings = AppSettings.Load();
        window = new SettingsForm(settings);
        window.SettingsChanged += ApplySettings;
        _ = window.Handle;
        shutdownEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\LangFix-Shutdown-8E63E520");
        _ = Task.Run(() =>
        {
            try
            {
                shutdownEvent.WaitOne();
                if (!window.IsDisposed) window.BeginInvoke(new Action(ExitThread));
            }
            catch (ObjectDisposedException) { }
        });

        keyboard = new KeyboardService();
        keyboard.CorrectionMade += OnCorrectionMade;

        enabledItem = new ToolStripMenuItem("เปิดใช้งาน") { CheckOnClick = true };
        automaticItem = new ToolStripMenuItem("แก้อัตโนมัติ") { CheckOnClick = true };
        enabledItem.CheckedChanged += (_, _) => { settings.Enabled = enabledItem.Checked; SaveFromTray(); };
        automaticItem.CheckedChanged += (_, _) => { settings.AutomaticMode = automaticItem.Checked; SaveFromTray(); };

        var settingsItem = new ToolStripMenuItem("ตั้งค่า");
        settingsItem.Click += (_, _) => ShowSettings();
        updateItem = new ToolStripMenuItem("ตรวจสอบการอัปเดต");
        updateItem.Click += async (_, _) => await CheckForUpdatesAsync(showWhenCurrent: true);
        var feedbackItem = new ToolStripMenuItem("ส่งความคิดเห็น / แจ้งปัญหา");
        feedbackItem.Click += (_, _) => OpenWebsite(UpdateService.FeedbackUrl);
        var exitItem = new ToolStripMenuItem("ออกจากโปรแกรม");
        exitItem.Click += (_, _) => ExitThread();

        var menu = new ContextMenuStrip();
        menu.Items.AddRange([enabledItem, automaticItem, new ToolStripSeparator(), updateItem, feedbackItem, settingsItem, exitItem]);
        tray = new NotifyIcon
        {
            Text = "LangFix — ไทย ↔ อังกฤษ",
            Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application,
            ContextMenuStrip = menu,
            Visible = true
        };
        keyboard.CorrectionFailed += _ =>
        {
            tray.Tag = null;
            tray.ShowBalloonTip(3000, "LangFix แก้ข้อความไม่สำเร็จ", "ดูรายละเอียดได้จากไฟล์ error.log", ToolTipIcon.Error);
        };
        tray.DoubleClick += (_, _) => ShowSettings();
        tray.BalloonTipClicked += (_, _) => OpenUpdateWebsite();

        ApplySettings();
        StartupManager.SetEnabled(settings.StartWithWindows);
        if (!Environment.GetCommandLineArgs().Contains("--startup")) ShowSettings();
        _ = CheckForUpdatesAsync(showWhenCurrent: false);
    }

    private void ApplySettings()
    {
        keyboard.Enabled = settings.Enabled;
        keyboard.AutomaticMode = settings.AutomaticMode;
        keyboard.SwitchLayoutAfterCorrection = settings.SwitchLayoutAfterCorrection;
        keyboard.Shortcut = settings.GetShortcut();
        enabledItem.Checked = settings.Enabled;
        automaticItem.Checked = settings.AutomaticMode;
        tray.Text = settings.Enabled
            ? $"LangFix — {(settings.AutomaticMode ? "อัตโนมัติ" : $"กด {settings.GetShortcut().DisplayText}")}" : "LangFix — หยุดชั่วคราว";
    }

    private void SaveFromTray()
    {
        settings.Save();
        ApplySettings();
    }

    private void OnCorrectionMade(string original, string converted)
    {
        if (!settings.ShowNotifications) return;
        tray.Tag = null; // clicking this balloon must not open a stale update link
        tray.ShowBalloonTip(1200, "LangFix แก้ภาษาแล้ว", $"{original}  →  {converted}", ToolTipIcon.Info);
    }

    private void ShowSettings()
    {
        window.Show();
        window.Activate();
    }

    private async Task CheckForUpdatesAsync(bool showWhenCurrent)
    {
        if (!showWhenCurrent)
        {
            if (!settings.CheckForUpdatesAutomatically) return;
            if (DateTimeOffset.UtcNow - settings.LastUpdateCheckUtc < TimeSpan.FromHours(24)) return;
            await Task.Delay(2500);
        }

        updateItem.Enabled = false;
        updateItem.Text = "กำลังตรวจสอบ...";
        try
        {
            var latest = await UpdateService.GetLatestAsync();
            settings.LastUpdateCheckUtc = DateTimeOffset.UtcNow;
            settings.Save();
            if (UpdateService.IsNewerVersion(UpdateService.CurrentVersion, latest.Version))
            {
                tray.Tag = UpdateService.ResolveSafeDownloadUrl(latest.DownloadUrl);
                tray.ShowBalloonTip(5000, $"มี LangFix {latest.Version} รุ่นใหม่", latest.ReleaseNotes ?? "คลิกเพื่อเปิดหน้าดาวน์โหลด", ToolTipIcon.Info);
            }
            else if (showWhenCurrent)
            {
                MessageBox.Show($"คุณใช้ LangFix {UpdateService.CurrentVersion} ซึ่งเป็นรุ่นล่าสุดแล้ว", "LangFix", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            ErrorLog.Write(ex);
            if (showWhenCurrent)
                MessageBox.Show("ตรวจสอบการอัปเดตไม่สำเร็จ กรุณาตรวจการเชื่อมต่ออินเทอร์เน็ตแล้วลองใหม่", "LangFix", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        finally
        {
            updateItem.Text = "ตรวจสอบการอัปเดต";
            updateItem.Enabled = true;
        }
    }

    private void OpenUpdateWebsite()
    {
        if (tray.Tag is not string url || string.IsNullOrWhiteSpace(url)) return;
        OpenWebsite(url);
    }

    private static void OpenWebsite(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { ErrorLog.Write(ex); }
    }

    protected override void ExitThreadCore()
    {
        tray.Visible = false;
        tray.Dispose();
        keyboard.Dispose();
        window.Dispose();
        shutdownEvent.Dispose();
        base.ExitThreadCore();
    }
}
