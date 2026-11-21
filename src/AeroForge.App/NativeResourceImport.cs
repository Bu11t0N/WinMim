using System.IO;
using AeroForge.Core;
using F = System.Windows.Forms;
using D = System.Drawing;

namespace AeroForge.App;

internal sealed class NativeResourceImport : F.Form
{
    public ResourcePackage? Package { get; private set; }
    public string? Archive { get; private set; }

    public NativeResourceImport()
    {
        Text = UiText.Get("ResourceImport.Action");
        Font = D.SystemFonts.MessageBoxFont;
        AutoScaleMode = F.AutoScaleMode.Font;
        StartPosition = F.FormStartPosition.CenterParent;
        FormBorderStyle = F.FormBorderStyle.FixedDialog;
        ClientSize = new D.Size(540, 235);
        MaximizeBox = MinimizeBox = false;

        var layout = new F.TableLayoutPanel { Dock = F.DockStyle.Fill, Padding = new F.Padding(12), ColumnCount = 2 };
        layout.ColumnStyles.Add(new(F.SizeType.Percent, 100));
        layout.ColumnStyles.Add(new(F.SizeType.AutoSize));
        Controls.Add(layout);
        var intro = new F.Label { Text = UiText.Get("ResourceImport.Intro"), AutoSize = true, MaximumSize = new D.Size(505, 0) };
        layout.Controls.Add(intro, 0, 0);
        layout.SetColumnSpan(intro, 2);
        var descriptor = AddFileField(layout, 1, "ResourceImport.Descriptor", "JSON (*.json)|*.json");
        var archive = AddFileField(layout, 3, "ResourceImport.Archive", "ZIP (*.zip)|*.zip");
        var actions = new F.FlowLayoutPanel { AutoSize = true, Dock = F.DockStyle.Fill };
        var import = new F.Button { AutoSize = true, Text = UiText.Get("ResourceImport.Import") };
        var cancel = new F.Button { AutoSize = true, Text = UiText.Get("ResourceImport.Cancel"), DialogResult = F.DialogResult.Cancel };
        actions.Controls.AddRange([import, cancel]);
        layout.Controls.Add(actions, 0, 5);
        layout.SetColumnSpan(actions, 2);
        AcceptButton = import;
        CancelButton = cancel;

        import.Click += (_, _) =>
        {
            try
            {
                var file = new FileInfo(descriptor.Text.Trim());
                if (file.Length > 64 * 1024) throw new InvalidDataException(UiText.Get("ResourceImport.DescriptorTooLarge"));
                Package = JsonFile.Read<ResourcePackage>(file.FullName);
                Archive = string.IsNullOrWhiteSpace(archive.Text) ? null : archive.Text.Trim();
                DialogResult = F.DialogResult.OK;
            }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or System.Text.Json.JsonException)
            {
                F.MessageBox.Show(this, UiText.Format("ResourceImport.DescriptorError", error.Message), Text,
                    F.MessageBoxButtons.OK, F.MessageBoxIcon.Error);
            }
        };
    }

    private static F.TextBox AddFileField(F.TableLayoutPanel layout, int row, string key, string filter)
    {
        string caption = UiText.Get(key);
        var label = new F.Label { Text = caption, AutoSize = true };
        var input = new F.TextBox { AccessibleName = caption, Dock = F.DockStyle.Fill };
        var browse = new F.Button { Text = UiText.Get("ResourceImport.Browse"), AutoSize = true };
        browse.Click += (_, _) =>
        {
            using var files = new F.OpenFileDialog { Filter = filter, Title = caption, CheckFileExists = true };
            if (files.ShowDialog() == F.DialogResult.OK) input.Text = files.FileName;
        };
        layout.Controls.Add(label, 0, row);
        layout.SetColumnSpan(label, 2);
        layout.Controls.Add(input, 0, row + 1);
        layout.Controls.Add(browse, 1, row + 1);
        return input;
    }
}
