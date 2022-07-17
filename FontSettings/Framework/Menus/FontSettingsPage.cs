﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FontSettings.Framework.FontInfomation;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValleyUI;
using StardewValleyUI.Controls;
using StardewValleyUI.Data;
using StardewValleyUI.Menus;

namespace FontSettings.Framework.Menus
{
    internal class FontSettingsPage : BaseMenu
    {
        private static readonly StateManager _states = new();
        private static FontSettingsPage Instance { get; set; }

        private readonly ModConfig _config;
        private readonly RuntimeFontManager _fontManager;
        private readonly GameFontChanger _fontChanger;
        private readonly Action<ModConfig> _saveConfig;
        private readonly ExampleFonts _exampleFonts;

        private readonly Color _gameExampleColor = Color.Gray * 0.67f;
        private readonly Color _customExampleColor = Game1.textColor;
        private readonly TextureButton _leftArrow;
        private readonly TextureButton _rightArrow;
        private readonly Label2 _label_title;
        private readonly TextureBox _exampleBoard;
        private readonly LabeledElement<Checkbox> _box_merge;  // TODO: 改成图标？
        private readonly LabeledElement<Checkbox> _box_showBounds;
        private readonly LabeledElement<Checkbox> _box_showText;
        private readonly FontExampleLabel _label_gameExample;
        private readonly FontExampleLabel _label_currentExample;
        private readonly LabeledElement<Checkbox> _box_enabledFont;
        private readonly Label2 _label_game;
        private readonly Label2 _label_current;
        private readonly ColorBlock _colorBlock_game;
        private readonly ColorBlock _colorBlock_current;
        private readonly ComboBox _dropDown_font;
        private readonly LabeledElement<Slider<int>> _slider_fontSize;
        private readonly LabeledElement<Slider<int>> _slider_spacing;
        private readonly LabeledElement<Slider<int>> _slider_lineSpacing;
        private readonly OKButton _okButton;

        private bool _lastEnabled;
        private string _lastFontFilePath;
        private int _lastFontSize;
        private int _lastSpacing;
        private int _lastLineSpacing;
        private FontModel[] _allFonts;

        protected override bool ManualInitializeComponents => true;

        private GameFontType CurrentFontType { get; set; }

