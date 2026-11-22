using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>批量下载壁纸：选时间范围 + 目标文件夹。</summary>
    internal sealed class DownloadForm : Form
    {
        private readonly DateTimePicker dtFrom;
        private readonly DateTimePicker dtTo;
        private readonly TextBox txtDest;
        private readonly ComboBox cmbQuality;
        private readonly Label lblEst;
        private readonly Button btnOk;
        private bool touchedDest;

        public string FromDate { get { return dtFrom.Value.ToString("yyyyMMdd"); } }
        public string ToDate { get { return dtTo.Value.ToString("yyyyMMdd"); } }
        public string DestPath { get { return txtDest.Text.Trim(); } }
        public string MaxRes
        {
            get
            {
                int i = cmbQuality == null ? 0 : cmbQuality.SelectedIndex;
                if (i == 1) { return "fhd"; }
                if (i == 2) { return "hd"; }
                return "uhd";
            }
        }

        public DownloadForm()
        {
            Text = Program.AppName + S.DlTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(540, 264);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label lblRange = new Label();
            lblRange.Text = S.DlRange;
            lblRange.SetBounds(16, 14, 200, 20);
            Controls.Add(lblRange);

            Label lblFrom = new Label();
            lblFrom.Text = S.DlFrom;
            lblFrom.SetBounds(16, 46, 30, 20);
            Controls.Add(lblFrom);

            dtFrom = MakePicker(50, 44);
            Controls.Add(dtFrom);

            Label lblTo = new Label();
            lblTo.Text = S.DlTo;
            lblTo.SetBounds(216, 46, 24, 20);
            Controls.Add(lblTo);

            dtTo = MakePicker(244, 44);
            Controls.Add(dtTo);

            DateTime today = DateTime.Today;
            DateTime first = new DateTime(today.Year, today.Month, 1);
            if (first < dtFrom.MinDate) { first = dtFrom.MinDate; }
            dtFrom.Value = first;
            dtTo.Value = today;
            dtFrom.ValueChanged += delegate { OnRangeChanged(); };
            dtTo.ValueChanged += delegate { OnRangeChanged(); };

            lblEst = new Label();
            lblEst.SetBounds(16, 78, 500, 20);
            lblEst.AutoSize = false;
            Controls.Add(lblEst);

            Panel line = new Panel();
            line.SetBounds(16, 108, 508, 1);
            line.BackColor = SystemColors.ControlLight;
            Controls.Add(line);

            Label lblDest = new Label();
            lblDest.Text = S.DlDest;
            lblDest.SetBounds(16, 120, 60, 20);
            Controls.Add(lblDest);

            txtDest = new TextBox();
            txtDest.SetBounds(76, 118, 340, 23);
            txtDest.TextChanged += delegate { touchedDest = true; };
            Controls.Add(txtDest);

            Button btnBrowse = new Button();
            btnBrowse.Text = S.BtnBrowse;
            btnBrowse.SetBounds(424, 117, 76, 25);
            btnBrowse.Click += delegate
            {
                string before = txtDest.Text;
                using (FolderBrowserDialog d = new FolderBrowserDialog())
                {
                    d.Description = S.DlDest;
                    d.ShowNewFolderButton = true;
                    try { if (Directory.Exists(before)) { d.SelectedPath = before; } } catch { }
                    if (d.ShowDialog(this) != DialogResult.OK) { return; }
                    if (Program.IsInsideProgramDir(d.SelectedPath))
                    {
                        MessageBox.Show(S.WarnInsideApp, Program.AppName,
                            MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        txtDest.Text = before;
                        touchedDest = true;
                        return;
                    }
                    txtDest.Text = d.SelectedPath;
                }
            };
            Controls.Add(btnBrowse);

            Label lblNote = new Label();
            lblNote.SetBounds(16, 150, 508, 20);
            lblNote.AutoSize = false;
            lblNote.ForeColor = SystemColors.GrayText;
            lblNote.Text = S.DlNote;
            Controls.Add(lblNote);

            Label lblQ = new Label();
            lblQ.Text = S.DlQuality;
            lblQ.SetBounds(16, 178, 60, 20);
            Controls.Add(lblQ);

            cmbQuality = new ComboBox();
            cmbQuality.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbQuality.SetBounds(76, 176, 264, 24);
            cmbQuality.Items.Add(S.DlQualityUhd);
            cmbQuality.Items.Add(S.DlQualityFhd);
            cmbQuality.Items.Add(S.DlQualityHd);
            cmbQuality.SelectedIndex = 0;
            cmbQuality.SelectedIndexChanged += delegate { OnRangeChanged(); };
            Controls.Add(cmbQuality);

            Button btnLinks = new Button();
            btnLinks.Text = S.BtnExportLinks;
            btnLinks.SetBounds(200, 220, 110, 30);
            btnLinks.Click += delegate
            {
                try
                {
                    string f1 = dtFrom.Value.ToString("yyyyMMdd");
                    string f2 = dtTo.Value.ToString("yyyyMMdd");
                    Program.DoExportLinks(f1, f2);
                }
                catch { }
            };
            Controls.Add(btnLinks);

            btnOk = new Button();
            btnOk.Text = S.DlBtn;
            btnOk.SetBounds(320, 220, 90, 30);
            btnOk.Click += delegate { Accept(); };
            Controls.Add(btnOk);

            Button btnCancel = new Button();
            btnCancel.Text = S.BtnCancel;
            btnCancel.SetBounds(420, 220, 90, 30);
            btnCancel.DialogResult = DialogResult.Cancel;
            Controls.Add(btnCancel);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            touchedDest = false;
            OnRangeChanged();
        }

        private DateTimePicker MakePicker(int left, int top)
        {
            DateTimePicker p = new DateTimePicker();
            p.Format = DateTimePickerFormat.Custom;
            p.CustomFormat = "yyyy-MM-dd";
            p.SetBounds(left, top, 150, 23);
            try
            {
                p.MinDate = Program.ArchiveMinDateValue;
                p.MaxDate = DateTime.Today;
            }
            catch { }
            return p;
        }

        private void OnRangeChanged()
        {
            int days = (dtTo.Value.Date - dtFrom.Value.Date).Days + 1;
            if (days < 0) { days = 0; }
            double mb = days * 3.5;
            int mins = (int)Math.Ceiling(days * 1.5 / 60.0);
            if (days > 500)
            {
                lblEst.ForeColor = Color.FromArgb(198, 92, 0);
                lblEst.Text = S.DlEstWarn(days, mb, mins);
            }
            else
            {
                lblEst.ForeColor = SystemColors.GrayText;
                lblEst.Text = S.DlEst(days, mb);
            }

            if (btnOk != null) { btnOk.Enabled = days > 0; }

            if (!touchedDest && txtDest != null)
            {
                try
                {
                    string desk = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string name = (S.IsEn ? "BingWallpaper_" : "Bing壁纸_") + FromDate + "-" + ToDate;
                    txtDest.Text = Path.Combine(desk, name);
                }
                catch { }
            }
        }

        private void Accept()
        {
            if (dtFrom.Value.Date > dtTo.Value.Date)
            {
                MessageBox.Show(S.WarnDateOrder, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string dest = DestPath;
            if (dest.Length == 0)
            {
                MessageBox.Show(S.WarnNoPath, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDest.Focus();
                return;
            }
            if (Program.IsInsideProgramDir(dest))
            {
                MessageBox.Show(S.WarnInsideApp, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDest.Focus();
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
