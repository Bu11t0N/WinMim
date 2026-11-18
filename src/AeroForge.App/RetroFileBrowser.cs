using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.VisualBasic.FileIO;

namespace AeroForge.App;

internal sealed class RetroFileBrowser : Form
{
    private const int SearchLimit = 2000;
    private const string DropEffectFormat = "Preferred DropEffect";
    private static readonly Color SidebarColor = Color.FromArgb(224, 238, 234);
    private readonly ListView files = new();
    private readonly TextBox address = new();
    private readonly TextBox query = new() { PlaceholderText = "Имя файла или папки" };
    private readonly ToolStripStatusLabel status = new();
    private readonly ToolStripButton back = new("Назад");
    private readonly ToolStripButton forward = new("Вперёд");
    private readonly ToolStripButton up = new("На уровень выше");
    private readonly List<string?> history = [];
    private int historyIndex = -1;
    private string? folder;
    private CancellationTokenSource? listing;
    private bool showingSearch;

    public RetroFileBrowser(string? initialFolder)
    {
        Text = "Файлы · WinMim";
        AccessibleName = "Файловый менеджер WinMim";
        AutoScaleMode = AutoScaleMode.Dpi;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(920, 600);
        MinimumSize = new Size(680, 390);

        files.Dock = DockStyle.Fill;
        files.View = View.Details;
        files.FullRowSelect = true;
        files.HideSelection = false;
        files.AccessibleName = "Список файлов";
        files.Columns.Add("Имя", 240);
        files.Columns.Add("Тип", 100);
        files.Columns.Add("Размер", 90, HorizontalAlignment.Right);
        files.Columns.Add("Изменён", 125);
        files.ItemActivate += (_, _) => OpenSelection();
        files.SelectedIndexChanged += (_, _) =>
        {
            if (files.SelectedItems.Count != 0)
                status.Text = $"Выбрано {files.SelectedItems.Count} из {files.Items.Count}";
            else
                status.Text = $"Объектов: {files.Items.Count}";
        };
        var context = new ContextMenuStrip();
        Add(context.Items, "Открыть", OpenSelection);
        Add(context.Items, "Вырезать", () => Copy(move: true));
        Add(context.Items, "Копировать", () => Copy());
        Add(context.Items, "Вставить", Paste);
        Add(context.Items, "Переименовать", Rename);
        Add(context.Items, "Удалить в корзину", DeleteSelection);
        Add(context.Items, "Свойства", ShowProperties);
        files.ContextMenuStrip = context;

        var toolbar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Fill };
        toolbar.Items.AddRange([back, forward, up]);
        var edit = new ToolStripDropDownButton("Правка");
        Add(edit.DropDownItems, "Вырезать", () => Copy(move: true));
        Add(edit.DropDownItems, "Копировать", () => Copy());
        Add(edit.DropDownItems, "Вставить", Paste);
        Add(edit.DropDownItems, "Переименовать", Rename);
        Add(edit.DropDownItems, "Удалить в корзину", DeleteSelection);
        toolbar.Items.Add(edit);
        var view = new ToolStripDropDownButton("Вид");
        Add(view.DropDownItems, "Таблица", () => files.View = View.Details);
        Add(view.DropDownItems, "Список", () => files.View = View.List);
        toolbar.Items.Add(view);
        Add(toolbar.Items, "Обновить", () => _ = Navigate(folder, record: false));
        Add(toolbar.Items, "Открыть в Проводнике Windows", OpenSystemFolder);
        back.Click += (_, _) => _ = VisitHistory(-1);
        forward.Click += (_, _) => _ = VisitHistory(1);
        up.Click += (_, _) => _ = Navigate(folder is null ? null : Directory.GetParent(folder)?.FullName);

