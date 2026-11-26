﻿using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BingWallpaperLite
{
    /// <summary>
    /// 接管目录：把图片仓库换成外部目录，状态全落在 takeover\ 沙盒里。
    /// 退出时整个沙盒删掉，安装目录的配置一点都没动过。
    /// </summary>
    internal sealed class TakeoverForm : Form
    {
        private readonly Label lblTitle;
        private readonly Label lblState;
        private readonly Label lblPath;
        private readonly Label lblToday;
        private readonly Label lblAuto;
        private readonly Label lblMissing;
        private readonly Button btnPick;
        private readonly Button btnExit;
        private readonly Button btnImport;
        private Button btnFill;
        private Button btnRefresh;
        // 补齐用的真实范围与数量：RefreshState 算好，DoFill 直接用。
        // 原来写死「最近 3 年」，那个数字和图库实际情况没关系 —— 缺的日期可能落在任何地方。
        private DateTime fillFrom = DateTime.MinValue;
        private DateTime fillTo = DateTime.MinValue;
        private int fillMissing = 0;
        private int fillUnfixable = 0;

        public TakeoverForm()
        {
            Text = Program.AppName + " · " + S.TkTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(620, 332);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            lblTitle = new Label();
            lblTitle.SetBounds(18, 16, 460, 22);
            lblTitle.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            lblTitle.Text = S.TkHeading;
            Controls.Add(lblTitle);

            // 图库可能在窗口开着时变化（补齐 / 手动丢图），给个重扫入口
            btnRefresh = new Button();
            btnRefresh.Text = S.TkRefresh;
            btnRefresh.SetBounds(486, 13, 108, 26);
            btnRefresh.Click += delegate { RefreshState(); };
            Controls.Add(btnRefresh);

            lblState = new Label();
            lblState.SetBounds(18, 46, 580, 22);
            Controls.Add(lblState);

            // 今天那张是谁：图库里最要紧的问题，单独一行
            lblToday = new Label();
            lblToday.AutoSize = false;
            lblToday.SetBounds(18, 72, 580, 20);
            Controls.Add(lblToday);

            // 自动换图 / 轮播的当前状态：改开关不用再点回托盘菜单确认
            lblAuto = new Label();
            lblAuto.AutoSize = false;
            lblAuto.SetBounds(18, 96, 580, 20);
            lblAuto.ForeColor = SystemColors.GrayText;
            Controls.Add(lblAuto);

            lblPath = new Label();
            lblPath.AutoSize = false;
            lblPath.SetBounds(18, 122, 580, 74);
            lblPath.ForeColor = SystemColors.GrayText;
            lblPath.Text = S.TkTip;
            Controls.Add(lblPath);

            // 图库断档：只在真有缺口时出现，没缺口时这一行留白
            lblMissing = new Label();
            lblMissing.AutoSize = false;
            lblMissing.SetBounds(18, 202, 580, 20);
            lblMissing.ForeColor = Color.FromArgb(198, 92, 0);
            lblMissing.Text = "";
            Controls.Add(lblMissing);

            btnPick = new Button();
            btnPick.Text = S.TkPick;
            btnPick.SetBounds(18, 282, 130, 34);
            btnPick.Click += delegate { DoPick(); };
            Controls.Add(btnPick);

            btnExit = new Button();
            btnExit.Text = S.TkExit;
            btnExit.SetBounds(158, 282, 130, 34);
            btnExit.Click += delegate { DoExit(); };
            Controls.Add(btnExit);

            btnImport = new Button();
            btnImport.Text = S.TkImport;
            btnImport.SetBounds(298, 282, 140, 34);
            btnImport.Click += delegate { DoImport(); };
            Controls.Add(btnImport);

            Button btnClose = new Button();
            btnClose.Text = S.BtnClose;
            btnClose.SetBounds(512, 282, 90, 34);
            btnClose.Click += delegate { Close(); };
            Controls.Add(btnClose);
            AcceptButton = btnClose;
            CancelButton = btnClose;

            // 补齐缺失日期：只补图库里没有的那几天，已有的一张都不碰
            btnFill = new Button();
            btnFill.Text = S.TkFillBtn;
            btnFill.SetBounds(18, 240, 200, 30);
            btnFill.Click += delegate { DoFill(); };
            Controls.Add(btnFill);

            RefreshState();
        }

        /// <summary>补齐图库里缺的日期。只补缺的，已有的一张都不动。</summary>
        private void DoFill()
        {
            if (!Program.TakeoverActive()) { return; }
            // 范围用 RefreshState 算出来的真实首日，不写死「最近 3 年」：
            // 图库建了十几年，缺的那几天可能落在任何地方
            DateTime to = fillTo == DateTime.MinValue ? DateTime.Today : fillTo;
            DateTime from = fillFrom == DateTime.MinValue ? to.AddYears(-3) : fillFrom;
            int missing = fillMissing > 0 ? fillMissing : 1;

            if (MessageBox.Show(S.TkFillAsk(from, to, missing), Program.AppName,
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK)
            {
                return;
            }
            lblState.Text = S.TkFilling;
            btnFill.Enabled = false;
            string args = "-FillLibrary -RangeFrom " + from.ToString("yyyyMMdd") + " -RangeTo " + to.ToString("yyyyMMdd");
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                try { Program.RunScriptExit(args); } catch { }
                try
                {
                    BeginInvoke((MethodInvoker)delegate
                    {
                        try { RefreshState(); } catch { }
                    });
                }
                catch { }
            });
        }

        private void RefreshState()
        {
            try
            {
                string root = Program.CurrentTakeoverRoot();
                bool active = Program.TakeoverActive();
                if (active)
                {
                    int n = 0;
                    int days = 0;
                    string first = "";
                    string latest = "";
                    int stale = -1;
                    int missingN = 0;
                    string missingSample = "";
                    try
                    {
                        string[] fs = Directory.GetFiles(root, "*.jpg", SearchOption.AllDirectories);
                        n = fs.Length;
                        System.Collections.Generic.HashSet<string> ds = new System.Collections.Generic.HashSet<string>();
                        foreach (string f in fs)
                        {
                            string fn = Path.GetFileName(f);
                            if (fn.Length >= 8) { ds.Add(fn.Substring(0, 8)); }
                        }
                        days = ds.Count;
                        if (days > 0)
                        {
                            System.Collections.Generic.List<string> ls =
                                new System.Collections.Generic.List<string>(ds);
                            ls.Sort();
                            first = ls[0];
                            latest = ls[ls.Count - 1];
                            DateTime dtL;
                            if (DateTime.TryParseExact(latest, "yyyyMMdd", null,
                                    System.Globalization.DateTimeStyles.None, out dtL))
                            {
                                stale = (int)(DateTime.Today - dtL.Date).TotalDays;
                            }

                            // 断档：首日到末日逐日查。只在真有缺口时列前 3 个，
                            // 缺口多到写不下时由 TkMissingDays 给总数。
                            DateTime dtF, dtE;
                            if (DateTime.TryParseExact(first, "yyyyMMdd", null,
                                    System.Globalization.DateTimeStyles.None, out dtF) &&
                                DateTime.TryParseExact(latest, "yyyyMMdd", null,
                                    System.Globalization.DateTimeStyles.None, out dtE))
                            {
                                System.Collections.Generic.List<string> miss =
                                    new System.Collections.Generic.List<string>();
                                int guard = 0;
                                for (DateTime d = dtF.Date; d <= dtE.Date && guard < 20000; d = d.AddDays(1), guard++)
                                {
                                    string k = d.ToString("yyyyMMdd");
                                    if (ds.Contains(k)) { continue; }
                                    missingN++;
                                    // 2016-03-05 是全量索引的最早一天。比它更早的日期
                                    // 必应索引本身就没有，想补也补不到 —— 两类要分开说，
                                    // 否则点「补齐」会以为全都能补回来。
                                    if (string.CompareOrdinal(k, "20160305") >= 0)
                                    {
                                        if (miss.Count < 3) { miss.Add(k.Substring(4, 2) + "-" + k.Substring(6, 2)); }
                                        fillMissing++;
                                        if (fillFrom == DateTime.MinValue || d.Date < fillFrom) { fillFrom = d.Date; }
                                    }
                                    else
                                    {
                                        fillUnfixable++;
                                    }
                                }
                                missingSample = string.Join(" / ", miss.ToArray());
                            }
                        }
                    }
                    catch { }
                    lblState.Text = S.TkStatLine(n, days, S.FormatDate(first), S.FormatDate(latest));

                    // 今日：桌面这张到底是不是今天的。接管后最容易出问题的就是这里 ——
                    // 图库再全，桌面停在前一天也没意义，所以单独一行说清楚。
                    string stDate, stTitle;
                    Program.CurrentStateDateTitle(out stDate, out stTitle);
                    string today = DateTime.Today.ToString("yyyyMMdd");
                    if (stDate.Length > 0 && string.Equals(stDate, today, StringComparison.Ordinal))
                    {
                        lblToday.Text = S.TkTodayLine(S.FormatDate(stDate), stTitle);
                    }
                    else
                    {
                        lblToday.Text = S.TkTodayMissing(S.FormatDate(stDate.Length > 0 ? stDate : latest));
                    }

                    // 自动换图与轮播：从这里就能看出会不会自动变，不用再点回托盘菜单
                    string mode = Program.CurrentWallMode();
                    lblAuto.Text = S.TkAutoLine(mode == Program.WallModePaused,
                                                mode == Program.WallModeRotate,
                                                Program.RotateMinutes);

                    if (fillMissing > 0 && fillUnfixable > 0)
                    {
                        lblMissing.Text = S.TkMissingMixed(fillMissing, fillUnfixable, missingSample);
                    }
                    else if (fillMissing > 0)
                    {
                        lblMissing.Text = S.TkMissingFixable(fillMissing, missingSample);
                    }
                    else if (fillUnfixable > 0)
                    {
                        lblMissing.Text = S.TkMissingNoIndex(fillUnfixable);
                    }
                    else
                    {
                        lblMissing.Text = "";
                    }
                    if (btnFill != null) { btnFill.Enabled = fillMissing > 0; }

                    // 统计时间附在目录那段末尾：窗口没开时看不到，开窗时一眼知道数据是新算的还是旧的
                    lblPath.Text = S.TkOnBody(root, n, days, latest, stale) +
                        "\r\n" + S.TkStatAt(DateTime.Now.ToString("HH:mm:ss"));
                    // 接管中不给「换一个目录」：换库要么先把当前库退出，要么两边搅在一起。
                    // 藏掉之后把后面两个按钮左移补位，不然左边留一个空洞。
                    if (btnPick != null) { btnPick.Visible = false; }
                    btnExit.Left = 18;
                    btnImport.Left = 158;
                    btnExit.Enabled = true;
                    if (btnFill != null) { btnFill.Enabled = true; }
                }
                else if (!string.IsNullOrEmpty(root))
                {
                    lblState.Text = S.TkBrokenState;
                    lblToday.Text = "";
                    lblAuto.Text = "";
                    lblMissing.Text = "";
                    lblPath.Text = S.TkBrokenBody(root);
                    if (btnPick != null) { btnPick.Visible = true; btnPick.Text = S.TkPick; btnPick.Left = 18; }
                    btnExit.Left = 158;
                    btnImport.Left = 298;
                    btnExit.Enabled = true;
                }
                else
                {
                    lblState.Text = S.TkOff;
                    lblToday.Text = "";
                    lblAuto.Text = "";
                    lblMissing.Text = "";
                    lblPath.Text = S.TkTip;
                    if (btnPick != null) { btnPick.Visible = true; btnPick.Text = S.TkPick; btnPick.Left = 18; }
                    btnExit.Left = 158;
                    btnImport.Left = 298;
                    btnExit.Enabled = false;
                }
                if (btnFill != null) { btnFill.Enabled = active; }
            }
            catch { }
        }

        private void DoPick()
        {
            if (Program.TakeoverActive())
            {
                MessageBox.Show(S.TkChangeBlocked, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = S.TkChoose;
                d.ShowNewFolderButton = false;
                if (d.ShowDialog(this) != DialogResult.OK) { return; }
                string chosen = d.SelectedPath;
                if (string.IsNullOrEmpty(chosen)) { return; }
                if (Directory.GetFiles(chosen, "*.jpg").Length == 0)
                {
                    if (MessageBox.Show(S.TkEmptyWarn, Program.AppName, MessageBoxButtons.OKCancel,
                            MessageBoxIcon.Warning) != DialogResult.OK) { return; }
                }
                try
                {
                    string dir = Program.TakeoverDirPath;
                    if (!Directory.Exists(dir)) { Directory.CreateDirectory(dir); }
                    System.Web.Script.Serialization.JavaScriptSerializer js =
                        new System.Web.Script.Serialization.JavaScriptSerializer();
                    System.Collections.Generic.Dictionary<string, object> cfg =
                        new System.Collections.Generic.Dictionary<string, object>();
                    cfg["root"] = chosen;
                    cfg["enteredAt"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                    File.WriteAllText(Program.TakeoverCfgPath, js.Serialize(cfg),
                        new System.Text.UTF8Encoding(false));
                    // 编年史接管期间画廊没有意义：进来就关掉并清空临时试看区，不留残图
                    if (Program.GalleryMode)
                    {
                        Program.GalleryMode = false;
                        try { Program.RunGalleryClear(null); } catch { }
                    }
                    MessageBox.Show(S.TkEntered(chosen), Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    RefreshState();
                    Program.RefreshAfterTakeover();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(S.TkFail + "\r\n" + ex.Message, Program.AppName,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void DoExit()
        {
            int favs = 0;
            try
            {
                string fd = Path.Combine(Program.TakeoverDirPath, "favorites");
                if (Directory.Exists(fd)) { favs = Directory.GetFiles(fd, "*.jpg").Length; }
            }
            catch { }
            string q = (favs > 0 ? S.TkExitConfirmFav(favs) : S.TkExitConfirm) + "\r\n\r\n" + S.TkBackupAsk;
            DialogResult r = MessageBox.Show(q, Program.AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
            if (r == DialogResult.Cancel) { return; }
            try
            {
                if (r == DialogResult.Yes)
                {
                    string bdir = Program.BackupTakeover();
                    if (!string.IsNullOrEmpty(bdir)) { MessageBox.Show(S.TkBackedUp(bdir), Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information); }
                }
                Program.ExitTakeover(true);
                MessageBox.Show(S.TkExited, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshState();
                Program.RefreshAfterTakeover();
            }
            catch (Exception ex)
            {
                MessageBox.Show(S.TkFail + "\r\n" + ex.Message, Program.AppName,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DoImport()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = S.TkImportPick;
                d.ShowNewFolderButton = false;
                if (d.ShowDialog(this) != DialogResult.OK) { return; }
                if (!Program.ImportTakeover(d.SelectedPath))
                {
                    MessageBox.Show(S.TkImportFail, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                MessageBox.Show(S.TkImported, Program.AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
                RefreshState();
                Program.RefreshAfterTakeover();
            }
        }

        /// <summary>打开图库目录；与托盘菜单那个标牌共用同一份实现。</summary>
        private void OpenRoot()
        {
            Program.OpenTakeoverRoot();
        }
    }
}
