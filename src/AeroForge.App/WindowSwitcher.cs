using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AeroForge.App;

/// <summary>Original managed Alt+Tab implementation. Keyboard events are never recorded.</summary>
public sealed class WindowSwitcher : Window
{
    private readonly HookProc _callback;
    private IntPtr _hook;
    private SwitcherOverlay? _overlay;
    private readonly TextBlock _state;
    public WindowSwitcher()
    {
        Title = "AeroForge · Переключатель окон"; Width = 540; Height = 320; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        
        _callback = Hook;
        var panel = new StackPanel { Margin = new Thickness(28) }; Content = panel;
        panel.Children.Add(new TextBlock { Text = "Классический Alt+Tab", FontSize = 26, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = "Tab — следующее окно, Shift+Tab — предыдущее. Отпустите Alt для выбора. Escape отменяет переключение.", TextWrapping = TextWrapping.Wrap, LineHeight = 23, Margin = new Thickness(0, 17, 0, 20) });
        var enable = new CheckBox { Content = "Использовать переключатель AeroForge", FontSize = 15 };
        panel.Children.Add(enable); _state = new TextBlock { Text = "Отключён. Сочетания клавиш Windows работают как обычно.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 20, 0, 0), Foreground = SystemColors.GrayTextBrush }; panel.Children.Add(_state);
        enable.Checked += (_, _) =>
        {
            _hook = SetWindowsHookEx(13, _callback, GetModuleHandle(null), 0);
            if (_hook == IntPtr.Zero) { enable.IsChecked = false; MessageBox.Show("Не удалось подключить клавиатурный обработчик: " + Marshal.GetLastWin32Error()); }
            else _state.Text = "Включён до закрытия этого окна. Обработчик использует только Alt+Tab и Escape; клавиши не сохраняются.";
        };
        enable.Unchecked += (_, _) => Disable(); Closed += (_, _) => Disable();
    }
    private void Disable()
    {
        if (_hook != IntPtr.Zero) { UnhookWindowsHookEx(_hook); _hook = IntPtr.Zero; }
        _overlay?.Close(); _overlay = null;
        _state.Text = "Отключён. Сочетания клавиш Windows работают как обычно.";
    }
    private IntPtr Hook(int code, IntPtr message, IntPtr pointer)
    {
        if (code >= 0)
        {
            var key = Marshal.PtrToStructure<KeyboardData>(pointer);
            bool down = message.ToInt64() is 0x100 or 0x104;
            bool up = message.ToInt64() is 0x101 or 0x105;
            if (key.Vk == 9 && down && (key.Flags & 0x20) != 0)
            {
                bool reverse = (GetAsyncKeyState(0x10) & 0x8000) != 0;
                Dispatcher.BeginInvoke(() =>
                {
                    if (_hook == IntPtr.Zero) return;
                    if (_overlay == null)
                    {
                        var tasks = Tasks(); if (tasks.Count == 0) return;
                        _overlay = new SwitcherOverlay(tasks, GetForegroundWindow());
                        _overlay.Closed += (_, _) => _overlay = null; _overlay.Show();
                    }
                    _overlay.Step(reverse ? -1 : 1);
                });
                return new IntPtr(1);
            }
            if (key.Vk is 0x12 or 0xA4 or 0xA5 && up)
                Dispatcher.BeginInvoke(() => _overlay?.Choose());
            if (key.Vk == 0x1B && down && _overlay != null)
            { Dispatcher.BeginInvoke(() => _overlay?.Close()); return new IntPtr(1); }
        }
        return CallNextHookEx(_hook, code, message, pointer);
    }
    private static List<WindowTask> Tasks()
    {
        List<WindowTask> result = [];
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window)) return true;
            GetWindowThreadProcessId(window, out uint pid); if (pid == Environment.ProcessId) return true;
            long styles = GetWindowLongPtr(window, -20).ToInt64();
            if ((styles & 0x80) != 0 && (styles & 0x40000) == 0) return true;
            if (GetWindow(window, 4) != IntPtr.Zero && (styles & 0x40000) == 0) return true;
            if (DwmGetWindowAttribute(window, 14, out int cloaked, 4) == 0 && cloaked != 0) return true;
            var title = new StringBuilder(512); GetWindowText(window, title, title.Capacity);
            if (title.Length == 0) return true;
            ImageSource? icon = null;
            if (SendMessageTimeout(window, 0x7F, new IntPtr(2), IntPtr.Zero, 2, 40, out var iconHandle) != IntPtr.Zero && iconHandle != IntPtr.Zero)
            {
                try { icon = Imaging.CreateBitmapSourceFromHIcon(iconHandle, Int32Rect.Empty, BitmapSizeOptions.FromWidthAndHeight(32, 32)); icon.Freeze(); }
                catch (Exception) { }
            }
            result.Add(new(window, title.ToString(), icon)); return true;
        }, IntPtr.Zero);
        return result;
    }
    private sealed record WindowTask(IntPtr Handle, string Title, ImageSource? Icon);
    private sealed class SwitcherOverlay : Window
    {
        private readonly List<WindowTask> _tasks;
        private readonly List<Border> _tiles = [];
        private readonly TextBlock _title;
        private int _selected;
        public SwitcherOverlay(List<WindowTask> tasks, IntPtr foreground)
        {
            _tasks = tasks; _selected = Math.Max(0, tasks.FindIndex(t => t.Handle == foreground));
            Width = Math.Min(940, SystemParameters.WorkArea.Width - 100); SizeToContent = SizeToContent.Height;
            WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.NoResize; ShowInTaskbar = false;
            Topmost = true; ShowActivated = false; WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = SystemColors.ControlBrush;
            var panel = new StackPanel { Margin = new Thickness(22) }; Content = panel;
            _title = new TextBlock { Foreground = SystemColors.ControlTextBrush, FontSize = 18, Margin = new Thickness(4, 0, 4, 20), TextTrimming = TextTrimming.CharacterEllipsis }; panel.Children.Add(_title);
            var wrap = new WrapPanel(); var scroll = new ScrollViewer { Content = wrap, MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; panel.Children.Add(scroll);
            for (int i = 0; i < tasks.Count; i++)
            {
                int index = i; var task = tasks[i]; var content = new StackPanel { Margin = new Thickness(10) };
                content.Children.Add(task.Icon != null ? new Image { Source = task.Icon, Width = 34, Height = 34, Margin = new Thickness(0, 4, 0, 10) }
                    : new TextBlock { Text = "▣", Foreground = SystemColors.HighlightBrush, FontSize = 28, HorizontalAlignment = HorizontalAlignment.Center });
                content.Children.Add(new TextBlock { Text = task.Title, Foreground = SystemColors.ControlTextBrush, FontSize = 11, Width = 112, TextTrimming = TextTrimming.CharacterEllipsis, TextAlignment = TextAlignment.Center });
                var tile = new Border { Child = content, Width = 137, Height = 100, CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(2), Margin = new Thickness(3) };
                tile.MouseLeftButtonDown += (_, _) => { _selected = index; Choose(); }; _tiles.Add(tile); wrap.Children.Add(tile);
            }
            UpdateSelection();
        }
        public void Step(int direction) { _selected = (_selected + direction + _tasks.Count) % _tasks.Count; UpdateSelection(); }
        private void UpdateSelection()
        {
            _title.Text = _tasks[_selected].Title;
            for (int i = 0; i < _tiles.Count; i++) { _tiles[i].Background = i == _selected ? SystemColors.HighlightBrush : SystemColors.ControlBrush; _tiles[i].BorderBrush = i == _selected ? SystemColors.HighlightBrush : SystemColors.ControlBrush; }
            _tiles[_selected].BringIntoView();
        }
        public void Choose()
        {
            var task = _tasks[_selected]; Close();
            if (IsIconic(task.Handle)) ShowWindowAsync(task.Handle, 9);
            SetForegroundWindow(task.Handle);
        }
    }
    private delegate IntPtr HookProc(int code, IntPtr wParam, IntPtr lParam);
    private delegate bool EnumProc(IntPtr hwnd, IntPtr parameter);
    [StructLayout(LayoutKind.Sequential)] private struct KeyboardData { public uint Vk, Scan, Flags, Time; public UIntPtr Extra; }
    [DllImport("user32.dll", SetLastError = true)] private static extern IntPtr SetWindowsHookEx(int id, HookProc callback, IntPtr module, uint thread);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hook);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetModuleHandle(string? name);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int vk);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr parameter);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool ShowWindowAsync(IntPtr window, int command);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll")] private static extern IntPtr GetWindow(IntPtr window, uint command);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr window, StringBuilder text, int capacity);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
}
