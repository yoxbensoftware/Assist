namespace Assist.Forms.Games;

using System.Drawing.Drawing2D;

/// <summary>
/// Classic Tetris game with terminal green aesthetic.
/// </summary>
internal sealed class TetrisGame : Form
{
    private const int CellSize = 28;
    private const int BoardWidth = 10;
    private const int BoardHeight = 20;
    private const int InitialSpeed = 500;
    private const int MinSpeed = 80;

    private static readonly Color GridColor = Color.FromArgb(0, 30, 0);

    private static readonly Color[] PieceColors =
    [
        Color.Cyan,            // I
        Color.Yellow,          // O
        Color.FromArgb(160, 0, 200), // T
        Color.Blue,            // J
        Color.Orange,          // L
        Color.Green,           // S
        Color.Red              // Z
    ];

    // Each piece: 4 rotations, each rotation is 4 (row, col) offsets
    private static readonly int[,,,] Pieces = new int[7, 4, 4, 2]
    {
        // I
        { { {0,0},{0,1},{0,2},{0,3} }, { {0,0},{1,0},{2,0},{3,0} }, { {0,0},{0,1},{0,2},{0,3} }, { {0,0},{1,0},{2,0},{3,0} } },
        // O
        { { {0,0},{0,1},{1,0},{1,1} }, { {0,0},{0,1},{1,0},{1,1} }, { {0,0},{0,1},{1,0},{1,1} }, { {0,0},{0,1},{1,0},{1,1} } },
        // T
        { { {0,0},{0,1},{0,2},{1,1} }, { {0,0},{1,0},{2,0},{1,1} }, { {1,0},{1,1},{1,2},{0,1} }, { {0,0},{1,0},{2,0},{1,-1} } },
        // J
        { { {0,0},{1,0},{1,1},{1,2} }, { {0,0},{0,1},{1,0},{2,0} }, { {0,0},{0,1},{0,2},{1,2} }, { {0,0},{1,0},{2,0},{2,-1} } },
        // L
        { { {0,2},{1,0},{1,1},{1,2} }, { {0,0},{1,0},{2,0},{2,1} }, { {0,0},{0,1},{0,2},{1,0} }, { {0,0},{0,1},{1,1},{2,1} } },
        // S
        { { {0,1},{0,2},{1,0},{1,1} }, { {0,0},{1,0},{1,1},{2,1} }, { {0,1},{0,2},{1,0},{1,1} }, { {0,0},{1,0},{1,1},{2,1} } },
        // Z
        { { {0,0},{0,1},{1,1},{1,2} }, { {0,1},{1,0},{1,1},{2,0} }, { {0,0},{0,1},{1,1},{1,2} }, { {0,1},{1,0},{1,1},{2,0} } },
    };

    private readonly int[,] _board = new int[BoardHeight, BoardWidth]; // 0 = empty, 1-7 = piece color index+1
    private readonly System.Windows.Forms.Timer _gameTimer;
    private readonly Label _lblScore;
    private readonly Label _lblLevel;
    private readonly Label _lblGameOver;
    private readonly Random _rng = new();

    private int _currentPiece;
    private int _currentRotation;
    private int _currentRow;
    private int _currentCol;
    private int _nextPiece;
    private int _score;
    private int _level = 1;
    private int _linesCleared;
    private bool _isGameOver;
    private bool _isPaused;

    public TetrisGame()
    {
        Text = "?? Tetris";
        var boardPixelW = BoardWidth * CellSize;
        var boardPixelH = BoardHeight * CellSize;
        var panelWidth = 160;
        ClientSize = new Size(boardPixelW + panelWidth + 20, boardPixelH + 10);
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);
        DoubleBuffered = true;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        KeyPreview = true;

        // Oyun başladığında odağı kendine al
        Shown += (_, _) => { this.ActiveControl = null; this.Focus(); };

