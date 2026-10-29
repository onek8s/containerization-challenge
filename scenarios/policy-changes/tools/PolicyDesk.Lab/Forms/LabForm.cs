using System.Globalization;

namespace PolicyDesk.Lab.Forms;

/// <summary>Form with label and input rows. Each label is created directly before its input.</summary>
public class LabForm : Form
{
    public const string Caption = "PolicyDesk";
    public const string DateFormat = "dd.MM.yyyy";

    private int _y = 14;

    public LabForm()
    {
        Font = new Font("Segoe UI", 9f);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        KeyPreview = true;
        ClientSize = new Size(520, 200);
    }

    protected T AddField<T>(string label, T input, int width = 300) where T : Control
    {
        Controls.Add(new Label { Text = label, Left = 14, Top = _y + 3, Width = 130, AutoSize = false });
        input.Left = 150;
        input.Top = _y;
        input.Width = width;
        Controls.Add(input);
        _y += input.Height + 10;
        return input;
    }

    protected TextBox AddText(string label, int width = 300, bool readOnly = false) =>
        AddField(label, new TextBox { ReadOnly = readOnly }, width);

    protected ComboBox AddList(string label, params string[] items)
    {
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        combo.Items.AddRange(items);
        return AddField(label, combo, 200);
    }

    protected ListView AddGrid(string label, int height, params (string Name, int Width)[] columns)
    {
        var grid = new ListView { View = View.Details, FullRowSelect = true, GridLines = true, Height = height, HideSelection = false };
        foreach (var c in columns) grid.Columns.Add(c.Name, c.Width);
        grid.KeyDown += (_, e) =>
        {
            if (e.Control && e.Shift && e.KeyCode == Keys.C)
            {
                CopyGrid(grid);
                e.Handled = true;
            }
        };
        return AddField(label, grid, 350);
    }

    protected Button AddButtons(params string[] texts)
    {
        Button? first = null;
        var x = 150;
        foreach (var text in texts)
        {
            var b = new Button { Text = text, Left = x, Top = _y + 6, Width = 110, Height = 28 };
            Controls.Add(b);
            first ??= b;
            x += 118;
        }
        _y += 46;
        ClientSize = new Size(Math.Max(ClientSize.Width, x + 20), _y);
        return first!;
    }

    protected Button ButtonByText(string text) => Controls.OfType<Button>().First(b => b.Text == text);

    /// <summary>Ctrl+Shift+C copies the grid with a header row as tab separated text.</summary>
    protected static void CopyGrid(ListView grid)
    {
        var lines = new List<string> { string.Join('\t', grid.Columns.Cast<ColumnHeader>().Select(c => c.Text)) };
        lines.AddRange(grid.Items.Cast<ListViewItem>().Select(i => string.Join('\t', i.SubItems.Cast<ListViewItem.ListViewSubItem>().Select(s => s.Text))));
        Clipboard.SetText(string.Join("\r\n", lines) + "\r\n");
    }

    protected static void Error(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);

    protected static void Info(IWin32Window owner, string text) =>
        MessageBox.Show(owner, text, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);

    protected static bool TryDate(string text, out DateTime date) =>
        DateTime.TryParseExact(text.Trim(), DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            Close();
            e.Handled = true;
        }
        base.OnKeyDown(e);
    }
}
