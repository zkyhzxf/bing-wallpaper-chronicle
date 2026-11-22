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
            sb.AppendLine(@"  -Date YYYYMMDD      换指定日期（2009-07-13 至今；2016-03 前是镜像档，分辨率较低）");
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
            sb.AppendLine(@"  -CleanArchive       清理归档图片（可加 -WithIndex 连索引、-IncludeOld 连老存档）");
            sb.AppendLine(@"  -CleanFavorites     清空收藏（当前壁纸移回缓存）");
            sb.AppendLine(@"  -Gallery            画廊模式：图落 cache\gallery，不登记保护名单");
            sb.AppendLine(@"  -GalleryClear       退出画廊模式，清空 cache\gallery");
            sb.AppendLine(@"  -FavDate 20261120   把该日期的图加入收藏");
            sb.AppendLine(@"  -UnfavDate 20261120 把收藏里该日期移回缓存");
            sb.AppendLine(@"  -ExportLinks -RangeFrom 20261101 -RangeTo 20261121");
            sb.AppendLine(@"                      只导出下载直链（JSON），不下载图片");
            sb.AppendLine(@"  -ExportRange -RangeFrom 20261101 -RangeTo 20261121 -DestDir 目录");
            sb.AppendLine(@"                      按日期范围批量下载到指定目录");
            sb.AppendLine(@"  -KeepExisting       批量下载时目标已有同名文件就直接跳过（默认会检查画质并升级）");
            sb.AppendLine();
            sb.AppendLine(@"  【照片墙与预取】");
            sb.AppendLine(@"  -WallDate YYYYMMDD  取该日期的图到 cache\wall（不设壁纸、不写状态）");
            sb.AppendLine(@"  -WallFull           配合 -WallDate，直接要 UHD 而非 1080p 预览");
            sb.AppendLine(@"  -WallKeep           把 cache\wall 的图转正进 archive\images（自动补 UHD）");
            sb.AppendLine(@"  -WallDrop           丢弃 cache\wall 的临时图");
            sb.AppendLine(@"  -PrefetchDate YYYYMMDD  预取到 cache\prefetch（不设壁纸）");
            sb.AppendLine();
            sb.AppendLine(@"  【查找与维护】");
            sb.AppendLine(@"  -KwFile kw.txt      关键词从 UTF-8 文件读（PS 5.1 传中文更稳）");
            sb.AppendLine(@"  -Festivals          九个节日各有多少天");
            sb.AppendLine(@"  -Festival 中秋节    该节日的全部日期（JSON，农历现场算）");
            sb.AppendLine(@"  -AuditDates         核对索引日期完整性");
            sb.AppendLine(@"  -AuditOld           核对老索引");
            sb.AppendLine(@"  -MigrateOld         把错位的老存档挪回 archive\old\<月份>\（3.6.48 及更早的遗留）");
            sb.AppendLine();
            sb.AppendLine(@"  -UpdateIndex        强制刷新全量索引");
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
            sb.AppendLine(@"  -Date YYYYMMDD      指定日期（2009-07-13 至今）");
            sb.AppendLine(@"  -Random             从收藏随机");
            sb.AppendLine(@"  -Use ""路径""         用本地图片");
            sb.AppendLine(@"  -Favorite           收藏当前");
            sb.AppendLine(@"  -Unfavorite         取消收藏");
            sb.AppendLine(@"  -Query ""关键词""     搜索并列出匹配项");
            sb.AppendLine(@"  -Status             查当前状态（返回 JSON）");
            sb.AppendLine(@"  -Json               加在任何命令后，返回结构化结果");
            sb.AppendLine(@"  -DryRun             只演示不执行");
            sb.AppendLine();
            sb.AppendLine(@"换图后加 -Json 就知道换成了哪张。");
            sb.AppendLine(@"  -Search ""关键词""    搜索并换第一张匹配的");
            sb.AppendLine();
            sb.AppendLine(@"搜索说明：");
            sb.AppendLine(@"  · 关键词用空格分隔，脚本做字面匹配（标题 + 版权）");
            sb.AppendLine(@"  · 中文分词要靠你自己扩词，比如「雪山」要拆成 ""雪 山 冰川 雪峰""");
            sb.AppendLine(@"  · 先用 -Query 看结果，挑好了再用 -Date 换");
            sb.AppendLine(@"  · 2019 年前的日期没有 UHD，拿到的是 1080p 或镜像档，不是出错");
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