        public FontSettingsPage(ModConfig config, RuntimeFontManager fontManager, GameFontChanger fontChanger, Action<ModConfig> saveConfig,
            int x, int y, int width, int height, bool showUpperRightCloseButton = false)
            : base(x, y, width, height, showUpperRightCloseButton)
        {
            Instance = this;

            this._config = config;
            this._fontManager = fontManager;
            this._fontChanger = fontChanger;
            this._saveConfig = saveConfig;
            this._exampleFonts = new ExampleFonts(fontManager);

            FontConfig fontConfig = config.Fonts.GetOrCreateFontConfig(LocalizedContentManager.CurrentLanguageCode,
                FontHelpers.GetCurrentLocale(), this.CurrentFontType);

            this._leftArrow = new TextureButton(Game1.mouseCursors, new(352, 495, 12, 11), 4f)
            {
                LocalPosition = new Vector2(-48 - 48, height / 2),
                SettableWidth = 48,
                SettableHeight = 44
            };
            this._leftArrow.Click += this.LeftArrowClicked;
            this._rightArrow = new TextureButton(Game1.mouseCursors, new(365, 495, 12, 11), 4f)
            {
                LocalPosition = new Vector2(width + 48, height / 2),
                SettableWidth = 48,
                SettableHeight = 44
            };
            this._rightArrow.Click += this.RightArrowClicked;

            this._label_title = new Label2
            {
                Bold = true,
                Text = this.CurrentFontType.LocalizedName(),
            };
            this._label_title.LocalPosition = new Vector2(width / 2 - this._label_title.Width / 2, 108);

            this._exampleBoard = new TextureBox
            {
                Kind = TextureBoxs.DefaultBorderless,
                DrawShadow = false,
                LocalPosition = new Vector2(spaceToClearSideBorder + borderWidth, this._label_title.LocalPosition.Y + this._label_title.Height),
                SettableHeight = height / 3
            };
            this._exampleBoard.SettableWidth = width - spaceToClearSideBorder - borderWidth - (int)this._exampleBoard.LocalPosition.X;

            Checkbox seperateBox = new Checkbox();
            seperateBox.Checked += this.ExampleSeperateChanged;
            seperateBox.Unchecked += this.ExampleSeperateChanged;
            this._box_merge = new LabeledElement<Checkbox>(seperateBox)
            {
                Text = I18n.OptionsPage_MergeExamples()
            };

            Checkbox showBoundsBox = new Checkbox();
            showBoundsBox.Checked += this.ShowBoundsChanged;
            showBoundsBox.Unchecked += this.ShowBoundsChanged;
            this._box_showBounds = new LabeledElement<Checkbox>(showBoundsBox)
            {
                Text = I18n.OptionsPage_ShowExampleBounds()
            };

            Checkbox showTextBox = new Checkbox();
            showTextBox.IsChecked = true;
            showTextBox.Checked += this.ShowTextChanged;
            showTextBox.Unchecked += this.ShowTextChanged;
            this._box_showText = new LabeledElement<Checkbox>(showTextBox)
            {
                Text = I18n.OptionsPage_ShowExampleText()
            };
            float gap = (this._exampleBoard.Height - this._box_merge.Height - this._box_showText.Height - this._box_showBounds.Height) / 4f;
            float insideBoardX = this._exampleBoard.LocalPosition.X + borderWidth / 3;
            this._box_merge.LocalPosition = new Vector2(insideBoardX, this._exampleBoard.LocalPosition.Y + gap);
            this._box_showBounds.LocalPosition = new Vector2(insideBoardX, this._box_merge.LocalPosition.Y + this._box_merge.Height + gap);
            this._box_showText.LocalPosition = new Vector2(insideBoardX, this._box_showBounds.LocalPosition.Y + this._box_showBounds.Height + gap);

            this._label_game = new Label2
            {
                Text = I18n.OptionsPage_OriginalExample()
            };
            this._label_current = new Label2
            {
                Text = I18n.OptionsPage_CustomExample()
            };
            this._colorBlock_game = new ColorBlock(this._gameExampleColor, 20);
            this._colorBlock_current = new ColorBlock(this._customExampleColor, 20);
            int maxWidth = Math.Max(this._label_game.Width, this._label_current.Width);
            int exampleLabelHeight = Math.Max(this._colorBlock_game.Height, this._label_game.Height);
            int currentLabelHeight = Math.Max(this._colorBlock_current.Height, this._label_current.Height);
            this._label_current.LocalPosition = new Vector2(this._exampleBoard.LocalPosition.X + this._exampleBoard.Width - borderWidth / 3 - maxWidth, this._exampleBoard.LocalPosition.Y + this._exampleBoard.Height - borderWidth / 3 - currentLabelHeight);
            this._label_game.LocalPosition = new Vector2(this._exampleBoard.LocalPosition.X + this._exampleBoard.Width - borderWidth / 3 - maxWidth, this._label_current.LocalPosition.Y - borderWidth / 3 - exampleLabelHeight);
            this._colorBlock_current.LocalPosition = new Vector2(this._label_current.LocalPosition.X - borderWidth / 6 - this._colorBlock_current.Width, this._label_current.LocalPosition.Y + this._label_current.Height / 2 - this._colorBlock_current.Height / 2);
            this._colorBlock_game.LocalPosition = new Vector2(this._label_game.LocalPosition.X - borderWidth / 6 - this._colorBlock_game.Width, this._label_game.LocalPosition.Y + this._label_game.Height / 2 - this._colorBlock_game.Height / 2);

            this._label_gameExample = new FontExampleLabel()
            {
                IdleTextColor = _gameExampleColor,
                BoundsColor = Color.Red * 0.5f,
                ShowBounds = this._box_showBounds.Element.IsChecked,
                ShowText = this._box_showText.Element.IsChecked,
            };
            this._label_currentExample = new FontExampleLabel()
            {
                IdleTextColor = _customExampleColor,
                BoundsColor = Color.Green * 0.5f,
                ShowBounds = this._box_showBounds.Element.IsChecked,
                ShowText = this._box_showText.Element.IsChecked,
            };

            Checkbox enabledFontBox = new Checkbox();
            enabledFontBox.Checked += this.FontEnableChanged;
            enabledFontBox.Unchecked += this.FontEnableChanged;
            enabledFontBox.IsChecked = fontConfig.Enabled;

            int sliderLength = this._exampleBoard.Width / 3;
            var fontSizeSlider = new Slider<int>
            {
                RequestWidth = sliderLength,
                Minimum = 1,
                Maximum = 100,
                Interval = 1,
                Value = (int)fontConfig.FontSize
            };
            fontSizeSlider.ValueChanged += this.FontSizeSlider_ValueChanged;
            var spacingSlider = new Slider<int>
            {
                RequestWidth = sliderLength,
                Minimum = -10,
                Maximum = 10,
                Interval = 1,
                Value = (int)fontConfig.Spacing
            };
            spacingSlider.ValueChanged += this.SpacingSlider_ValueChanged;
            var lineSpacingSlider = new Slider<int>
            {
                RequestWidth = sliderLength,
                Minimum = 1,
                Maximum = 100,
                Interval = 1,
                Value = fontConfig.LineSpacing
            };
            lineSpacingSlider.ValueChanged += this.LineSpacingSlider_ValueChanged;

            this._box_enabledFont = new LabeledElement<Checkbox>(enabledFontBox)
            {
                Text = I18n.OptionsPage_Enable()
            };
            this._slider_fontSize = new LabeledElement<Slider<int>>(fontSizeSlider)
            {
                Text = I18n.OptionsPage_FontSize()
            };
            this._slider_spacing = new LabeledElement<Slider<int>>(spacingSlider)
            {
                Text = I18n.OptionsPage_Spacing()
            };
            this._slider_lineSpacing = new LabeledElement<Slider<int>>(lineSpacingSlider)
            {
                Text = I18n.OptionsPage_LineSpacing()
            };
            float exampleBoardBottom = this._exampleBoard.LocalPosition.Y + this._exampleBoard.Height;
            float exampleBoardX = this._exampleBoard.LocalPosition.X;
            gap = (height - spaceToClearSideBorder - exampleBoardBottom - this._box_enabledFont.Height - this._slider_fontSize.Height - this._slider_spacing.Height - this._slider_lineSpacing.Height) / 5;
            this._box_enabledFont.LocalPosition = new Vector2(exampleBoardX, exampleBoardBottom + gap);
            this._slider_fontSize.LocalPosition = new Vector2(exampleBoardX, this._box_enabledFont.LocalPosition.Y + this._box_enabledFont.Height + gap);
            this._slider_spacing.LocalPosition = new Vector2(exampleBoardX, this._slider_fontSize.LocalPosition.Y + this._slider_fontSize.Height + gap);
            this._slider_lineSpacing.LocalPosition = new Vector2(exampleBoardX, this._slider_spacing.LocalPosition.Y + this._slider_spacing.Height + gap);

            this._allFonts = this.LoadAllFonts();
            string ParseFontData(FontModel item)
            {
                if (item.FullPath == null)
                    return I18n.OptionsPage_Font_KeepOrig();
                else
                    return $"{item.FamilyName} ({item.SubfamilyName})";
            }
            this._dropDown_font = new ComboBox
            {
                SettableWidth = this._exampleBoard.Width / 2,
                Choices = _allFonts,
                DisplayTextReslover = (_, item) => ParseFontData((FontModel)item),
                MaxDisplayRows = 6,
            };
            this._dropDown_font.LocalPosition = new Vector2(this._exampleBoard.LocalPosition.X + this._exampleBoard.Width - this._dropDown_font.Width, this._box_enabledFont.LocalPosition.Y);

            this._okButton = new OKButton();
            this._okButton.Click += this.OkButtonClicked;
            this._okButton.GreyedOut = _states.IsOn(this.CurrentFontType);
            this._okButton.LocalPosition = new Vector2(
                width - spaceToClearSideBorder - borderWidth - this._okButton.Width,
                height - spaceToClearSideBorder - borderWidth - this._okButton.Height);

            this.OnFontTypeChanged(this.CurrentFontType);

            this.ResetComponents();
        }


