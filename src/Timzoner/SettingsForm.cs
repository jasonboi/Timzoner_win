using Microsoft.Win32;
using Timzoner.Core;

namespace Timzoner;

internal sealed class SettingsForm : Form
{
    private readonly AppController controller;
    private readonly Action saved;
    private readonly Func<Task<NetworkRefreshResult>> refreshNetwork;
    private readonly Func<string?> networkZoneName;
    private readonly Settings original;
    private readonly List<string> favorites;
    private readonly TextBox search = new() { Name = "Search", AccessibleName = "搜索时区", PlaceholderText = "搜索城市、时区名称或 ID", BorderStyle = BorderStyle.None, Dock = DockStyle.Fill, Margin = new Padding(3, 0, 3, 8) };
    private readonly ListBox available = new WrappingZoneListBox { Name = "AvailableZones", AccessibleName = "可用时区", Dock = DockStyle.Fill };
    private readonly ListBox selected = new WrappingZoneListBox { Name = "FavoriteZones", AccessibleName = "收藏时区", Dock = DockStyle.Fill };
    private readonly ComboBox theme = new FluentComboBox { Name = "Theme", AccessibleName = "外观主题", Width = 160 };
    private readonly CheckBox startup = new FluentCheckBox { Name = "StartWithWindows", Text = "登录 Windows 时启动" };
    private readonly Label availableHeading = Heading("可用时区");
    private readonly Label favoritesHeading = Heading("收藏时区");
    private readonly Label status = new() { Name = "Status", AutoSize = false, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AccessibleRole = AccessibleRole.StaticText };
    private readonly Label current = new() { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 14) };
    private readonly Button add = ActionButton("添加到收藏", "AddFavorite");
    private readonly Button remove = ActionButton("删除", "RemoveFavorite");
    private readonly Button up = ActionButton("上移", "MoveUp");
    private readonly Button down = ActionButton("下移", "MoveDown");
    private readonly Button save = ActionButton("保存", "SaveSettings", true);
    private readonly Button cancel = ActionButton("取消", "CancelSettings");
    private readonly Button languageButton = ActionButton("English", "LanguageToggle");
    private readonly Button networkRefresh = ActionButton("刷新网络环境", "RefreshNetwork");
    private readonly Label networkZone = new() { Name = "NetworkTimeZone", AutoSize = true, Margin = new Padding(3, 6, 3, 6) };
    private bool refreshingNetwork;
    private readonly Label title = new() { AutoSize = true, Margin = new Padding(0, 0, 0, 8) };
    private readonly Label orderHint = new() { AutoSize = true, Dock = DockStyle.Fill, Margin = new Padding(3, 7, 3, 7) };
    private readonly Label appearanceLabel = new() { AutoSize = true, Margin = new Padding(3, 7, 10, 3) };
    private readonly Label languageLabel = new() { AutoSize = true, Margin = new Padding(3, 7, 10, 3) };
    private string languageChoice;
    private bool statusIsError;
    private readonly Font titleFont;
    private readonly Font headingFont;

    public SettingsForm(AppController controller, Action saved, Icon icon,
        Func<Task<NetworkRefreshResult>> refreshNetwork, Func<string?> networkZoneName)
    {
        SuspendLayout();
        this.controller = controller;
        this.saved = saved;
        this.refreshNetwork = refreshNetwork;
        this.networkZoneName = networkZoneName;
        original = controller.GetSettings();
        languageChoice = original.Language;
        favorites = [.. original.FavoriteTimeZoneIds];
        Text = "Timzoner · 时区配置";
        Name = "SettingsWindow";
        Icon = icon;
        AutoScaleDimensions = new SizeF(96F, 96F);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(920, 650);
        MinimumSize = new Size(380, 320);
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(24);

        var viewport = new Panel { Name = "SettingsViewport", Dock = DockStyle.Fill, AutoScroll = true, Visible = false };
        var root = new TableLayoutPanel { Name = "SettingsContent", ColumnCount = 1, RowCount = 7, Margin = Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        titleFont = new Font(Font.FontFamily, 18, FontStyle.Bold);
        title.Font = titleFont;
        headingFont = new Font(Font, FontStyle.Bold);
        availableHeading.Font = favoritesHeading.Font = headingFont;
        root.Controls.Add(title, 0, 0);
        root.Controls.Add(current, 0, 1);

        var lists = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        var left = ListPanel(); left.Margin = new Padding(0, 0, 12, 0);
        left.Controls.Add(availableHeading, 0, 0);
        left.Controls.Add(new FluentFrame(search), 0, 1);
        left.Controls.Add(new FluentFrame(available), 0, 2);
        left.Controls.Add(Buttons(add), 0, 3);
        var right = ListPanel(); right.Margin = new Padding(12, 0, 0, 0);
        right.Controls.Add(favoritesHeading, 0, 0);
        right.Controls.Add(orderHint, 0, 1);
        right.Controls.Add(new FluentFrame(selected), 0, 2);
        right.Controls.Add(Buttons(up, down, remove), 0, 3);
        lists.Controls.Add(left, 0, 0); lists.Controls.Add(right, 1, 0);
        bool? stacked = null;
        lists.SizeChanged += (_, _) =>
        {
            bool narrow = viewport.ClientSize.Width < LogicalToDeviceUnits(780);
            if (stacked == narrow) return;
            stacked = narrow;
            lists.SuspendLayout();
            lists.Controls.Remove(right);
            lists.ColumnStyles.Clear(); lists.RowStyles.Clear();
            lists.ColumnCount = narrow ? 1 : 2;
            lists.RowCount = narrow ? 2 : 1;
            lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, narrow ? 100 : 50));
            lists.RowStyles.Add(new RowStyle(SizeType.Percent, narrow ? 50 : 100));
            if (narrow) lists.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            else lists.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            left.Margin = narrow ? new Padding(0, 0, 0, LogicalToDeviceUnits(6)) : new Padding(0, 0, LogicalToDeviceUnits(12), 0);
            right.Margin = narrow ? new Padding(0, LogicalToDeviceUnits(6), 0, 0) : new Padding(LogicalToDeviceUnits(12), 0, 0, 0);
            lists.Controls.Add(right, narrow ? 0 : 1, narrow ? 1 : 0);
            lists.ResumeLayout(true);
        };
        root.Controls.Add(lists, 0, 2);

        theme.Items.AddRange(["跟随系统", "浅色", "深色"]);
        theme.SelectedIndex = original.Theme switch { "light" => 1, "dark" => 2, _ => 0 };
        startup.Checked = original.StartWithWindows;
        var preferences = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 2, Margin = new Padding(0, 16, 0, 0) };
        preferences.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        preferences.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        preferences.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        appearanceLabel.Anchor = languageLabel.Anchor = AnchorStyles.Left;
        appearanceLabel.Margin = languageLabel.Margin = new Padding(3, 4, 16, 4);
        theme.Anchor = languageButton.Anchor = AnchorStyles.Left;
        theme.Margin = languageButton.Margin = new Padding(0, 4, 0, 4);
        startup.Anchor = AnchorStyles.Left;
        startup.Margin = new Padding(28, 4, 0, 4);
        preferences.Controls.Add(appearanceLabel, 0, 0);
        preferences.Controls.Add(theme, 1, 0);
        preferences.Controls.Add(startup, 2, 0);
        preferences.Controls.Add(languageLabel, 0, 1);
        preferences.Controls.Add(languageButton, 1, 1);
        root.Controls.Add(preferences, 0, 3);
        var network = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 1, RowCount = 2, Margin = new Padding(0, 8, 0, 0) };
        network.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        network.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        network.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        networkZone.Dock = DockStyle.Fill;
        network.Controls.Add(networkZone, 0, 0);
        network.Controls.Add(Buttons(networkRefresh), 0, 1);
        root.Controls.Add(network, 0, 4);
        root.Controls.Add(status, 0, 5);
        var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, Margin = Padding.Empty };
        footer.Controls.Add(cancel); footer.Controls.Add(save);
        root.Controls.Add(footer, 0, 6);
        viewport.Controls.Add(root);
        Controls.Add(viewport);
        bool sizingContent = false;
        void SizeContent()
        {
            if (sizingContent) return;
            sizingContent = true;
            try
            {
                bool narrow = viewport.ClientSize.Width < LogicalToDeviceUnits(780);
                int height = Math.Max(viewport.ClientSize.Height, LogicalToDeviceUnits(narrow ? 840 : 580));
                int scrollWidth = height > viewport.ClientSize.Height ? SystemInformation.VerticalScrollBarWidth : 0;
                root.Size = new Size(Math.Max(viewport.ClientSize.Width - scrollWidth, LogicalToDeviceUnits(280)), height);
                viewport.AutoScrollMinSize = new Size(0, height);
            }
            finally { sizingContent = false; }
            viewport.PerformLayout();
        }
        viewport.SizeChanged += (_, _) => SizeContent();
        SizeContent();
        AcceptButton = save; CancelButton = cancel;

        search.TextChanged += (_, _) => Filter();
        available.SelectedIndexChanged += (_, _) => UpdateButtons();
        selected.SelectedIndexChanged += (_, _) => UpdateButtons();
        available.DoubleClick += (_, _) => AddFavorite();
        add.Click += (_, _) => AddFavorite();
        remove.Click += (_, _) => RemoveFavorite();
        up.Click += (_, _) => MoveFavorite(-1); down.Click += (_, _) => MoveFavorite(1);
        selected.KeyDown += (_, e) => { if (e.KeyCode == Keys.Delete) { RemoveFavorite(); e.Handled = true; } };
        theme.SelectedIndexChanged += (_, _) => ApplyPreview();
        languageButton.Click += (_, _) =>
        {
            languageChoice = languageChoice == "en" ? "zh-CN" : "en";
            ApplyLanguage();
            SetStatus(T("语言已预览，保存后生效。", "Language preview. Save to keep this choice."));
        };
        save.Click += (_, _) => Save();
        networkRefresh.Click += async (_, _) =>
        {
            SetStatus("");
            var result = await refreshNetwork();
            if (IsDisposed) return;
            RefreshCurrent();
            RefreshNetworkZone();
            if (result.Result.Status is not (SwitchStatus.Success or SwitchStatus.Unchanged)) SetStatus(result.Result.Message, true);
        };
        cancel.Click += (_, _) => Close();
        SystemEvents.UserPreferenceChanged += PreferenceChanged;
        SystemEvents.TimeChanged += TimeChanged;
        FormClosed += (_, _) =>
        {
            Theme.Apply(controller.GetSettings().Theme);
            AppText.Language = controller.GetSettings().Language;
        };
        // Tiling window managers resize the window right after it appears. Content stays hidden until the size
        // has been stable briefly, so the lists lay out once at the final size instead of visibly jumping.
        var settle = new System.Windows.Forms.Timer { Interval = 50 };
        long shownAt = 0;
        void Reveal()
        {
            settle.Dispose();
            if (viewport.Visible) return;
            viewport.Visible = true;
            Theme.ApplyNative(viewport);
            search.Focus();
        }
        settle.Tick += (_, _) => Reveal();
        Shown += (_, _) => { shownAt = Environment.TickCount64; settle.Start(); };
        SizeChanged += (_, _) =>
        {
            if (viewport.Visible || shownAt == 0) return;
            settle.Stop();
            if (Environment.TickCount64 - shownAt < 300) settle.Start(); else Reveal();
        };
        FormClosed += (_, _) => settle.Dispose();
        ApplyLanguage();
        if (controller.Notice is { } notice) SetStatus(notice, true);
        save.Enabled = !controller.IsReadOnly;
        ResumeLayout(true);
        ApplyPreview();
    }

    private static string T(string chinese, string english) => AppText.Get(chinese, english);
    private static Zone DisplayZone(Zone zone) => AppText.IsEnglish ? zone with { Name = zone.EnglishName } : zone;
    private void ApplyLanguage()
    {
        AppText.Language = languageChoice;
        Text = T("Timzoner · 时区配置", "Timzoner · Time zone settings");
        title.Text = T("时区配置", "Time zone settings");
        search.PlaceholderText = T("搜索城市、时区名称或 ID", "Search a city, time zone or ID");
        search.AccessibleName = T("搜索时区", "Search time zones");
        available.AccessibleName = T("可用时区", "Available time zones");
        selected.AccessibleName = T("收藏时区", "Favorite time zones");
        orderHint.Text = T("与托盘菜单顺序一致", "Shown in this order in the tray menu");
        appearanceLabel.Text = T("外观", "Appearance");
        languageLabel.Text = T("界面语言", "Language");
        startup.Text = T("登录 Windows 时启动", "Start when I sign in to Windows");
        theme.AccessibleName = T("外观主题", "Appearance theme");
        int index = theme.SelectedIndex;
        theme.Items.Clear();
        theme.Items.AddRange([T("跟随系统", "System"), T("浅色", "Light"), T("深色", "Dark")]);
        theme.SelectedIndex = Math.Max(0, index);
        void Caption(Button button, string text) { button.Text = text; button.AccessibleName = text; }
        Caption(add, T("添加到收藏", "Add favorite"));
        Caption(remove, T("删除", "Remove"));
        Caption(up, T("上移", "Move up"));
        Caption(down, T("下移", "Move down"));
        Caption(save, T("保存", "Save"));
        Caption(cancel, T("取消", "Cancel"));
        Caption(languageButton, AppText.IsEnglish ? "简体中文" : "English");
        SetNetworkRefreshing(refreshingNetwork);
        RefreshNetworkZone();
        int selectedIndex = selected.SelectedIndex;
        Filter(); RefreshFavorites(selectedIndex); RefreshCurrent();
        ApplyPreview();
    }

    private static Label Heading(string text) => new() { Text = text, AutoSize = true, Margin = new Padding(3, 0, 3, 8) };
    private static Button ActionButton(string text, string name, bool accent = false) => new FluentButton { Text = text, Name = name, AccessibleName = text, Accent = accent };
    private static TableLayoutPanel ListPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        return panel;
    }
    private static FlowLayoutPanel Buttons(params Button[] buttons)
    {
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0, 8, 0, 0) };
        panel.Controls.AddRange(buttons); return panel;
    }

    private void Filter()
    {
        string query = search.Text.Trim();
        var matches = controller.Zones.Where(z => z.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || z.Id.Contains(query, StringComparison.OrdinalIgnoreCase)).Select(DisplayZone).ToArray();
        available.BeginUpdate(); available.Items.Clear(); available.Items.AddRange(matches); available.EndUpdate();
        availableHeading.Text = matches.Length == 0 ? T("没有匹配的时区", "No matching time zones") : T($"可用时区 · {matches.Length}", $"Available time zones · {matches.Length}");
        if (matches.Length > 0) available.SelectedIndex = 0;
        UpdateButtons();
    }
    private void RefreshFavorites(int index = -1)
    {
        selected.BeginUpdate(); selected.Items.Clear();
        foreach (var id in favorites)
        {
            var zone = controller.Zones.FirstOrDefault(z => string.Equals(z.Id, id, StringComparison.OrdinalIgnoreCase));
            selected.Items.Add(zone is null ? new Zone(id, id + T("（不可用）", " (unavailable)")) : DisplayZone(zone));
        }
        selected.EndUpdate();
        if (index >= 0 && selected.Items.Count > 0) selected.SelectedIndex = Math.Min(index, selected.Items.Count - 1);
        favoritesHeading.Text = T($"收藏时区 · {favorites.Count}", $"Favorite time zones · {favorites.Count}");
        if (favorites.Count == 0) SetStatus(T("收藏为空，添加后即可从托盘菜单快速切换。", "Add a favorite to switch time zones from the tray menu."));
        UpdateButtons();
    }
    private void AddFavorite()
    {
        if (available.SelectedItem is not Zone zone) return;
        if (favorites.Contains(zone.Id, StringComparer.OrdinalIgnoreCase)) { SetStatus(T("这个时区已在收藏中。", "This time zone is already a favorite.")); return; }
        favorites.Add(zone.Id); RefreshFavorites(favorites.Count - 1); SetStatus(T("已添加到草稿，保存后更新托盘菜单。", "Favorite added. Save to update the tray menu."));
    }
    private void RemoveFavorite()
    {
        int index = selected.SelectedIndex; if (index < 0) return;
        favorites.RemoveAt(index); RefreshFavorites(index);
        if (favorites.Count > 0) SetStatus(T("已从草稿删除，保存后生效。", "Favorite removed. Save to keep this change."));
    }
    private void MoveFavorite(int delta)
    {
        int index = selected.SelectedIndex; int next = index + delta;
        if (index < 0 || next < 0 || next >= favorites.Count) return;
        (favorites[index], favorites[next]) = (favorites[next], favorites[index]);
        RefreshFavorites(next); SetStatus(T("顺序已调整，保存后生效。", "Order changed. Save to update the tray menu."));
    }
    private void UpdateButtons()
    {
        add.Enabled = available.SelectedItem is Zone && !controller.IsReadOnly;
        remove.Enabled = selected.SelectedIndex >= 0 && !controller.IsReadOnly;
        up.Enabled = selected.SelectedIndex > 0 && !controller.IsReadOnly;
        down.Enabled = selected.SelectedIndex >= 0 && selected.SelectedIndex < favorites.Count - 1 && !controller.IsReadOnly;
    }
    private string SelectedTheme => theme.SelectedIndex switch { 1 => "light", 2 => "dark", _ => "system" };
    private void ApplyPreview()
    {
        Theme.Apply(SelectedTheme);
        Theme.Paint(this);
        if (!Theme.HighContrast) current.ForeColor = orderHint.ForeColor = Theme.Colors.TextSecondary;
        SetStatus(status.Text, statusIsError);
    }
    private void PreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Queue(ApplyPreview);
    }
    private void TimeChanged(object? sender, EventArgs e)
    {
        Queue(RefreshCurrent);
    }
    private void Queue(Action action)
    {
        if (!IsDisposed && IsHandleCreated)
            try { BeginInvoke(() => { if (!IsDisposed) action(); }); } catch (InvalidOperationException) { }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var work = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(LogicalToDeviceUnits(380), work.Width), Math.Min(LogicalToDeviceUnits(320), work.Height));
        ClientSize = LogicalToDeviceUnits(new Size(920, 650));
        Size = new Size(Math.Min(Width, work.Width), Math.Min(Height, work.Height));
        Location = new Point(work.Left + (work.Width - Width) / 2, work.Top + (work.Height - Height) / 2);
        Theme.Paint(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            SystemEvents.UserPreferenceChanged -= PreferenceChanged;
            SystemEvents.TimeChanged -= TimeChanged;
            titleFont.Dispose();
            headingFont.Dispose();
        }
        base.Dispose(disposing);
    }
    private void RefreshCurrent()
    {
        try
        {
            var id = controller.GetCurrentId();
            var zone = controller.Zones.FirstOrDefault(z => z.Id == id);
            current.Text = T("当前 · ", "Current · ") + (zone is null ? id : DisplayZone(zone).Name);
        }
        catch (Exception) { current.Text = T("暂时无法读取当前时区，可从托盘菜单重试。", "Unable to read the current time zone. Retry from the tray menu."); }
    }

    public void SetNetworkRefreshing(bool value)
    {
        refreshingNetwork = value;
        networkRefresh.Enabled = !value;
        networkRefresh.Text = value ? T("正在刷新…", "Refreshing…") : T("刷新网络环境", "Refresh network location");
        networkRefresh.AccessibleName = networkRefresh.Text;
    }

    public void RefreshNetworkZone()
    {
        networkZone.Text = T("当前网络环境时区：", "Network time zone: ") + (networkZoneName() ?? T("未检测", "Not checked"));
        networkZone.AccessibleName = networkZone.Text;
    }
    private void Save()
    {
        var result = controller.Save(original with { FavoriteTimeZoneIds = [.. favorites], Theme = SelectedTheme, Language = languageChoice, StartWithWindows = startup.Checked, AutomaticTimeZone = controller.GetSettings().AutomaticTimeZone });
        if (!result.Success) { SetStatus(result.Message, true); return; }
        saved(); Close();
    }
    private void SetStatus(string message, bool error = false)
    {
        status.Text = message;
        statusIsError = error;
        status.ForeColor = Theme.HighContrast ? SystemColors.ControlText : error ? Theme.Colors.Critical : Theme.Colors.TextSecondary;
    }
}
