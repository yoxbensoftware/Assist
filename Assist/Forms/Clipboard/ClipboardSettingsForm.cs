using Assist.Services;

namespace Assist;

public sealed class ClipboardSettingsForm : Form
{
        private NumericUpDown numCapacity = null!;
        private NumericUpDown numInterval = null!;
        private CheckBox chkFilter = null!;
        private Button btnSave = null!;
        private Button btnCancel = null!;

        private readonly ClipboardHistoryService _service;

        public ClipboardSettingsForm(ClipboardHistoryService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            InitializeComponents();
            LoadValues();
        }

        private void InitializeComponents()
        {
            Text = "Pano Ayarları";
            ClientSize = new Size(320, 180);

            var lblCap = new Label { Text = "Kapasite (örn 50):", Location = new Point(10, 15), AutoSize = true };
            numCapacity = new NumericUpDown { Location = new Point(150, 12), Width = 120, Minimum = 10, Maximum = 1000 };

            var lblInterval = new Label { Text = "Poll Interval (ms):", Location = new Point(10, 50), AutoSize = true };
            numInterval = new NumericUpDown { Location = new Point(150, 48), Width = 120, Minimum = 200, Maximum = 60_000, Increment = 100 };

            var lblFilter = new Label { Text = "Hassas İçerik Filtrele:", Location = new Point(10, 85), AutoSize = true };
            chkFilter = new CheckBox { Location = new Point(150, 82) };

            btnSave = new Button { Text = "Kaydet", Location = new Point(50, 120), Width = 80 };
            btnCancel = new Button { Text = "İptal", Location = new Point(160, 120), Width = 80 };

            btnSave.Click += (s, e) => SaveAndClose();
            btnCancel.Click += (s, e) => Close();

            Controls.Add(lblCap);
            Controls.Add(numCapacity);
            Controls.Add(lblInterval);
            Controls.Add(numInterval);
            Controls.Add(lblFilter);
            Controls.Add(chkFilter);
            Controls.Add(btnSave);
            Controls.Add(btnCancel);
        }

        private void LoadValues()
        {
            var opts = _service.GetOptions();
            numCapacity.Value = opts.capacity;
            numInterval.Value = opts.intervalMs;
            chkFilter.Checked = opts.filterSensitive;
        }

        private void SaveAndClose()
        {
            _service.SetOptions((int)numCapacity.Value, (int)numInterval.Value, chkFilter.Checked);
            MessageBox.Show("Pano ayarları güncellendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
}