        private void LeftArrowClicked(object sender, EventArgs e)
        {
            Game1.playSound("smallSelect");
            this.CurrentFontType = this.CurrentFontType.Previous(LocalizedContentManager.CurrentLanguageLatin);
            this.OnFontTypeChanged(this.CurrentFontType);
        }

        private void RightArrowClicked(object sender, EventArgs e)
        {
            Game1.playSound("smallSelect");
            this.CurrentFontType = this.CurrentFontType.Next(LocalizedContentManager.CurrentLanguageLatin);
            this.OnFontTypeChanged(this.CurrentFontType);
        }

        private void ShowTextChanged(object sender, EventArgs e)
        {
            bool newValue = this._box_showText.Element.IsChecked;
            this._label_gameExample.ShowText = newValue;
            this._label_currentExample.ShowText = newValue;
        }

        private void ShowBoundsChanged(object sender, EventArgs e)
        {
            bool newValue = this._box_showBounds.Element.IsChecked;
            this._label_gameExample.ShowBounds = newValue;
            this._label_currentExample.ShowBounds = newValue;
        }

        private void ExampleSeperateChanged(object sender, EventArgs e)
        {
            this.UpdateExamplePositions();
        }

        private void FontEnableChanged(object sender, EventArgs e)
        {
            this.UpdateCustomExample();
        }

