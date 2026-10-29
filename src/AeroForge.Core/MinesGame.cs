namespace AeroForge.Core;

public sealed class MineCell
{
    public bool Mine { get; internal set; }
    public bool Revealed { get; internal set; }
    public bool Flagged { get; internal set; }
    public int Adjacent { get; internal set; }
}
public sealed class MinesGame
{
    public int Width { get; }
    public int Height { get; }
    public int MineCount { get; }
    public bool Started { get; private set; }
    public bool Lost { get; private set; }
    public bool Won { get; private set; }
    public MineCell[,] Cells { get; }
    private readonly Random random;
    public MinesGame(int width = 9, int height = 9, int mines = 10, int? seed = null)
    {
        if (width < 4 || height < 4 || mines < 1 || mines > width * height - 9) throw new ArgumentOutOfRangeException(nameof(mines));
        Width = width; Height = height; MineCount = mines; random = seed.HasValue ? new Random(seed.Value) : new Random();
        Cells = new MineCell[height, width];
        for (int r = 0; r < height; r++) for (int c = 0; c < width; c++) Cells[r, c] = new();
    }
    private IEnumerable<(int R, int C)> Neighbors(int row, int col)
    {
        for (int r = Math.Max(0, row - 1); r <= Math.Min(Height - 1, row + 1); r++)
            for (int c = Math.Max(0, col - 1); c <= Math.Min(Width - 1, col + 1); c++)
                if (r != row || c != col) yield return (r, c);
    }
    public void Flag(int row, int col)
    { if (!Lost && !Won && !Cells[row, col].Revealed) Cells[row, col].Flagged = !Cells[row, col].Flagged; }
    private void Generate(int row, int col)
    {
        var eligible = new List<(int R, int C)>();
        for (int r = 0; r < Height; r++) for (int c = 0; c < Width; c++)
            if (Math.Abs(r - row) > 1 || Math.Abs(c - col) > 1) eligible.Add((r, c));
        random.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(eligible));
        foreach (var (r, c) in eligible.Take(MineCount)) Cells[r, c].Mine = true;
        for (int r = 0; r < Height; r++) for (int c = 0; c < Width; c++)
            Cells[r, c].Adjacent = Neighbors(r, c).Count(p => Cells[p.R, p.C].Mine);
        Started = true;
    }
    public void Reveal(int row, int col)
    {
        if (Lost || Won || Cells[row, col].Flagged) return;
        if (!Started) Generate(row, col);
        if (Cells[row, col].Revealed)
        {
            var around = Neighbors(row, col).ToArray();
            if (Cells[row, col].Adjacent == around.Count(p => Cells[p.R, p.C].Flagged))
                foreach (var p in around.Where(p => !Cells[p.R, p.C].Revealed && !Cells[p.R, p.C].Flagged)) Reveal(p.R, p.C);
            return;
        }
        var pending = new Queue<(int R, int C)>(); pending.Enqueue((row, col));
        while (pending.TryDequeue(out var p))
        {
            var cell = Cells[p.R, p.C]; if (cell.Revealed || cell.Flagged) continue;
            cell.Revealed = true;
            if (cell.Mine) { Lost = true; return; }
            if (cell.Adjacent == 0) foreach (var n in Neighbors(p.R, p.C)) pending.Enqueue(n);
        }
        Won = Cells.Cast<MineCell>().All(c => c.Mine || c.Revealed);
    }
}
