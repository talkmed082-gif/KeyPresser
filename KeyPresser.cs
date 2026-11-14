// KeyPresser: 지정한 키를 일정 주기마다 현재 활성 창 또는 고른 프로그램 창에 보냄
// 빌드: C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe /out:KeyPresser.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll /r:System.Core.dll /win32icon:KeyPresser.ico KeyPresser.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

class KeyPresser : Form
{
    [StructLayout(LayoutKind.Sequential)]
    struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)]
    struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Explicit)]
    struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
    [StructLayout(LayoutKind.Sequential)]
    struct INPUT { public uint type; public InputUnion u; }
    [StructLayout(LayoutKind.Sequential)]
    struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)]
    struct GUITHREADINFO { public int cbSize; public uint flags; public IntPtr hwndActive, hwndFocus, hwndCapture, hwndMenuOwner, hwndMoveSize, hwndCaret; public RECT rcCaret; }
    delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr hWnd, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc f, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder s, int n);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h, uint cmd);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int idx);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool BringWindowToTop(IntPtr h);
    [DllImport("user32.dll")] static extern bool AttachThreadInput(uint a, uint b, bool attach);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] static extern bool GetGUIThreadInfo(uint tid, ref GUITHREADINFO info);
    [DllImport("user32.dll", SetLastError = true)] static extern bool PostMessage(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] static extern uint MapVirtualKey(uint code, uint mapType);
    [DllImport("user32.dll")] static extern bool IsChild(IntPtr parent, IntPtr child);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);

    const uint KEYEVENTF_EXTENDEDKEY = 0x1, KEYEVENTF_KEYUP = 0x2;
    const uint WM_KEYDOWN = 0x100, WM_KEYUP = 0x101;
    const int WM_HOTKEY = 0x312, HOTKEY_ID = 1;
    const uint MOD_ALT = 0x1, MOD_CONTROL = 0x2;
    const uint GW_OWNER = 4;
    const int GWL_EXSTYLE = -20, WS_EX_TOOLWINDOW = 0x80, SW_RESTORE = 9;

    class Target
    {
        public IntPtr Hwnd; public string Text; public string Proc = ""; public string Title = "";
        public override string ToString() { return Text; }
    }

    ComboBox keyBox = new ComboBox();
    NumericUpDown intervalBox = new NumericUpDown();
    ComboBox targetBox = new ComboBox();
    Button refreshBtn = new Button();
    CheckBox backgroundChk = new CheckBox();
    CheckBox returnChk = new CheckBox();
    CheckBox untilChk = new CheckBox();
    DateTimePicker untilPicker = new DateTimePicker();
    CheckBox repeatChk = new CheckBox();
    NumericUpDown repeatBox = new NumericUpDown();
    CheckBox rdpChk = new CheckBox();
    bool settingsLoaded;
    Button toggleBtn = new Button();
    Label statusLbl = new Label();
    System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
    System.Windows.Forms.Timer focusWatch = new System.Windows.Forms.Timer { Interval = 300 };
    // 뒤에 있는 창은 Windows가 포커스 컨트롤을 알려주지 않으므로, 대상 창이 앞에 있을 때 입력칸을 기억해 둠
    IntPtr lastFocus;
    int count;
    DateTime stopAt;

    static readonly Keys[] KeyList = BuildKeyList();

    static Keys[] BuildKeyList()
    {
        var list = new List<Keys>();
        for (Keys k = Keys.F1; k <= Keys.F12; k++) list.Add(k);
        list.AddRange(new[] { Keys.Enter, Keys.Space, Keys.Tab, Keys.Escape, Keys.Up, Keys.Down, Keys.Left, Keys.Right,
                              Keys.PageUp, Keys.PageDown, Keys.Home, Keys.End });
        for (Keys k = Keys.A; k <= Keys.Z; k++) list.Add(k);
        for (Keys k = Keys.D0; k <= Keys.D9; k++) list.Add(k);
        return list.ToArray();
    }

    KeyPresser()
    {
        Text = "KeyPresser";
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        TopMost = true;
        ClientSize = new Size(320, 318);
        Font = new Font("Malgun Gothic", 9f);
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);

        Controls.Add(new Label { Text = "키", Location = new Point(12, 15), AutoSize = true });
        keyBox.DropDownStyle = ComboBoxStyle.DropDownList;
        keyBox.Location = new Point(80, 12);
        keyBox.Width = 228;
        foreach (var k in KeyList) keyBox.Items.Add(k);
        keyBox.SelectedItem = Keys.F5;
        Controls.Add(keyBox);

        Controls.Add(new Label { Text = "주기(초)", Location = new Point(12, 47), AutoSize = true });
        intervalBox.Location = new Point(80, 44);
        intervalBox.Width = 228;
        intervalBox.Minimum = 1;
        intervalBox.Maximum = 86400;
        intervalBox.Value = 60;
        Controls.Add(intervalBox);

        Controls.Add(new Label { Text = "대상", Location = new Point(12, 79), AutoSize = true });
        targetBox.DropDownStyle = ComboBoxStyle.DropDownList;
        targetBox.Location = new Point(80, 76);
        targetBox.Width = 196;
        targetBox.DropDownWidth = 420;
        targetBox.SelectedIndexChanged += (s, e) => { lastFocus = IntPtr.Zero; UpdateTargetOptions(); };
        Controls.Add(targetBox);
        refreshBtn.Text = "↻";
        refreshBtn.Location = new Point(282, 75);
        refreshBtn.Size = new Size(26, 25);
        refreshBtn.Click += (s, e) => { LoadTargets(); if (missingProc != null) SelectSavedTarget(); };
        Controls.Add(refreshBtn);

        backgroundChk.Text = "백그라운드로 보내기 (창을 앞으로 안 불러옴)";
        backgroundChk.Location = new Point(80, 104);
        backgroundChk.AutoSize = true;
        backgroundChk.Enabled = false;
        backgroundChk.CheckedChanged += (s, e) => UpdateTargetOptions();
        Controls.Add(backgroundChk);

        returnChk.Text = "누른 뒤 원래 창으로 돌아가기";
        returnChk.Location = new Point(80, 126);
        returnChk.AutoSize = true;
        returnChk.Checked = true;
        returnChk.Enabled = false;
        Controls.Add(returnChk);

        untilChk.Text = "멈출 시각";
        untilChk.Location = new Point(12, 156);
        untilChk.AutoSize = true;
        untilChk.CheckedChanged += (s, e) => untilPicker.Enabled = untilChk.Checked;
        Controls.Add(untilChk);
        untilPicker.Format = DateTimePickerFormat.Custom;
        untilPicker.CustomFormat = "HH:mm";
        untilPicker.ShowUpDown = true;
        untilPicker.Location = new Point(100, 154);
        untilPicker.Width = 80;
        untilPicker.Value = DateTime.Today.AddHours(DateTime.Now.Hour + 1);
        untilPicker.Enabled = false;
        Controls.Add(untilPicker);

        repeatChk.Text = "반복 횟수";
        repeatChk.Location = new Point(12, 186);
        repeatChk.AutoSize = true;
        repeatChk.CheckedChanged += (s, e) => repeatBox.Enabled = repeatChk.Checked;
        Controls.Add(repeatChk);
        repeatBox.Location = new Point(100, 184);
        repeatBox.Width = 80;
        repeatBox.Minimum = 1;
        repeatBox.Maximum = 1000000;
        repeatBox.Value = 10;
        repeatBox.Enabled = false;
        Controls.Add(repeatBox);

        // rdp_to_console.ps1이 ini의 이 값을 읽으므로 바꾸는 즉시 저장
        rdpChk.Text = "원격 연결이 끊겨도 계속 (PC 화면이 안 잠김)";
        rdpChk.Location = new Point(12, 216);
        rdpChk.AutoSize = true;
        rdpChk.CheckedChanged += (s, e) => { if (settingsLoaded) SaveSettings(); };
        Controls.Add(rdpChk);

        toggleBtn.Text = "시작 (Ctrl+Alt+P)";
        toggleBtn.Location = new Point(12, 246);
        toggleBtn.Size = new Size(296, 30);
        toggleBtn.Click += (s, e) => Toggle();
        Controls.Add(toggleBtn);

        statusLbl.Location = new Point(12, 286);
        statusLbl.Size = new Size(296, 30);
        statusLbl.Text = "정지됨";
        Controls.Add(statusLbl);

        LoadTargets();
        LoadSettings();
        settingsLoaded = true;
        timer.Tick += (s, e) => Tick();
        focusWatch.Tick += (s, e) => WatchFocus();
        focusWatch.Start();
    }

    void WatchFocus()
    {
        var target = targetBox.SelectedItem as Target;
        if (target == null || target.Hwnd == IntPtr.Zero || GetForegroundWindow() != target.Hwnd) return;
        IntPtr f = FocusOf(target.Hwnd);
        if (f != IntPtr.Zero) lastFocus = f;
    }

    static IntPtr FocusOf(IntPtr top)
    {
        uint pid;
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf(typeof(GUITHREADINFO)) };
        return GetGUIThreadInfo(GetWindowThreadProcessId(top, out pid), ref info) ? info.hwndFocus : IntPtr.Zero;
    }

    // 마지막 설정을 exe 옆 KeyPresser.ini에 저장 (대상은 창 핸들이 매번 바뀌므로 프로세스 이름으로)
    static readonly string SettingsPath = System.IO.Path.Combine(Application.StartupPath, "KeyPresser.ini");

    // 저장된 대상 창을 아직 못 찾았으면 그 값을 유지 (프로그램을 나중에 켜는 경우)
    string missingProc, missingTitle;

    // 같은 프로그램 창이 여러 개면 제목까지 같은 창을 우선, 없으면 같은 프로그램의 첫 창
    void SelectSavedTarget()
    {
        int byProc = -1, byTitle = -1;
        for (int i = 1; i < targetBox.Items.Count; i++)
        {
            var item = (Target)targetBox.Items[i];
            if (!item.Proc.Equals(missingProc, StringComparison.OrdinalIgnoreCase)) continue;
            if (byProc < 0) byProc = i;
            if (byTitle < 0 && item.Title == missingTitle) byTitle = i;
        }
        int found = byTitle > 0 ? byTitle : byProc;
        if (found > 0)
        {
            targetBox.SelectedIndex = found;
            missingProc = missingTitle = null;
            statusLbl.Text = "정지됨";
        }
        else statusLbl.Text = "저장된 대상(" + missingProc + ") 창이 없음 → 켠 뒤 ↻ 또는 대상을 다시 고르세요";
    }

    void SaveSettings()
    {
        var sel = (Target)targetBox.SelectedItem;
        bool keepMissing = sel.Hwnd == IntPtr.Zero && missingProc != null;
        var lines = new[] {
            "key=" + keyBox.SelectedItem,
            "interval=" + intervalBox.Value,
            "target=" + (keepMissing ? missingProc : sel.Proc),
            "targetTitle=" + (keepMissing ? missingTitle : sel.Title),
            "background=" + backgroundChk.Checked,
            "returnBack=" + returnChk.Checked,
            "until=" + untilChk.Checked,
            "untilTime=" + untilPicker.Value.ToString("HH:mm"),
            "repeat=" + repeatChk.Checked,
            "repeatCount=" + repeatBox.Value,
            "rdpKeep=" + rdpChk.Checked,
        };
        try { System.IO.File.WriteAllLines(SettingsPath, lines); } catch { }
    }

    void LoadSettings()
    {
        string[] lines;
        try { lines = System.IO.File.ReadAllLines(SettingsPath); } catch { return; }
        var d = new Dictionary<string, string>();
        foreach (var line in lines)
        {
            int i = line.IndexOf('=');
            if (i > 0) d[line.Substring(0, i)] = line.Substring(i + 1);
        }
        string v;
        Keys k;
        if (d.TryGetValue("key", out v) && Enum.TryParse(v, out k) && keyBox.Items.Contains(k)) keyBox.SelectedItem = k;
        decimal n;
        if (d.TryGetValue("interval", out v) && decimal.TryParse(v, out n))
            intervalBox.Value = Math.Max(intervalBox.Minimum, Math.Min(intervalBox.Maximum, n));
        if (d.TryGetValue("target", out v) && v != "")
        {
            missingProc = v;
            d.TryGetValue("targetTitle", out missingTitle);
            SelectSavedTarget();
        }
        bool b;
        if (d.TryGetValue("background", out v) && bool.TryParse(v, out b)) backgroundChk.Checked = b;
        if (d.TryGetValue("returnBack", out v) && bool.TryParse(v, out b)) returnChk.Checked = b;
        if (d.TryGetValue("until", out v) && bool.TryParse(v, out b)) untilChk.Checked = b;
        DateTime t;
        if (d.TryGetValue("untilTime", out v) && DateTime.TryParseExact(v, "HH:mm", null, System.Globalization.DateTimeStyles.None, out t))
            untilPicker.Value = DateTime.Today.Add(t.TimeOfDay);
        if (d.TryGetValue("repeat", out v) && bool.TryParse(v, out b)) repeatChk.Checked = b;
        if (d.TryGetValue("repeatCount", out v) && decimal.TryParse(v, out n))
            repeatBox.Value = Math.Max(repeatBox.Minimum, Math.Min(repeatBox.Maximum, n));
        if (d.TryGetValue("rdpKeep", out v) && bool.TryParse(v, out b)) rdpChk.Checked = b;
    }

    void LoadTargets()
    {
        var prev = targetBox.SelectedItem as Target;
        targetBox.Items.Clear();
        targetBox.Items.Add(new Target { Hwnd = IntPtr.Zero, Text = "(앞에 있는 창)" });
        var found = new List<Target>();
        EnumWindows((h, l) =>
        {
            if (h == Handle || !IsWindowVisible(h) || GetWindow(h, GW_OWNER) != IntPtr.Zero) return true;
            if ((GetWindowLong(h, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) != 0) return true;
            var sb = new StringBuilder(256);
            GetWindowText(h, sb, 256);
            if (sb.Length == 0) return true;
            uint pid;
            GetWindowThreadProcessId(h, out pid);
            string proc = "";
            try { proc = Process.GetProcessById((int)pid).ProcessName + ".exe"; } catch { }
            found.Add(new Target { Hwnd = h, Text = proc + "  —  " + sb, Proc = proc, Title = sb.ToString() });
            return true;
        }, IntPtr.Zero);
        // 프로그램 이름 순으로 정렬 (같은 프로그램끼리는 창 순서 유지)
        foreach (var t in System.Linq.Enumerable.OrderBy(found, t => t.Proc, StringComparer.OrdinalIgnoreCase))
            targetBox.Items.Add(t);
        targetBox.SelectedIndex = 0;
        if (prev != null)
            for (int i = 1; i < targetBox.Items.Count; i++)
                if (((Target)targetBox.Items[i]).Hwnd == prev.Hwnd) targetBox.SelectedIndex = i;
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        if (!RegisterHotKey(Handle, HOTKEY_ID, MOD_CONTROL | MOD_ALT, (uint)Keys.P))
            statusLbl.Text = "정지됨 (Ctrl+Alt+P 등록 실패: 다른 프로그램이 사용 중)";
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        UnregisterHotKey(Handle, HOTKEY_ID);
        SaveSettings();
        base.OnFormClosed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_HOTKEY && m.WParam.ToInt32() == HOTKEY_ID) Toggle();
        base.WndProc(ref m);
    }

    void Toggle()
    {
        if (timer.Enabled) Stop("정지됨");
        else Start();
    }

    void Start()
    {
        missingProc = missingTitle = null; // 시작하면 지금 고른 대상이 기준
        SaveSettings();
        count = 0;
        stopAt = DateTime.MaxValue;
        if (untilChk.Checked)
        {
            stopAt = DateTime.Today.Add(untilPicker.Value.TimeOfDay);
            stopAt = stopAt.AddSeconds(-stopAt.Second);
            if (stopAt <= DateTime.Now) stopAt = stopAt.AddDays(1);
        }
        timer.Interval = (int)intervalBox.Value * 1000;
        timer.Start();
        toggleBtn.Text = "정지 (Ctrl+Alt+P)";
        statusLbl.Text = "실행 중 · 첫 입력까지 " + intervalBox.Value + "초" + StopAtText();
        SetInputsEnabled(false);
    }

    void Stop(string reason)
    {
        timer.Stop();
        toggleBtn.Text = "시작 (Ctrl+Alt+P)";
        statusLbl.Text = reason + " · 누른 횟수 " + count;
        SetInputsEnabled(true);
    }

    void SetInputsEnabled(bool on)
    {
        keyBox.Enabled = intervalBox.Enabled = targetBox.Enabled = refreshBtn.Enabled = untilChk.Enabled = repeatChk.Enabled = on;
        untilPicker.Enabled = on && untilChk.Checked;
        repeatBox.Enabled = on && repeatChk.Checked;
        UpdateTargetOptions();
    }

    // 백그라운드·돌아가기는 특정 창을 골랐을 때만, 돌아가기는 앞으로 불러오는 모드에서만 의미 있음
    void UpdateTargetOptions()
    {
        bool idle = !timer.Enabled, hasTarget = targetBox.SelectedIndex > 0;
        bool unsupported = hasTarget && IgnoresBackgroundKeys(((Target)targetBox.SelectedItem).Hwnd);
        if (unsupported) backgroundChk.Checked = false;
        backgroundChk.Text = unsupported ? "백그라운드 미지원 프로그램" : "백그라운드로 보내기 (창을 앞으로 안 불러옴)";
        backgroundChk.Enabled = idle && hasTarget && !unsupported;
        returnChk.Enabled = idle && hasTarget && !backgroundChk.Checked;
    }

    // Chromium 계열(VS Code, Chrome 등)은 뒤에 있을 때 보낸 키 메시지를 무시함
    static bool IgnoresBackgroundKeys(IntPtr h)
    {
        var sb = new StringBuilder(64);
        GetClassName(h, sb, 64);
        return sb.ToString().StartsWith("Chrome_WidgetWin");
    }

    string StopAtText()
    {
        string s = "";
        if (repeatChk.Checked) s += " · " + repeatBox.Value + "회에 멈춤";
        if (stopAt != DateTime.MaxValue) s += " · " + (stopAt.Date > DateTime.Today ? "내일 " : "") + stopAt.ToString("HH:mm") + "에 멈춤";
        return s;
    }

    void Tick()
    {
        if (DateTime.Now >= stopAt) { Stop("멈출 시각 도달 (" + stopAt.ToString("HH:mm") + ")"); return; }

        var key = (Keys)keyBox.SelectedItem;
        var target = (Target)targetBox.SelectedItem;
        bool ok;
        string fail = "입력 실패 (관리자 권한 창일 수 있음)";
        if (target.Hwnd == IntPtr.Zero) ok = SendKey(key);
        else if (!IsWindow(target.Hwnd)) { Stop("대상 창이 닫혀 멈춤"); return; }
        else if (backgroundChk.Checked)
        {
            IntPtr dest = FocusOf(target.Hwnd);
            if (dest == IntPtr.Zero && IsWindow(lastFocus) && IsChild(target.Hwnd, lastFocus)) dest = lastFocus;
            if (dest == IntPtr.Zero) { ok = false; fail = "입력칸 모름 → 대상 창의 입력칸을 한 번 클릭해 주세요"; }
            else ok = PostKey(dest, key);
        }
        else ok = SendKeyToFront(target.Hwnd, key, returnChk.Checked);

        if (ok) count++;
        if (repeatChk.Checked && count >= repeatBox.Value) { Stop("반복 횟수 도달"); return; }
        statusLbl.Text = "실행 중 · " + (ok ? "누른 횟수 " + count : fail)
                         + " · " + DateTime.Now.ToString("HH:mm:ss") + StopAtText();
    }

    static bool SendKey(Keys key)
    {
        uint ext = IsExtended(key) ? KEYEVENTF_EXTENDEDKEY : 0;
        var inputs = new INPUT[2];
        inputs[0].type = inputs[1].type = 1; // INPUT_KEYBOARD
        inputs[0].u.ki.wVk = inputs[1].u.ki.wVk = (ushort)key;
        inputs[0].u.ki.dwFlags = ext;
        inputs[1].u.ki.dwFlags = ext | KEYEVENTF_KEYUP;
        return SendInput(2, inputs, Marshal.SizeOf(typeof(INPUT))) == 2;
    }

    // 대상 창을 앞으로 불러 키를 누르고, returnBack이면 원래 창으로 되돌림
    static bool SendKeyToFront(IntPtr target, Keys key, bool returnBack)
    {
        IntPtr prev = GetForegroundWindow();
        if (IsIconic(target)) ShowWindow(target, SW_RESTORE);
        if (!Activate(target)) return false;
        Thread.Sleep(100);
        bool ok = SendKey(key);
        if (!returnBack) return ok;
        Thread.Sleep(100); // 키가 대상 창에 처리된 뒤 포커스를 돌려줌
        if (prev != IntPtr.Zero && prev != target) Activate(prev);
        return ok;
    }

    static bool Activate(IntPtr h)
    {
        uint pid;
        uint fgThread = GetWindowThreadProcessId(GetForegroundWindow(), out pid);
        uint me = GetCurrentThreadId();
        bool attached = fgThread != me && AttachThreadInput(me, fgThread, true);
        BringWindowToTop(h);
        SetForegroundWindow(h);
        if (attached) AttachThreadInput(me, fgThread, false);
        return GetForegroundWindow() == h;
    }

    // 창을 앞으로 불러오지 않고 입력칸에 키 메시지만 보냄 (프로그램에 따라 무시될 수 있음)
    static bool PostKey(IntPtr dest, Keys key)
    {
        uint scan = MapVirtualKey((uint)key, 0);
        uint lp = 1 | (scan << 16) | (IsExtended(key) ? 1u << 24 : 0);
        bool down = PostMessage(dest, WM_KEYDOWN, (IntPtr)(int)key, (IntPtr)(int)lp);
        bool up = PostMessage(dest, WM_KEYUP, (IntPtr)(int)key, (IntPtr)unchecked((int)(lp | 0xC0000000)));
        return down && up;
    }

    static bool IsExtended(Keys k)
    {
        return k == Keys.Up || k == Keys.Down || k == Keys.Left || k == Keys.Right
            || k == Keys.PageUp || k == Keys.PageDown || k == Keys.Home || k == Keys.End;
    }

    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.Run(new KeyPresser());
    }
}
