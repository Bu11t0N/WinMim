using AeroForge.Core;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace AeroForge.App;

public sealed class ExtrasWindow : Window
{
    private readonly List<DispatcherTimer> _timers = [];
    public ExtrasWindow(string kind)
    {
        Width = 670; Height = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (kind == "--media") Media(); else if (kind == "--mines") Mines(); else Gadgets();
        Closed += (_, _) => { foreach (var timer in _timers) timer.Stop(); };
    }
    private DispatcherTimer Timer(Action tick, double seconds = 1)
    { var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) }; timer.Tick += (_, _) => tick(); _timers.Add(timer); timer.Start(); return timer; }
    private static TextBlock Label(string s, double size = 14) => new() { Text = s, FontSize = size, Margin = new Thickness(0, 0, 0, 12), TextWrapping = TextWrapping.Wrap };
    private static Button Button(string s, RoutedEventHandler action)
    { var b = new Button { Content = s, Margin = new Thickness(0, 0, 10, 0) }; b.Click += action; return b; }
    private void Gadgets()
    {
        Title = "WinMim · Гаджеты"; Width = 390; Height = 650;
        var panel = new StackPanel { Margin = new Thickness(25) }; Content = panel;
        panel.Children.Add(Label("Небольшой помощник", 23));
        var clock = Label("", 49); var date = Label("", 15); var memory = Label("", 14);
        panel.Children.Add(clock); panel.Children.Add(date); panel.Children.Add(memory);
        void Tick()
        {
            clock.Text = DateTime.Now.ToString("HH:mm:ss"); date.Text = DateTime.Now.ToString("dddd, d MMMM");
            var m = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
            memory.Text = GlobalMemoryStatusEx(ref m) ? $"Память: {m.Load}% · {m.Available / 1073741824d:F1} ГБ свободно" : "Статистика памяти недоступна";
        }
        Tick(); Timer(Tick);
        panel.Children.Add(new Separator { Margin = new Thickness(0, 12, 0, 20) }); panel.Children.Add(Label("Заметки", 18));
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AeroForge", "Tools");
        string path = SafePath.Under(root, "notes.txt");
        var notes = new TextBox { AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, Height = 265, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Text = File.Exists(path) ? File.ReadAllText(path) : "" };
        bool edited = false; notes.TextChanged += (_, _) => edited = true; panel.Children.Add(notes);
        panel.Children.Add(new TextBlock { Text = "Сохраняются локально при закрытии окна", FontSize = 11, Foreground = SystemColors.GrayTextBrush, Margin = new Thickness(0, 10, 0, 0) });
        Closing += (_, e) => { if (edited) try { Directory.CreateDirectory(root); File.WriteAllText(SafePath.Under(root, "notes.txt"), notes.Text); } catch (Exception ex) { e.Cancel = true; MessageBox.Show(ex.Message); } };
    }
    private void Media()
    {
        Title = "WinMim · Медиатека"; Width = 900; Height = 670;
        var root = new DockPanel { Margin = new Thickness(22) }; Content = root;
        var header = Label("Ваша медиатека", 25); DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var bottom = new StackPanel { Margin = new Thickness(0, 15, 0, 0) }; DockPanel.SetDock(bottom, Dock.Bottom); root.Children.Add(bottom);
        var controls = new StackPanel { Orientation = Orientation.Horizontal }; bottom.Children.Add(controls);
        var seek = new Slider { Minimum = 0, Maximum = 1, Margin = new Thickness(0, 15, 0, 0) }; bottom.Children.Add(seek);
        var volume = new Slider { Minimum = 0, Maximum = 1, Value = .7, Width = 110, Margin = new Thickness(15, 0, 0, 0) };
        var layout = new Grid(); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) }); root.Children.Add(layout);
        var player = new MediaElement { LoadedBehavior = MediaState.Manual, UnloadedBehavior = MediaState.Close, Stretch = Stretch.Uniform, Volume = .7 };
        var video = new Border { Background = Brushes.Black, Child = player, Margin = new Thickness(0, 0, 15, 0) }; layout.Children.Add(video);
        var list = new ListBox { DisplayMemberPath = nameof(MediaItem.Name), BorderBrush = SystemColors.ActiveBorderBrush }; Grid.SetColumn(list, 1); layout.Children.Add(list);
        controls.Children.Add(Button("Открыть файлы…", (_, _) =>
        {
            var open = new OpenFileDialog { Filter = "Аудио и видео|*.mp4;*.wmv;*.avi;*.mp3;*.wav;*.wma;*.m4a|Все файлы|*.*", Multiselect = true };
            if (open.ShowDialog(this) != true) return;
            foreach (var file in open.FileNames) list.Items.Add(new MediaItem(file));
            if (list.SelectedIndex < 0) list.SelectedIndex = 0;
        }));
        controls.Children.Add(Button("▶", (_, _) => player.Play())); controls.Children.Add(Button("Ⅱ", (_, _) => player.Pause())); controls.Children.Add(Button("■", (_, _) => player.Stop())); controls.Children.Add(volume);
        volume.ValueChanged += (_, _) => player.Volume = volume.Value;
        list.SelectionChanged += (_, _) => { if (list.SelectedItem is MediaItem item) { player.Source = new Uri(item.Path); player.Play(); } };
        player.MediaOpened += (_, _) => { if (player.NaturalDuration.HasTimeSpan) seek.Maximum = Math.Max(1, player.NaturalDuration.TimeSpan.TotalSeconds); };
        player.MediaEnded += (_, _) => { if (list.SelectedIndex + 1 < list.Items.Count) list.SelectedIndex++; };
        player.MediaFailed += (_, e) => MessageBox.Show(this, "Файл не удалось воспроизвести установленными кодеками Windows.\n" + e.ErrorException.Message, "Медиатека");
        seek.PreviewMouseLeftButtonUp += (_, _) => player.Position = TimeSpan.FromSeconds(seek.Value);
        Timer(() => { if (!seek.IsMouseCaptureWithin) seek.Value = player.Position.TotalSeconds; }, .5);
        Closed += (_, _) => player.Close();
    }
    private void Mines()
    {
        Title = "WinMim · Сапёр"; Width = 490; Height = 615;
        var root = new DockPanel { Margin = new Thickness(25) }; Content = root;
        var head = new StackPanel(); DockPanel.SetDock(head, Dock.Top); root.Children.Add(head);
        head.Children.Add(Label("Сапёр", 30)); var status = Label("", 16); head.Children.Add(status);
        head.Children.Add(Label("Левая кнопка — открыть. Правая — флажок. Повторное нажатие на число открывает соседей, если флажков достаточно.", 11));
        var foot = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 15, 0, 0) }; DockPanel.SetDock(foot, Dock.Bottom); root.Children.Add(foot);
        var board = new UniformGrid { Columns = 9, Rows = 9 }; root.Children.Add(board);
        MinesGame game = new(); Stopwatch time = new(); Button[,] buttons = new Button[9, 9];
        void Render()
        {
            int flags = game.Cells.Cast<MineCell>().Count(c => c.Flagged);
            status.Text = game.Won ? "Победа! " + time.Elapsed.ToString("mm\\:ss") : game.Lost ? "Мина. Попробуем ещё раз?" : $"Мины: {10 - flags}    Время: {time.Elapsed:mm\\:ss}";
            if (game.Won || game.Lost) time.Stop();
            for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
            {
                var cell = game.Cells[r, c]; var b = buttons[r, c];
                b.Content = cell.Flagged && !game.Lost ? "⚑" : cell.Mine && game.Lost ? "✳" : cell.Revealed && cell.Adjacent > 0 ? cell.Adjacent.ToString() : "";
                b.Background = cell.Mine && game.Lost ? Brushes.LightCoral : cell.Revealed ? new SolidColorBrush(Color.FromRgb(229, 239, 244)) : Brushes.White;
                b.Foreground = cell.Adjacent == 1 ? Brushes.RoyalBlue : cell.Adjacent == 2 ? Brushes.SeaGreen : Brushes.IndianRed;
            }
        }
        for (int r = 0; r < 9; r++) for (int c = 0; c < 9; c++)
        {
            int row = r, col = c; var b = new Button { Padding = new Thickness(0), FontSize = 19, FontWeight = FontWeights.Bold, Margin = new Thickness(2) };
            buttons[r, c] = b; board.Children.Add(b);
            b.Click += (_, _) => { game.Reveal(row, col); if (game.Started && !game.Lost && !game.Won) time.Start(); Render(); };
            b.PreviewMouseRightButtonDown += (_, e) => { game.Flag(row, col); Render(); e.Handled = true; };
        }
        foot.Children.Add(Button("Новая игра", (_, _) => { game = new(); time.Reset(); Render(); })); Render(); Timer(Render, .25);
    }
    private sealed record MediaItem(string Path) { public string Name => System.IO.Path.GetFileName(Path); }
    [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus
    { public uint Length, Load; public ulong Total, Available, TotalPage, AvailablePage, TotalVirtual, AvailableVirtual, AvailableExtended; }
    [DllImport("kernel32.dll", SetLastError = true)] [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);
}
