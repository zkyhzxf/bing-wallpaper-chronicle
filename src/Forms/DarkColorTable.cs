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
    internal sealed class DarkColorTable : ProfessionalColorTable
    {
        public static readonly Color Bg        = Color.FromArgb(32, 32, 32);
        public static readonly Color BgHover   = Color.FromArgb(58, 58, 58);
        public static readonly Color Border    = Color.FromArgb(70, 70, 70);
        public static readonly Color Separator = Color.FromArgb(70, 70, 70);
        public static readonly Color Text      = Color.FromArgb(235, 235, 235);

        public override Color ToolStripDropDownBackground { get { return Bg; } }
        public override Color ImageMarginGradientBegin    { get { return Bg; } }
        public override Color ImageMarginGradientMiddle   { get { return Bg; } }
        public override Color ImageMarginGradientEnd      { get { return Bg; } }
        public override Color MenuBorder                  { get { return Border; } }
        public override Color MenuItemBorder              { get { return BgHover; } }
        public override Color MenuItemSelected            { get { return BgHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return BgHover; } }
        public override Color MenuItemSelectedGradientEnd   { get { return BgHover; } }
        public override Color MenuItemPressedGradientBegin  { get { return BgHover; } }
        public override Color MenuItemPressedGradientEnd    { get { return BgHover; } }
        public override Color SeparatorDark               { get { return Separator; } }
        public override Color SeparatorLight              { get { return Separator; } }
    }

    // -------------------------------------------------------------------- 选日期
}