        _lblScore = new Label
        {
            Text = "Skor: 0",
            Location = new Point(boardPixelW + 20, 20),
            Width = panelWidth - 10,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 11, FontStyle.Bold)
        };

        _lblLevel = new Label
        {
            Text = "Seviye: 1",
            Location = new Point(boardPixelW + 20, 50),
            Width = panelWidth - 10,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 11, FontStyle.Bold)
        };

        var lblNext = new Label
        {
            Text = "Sıradaki:",
            Location = new Point(boardPixelW + 20, 90),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        var lblControls = new Label
        {
            Text = "‹ › : Hareket\n^ : Döndür\nv : Hızlı\nSpace: Düşür\nP : Duraklat",
            Location = new Point(boardPixelW + 20, 220),
            Size = new Size(panelWidth - 10, 120),
            ForeColor = Color.Gray,
            Font = new Font("Consolas", 8)
        };

        _lblGameOver = new Label
        {
            Text = "GAME OVER\nEnter: Yeniden",
            Location = new Point(boardPixelW / 2 - 80, boardPixelH / 2 - 20),
            AutoSize = true,
            ForeColor = Color.Red,
            Font = new Font("Consolas", 14, FontStyle.Bold),
            BackColor = Color.Black,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };

        Controls.AddRange([_lblScore, _lblLevel, lblNext, lblControls, _lblGameOver]);

        _gameTimer = new System.Windows.Forms.Timer { Interval = InitialSpeed };
        _gameTimer.Tick += OnGameTick;


        KeyDown += OnKeyDown;
        PreviewKeyDown += OnPreviewKeyDown;
        Paint += OnPaint;

        StartNewGame();
    }

    private void StartNewGame()
    {
        Array.Clear(_board);
        _score = 0;
        _level = 1;
        _linesCleared = 0;
        _isGameOver = false;
        _isPaused = false;
        _lblScore.Text = "Skor: 0";
        _lblLevel.Text = "Seviye: 1";
        _lblGameOver.Visible = false;
        _gameTimer.Interval = InitialSpeed;

        _nextPiece = _rng.Next(7);
        SpawnNewPiece();
        _gameTimer.Start();
        Invalidate();
    }

    private void SpawnNewPiece()
    {
        _currentPiece = _nextPiece;
        _nextPiece = _rng.Next(7);
        _currentRotation = 0;
        _currentRow = 0;
        _currentCol = BoardWidth / 2 - 1;

        if (!IsValidPosition(_currentRow, _currentCol, _currentPiece, _currentRotation))
        {
            _isGameOver = true;
            _gameTimer.Stop();
            _lblGameOver.Visible = true;
        }
    }

    private void OnGameTick(object? sender, EventArgs e)
    {
        if (_isGameOver || _isPaused) return;
        MoveDown();
    }

    private void MoveDown()
    {
        if (IsValidPosition(_currentRow + 1, _currentCol, _currentPiece, _currentRotation))
        {
            _currentRow++;
        }
        else
        {
            LockPiece();
            ClearLines();
            SpawnNewPiece();
        }
        Invalidate();
    }

    private void HardDrop()
    {
        while (IsValidPosition(_currentRow + 1, _currentCol, _currentPiece, _currentRotation))
            _currentRow++;

        LockPiece();
        ClearLines();
        SpawnNewPiece();
        Invalidate();
    }

    private void LockPiece()
    {
        for (var i = 0; i < 4; i++)
        {
            var r = _currentRow + Pieces[_currentPiece, _currentRotation, i, 0];
            var c = _currentCol + Pieces[_currentPiece, _currentRotation, i, 1];
            if (r >= 0 && r < BoardHeight && c >= 0 && c < BoardWidth)
                _board[r, c] = _currentPiece + 1;
        }
    }

    private void ClearLines()
    {
        var lines = 0;

        for (var row = BoardHeight - 1; row >= 0; row--)
        {
            var full = true;
            for (var col = 0; col < BoardWidth; col++)
            {
                if (_board[row, col] == 0) { full = false; break; }
            }

            if (full)
            {
                lines++;
                for (var r = row; r > 0; r--)
                    for (var c = 0; c < BoardWidth; c++)
                        _board[r, c] = _board[r - 1, c];

                for (var c = 0; c < BoardWidth; c++)
                    _board[0, c] = 0;

                row++; // Re-check same row
            }
        }

        if (lines > 0)
        {
            var points = lines switch
            {
                1 => 100,
                2 => 300,
                3 => 500,
                4 => 800,
                _ => lines * 200
            };
            _score += points * _level;
            _linesCleared += lines;
            _level = (_linesCleared / 10) + 1;

            _lblScore.Text = $"Skor: {_score}";
            _lblLevel.Text = $"Seviye: {_level}";

            _gameTimer.Interval = Math.Max(MinSpeed, InitialSpeed - (_level - 1) * 40);
        }
    }

    private bool IsValidPosition(int row, int col, int piece, int rotation)
    {
        for (var i = 0; i < 4; i++)
        {
            var r = row + Pieces[piece, rotation, i, 0];
            var c = col + Pieces[piece, rotation, i, 1];

            if (r < 0 || r >= BoardHeight || c < 0 || c >= BoardWidth)
                return false;

            if (_board[r, c] != 0)
                return false;
        }
        return true;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (_isGameOver)
        {
            if (e.KeyCode == Keys.Enter) StartNewGame();
            return;
        }

        if (e.KeyCode == Keys.P)
        {
            _isPaused = !_isPaused;
            return;
        }

        if (_isPaused) return;

        switch (e.KeyCode)
        {
            case Keys.Left:
                if (IsValidPosition(_currentRow, _currentCol - 1, _currentPiece, _currentRotation))
                    _currentCol--;
                break;
            case Keys.Right:
                if (IsValidPosition(_currentRow, _currentCol + 1, _currentPiece, _currentRotation))
                    _currentCol++;
                break;
            case Keys.Down:
                MoveDown();
                break;
            case Keys.Up:
                var newRot = (_currentRotation + 1) % 4;
                if (IsValidPosition(_currentRow, _currentCol, _currentPiece, newRot))
                    _currentRotation = newRot;
                break;
            case Keys.Space:
                HardDrop();
                break;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;
        Invalidate();
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.HighSpeed;

        // Grid lines
        using var gridPen = new Pen(GridColor, 1);
        for (var x = 0; x <= BoardWidth; x++)
            g.DrawLine(gridPen, x * CellSize, 0, x * CellSize, BoardHeight * CellSize);
        for (var y = 0; y <= BoardHeight; y++)
            g.DrawLine(gridPen, 0, y * CellSize, BoardWidth * CellSize, y * CellSize);

        // Board
        for (var r = 0; r < BoardHeight; r++)
        {
            for (var c = 0; c < BoardWidth; c++)
            {
                if (_board[r, c] > 0)
                {
                    DrawCell(g, c, r, PieceColors[_board[r, c] - 1]);
                }
            }
        }

        // Current piece
        if (!_isGameOver)
        {
            var color = PieceColors[_currentPiece];
            for (var i = 0; i < 4; i++)
            {
                var r = _currentRow + Pieces[_currentPiece, _currentRotation, i, 0];
                var c = _currentCol + Pieces[_currentPiece, _currentRotation, i, 1];
                if (r >= 0 && r < BoardHeight && c >= 0 && c < BoardWidth)
                    DrawCell(g, c, r, color);
            }
        }

        // Next piece preview
        var previewX = BoardWidth * CellSize + 25;
        var previewY = 115;
        var previewCell = 18;

        for (var i = 0; i < 4; i++)
        {
            var r = Pieces[_nextPiece, 0, i, 0];
            var c = Pieces[_nextPiece, 0, i, 1];
            using var brush = new SolidBrush(PieceColors[_nextPiece]);
            g.FillRectangle(brush,
                previewX + c * previewCell + 1,
                previewY + r * previewCell + 1,
                previewCell - 2, previewCell - 2);
        }

        // Border
        using var borderPen = new Pen(AppConstants.AccentText, 2);
        g.DrawRectangle(borderPen, 0, 0, BoardWidth * CellSize, BoardHeight * CellSize);
    }

    private static void DrawCell(Graphics g, int col, int row, Color color)
    {
        var rect = new Rectangle(col * CellSize + 1, row * CellSize + 1, CellSize - 2, CellSize - 2);
        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, rect);
        using var highlight = new Pen(Color.FromArgb(60, 255, 255, 255));
        g.DrawLine(highlight, rect.Left, rect.Top, rect.Right, rect.Top);
        g.DrawLine(highlight, rect.Left, rect.Top, rect.Left, rect.Bottom);
    }
    // Ok tuşlarının ve space'in input key olarak algılanmasını sağlar
    private void OnPreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
    {
        if (e.KeyCode == Keys.Left || e.KeyCode == Keys.Right ||
            e.KeyCode == Keys.Up || e.KeyCode == Keys.Down ||
            e.KeyCode == Keys.Space)
        {
            e.IsInputKey = true;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _gameTimer.Stop();
            _gameTimer.Dispose();
        }
        base.Dispose(disposing);
    }
}
