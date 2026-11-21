using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace BingWallpaperLite
{
    internal sealed class AiForm : Form
    {
        public AiForm()
        {
            Text = Program.AppName + S.AiTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition = FormStartPosition.CenterScreen;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = true;
            ClientSize = new Size(660, 540);
            try { Font = new Font("Microsoft YaHei UI", 9F); } catch { }

            Label intro = new Label();
            intro.AutoSize = false;
            intro.SetBounds(20, 16, 620, 76);
            intro.Text = S.AiIntro;
            Controls.Add(intro);
            int boxTop = intro.Bottom + 10;

            TextBox box = new TextBox();
            box.Multiline = true;
            box.ScrollBars = ScrollBars.Vertical;
            box.ReadOnly = true;
            box.SetBounds(20, boxTop, 620, 340);
            box.Font = new Font("Consolas", 9F);
            box.Text = BuildPrompt();
            box.Select(0, 0);
            Controls.Add(box);

            int btnTop = box.Bottom + 16;

            Button btnPath = new Button();
            btnPath.Text = S.AiCopyPath;
            btnPath.SetBounds(20, btnTop, 130, 32);
            btnPath.Click += delegate { CopyTo(Program.ScriptPathPublic, btnPath); };
            Controls.Add(btnPath);

            Button btnCmds = new Button();
            btnCmds.Text = S.AiCopyCmds;
            btnCmds.SetBounds(160, btnTop, 140, 32);
            btnCmds.Click += delegate { CopyTo(BuildCommands(), btnCmds); };
            Controls.Add(btnCmds);

            Button btnPrompt = new Button();
            btnPrompt.Text = S.AiCopyPrompt;
            btnPrompt.SetBounds(310, btnTop, 170, 32);
            btnPrompt.Click += delegate { CopyTo(BuildPrompt(), btnPrompt); };
            Controls.Add(btnPrompt);

            Button btnClose = new Button();
            btnClose.Text = S.BtnOK;
            btnClose.SetBounds(554, btnTop, 86, 32);
            btnClose.DialogResult = DialogResult.OK;
            Controls.Add(btnClose);

            AcceptButton = btnClose;
            CancelButton = btnClose;

            ClientSize = new Size(660, btnTop + 32 + 20);
        }

        private static string BuildCommands()
        {
            string exe = Program.ScriptPathPublic;
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"命令行入口：");
            sb.AppendLine(exe);
            sb.AppendLine();
            sb.AppendLine(@"可用参数：");
            sb.AppendLine(@"  -Index 0            换今天的图");
            sb.AppendLine(@"  -Index 1..7         往前翻 1-7 天");
            sb.AppendLine(@"  -Step 1 / -Step -1  相对前后翻一天");
            sb.AppendLine(@"  -Date YYYYMMDD      换指定日期（2016-03-05 至今）");
            sb.AppendLine(@"  -Random             从收藏里随机");
            sb.AppendLine(@"  -Use ""路径""         用本地图片");
            sb.AppendLine(@"  -Favorite           收藏当前这张");
            sb.AppendLine(@"  -Unfavorite         取消收藏");
            sb.AppendLine(@"  -Query ""雪 山""      搜索并列出匹配项（不改图）");
            sb.AppendLine(@"  -Status             查当前状态（壁纸、各段计数、配置）");
            sb.AppendLine(@"  -Json               让任何操作返回结构化 JSON 结果");
            sb.AppendLine(@"  -Search ""雪 山""     搜索并换第一张匹配的");
            sb.AppendLine(@"  -Clean -Keep 7      清理 7 天前的缓存");
            sb.AppendLine(@"  -CleanAll           清空缓存（保留当前壁纸）");
            sb.AppendLine(@"  -DryRun             只演示不执行");
            return sb.ToString();
        }

        private static string BuildPrompt()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine(@"我有一个必应壁纸工具，命令行入口是：");
            sb.AppendLine(Program.ScriptPathPublic);
            sb.AppendLine();
            sb.AppendLine(@"用 pwsh 调用，例如：");
            sb.AppendLine(@"  pwsh -File """ + Program.ScriptPathPublic + @""" -Date 20230615");
            sb.AppendLine();
            sb.AppendLine(@"支持的参数：");
            sb.AppendLine(@"  -Index 0            换今天的");
            sb.AppendLine(@"  -Index 1..7         往前翻");
            sb.AppendLine(@"  -Step 1 / -Step -1  前后翻一天");
            sb.AppendLine(@"  -Date YYYYMMDD      指定日期（2016-03-05 至今）");
            sb.AppendLine(@"  -Random             从收藏随机");
            sb.AppendLine(@"  -Favorite           收藏当前");
            sb.AppendLine(@"  -Query ""关键词""     搜索并列出匹配项");
            sb.AppendLine(@"  -Status             查当前状态（返回 JSON）");
            sb.AppendLine(@"  -Json               加在任何命令后，返回结构化结果");
            sb.AppendLine();
            sb.AppendLine(@"换图后加 -Json 就知道换成了哪张。");
            sb.AppendLine(@"  -Search ""关键词""    搜索并换第一张匹配的");
            sb.AppendLine();
            sb.AppendLine(@"搜索说明：");
            sb.AppendLine(@"  · 关键词用空格分隔，脚本做字面匹配（标题 + 版权）");
            sb.AppendLine(@"  · 中文分词要靠你自己扩词，比如「雪山」要拆成 ""雪 山 冰川 雪峰""");
            sb.AppendLine(@"  · 先用 -Query 看结果，挑好了再用 -Date 换");
            sb.AppendLine();
            sb.AppendLine(@"我的需求：在这里写你要什么。");
            return sb.ToString();
        }

        private static void CopyTo(string text, Button b)
        {
            try
            {
                Clipboard.SetText(text);
                string old = b.Text;
                b.Text = S.AiCopied;
                System.Windows.Forms.Timer tm = new System.Windows.Forms.Timer();
                tm.Interval = 1200;
                tm.Tick += delegate { b.Text = old; tm.Stop(); tm.Dispose(); };
                tm.Start();
            }
            catch { }
        }
    }
    /// <summary>清理前弹窗：显示数量与体积，选择删除方式。</summary>
}
