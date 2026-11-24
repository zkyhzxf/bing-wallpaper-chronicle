using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace BingWallDownloader
{
    internal sealed class MainForm : Form
    {
        private DateTimePicker dtFrom;
        private DateTimePicker dtTo;
        private ComboBox cmbQuality;
        private ComboBox cmbThreads;
        private TextBox txtDest;
        private TextBox txtIndex;
        private Button btnBrowseDest;
        private Button btnBrowseIndex;
        private Button btnStart;
        private Button btnStop;
        private Button btnOpenDest;
        private ProgressBar bar;
        private Label lblStat;
        private TextBox txtLog;

        private Downloader worker;
        private Thread workerThread;
        private bool running;

        public MainForm()
        {
            Text = "必应壁纸 · 全量下载器 v1.1";
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimizeBox = true;
            ClientSize = new Size(760, 560);
            MinimumSize = new Size(680, 480);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }
            BackColor = Color.FromArgb(250, 250, 250);

            BuildUi();
            AutoDetect();
        }

        private void BuildUi()
        {
            int y = 14;
            Label l1 = new Label();
            l1.Text = "日期范围";
            l1.SetBounds(16, y + 3, 64, 20);
            Controls.Add(l1);

            dtFrom = new DateTimePicker();
            dtFrom.Format = DateTimePickerFormat.Custom;
            dtFrom.CustomFormat = "yyyy-MM-dd";
            dtFrom.SetBounds(84, y, 116, 24);
            dtFrom.Value = new DateTime(2009, 7, 13);
            Controls.Add(dtFrom);

            Label l1b = new Label();
            l1b.Text = "到";
            l1b.SetBounds(206, y + 3, 20, 20);
            Controls.Add(l1b);

            dtTo = new DateTimePicker();
            dtTo.Format = DateTimePickerFormat.Custom;
            dtTo.CustomFormat = "yyyy-MM-dd";
            dtTo.SetBounds(228, y, 116, 24);
            dtTo.Value = DateTime.Today;
            Controls.Add(dtTo);

            Label l2 = new Label();
            l2.Text = "画质";
            l2.SetBounds(364, y + 3, 40, 20);
            Controls.Add(l2);

            cmbQuality = new ComboBox();
            cmbQuality.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbQuality.SetBounds(404, y, 190, 24);
            cmbQuality.Items.Add("最高可用（UHD → 1080p → 镜像）");
            cmbQuality.Items.Add("1080p（没有就退回镜像）");
            cmbQuality.Items.Add("只用镜像（体积最小）");
            cmbQuality.SelectedIndex = 0;
            Controls.Add(cmbQuality);

            Label l2b = new Label();
            l2b.Text = "并发";
            l2b.SetBounds(600, y + 3, 32, 20);
            Controls.Add(l2b);

            // 8 并发对源站并不更快，反而更容易撞 429；4 是实测的平衡点
            cmbThreads = new ComboBox();
            cmbThreads.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbThreads.SetBounds(632, y, 80, 24);
            cmbThreads.Items.Add("2");
            cmbThreads.Items.Add("4");
            cmbThreads.Items.Add("6");
            cmbThreads.SelectedIndex = 1;
            Controls.Add(cmbThreads);

            y += 36;
            Label l3 = new Label();
            l3.Text = "保存到";
            l3.SetBounds(16, y + 3, 64, 20);
            Controls.Add(l3);

            txtDest = new TextBox();
            txtDest.SetBounds(84, y, 546, 24);
            txtDest.Text = Path.Combine(Path.GetPathRoot(Environment.SystemDirectory) ?? "D:\\", "BingWall-全量");
            Controls.Add(txtDest);

            btnBrowseDest = new Button();
            btnBrowseDest.Text = "浏览…";
            btnBrowseDest.SetBounds(636, y, 76, 24);
            btnBrowseDest.Click += delegate { BrowseDest(); };
            Controls.Add(btnBrowseDest);

            y += 34;
            Label l4 = new Label();
            l4.Text = "索引目录";
            l4.SetBounds(16, y + 3, 64, 20);
            Controls.Add(l4);

            txtIndex = new TextBox();
            txtIndex.SetBounds(84, y, 546, 24);
            Controls.Add(txtIndex);

            btnBrowseIndex = new Button();
            btnBrowseIndex.Text = "浏览…";
            btnBrowseIndex.SetBounds(636, y, 76, 24);
            btnBrowseIndex.Click += delegate { BrowseIndex(); };
            Controls.Add(btnBrowseIndex);

            y += 34;
            Label l5 = new Label();
            l5.Text = "需要指向含 all-index.json / old-index.json 的目录（主程序的 archive 目录）";
            l5.ForeColor = Color.Gray;
            l5.SetBounds(84, y, 620, 18);
            Controls.Add(l5);

            y += 28;
            btnStart = new Button();
            btnStart.Text = "开始下载";
            btnStart.SetBounds(16, y, 110, 32);
            btnStart.Click += delegate { StartRun(); };
            Controls.Add(btnStart);

            btnStop = new Button();
            btnStop.Text = "停止";
            btnStop.SetBounds(134, y, 84, 32);
            btnStop.Enabled = false;
            btnStop.Click += delegate { StopRun(); };
            Controls.Add(btnStop);

            btnOpenDest = new Button();
            btnOpenDest.Text = "打开目录";
            btnOpenDest.SetBounds(226, y, 96, 32);
            btnOpenDest.Click += delegate { OpenDest(); };
            Controls.Add(btnOpenDest);

            y += 42;
            bar = new ProgressBar();
            bar.SetBounds(16, y, 696, 18);
            Controls.Add(bar);

            y += 24;
            lblStat = new Label();
            lblStat.Text = "就绪";
            lblStat.SetBounds(16, y, 696, 20);
            Controls.Add(lblStat);

            y += 26;
            txtLog = new TextBox();
            txtLog.Multiline = true;
            txtLog.ScrollBars = ScrollBars.Vertical;
            txtLog.ReadOnly = true;
            txtLog.BackColor = Color.White;
            txtLog.SetBounds(16, y, 696, ClientSize.Height - y - 16);
            txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            Controls.Add(txtLog);
        }

        private void AutoDetect()
        {
            string exeDir = Path.GetDirectoryName(Application.ExecutablePath);
            string d = Downloader.ResolveIndexDir(exeDir);
            if (!string.IsNullOrEmpty(d))
            {
                txtIndex.Text = d;
                Log("自动找到索引目录：" + d);
            }
            else
            {
                Log("没自动找到索引目录，请手动选择主程序下的 archive 文件夹");
            }
        }

        private void BrowseDest()
        {
            FolderBrowserDialog f = new FolderBrowserDialog();
            f.Description = "选保存到哪个目录";
            if (f.ShowDialog() == DialogResult.OK) { txtDest.Text = f.SelectedPath; }
        }

        private void BrowseIndex()
        {
            FolderBrowserDialog f = new FolderBrowserDialog();
            f.Description = "选主程序的 archive 目录（含 all-index.json / old-index.json）";
            if (f.ShowDialog() == DialogResult.OK) { txtIndex.Text = f.SelectedPath; }
        }

        private void OpenDest()
        {
            try
            {
                string d = txtDest.Text.Trim();
                if (Directory.Exists(d)) { System.Diagnostics.Process.Start("explorer.exe", d); }
                else { MessageBox.Show("目录还不存在", Text); }
            }
            catch { }
        }

        private void StartRun()
        {
            if (running) { return; }
            string idx = txtIndex.Text.Trim();
            string dest = txtDest.Text.Trim();
            if (string.IsNullOrEmpty(dest)) { MessageBox.Show("先选保存目录", Text); return; }
            string realIdx = Downloader.ResolveIndexDir(idx);
            if (string.IsNullOrEmpty(realIdx))
            {
                MessageBox.Show("找不到索引文件。请选择主程序目录下的 archive 文件夹（里面有 all-index.json）。", Text);
                return;
            }
            txtIndex.Text = realIdx;

            string from = dtFrom.Value.ToString("yyyyMMdd");
            string to = dtTo.Value.ToString("yyyyMMdd");
            if (string.CompareOrdinal(from, to) > 0) { MessageBox.Show("起始日期晚于结束日期", Text); return; }
            if (to.Length != 8) { return; }

            Quality q = Quality.Best;
            if (cmbQuality.SelectedIndex == 1) { q = Quality.Fhd; }
            if (cmbQuality.SelectedIndex == 2) { q = Quality.Hd; }

            txtLog.Clear();
            Log("开始：" + from + " ~ " + to + "，画质=" + cmbQuality.Text);
            Log("保存到：" + dest);

            running = true;
            btnStart.Enabled = false;
            btnStop.Enabled = true;
            bar.Value = 0;

            worker = new Downloader(realIdx, dest, q, Log, OnProgress);
            workerThread = new Thread(delegate()
            {
                int th = 4;
                try { th = int.Parse(cmbThreads.Text); } catch { }
                try { worker.Run(from, to, th); }
                catch (Exception ex) { Log("出错：" + ex.Message); }
                finally
                {
                    running = false;
                    try
                    {
                        BeginInvoke((MethodInvoker)delegate()
                        {
                            btnStart.Enabled = true;
                            btnStop.Enabled = false;
                            lblStat.Text = "已结束";
                        });
                    }
                    catch { }
                }
            });
            workerThread.IsBackground = true;
            workerThread.Start();
        }

        private void StopRun()
        {
            if (worker != null) { worker.Stop(); Log("已请求停止，正在收尾…"); }
            btnStop.Enabled = false;
        }

        private void OnProgress(ProgressInfo p)
        {
            try
            {
                BeginInvoke((MethodInvoker)delegate()
                {
                    if (p.Total > 0)
                    {
                        int v = (int)((long)p.Done * 100 / p.Total);
                        if (v < 0) { v = 0; }
                        if (v > 100) { v = 100; }
                        bar.Value = v;
                    }
                    lblStat.Text = string.Format("{0}/{1}  成功 {2}  跳过 {3}  失败 {4}  无 UHD {5}",
                        p.Done, p.Total, p.Ok, p.Skip, p.Fail, p.Low);
                });
            }
            catch { }
        }

        private void Log(string line)
        {
            try
            {
                if (InvokeRequired)
                {
                    BeginInvoke((MethodInvoker)delegate() { AppendLog(line); });
                    return;
                }
                AppendLog(line);
            }
            catch { }
        }

        private void AppendLog(string line)
        {
            try
            {
                if (txtLog.Lines.Length > 3000)
                {
                    string[] keep = new string[1000];
                    Array.Copy(txtLog.Lines, txtLog.Lines.Length - 1000, keep, 0, 1000);
                    txtLog.Lines = keep;
                }
                txtLog.AppendText(DateTime.Now.ToString("HH:mm:ss") + "  " + line + "\r\n");
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (running)
            {
                DialogResult r = MessageBox.Show("还在下载，要停下来吗？\r\n（已下载的不受影响，下次可以接着下）",
                    Text, MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (r != DialogResult.OK) { e.Cancel = true; return; }
                if (worker != null) { worker.Stop(); }
            }
            base.OnFormClosing(e);
        }
    }

    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // 自检模式：--test <from> <to> <dest> [索引目录]
            // winexe 没有控制台，结果写进 %TEMP%\bwd-test.log
            if (args.Length >= 4 && args[0] == "--test")
            {
                string testLog = Path.Combine(Path.GetTempPath(), "bwd-test.log");
                try { File.WriteAllText(testLog, ""); } catch { }
                Downloader.LogHandler say = delegate(string s)
                {
                    try { File.AppendAllText(testLog, s + "\r\n"); } catch { }
                };
                try
                {
                    string hint = args.Length >= 5 ? args[4] : Path.GetDirectoryName(Application.ExecutablePath);
                    string idx = Downloader.ResolveIndexDir(hint);
                    say("索引目录：" + (string.IsNullOrEmpty(idx) ? "(没找到)" : idx));
                    if (string.IsNullOrEmpty(idx)) { throw new Exception("索引目录为空"); }
                    Downloader d = new Downloader(idx, args[3], Quality.Best, say, null);
                    System.Collections.Generic.List<DayEntry> list = d.LoadEntries(args[1], args[2]);
                    say(string.Format("范围 {0} ~ {1}：{2} 天", args[1], args[2], list.Count));
                    int show = Math.Min(5, list.Count);
                    for (int i = 0; i < show; i++)
                    {
                        DayEntry e = list[i];
                        say(string.Format("  {0}  slug={1}  id={2}  old={3}",
                            e.Date, e.Slug, e.ImageId == null ? "-" : (e.ImageId.Length > 26 ? e.ImageId.Substring(0, 26) : e.ImageId),
                            e.OldName == null ? "-" : e.OldName));
                    }
                    say("开始下载…");
                    d.Run(args[1], args[2], 2);
                    say("自检结束");
                }
                catch (Exception ex)
                {
                    say("ERROR: " + ex.Message);
                }
                Environment.Exit(0);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new MainForm()); }
            catch (Exception ex)
            {
                MessageBox.Show("启动失败：\r\n" + ex.Message, "必应壁纸 · 全量下载器",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
