using System.Text;

namespace Assist.Forms.DeveloperTools;

/// <summary>
/// Lorem Ipsum dummy text generator with customizable paragraphs and words.
/// </summary>
internal sealed class LoremIpsumForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private readonly NumericUpDown _numParagraphs = null!;
    private readonly NumericUpDown _numWordsPerPara = null!;
    private readonly CheckBox _chkStartWithLorem = null!;
    private readonly TextBox _txtOutput = null!;
    private readonly Label _lblStatus = null!;

    private static readonly string[] LoremWords = 
    [
        "lorem", "ipsum", "dolor", "sit", "amet", "consectetur", "adipiscing", "elit",
        "sed", "do", "eiusmod", "tempor", "incididunt", "ut", "labore", "et", "dolore",
        "magna", "aliqua", "enim", "ad", "minim", "veniam", "quis", "nostrud",
        "exercitation", "ullamco", "laboris", "nisi", "aliquip", "ex", "ea", "commodo",
        "consequat", "duis", "aute", "irure", "in", "reprehenderit", "voluptate",
        "velit", "esse", "cillum", "fugiat", "nulla", "pariatur", "excepteur", "sint",
        "occaecat", "cupidatat", "non", "proident", "sunt", "culpa", "qui", "officia",
        "deserunt", "mollit", "anim", "id", "est", "laborum", "vitae", "elementum",
        "tempus", "quam", "pellentesque", "nec", "nam", "aliquam", "sem", "fringilla",
        "urna", "porttitor", "rhoncus", "mattis", "viverra", "suspendisse", "potenti",
        "nullam", "ac", "tortor", "vitae", "purus", "faucibus", "ornare", "massa",
        "eget", "egestas", "pede", "justo", "fringilla", "vel", "aliquet", "nec"
    ];

    public LoremIpsumForm()
    {
        Text = "Lorem Ipsum Generator";
        ClientSize = new Size(800, 600);
        MinimumSize = new Size(650, 460);
        StartPosition = FormStartPosition.CenterParent;
        AutoScroll = true;
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== LOREM IPSUM GENERATOR ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        var lblParagraphs = new Label
        {
            Text = "Paragraphs:",
            Location = new Point(20, 70),
            Width = 150,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _numParagraphs = new NumericUpDown
        {
            Location = new Point(180, 67),
            Width = 100,
            Minimum = 1,
            Maximum = 50,
            Value = 3,
            BackColor = Color.Black,
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblWords = new Label
        {
            Text = "Words per Paragraph:",
            Location = new Point(20, 110),
            Width = 200,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _numWordsPerPara = new NumericUpDown
        {
            Location = new Point(230, 107),
            Width = 100,
            Minimum = 10,
            Maximum = 500,
            Value = 50,
            BackColor = Color.Black,
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle
        };

        _chkStartWithLorem = new CheckBox
        {
            Text = "Start with 'Lorem ipsum dolor sit amet...'",
            Location = new Point(20, 150),
            Width = 400,
            Checked = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9)
        };

        var btnGenerate = new Button
        {
            Text = "Generate Lorem Ipsum",
            Location = new Point(20, 190),
            Width = 200,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnGenerate.FlatAppearance.BorderColor = GreenText;
        btnGenerate.Click += (_, _) => GenerateLoremIpsum();

        var btnCopy = new Button
        {
            Text = "Copy to Clipboard",
            Location = new Point(230, 190),
            Width = 180,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnCopy.FlatAppearance.BorderColor = GreenText;
        btnCopy.Click += (_, _) => CopyToClipboard();

        var btnClear = new Button
        {
            Text = "Clear",
            Location = new Point(420, 190),
            Width = 100,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnClear.FlatAppearance.BorderColor = GreenText;
        btnClear.Click += (_, _) => 
        { 
            _txtOutput?.Clear(); 
            if (_lblStatus is not null) 
                _lblStatus.Text = "Status: Ready"; 
        };

        _lblStatus = new Label
        {
            Text = "Status: Ready",
            Location = new Point(20, 235),
            Width = 760,
            Height = 20,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _txtOutput = new TextBox
        {
            Location = new Point(20, 265),
            Width = 760,
            Height = 315,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.AddRange([lblTitle, lblParagraphs, _numParagraphs, lblWords, _numWordsPerPara, _chkStartWithLorem, btnGenerate, btnCopy, btnClear, _lblStatus, _txtOutput]);

        // Generate initial lorem ipsum
        GenerateLoremIpsum();
    }

    private void GenerateLoremIpsum()
    {
        try
        {
            var paragraphCount = (int)_numParagraphs.Value;
            var wordsPerParagraph = (int)_numWordsPerPara.Value;
            var random = new Random();
            var result = new StringBuilder();

            for (int p = 0; p < paragraphCount; p++)
            {
                var paragraph = new StringBuilder();
                
                // First paragraph starts with classic "Lorem ipsum dolor sit amet"
                if (p == 0 && _chkStartWithLorem.Checked)
                {
                    paragraph.Append("Lorem ipsum dolor sit amet, consectetur adipiscing elit");
                    
                    // Add remaining words
                    for (int w = 8; w < wordsPerParagraph; w++)
                    {
                        paragraph.Append(", ");
                        paragraph.Append(LoremWords[random.Next(LoremWords.Length)]);
                    }
                }
                else
                {
                    // Regular paragraphs
                    for (int w = 0; w < wordsPerParagraph; w++)
                    {
                        if (w == 0)
                        {
                            // Capitalize first word
                            var word = LoremWords[random.Next(LoremWords.Length)];
                            paragraph.Append(char.ToUpperInvariant(word[0]) + word[1..]);
                        }
                        else
                        {
                            paragraph.Append(' ');
                            paragraph.Append(LoremWords[random.Next(LoremWords.Length)]);
                        }

                        // Add punctuation randomly
                        if (w > 5 && random.Next(15) == 0 && w < wordsPerParagraph - 1)
                        {
                            paragraph.Append(random.Next(2) == 0 ? ',' : '.');
                            if (paragraph[^1] == '.')
                            {
                                paragraph.Append(' ');
                                var word = LoremWords[random.Next(LoremWords.Length)];
                                paragraph.Append(char.ToUpperInvariant(word[0]) + word[1..]);
                            }
                        }
                    }
                }

                paragraph.Append('.');
                result.AppendLine(paragraph.ToString());
                
                if (p < paragraphCount - 1)
                {
                    result.AppendLine(); // Empty line between paragraphs
                }
            }

            _txtOutput.Text = result.ToString();
            
            var wordCount = wordsPerParagraph * paragraphCount;
            var charCount = result.Length;
            _lblStatus.Text = $"Status: ✓ {paragraphCount} paragraphs, ~{wordCount} words, {charCount} characters generated";
            _lblStatus.ForeColor = GreenText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Hata - {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
    }

    private void CopyToClipboard()
    {
        if (!string.IsNullOrWhiteSpace(_txtOutput.Text))
        {
            Clipboard.SetText(_txtOutput.Text);
            MessageBox.Show("Lorem ipsum panoya kopyalandı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