        private void FontSizeSlider_ValueChanged(object sender, EventArgs e)
        {
            this.UpdateCustomExample();
        }

        private void SpacingSlider_ValueChanged(object sender, EventArgs e)
        {
            this.UpdateCustomExample();
        }

        private void LineSpacingSlider_ValueChanged(object sender, EventArgs e)
        {
            this.UpdateCustomExample();
        }

        private async void OkButtonClicked(object sender, EventArgs e)
        {
            Game1.playSound("coin");
            FontConfig config = this._config.Fonts.GetOrCreateFontConfig(LocalizedContentManager.CurrentLanguageCode,
                FontHelpers.GetCurrentLocale(), this.CurrentFontType);

            FontConfig tempConfig = new FontConfig();
            config.CopyTo(tempConfig);

            this._lastEnabled = tempConfig.Enabled;
            this._lastFontFilePath = tempConfig.FontFilePath;
            this._lastFontSize = (int)tempConfig.FontSize;
            this._lastSpacing = (int)tempConfig.Spacing;
            this._lastLineSpacing = tempConfig.LineSpacing;

            tempConfig.Enabled = this._box_enabledFont.Element.IsChecked;
            tempConfig.FontFilePath = this.GetFontFile();
            tempConfig.FontIndex = this.GetFontIndex();
            tempConfig.FontSize = this._slider_fontSize.Element.Value;
            tempConfig.Spacing = this._slider_spacing.Element.Value;
            tempConfig.LineSpacing = this._slider_lineSpacing.Element.Value;

            bool enabledChanged = this._lastEnabled != tempConfig.Enabled;
            bool fontFilePathChanged = this._lastFontFilePath != tempConfig.FontFilePath;
            bool fontSizeChanged = this._lastFontSize != tempConfig.FontSize;
            bool spacingChanged = this._lastSpacing != tempConfig.Spacing;
            bool lineSpacingChanged = this._lastLineSpacing != tempConfig.LineSpacing;

            // 必要时重置字体文件路径。
            if (fontFilePathChanged || fontSizeChanged || spacingChanged || lineSpacingChanged)
                tempConfig.ExistingFontPath = null;

            var fontType = this.CurrentFontType;  // 这行是必要的，因为要确保On和Off的是同一个字体。
            this._okButton.GreyedOut = true;
            _states.On(fontType);
            bool success = await this._fontChanger.ReplaceOriginalOrRemainAsync(tempConfig);
            if (success)
            {
                Game1.addHUDMessage(new HUDMessage(I18n.HudMessage_SuccessSetFont(tempConfig.InGameType.LocalizedName()), null));
                Game1.playSound("money");
            }
            else
            {
                Game1.addHUDMessage(new HUDMessage(I18n.HudMessage_FailedSetFont(tempConfig.InGameType.LocalizedName()), HUDMessage.error_type));
                Game1.playSound("cancel");
            }
            _states.Off(fontType);
            /*this._okButton.GreyedOut = false;*/  // X
            Instance._okButton.GreyedOut = false;  // √（因为是异步设置，所以这行代码运行的时候既可能是同一个菜单实例，又可能是关闭，再打开后的另一个实例。如果是不同实例，this.xxx表示的就是上一个实例，因此想要设置当前实例，就得用静态单例Instance下的字段）

            // 如果成功，更新配置值。
            if (success)
            {
                tempConfig.CopyTo(config);
                this._saveConfig(this._config);
            }
        }