        address.Dock = DockStyle.Fill;
        address.AccessibleName = "Адрес папки";
        address.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            GoToAddress();
        };
        var location = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, AutoSize = true };
        location.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        location.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        location.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        location.Controls.Add(new Label { Text = "Адрес", AutoSize = true, Anchor = AnchorStyles.Left });
        location.Controls.Add(address);
        location.Controls.Add(Button("Перейти", GoToAddress));

        var sidebar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false,
            AutoScroll = true, Padding = new Padding(12), BackColor = SidebarColor
        };
        sidebar.Controls.Add(new Label { Text = "Места", AutoSize = true });
        sidebar.Controls.Add(Button("Диски", () => _ = Navigate(null)));
        AddPlace(sidebar, "Документы", Environment.SpecialFolder.MyDocuments);
        AddPlace(sidebar, "Рабочий стол", Environment.SpecialFolder.DesktopDirectory);
        AddPlace(sidebar, "Изображения", Environment.SpecialFolder.MyPictures);
        sidebar.Controls.Add(new Label { Text = "Действия", AutoSize = true, Margin = new Padding(3, 14, 3, 3) });
        sidebar.Controls.Add(Button("Создать папку", NewFolder));
        sidebar.Controls.Add(Button("Выход из поиска", () => _ = Navigate(folder, record: false)));
        query.Width = 148;
        query.AccessibleName = "Поиск по имени";
        query.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            e.SuppressKeyPress = true;
            _ = Search();
        };
        sidebar.Controls.Add(new Label { Text = "Поиск в папке", AutoSize = true, Margin = new Padding(3, 14, 3, 3) });
        sidebar.Controls.Add(query);
        sidebar.Controls.Add(Button("Найти", () => _ = Search()));
        sidebar.Controls.Add(Button("Остановить поиск", () => listing?.Cancel()));

        var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        content.Controls.Add(sidebar, 0, 0);
        content.Controls.Add(files, 1, 0);
        var statusBar = new StatusStrip { Dock = DockStyle.Fill };
        statusBar.Items.Add(status);
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(toolbar);
        layout.Controls.Add(location);
        layout.Controls.Add(content);
        layout.Controls.Add(statusBar);
        Controls.Add(layout);
        Shown += (_, _) => _ = Navigate(initialFolder);
        FormClosed += (_, _) => listing?.Cancel();
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keys)
    {
        if (keys == (Keys.Control | Keys.L)) { address.Focus(); address.SelectAll(); return true; }
        if (keys == (Keys.Control | Keys.F)) { query.Focus(); query.SelectAll(); return true; }
        if (keys == (Keys.Alt | Keys.Left)) { _ = VisitHistory(-1); return true; }
        if (keys == (Keys.Alt | Keys.Right)) { _ = VisitHistory(1); return true; }
        if (keys == Keys.F5) { _ = Navigate(folder, record: false); return true; }
        if (files.Focused)
        {
            switch (keys)
            {
                case Keys.F2: Rename(); return true;
                case Keys.Delete: DeleteSelection(); return true;
                case Keys.Control | Keys.C: Copy(); return true;
                case Keys.Control | Keys.X: Copy(move: true); return true;
                case Keys.Control | Keys.V: Paste(); return true;
                case Keys.Alt | Keys.Enter: ShowProperties(); return true;
            }
        }
        return base.ProcessCmdKey(ref message, keys);
    }

    private void AddPlace(FlowLayoutPanel panel, string label, Environment.SpecialFolder location)
    {
        string path = Environment.GetFolderPath(location);
        if (Directory.Exists(path)) panel.Controls.Add(Button(label, () => _ = Navigate(path)));
    }

    private static Button Button(string label, Action action)
    {
        var button = new Button { Text = label, AutoSize = true };
        button.Click += (_, _) => action();
        return button;
    }

    private static void Add(ToolStripItemCollection items, string label, Action action)
        => items.Add(label, null, (_, _) => action());

    private void GoToAddress() => Try(() =>
        _ = Navigate(Path.GetFullPath(Environment.ExpandEnvironmentVariables(address.Text.Trim()), folder ?? Environment.CurrentDirectory)));

    private async Task VisitHistory(int direction)
    {
        int next = historyIndex + direction;
        if (next < 0 || next >= history.Count) return;
        await Navigate(history[next], record: false, historyTarget: next);
    }

    private async Task Navigate(string? path, bool record = true, int? historyTarget = null)
    {
        await ListAsync("Открытие папки…", token => ReadFolder(path, token), rows =>
        {
            if (record && (historyIndex < 0 || !string.Equals(folder, path, StringComparison.OrdinalIgnoreCase)))
            {
                history.RemoveRange(historyIndex + 1, history.Count - historyIndex - 1);
                history.Add(path);
                historyIndex = history.Count - 1;
            }
            if (historyTarget.HasValue) historyIndex = historyTarget.Value;
            folder = path;
            showingSearch = false;
            address.Text = path ?? "";
            Text = $"{path ?? "Диски"} · Файлы WinMim";
            ShowRows(rows);
        });
    }

    private async Task Search()
    {
        string? root = folder;
        string pattern = query.Text.Trim();
        if (root is null || pattern.Length == 0)
        {
            status.Text = "Откройте папку и введите часть имени.";
            return;
        }
        await ListAsync("Поиск…", token =>
        {
            var options = new EnumerationOptions
            {
                RecurseSubdirectories = true, IgnoreInaccessible = true,
                AttributesToSkip = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReparsePoint
            };
            var found = new List<Entry>();
            foreach (var entry in new DirectoryInfo(root).EnumerateFileSystemInfos("*", options))
            {
                token.ThrowIfCancellationRequested();
                if (!entry.Name.Contains(pattern, StringComparison.OrdinalIgnoreCase)) continue;
                found.Add(Entry.From(entry, Path.GetRelativePath(root, entry.FullName)));
                if (found.Count > SearchLimit) break;
            }
            return found.ToArray();
        }, rows =>
        {
            showingSearch = true;
            ShowRows(rows.Take(SearchLimit));
            status.Text = rows.Length > SearchLimit ? $"Показаны первые {SearchLimit} совпадений" : $"Совпадений: {rows.Length}";
        });
    }

    private async Task ListAsync(string message, Func<CancellationToken, Entry[]> read, Action<Entry[]> show)
    {
        listing?.Cancel();
        using var request = new CancellationTokenSource();
        listing = request;
        files.Enabled = false;
        status.Text = message;
        try
        {
            var results = await Task.Run(() => read(request.Token), request.Token);
            if (!request.IsCancellationRequested && !IsDisposed) show(results);
            else if (listing == request && !IsDisposed) status.Text = "Операция отменена";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
            if (listing == request && !IsDisposed) status.Text = "Операция отменена";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException)
        {
            if (listing == request && !IsDisposed) status.Text = error.Message;
        }
        finally
        {
            if (listing == request)
            {
                listing = null;
                if (!IsDisposed)
                {
                    files.Enabled = true;
                    back.Enabled = historyIndex > 0;
                    forward.Enabled = historyIndex >= 0 && historyIndex < history.Count - 1;
                    up.Enabled = folder is not null;
                }
            }
        }
    }

    private static Entry[] ReadFolder(string? path, CancellationToken token)
    {
        if (path == null)
            return DriveInfo.GetDrives().Select(drive => new Entry(drive.Name, drive.Name, true, "Диск", "", "")).ToArray();
        var rows = new List<Entry>();
        var options = new EnumerationOptions { AttributesToSkip = FileAttributes.Hidden | FileAttributes.System, IgnoreInaccessible = false };
        foreach (var item in new DirectoryInfo(path).EnumerateFileSystemInfos("*", options))
        {
            token.ThrowIfCancellationRequested();
            rows.Add(Entry.From(item, item.Name));
        }
        return rows.OrderByDescending(row => row.Directory).ThenBy(row => row.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private void ShowRows(IEnumerable<Entry> rows)
    {
        files.BeginUpdate();
        try
        {
            files.Items.Clear();
            files.Items.AddRange(rows.Select(row => new ListViewItem([row.Name, row.Type, row.Size, row.Modified]) { Tag = row }).ToArray());
        }
        finally { files.EndUpdate(); }
        status.Text = $"Объектов: {files.Items.Count}";
    }

    private Entry[] Selected() => files.Enabled
        ? files.SelectedItems.Cast<ListViewItem>().Select(row => (Entry)row.Tag!).ToArray()
        : [];

    private void OpenSelection()
    {
        var selected = Selected();
        if (selected.Length != 1) return;
        if (selected[0].Directory) _ = Navigate(selected[0].Path);
        else Try(() => Start(selected[0].Path));
    }

    private void OpenSystemFolder() => Try(() =>
    {
        if (folder is null) Start("explorer.exe", "shell:MyComputerFolder");
        else Start(folder);
    });

    private void Copy(bool move = false)
    {
        var selected = Selected();
        if (selected.Length == 0) return;
        Try(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, selected.Select(item => item.Path).ToArray());
            using var effect = new MemoryStream(BitConverter.GetBytes((int)(move ? DragDropEffects.Move : DragDropEffects.Copy)));
            data.SetData(DropEffectFormat, effect);
            Clipboard.SetDataObject(data, copy: true);
        });
    }

    private void Paste()
    {
        if (listing != null || folder is not string destination) return;
        Try(() =>
        {
            var data = Clipboard.GetDataObject();
            if (data?.GetData(DataFormats.FileDrop) is not string[] paths) return;
            bool move = data.GetData(DropEffectFormat) is MemoryStream effect
                && effect.Length >= sizeof(int)
                && BitConverter.ToInt32(effect.ToArray()) == (int)DragDropEffects.Move;
            foreach (string source in paths)
            {
                if (string.IsNullOrEmpty(source) || !Path.IsPathFullyQualified(source)) continue;
                string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(source));
                if (name.Length == 0) throw new IOException("Выберите файл или папку внутри диска.");
                string target = Path.Combine(destination, name);
                if (string.Equals(Path.GetFullPath(source), target, StringComparison.OrdinalIgnoreCase))
                    throw new IOException("Выберите другую папку назначения.");
                const UIOption ui = UIOption.AllDialogs;
                const UICancelOption cancel = UICancelOption.ThrowException;
                if (Directory.Exists(source))
                {
                    if (move) FileSystem.MoveDirectory(source, target, ui, cancel);
                    else FileSystem.CopyDirectory(source, target, ui, cancel);
                }
                else if (move) FileSystem.MoveFile(source, target, ui, cancel);
                else FileSystem.CopyFile(source, target, ui, cancel);
            }
        });
        RefreshResults();
    }

    private void NewFolder()
    {
        if (listing != null || folder is not string parent) return;
        Try(() =>
        {
            string? name = AskName("Создать папку", "Новая папка");
            if (name is null) return;
            string target = Path.Combine(parent, name);
            if (Directory.Exists(target) || File.Exists(target)) throw new IOException("Это имя уже используется.");
            Directory.CreateDirectory(target);
            RefreshResults();
        });
    }

    private void Rename()
    {
        var selected = Selected();
        if (selected.Length != 1 || folder is null) return;
        var entry = selected[0];
        Try(() =>
        {
            string? name = AskName("Переименовать", Path.GetFileName(entry.Path));
            if (name is null) return;
            if (entry.Directory) FileSystem.RenameDirectory(entry.Path, name);
            else FileSystem.RenameFile(entry.Path, name);
            RefreshResults();
        });
    }

    private void DeleteSelection()
    {
        var selected = Selected();
        if (selected.Length == 0 || folder is null) return;
        if (MessageBox.Show(this, $"Переместить в корзину выбранные объекты ({selected.Length})?", Text,
            MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
        Try(() =>
        {
            foreach (var entry in selected)
                if (entry.Directory) FileSystem.DeleteDirectory(entry.Path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
                else FileSystem.DeleteFile(entry.Path, UIOption.AllDialogs, RecycleOption.SendToRecycleBin);
        });
        RefreshResults();
    }

    private void ShowProperties()
    {
        var selected = Selected();
        if (selected.Length != 1) return;
        Try(() =>
        {
            if (!SHObjectProperties(Handle, 2, selected[0].Path, null))
                throw new IOException("Не удалось открыть свойства объекта.");
        });
    }

    private void RefreshResults()
    {
        if (showingSearch) _ = Search();
        else _ = Navigate(folder, record: false);
    }

    private string? AskName(string title, string initial)
    {
        using var dialog = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, ShowInTaskbar = false,
            StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false,
            AutoScaleMode = AutoScaleMode.Dpi, ClientSize = new Size(360, 85)
        };
        var name = new TextBox { Text = initial, Dock = DockStyle.Top, AccessibleName = "Имя" };
        var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        var ok = new Button { Text = "ОК", DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Отмена", DialogResult = DialogResult.Cancel };
        actions.Controls.AddRange([cancel, ok]);
        dialog.Controls.AddRange([actions, name]);
        dialog.AcceptButton = ok;
        dialog.CancelButton = cancel;
        dialog.ActiveControl = name;
        name.SelectAll();
        if (dialog.ShowDialog(this) != DialogResult.OK) return null;
        string value = name.Text.Trim();
        if (value.Length == 0 || value is "." or ".." || value.EndsWith('.') || value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            throw new IOException("Недопустимое имя файла или папки.");
        return value;
    }

    private static void Start(string path, string? argument = null)
    {
        var start = new ProcessStartInfo(path) { UseShellExecute = true };
        if (argument is not null) start.ArgumentList.Add(argument);
        Process.Start(start)?.Dispose();
    }

    private void Try(Action action)
    {
        try { action(); }
        catch (OperationCanceledException) { status.Text = "Операция отменена"; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException
            or ExternalException or NotSupportedException)
        {
            MessageBox.Show(this, error.Message, "Файлы WinMim", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SHObjectProperties(IntPtr owner, uint type, string path, string? page);

    private sealed record Entry(string Path, string Name, bool Directory, string Type, string Size, string Modified)
    {
        public static Entry From(FileSystemInfo item, string displayName)
        {
            var file = item as FileInfo;
            var type = file is null ? "Папка" : file.Extension.Length > 1 ? file.Extension[1..].ToUpperInvariant() : "Файл";
            string size = file is null ? "" : file.Length.ToString("N0", CultureInfo.CurrentCulture) + " Б";
            return new Entry(item.FullName, displayName, file is null, type, size, item.LastWriteTime.ToString("g"));
        }
    }
}
