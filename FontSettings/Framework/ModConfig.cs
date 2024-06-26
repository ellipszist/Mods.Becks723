﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using FontSettings.Framework.DataAccess.Models;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace FontSettings.Framework
{
    internal class ModConfig
    {
        public string ExampleText { get; set; }

        internal SampleData Sample { get; set; }  // 这个不要储存在config.json里，故设为internal。

        public int MinFontSize { get; set; }

        public int MaxFontSize { get; set; }

        public int MinSpacing { get; set; }

        public int MaxSpacing { get; set; }

        public int MinLineSpacing { get; set; }

        public int MaxLineSpacing { get; set; }

        public int MinCharOffsetX { get; set; }

        public int MaxCharOffsetX { get; set; }

        public int MinCharOffsetY { get; set; }

        public int MaxCharOffsetY { get; set; }

        public float MinPixelZoom { get; set; }

        public float MaxPixelZoom { get; set; }

        public bool DisableTextShadow { get; set; }

        public Color TextColor { get; set; }

        public Color TextColorDialogue { get; set; }

        public Color ShadowColorGame1 { get; set; }

        public Color ShadowColorUtility { get; set; }

        public bool EnableLatinDialogueFont { get; set; }

        public KeybindList OpenFontSettingsMenu { get; set; }

        public IList<string> CustomFontFolders { get; set; }

        public bool EditMode { get; set; }

        public int EditPriority { get; set; }

        private readonly int DEFAULT_MinFontSize = 1;
        private readonly int DEFAULT_MaxFontSize = 100;
        private readonly int DEFAULT_MinSpacing = -10;
        private readonly int DEFAULT_MaxSpacing = 10;
        private readonly int DEFAULT_MinLineSpacing = 1;
        private readonly int DEFAULT_MaxLineSpacing = 100;
        private readonly int DEFAULT_MinCharOffsetX = -50;
        private readonly int DEFAULT_MaxCharOffsetX = 50;
        private readonly int DEFAULT_MinCharOffsetY = -50;
        private readonly int DEFAULT_MaxCharOffsetY = 50;
        private readonly float DEFAULT_MinPixelZoom = 0.5f;
        private readonly float DEFAULT_MaxPixelZoom = 5f;
        private readonly bool DEFAULT_DisableTextShadow = false;
        private readonly Color DEFAULT_TextColor = new Color(34, 17, 34);  // from `Game1.textColor`
        private readonly Color DEFAULT_TextColorDialogue = new Color(86, 22, 12);  // from `SpriteText.getColorFromIndex`
        private readonly Color DEFAULT_ShadowColorGame1 = new Color(206, 156, 95);  // from `Game1.textShadowColor`
        private readonly Color DEFAULT_ShadowColorUtility = new Color(221, 148, 84);  // from `Game1.textShadowDarkerColor` (1.6) and `Utility.drawTextWithShadow()` (before 1.6)
        private readonly bool DEFAULT_EnableLatinDialogueFont = true;
        private readonly KeybindList DEFAULT_OpenFontSettingsMenu = KeybindList.Parse($"{nameof(SButton.LeftAlt)} + {nameof(SButton.F)}");
        private readonly string[] DEFAULT_CustomFontFolders = GetDefaultCustomFontFolders().ToArray();
        private readonly bool DEFAULT_EditMode = true;
        private readonly int DEFAULT_EditPriority = 0;

        public ModConfig()
        {
            this.ResetToDefault();
        }

        public void ResetToDefault()
        {
            this.ExampleText = string.Empty;
            this.MinFontSize = this.DEFAULT_MinFontSize;
            this.MaxFontSize = this.DEFAULT_MaxFontSize;
            this.MinSpacing = this.DEFAULT_MinSpacing;
            this.MaxSpacing = this.DEFAULT_MaxSpacing;
            this.MinLineSpacing = this.DEFAULT_MinLineSpacing;
            this.MaxLineSpacing = this.DEFAULT_MaxLineSpacing;
            this.MinCharOffsetX = this.DEFAULT_MinCharOffsetX;
            this.MaxCharOffsetX = this.DEFAULT_MaxCharOffsetX;
            this.MinCharOffsetY = this.DEFAULT_MinCharOffsetY;
            this.MaxCharOffsetY = this.DEFAULT_MaxCharOffsetY;
            this.MinPixelZoom = this.DEFAULT_MinPixelZoom;
            this.MaxPixelZoom = this.DEFAULT_MaxPixelZoom;
            this.DisableTextShadow = this.DEFAULT_DisableTextShadow;
            this.TextColor = this.DEFAULT_TextColor;
            this.TextColorDialogue = this.DEFAULT_TextColorDialogue;
            this.ShadowColorGame1 = this.DEFAULT_ShadowColorGame1;
            this.ShadowColorUtility = this.DEFAULT_ShadowColorUtility;
            this.EnableLatinDialogueFont = this.DEFAULT_EnableLatinDialogueFont;
            this.OpenFontSettingsMenu = this.DEFAULT_OpenFontSettingsMenu;
            this.CustomFontFolders = new List<string>(this.DEFAULT_CustomFontFolders);
            foreach (string folder in this.CustomFontFolders)
                Directory.CreateDirectory(folder);
            this.EditMode = this.DEFAULT_EditMode;
            this.EditPriority = this.DEFAULT_EditPriority;
        }

        public void ValidateValues(IMonitor? monitor)
        {
            string WarnMessage<T>(string name, T max, T min) => $"{name}：最大值（{max}）小于最小值（{min}）。已重置。";
            void WarnLog<T>(string name, T max, T min) => monitor?.Log(WarnMessage(name, max, min), LogLevel.Warn);

            // x offset
            if (this.MaxCharOffsetX < this.MinCharOffsetX)
            {
                WarnLog("横轴偏移量", this.MaxCharOffsetX, this.MinCharOffsetX);
                this.MaxCharOffsetX = this.DEFAULT_MaxCharOffsetX;
                this.MinCharOffsetX = this.DEFAULT_MinCharOffsetX;
            }

            // y offset
            if (this.MaxCharOffsetY < this.MinCharOffsetY)
            {
                ILog.Warn(WarnMessage("纵轴偏移量", this.MaxCharOffsetY, this.MinCharOffsetY));
                this.MaxCharOffsetY = this.DEFAULT_MaxCharOffsetY;
                this.MinCharOffsetY = this.DEFAULT_MinCharOffsetY;
            }

            // font size
            if (this.MaxFontSize < this.MinFontSize)
            {
                ILog.Warn(WarnMessage("字体大小", this.MaxFontSize, this.MinFontSize));
                this.MaxFontSize = this.DEFAULT_MaxFontSize;
                this.MinFontSize = this.DEFAULT_MinFontSize;
            }

            // spacing
            if (this.MaxSpacing < this.MinSpacing)
            {
                ILog.Warn(WarnMessage("字间距", this.MaxSpacing, this.MinSpacing));
                this.MaxSpacing = this.DEFAULT_MaxSpacing;
                this.MinSpacing = this.DEFAULT_MinSpacing;
            }

            // line spacing
            if (this.MaxLineSpacing < this.MinLineSpacing)
            {
                ILog.Warn(WarnMessage("行间距", this.MaxLineSpacing, this.MinLineSpacing));
                this.MaxLineSpacing = this.DEFAULT_MaxLineSpacing;
                this.MinLineSpacing = this.DEFAULT_MinLineSpacing;
            }

            // pixel zoom
            if (this.MaxPixelZoom < this.MinPixelZoom)
            {
                ILog.Warn(WarnMessage("缩放比例", this.MaxPixelZoom, this.MinPixelZoom));
                this.MaxPixelZoom = this.DEFAULT_MaxPixelZoom;
                this.MinPixelZoom = this.DEFAULT_MinPixelZoom;
            }
        }

        private static IEnumerable<string> GetDefaultCustomFontFolders()
        {
            switch (Constants.TargetPlatform)
            {
                case GamePlatform.Android:
                    yield return Path.Combine(Constants.GamePath, "FontSettings", "fonts");
                    break;

                default:
                    yield break;
            }
        }
    }
}