        private void OnFontTypeChanged(GameFontType fontType)
        {
            this._label_title.Text = fontType.LocalizedName();
            this._label_title.LocalPosition = new Vector2(this.width / 2 - this._label_title.Width / 2, 108);

            FontConfig fontConfig = this._config.Fonts.GetOrCreateFontConfig(LocalizedContentManager.CurrentLanguageCode,
                FontHelpers.GetCurrentLocale(), this.CurrentFontType);
            this._box_enabledFont.Element.IsChecked = fontConfig.Enabled;
            this._slider_fontSize.Element.Value = (int)fontConfig.FontSize;
            this._slider_spacing.Element.Value = (int)fontConfig.Spacing;
            this._slider_lineSpacing.Element.Value = fontConfig.LineSpacing;
            this._dropDown_font.SelectedItem = this.FindFont(this._allFonts, fontConfig.FontFilePath,
                fontConfig.FontIndex);
            this._okButton.GreyedOut = _states.IsOn(fontType);

            this.UpdateGameExample();
            this.UpdateCustomExample();
        }

        protected override void ResetComponents(RootElement root, IBindingContext context)
        {
            root.LocalPosition = new Vector2(this.xPositionOnScreen, this.yPositionOnScreen);
            root.SettableWidth = this.width;
            root.SettableHeight = this.height;

            root.Add(
                this._leftArrow,
                this._rightArrow,
                this._label_title,
                this._exampleBoard,
                this._box_merge,
                this._box_showBounds,
                this._box_showText,
                this._colorBlock_game,
                this._label_game,
                this._colorBlock_current,
                this._label_current,
                this._label_gameExample,
                this._label_currentExample,
                this._box_enabledFont,
                this._slider_fontSize,
                this._slider_spacing,
                this._slider_lineSpacing,
                this._dropDown_font,
                this._okButton);
        }

        private void UpdateGameExample()
        {
            if (this.CurrentFontType is GameFontType.SpriteText)
            {
                this._label_gameExample.Font = this._fontManager.GetBuiltInBmFont();
                this._label_gameExample.Text = this._config.ExampleText?.Replace('\n', '^');
            }
            else
            {
                this._label_gameExample.Font = new XNASpriteFont(this._fontManager.GetBuiltInSpriteFont(this.CurrentFontType));
                this._label_gameExample.Text = this._config.ExampleText;
            }

            this.UpdateExamplePositions();
        }

        private void UpdateCustomExample(bool reset = true)  // reset：是否重置当前的示例。
        {
            if (this.CurrentFontType is GameFontType.SpriteText)
                this._label_currentExample.Text = this._config.ExampleText?.Replace('\n', '^');
            else
                this._label_currentExample.Text = this._config.ExampleText;

            if (reset)
                this._label_currentExample.Font = this._exampleFonts.ResetThenGet(this.CurrentFontType,
                    this._box_enabledFont.Element.IsChecked,
                    this.GetFontFile(),
                    this.GetFontIndex(),
                    this._slider_fontSize.Element.Value,
                    this._slider_spacing.Element.Value,
                    this._slider_lineSpacing.Element.Value,
                    this._label_currentExample.Text
                );
            else
                this._label_currentExample.Font = this._exampleFonts.Get(this.CurrentFontType,
                    this._box_enabledFont.Element.IsChecked,
                    this.GetFontFile(),
                    this.GetFontIndex(),
                    this._slider_fontSize.Element.Value,
                    this._slider_spacing.Element.Value,
                    this._slider_lineSpacing.Element.Value,
                    this._label_currentExample.Text
                );

            this.UpdateExamplePositions();
        }

        private void UpdateExamplePositions()
        {
            int maxWidthInLeftThree = new[] { this._box_merge.Width, this._box_showBounds.Width, this._box_showText.Width }.Max();
            Rectangle exampleBounds = new Rectangle(
                (int)(this._box_merge.LocalPosition.X + maxWidthInLeftThree + borderWidth / 2),
                (int)(this._exampleBoard.LocalPosition.Y + borderWidth / 3),
                (int)(this._colorBlock_game.LocalPosition.X - borderWidth - this._box_merge.LocalPosition.X - maxWidthInLeftThree),
                this._exampleBoard.Height - borderWidth / 3 * 2);

            int maxWidth = Math.Max(this._label_gameExample.Width, this._label_currentExample.Width);
            int maxHeight = Math.Max(this._label_gameExample.Height, this._label_currentExample.Height);
            int exampleX = exampleBounds.Center.X - maxWidth / 2;
            int exampleY = exampleBounds.Center.Y - maxHeight / 2;
            if (this._box_merge.Element.IsChecked)
            {
                this._label_gameExample.LocalPosition = new Vector2(exampleX, exampleY);
                this._label_currentExample.LocalPosition = new Vector2(exampleX, exampleY);
            }
            else
            {
                this._label_gameExample.LocalPosition = new Vector2(exampleX, exampleBounds.Y);
                this._label_currentExample.LocalPosition = new Vector2(exampleX, exampleBounds.Center.Y /*exampleBounds.Bottom - this._label_currentExample.Height*/);
            }
        }

        private FontModel[] LoadAllFonts()
        {
            FontModel empty = new FontModel();
            FontModel[] fonts = InstalledFonts.GetAll().ToArray();
            return new FontModel[1] { empty }
                .Concat(fonts)
                .ToArray();
        }

        private FontModel FindFont(FontModel[] fonts, string fontFilePath, int fontIndex) // 这里path是简化后的
        {
            if (fontFilePath == null)
                return fonts[0];
            return fonts.Where(f => f.FullPath == InstalledFonts.GetFullPath(fontFilePath) && f.FontIndex == fontIndex)
                .FirstOrDefault();
        }

        private string GetFontFile()
        {
            FontModel selectedFont = this._dropDown_font.SelectedItem as FontModel;
            return InstalledFonts.SimplifyPath(selectedFont.FullPath);
        }

        private int GetFontIndex()
        {
            FontModel selectedFont = this._dropDown_font.SelectedItem as FontModel;
            return selectedFont.FontIndex;
        }

        private class StateManager
        {
            private readonly Dictionary<GameFontType, bool> _states = new();

            public StateManager()
            {
                foreach (GameFontType key in Enum.GetValues<GameFontType>())
                    this._states[key] = false;
            }

            public bool IsOn(GameFontType fontType)
            {
                return this._states[fontType];
            }

            public void On(GameFontType fontType)
            {
                if (!this._states[fontType])
                    this._states[fontType] = true;
            }

            public void Off(GameFontType fontType)
            {
                if (this._states[fontType])
                    this._states[fontType] = false;
            }
        }
    }
}