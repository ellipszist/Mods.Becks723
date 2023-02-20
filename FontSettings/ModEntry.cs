﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BmFont;
using FontSettings.Framework;
using FontSettings.Framework.DataAccess;
using FontSettings.Framework.DataAccess.Models;
using FontSettings.Framework.DataAccess.Parsing;
using FontSettings.Framework.FontGenerators;
using FontSettings.Framework.FontPatching;
using FontSettings.Framework.FontScanning;
using FontSettings.Framework.FontScanning.Scanners;
using FontSettings.Framework.Menus;
using FontSettings.Framework.Menus.Views;
using FontSettings.Framework.Migrations;
using FontSettings.Framework.Models;
using FontSettings.Framework.Patchers;
using FontSettings.Framework.Preset;
using HarmonyLib;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.GameData;
using StardewValley.Menus;

namespace FontSettings
{
    internal class ModEntry : Mod
    {
        private readonly string _globalFontDataKey = "font-data";
        private readonly string _const_fontPath_ja = "assets/fonts/sp-setofont/sp-setofont.ttf";
        private readonly string _const_fontPath_ko = "assets/fonts/SDMiSaeng/SDMiSaeng.ttf";
        private readonly string _const_fontPath_zh = "assets/fonts/NotoSansCJKsc-Bold/NotoSansCJKsc-Bold.otf";

        private MigrateTo_0_6_0 _0_6_0_Migration;

        private ModConfig _config;

        private FontConfigRepository _fontConfigRepository;
        private FontPresetRepository _fontPresetRepository;
        private VanillaFontDataRepository _vanillaFontDataRepository;
        private SampleDataRepository _sampleDataRepository;

        private FontConfigParser _vanillaFontConfigParser;
        private FontConfigParserForUser _userFontConfigParser;
        private FontPresetParser _fontPresetParser;

        private FontConfigManager _fontConfigManager;
        private VanillaFontConfigProvider _vanillaFontConfigProvider;
        private IFontFileProvider _fontFileProvider;
        private Framework.Preset.FontPresetManager _fontPresetManager;
        private IGameFontChangerFactory _fontChangerFactory;
        private FontPatchInvalidatorManager _invalidatorManager;

        private readonly FontSettingsMenuContextModel _menuContextModel = new();

        private MainFontPatcher _mainFontPatcher;

        private VanillaFontProvider _vanillaFontProvider;

        private TitleFontButton _titleFontButton;

        internal static IModHelper ModHelper { get; private set; }

        internal static Harmony Harmony { get; private set; }

        public override void Entry(IModHelper helper)
        {
            ModHelper = this.Helper;
            I18n.Init(helper.Translation);
            Log.Init(this.Monitor);
            Textures.Init(helper.ModContent);

            this._config = helper.ReadConfig<ModConfig>();
            this.CheckConfigValid(this._config);

            // init vanilla font provider.
            this._vanillaFontProvider = new VanillaFontProvider(helper, this.Monitor);
            this._vanillaFontProvider.RecordStarted += this.OnFontRecordStarted;
            this._vanillaFontProvider.RecordFinished += this.OnFontRecordFinished;

            // init migrations.
            this._0_6_0_Migration = new(helper, this.ModManifest);

            // init repositories.
            this._fontConfigRepository = new FontConfigRepository(helper);
            this._fontPresetRepository = new FontPresetRepository(Path.Combine(Constants.DataPath, ".smapi", "mod-data", this.ModManifest.UniqueID.ToLower(), "Presets"));
            this._vanillaFontDataRepository = new VanillaFontDataRepository(helper, this.Monitor);
            this._sampleDataRepository = new SampleDataRepository(helper, this.Monitor);

            // do changes to database.
            this._0_6_0_Migration.ApplyDatabaseChanges(
                fontConfigRepository: this._fontConfigRepository,
                fontPresetRepository: this._fontPresetRepository,
                modConfig: this._config,
                writeModConfig: this.SaveConfig);

            // init service objects.
            this._config.Sample = this._sampleDataRepository.ReadSampleData();
            this._vanillaFontConfigProvider = new VanillaFontConfigProvider(this._vanillaFontProvider);
            this._fontFileProvider = this.GetFontFileProvider();

            this._vanillaFontConfigParser = new FontConfigParser(this.GetModFontFileProvider(), this._vanillaFontProvider);
            this._userFontConfigParser = new FontConfigParserForUser(this._fontFileProvider, this._vanillaFontProvider, this._vanillaFontConfigProvider);
            this._fontPresetParser = new FontPresetParser(this._fontFileProvider, this._vanillaFontConfigProvider, this._vanillaFontProvider);

            this._fontConfigManager = new FontConfigManager();
            this._fontPresetManager = new Framework.Preset.FontPresetManager();
            this._invalidatorManager = new FontPatchInvalidatorManager(helper);
            this._fontChangerFactory = new FontPatchChangerFactory(new FontPatchResolverFactory(), this._invalidatorManager);

            // connect manager and repository.
            this._fontConfigManager.ConfigUpdated += this.OnFontConfigUpdated;
            this._fontPresetManager.PresetUpdated += this.OnFontPresetUpdated;

            // init font patching.
            this._mainFontPatcher = new MainFontPatcher(this._fontConfigManager, new FontPatchResolverFactory(), this._invalidatorManager);

            // init title font button.
            this._titleFontButton = new TitleFontButton(
                position: this.GetTitleFontButtonPosition(),
                onClicked: () => this.OpenFontSettingsMenu());

            this.AssertModFileExists(this._const_fontPath_ja, out _);
            this.AssertModFileExists(this._const_fontPath_ko, out _);
            this.AssertModFileExists(this._const_fontPath_zh, out _);

            Harmony = new Harmony(this.ModManifest.UniqueID);
            {
                new GameMenuPatcher()
                    .AddFontSettingsPage(helper, Harmony, this._config, this.SaveConfig);

                new FontShadowPatcher(this._config)
                    .Patch(Harmony, this.Monitor);

                var spriteTextPatcher = new SpriteTextPatcher();
                spriteTextPatcher.Patch(Harmony, this.Monitor);
                this._mainFontPatcher.FontPixelZoomOverride += (s, e) =>
                    spriteTextPatcher.SetOverridePixelZoom(e.PixelZoom);
            }

            helper.Events.Content.AssetRequested += this.OnAssetRequestedEarly;
            helper.Events.Content.AssetReady += this.OnAssetReadyEarly;
            helper.Events.GameLoop.UpdateTicking += this.OnUpdateTicking;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
            helper.Events.Content.AssetReady += this.OnAssetReady;
            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Input.ButtonsChanged += this.OnButtonsChanged;
            helper.Events.Display.WindowResized += this.OnWindowResized;
            helper.Events.Display.Rendered += this.OnRendered;
            helper.Events.GameLoop.UpdateTicked += this.OnUpdateTicked;
        }

        private void OnUpdateTicking(object sender, UpdateTickingEventArgs e)
        {
            this._vanillaFontProvider.OnUpdateTicking(e);
        }


        private void OnAssetRequestedEarly(object sender, AssetRequestedEventArgs e)
        {
            this._vanillaFontProvider.OnAssetRequested(e);
        }

        private void OnAssetReadyEarly(object sender, AssetReadyEventArgs e)
        {
            this._vanillaFontProvider.OnAssetReady(e);
        }

        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            new GMCMIntegration(
                config: this._config,
                reset: this.ResetConfig,
                save: () => this.SaveConfig(this._config),
                modRegistry: this.Helper.ModRegistry,
                monitor: this.Monitor,
                manifest: this.ModManifest
            ).Integrate();
        }

        private void OnButtonsChanged(object sender, ButtonsChangedEventArgs e)
        {
            if (this._config.OpenFontSettingsMenu.JustPressed())
            {
                this.OpenFontSettingsMenu();
            }
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            this._mainFontPatcher.OnAssetRequested(e);
        }

        private void OnAssetReady(object sender, AssetReadyEventArgs e)
        {
            this._mainFontPatcher.OnAssetReady(e);
        }

        private void OnWindowResized(object sender, WindowResizedEventArgs e)
        {
            this._titleFontButton.Position = this.GetTitleFontButtonPosition();
        }

        private void OnRendered(object sender, RenderedEventArgs e)
        {
            if (this.IsTitleMenuInteractable())
                this._titleFontButton.Draw(e.SpriteBatch);
        }

        private void OnUpdateTicked(object sender, UpdateTickedEventArgs e)
        {
            if (this.IsTitleMenuInteractable())
                this._titleFontButton.Update();
        }

        private FontConfigs ReadFontSettings()
        {
            FontConfigs fonts = this.Helper.Data.ReadGlobalData<FontConfigs>(this._globalFontDataKey);
            if (fonts == null)
            {
                fonts = new FontConfigs();
                this.Helper.Data.WriteGlobalData(this._globalFontDataKey, fonts);
            }

            return fonts;
        }

        private void SaveConfig(ModConfig config)
        {
            this.Helper.WriteConfig(config);
        }

        private void SaveFontSettings(FontConfigs fonts)
        {
            this.Helper.Data.WriteGlobalData(this._globalFontDataKey, fonts);
        }

        private void ResetConfig()
        {
            // 重置
            this._config.ResetToDefault();

            // 保存
            this.Helper.WriteConfig(this._config);
        }

        private void CheckConfigValid(ModConfig config)
        {
            string WarnMessage<T>(string name, T max, T min) => $"{name}：最大值（{max}）小于最小值（{min}）。已重置。";

            // x offset
            if (config.MaxCharOffsetX < config.MinCharOffsetX)
            {
                ILog.Warn(WarnMessage("横轴偏移量", config.MaxCharOffsetX, config.MinCharOffsetX));
                config.MaxCharOffsetX = 10;
                config.MinCharOffsetX = -10;
            }

            // y offset
            if (config.MaxCharOffsetY < config.MinCharOffsetY)
            {
                ILog.Warn(WarnMessage("纵轴偏移量", config.MaxCharOffsetY, config.MinCharOffsetY));
                config.MaxCharOffsetY = 10;
                config.MinCharOffsetY = -10;
            }

            // font size
            if (config.MaxFontSize < config.MinFontSize)
            {
                ILog.Warn(WarnMessage("字体大小", config.MaxFontSize, config.MinFontSize));
                config.MaxFontSize = 100;
            }

            // spacing
            if (config.MaxSpacing < config.MinSpacing)
            {
                ILog.Warn(WarnMessage("字间距", config.MaxSpacing, config.MinSpacing));
                config.MaxSpacing = 10;
                config.MinSpacing = -10;
            }

            // line spacing
            if (config.MaxLineSpacing < config.MinLineSpacing)
            {
                ILog.Warn(WarnMessage("行间距", config.MaxLineSpacing, config.MinLineSpacing));
                config.MaxLineSpacing = 100;
            }

            // pixel zoom
            if (config.MaxPixelZoom < config.MinPixelZoom)
            {
                ILog.Warn(WarnMessage("缩放比例", config.MaxPixelZoom, config.MinPixelZoom));
                config.MaxPixelZoom = 5f;
                config.MinPixelZoom = 0.1f;
            }
        }

        IFontFileProvider GetFontFileProvider()
        {
            var fontFileProvider = new FontFileProvider();
            {
                var scanSettings = new ScanSettings();
                fontFileProvider.Scanners.Add(new InstalledFontScannerForWindows(scanSettings));
                fontFileProvider.Scanners.Add(new InstalledFontScannerForMacOS(scanSettings));
                fontFileProvider.Scanners.Add(new InstalledFontScannerForLinux(scanSettings));
            }
            return fontFileProvider;
        }

        IFontFileProvider GetModFontFileProvider()
        {
            var fontFileProvider = new FontFileProvider();
            {
                var scanSettings = new ScanSettings();
                fontFileProvider.Scanners.Add(new BasicFontFileScanner(this.Helper.DirectoryPath, scanSettings));
            }
            return fontFileProvider;
        }

        private void OnFontRecordStarted(object sender, RecordEventArgs e)
        {
            this.Monitor.Log($"记录{e.Language}的{e.FontType}，中断font patch。");
            this._mainFontPatcher.PauseFontPatch();
        }

        private void OnFontRecordFinished(object sender, RecordEventArgs e)
        {
            this.Monitor.Log($"完成记录{e.Language}的{e.FontType}。");

            // parse vanilla configs in context.
            if (this._vanillaFontDataRepository != null
                && this._vanillaFontConfigParser != null
                && this._vanillaFontConfigProvider != null)
            {
                var vanillaConfigs = this._vanillaFontDataRepository.ReadVanillaFontData().Fonts;
                var parsedConfigs = this._vanillaFontConfigParser.ParseCollection(vanillaConfigs, e.Language, e.FontType);
                this._vanillaFontConfigProvider.AddVanillaFontConfigs(parsedConfigs);
            }

            // parse configs in context.
            if (this._fontConfigRepository != null
                && this._userFontConfigParser != null
                && this._fontConfigManager != null)
            {
                var fontConfigs = this._fontConfigRepository.ReadAllConfigs();
                var parsedConfigs = this._userFontConfigParser.ParseCollection(fontConfigs, e.Language, e.FontType);
                foreach (var pair in parsedConfigs)
                    this._fontConfigManager.AddFontConfig(pair);
            }

            // parse presets in context.
            if (this._fontPresetRepository != null
                && this._fontPresetParser != null
                && this._fontPresetManager != null)
            {
                var presets = this._fontPresetRepository.ReadAllPresets();
                var parsedPresets = this._fontPresetParser.ParseCollection(presets.Values, e.Language, e.FontType);
                this._fontPresetManager.AddPresets(parsedPresets);
            }

            this.Monitor.Log($"恢复font patch。");

            this._mainFontPatcher.ResumeFontPatch();
        }

        private void OnFontConfigUpdated(object sender, EventArgs e)
        {
            var configs = this._fontConfigManager.GetAllFontConfigs()
                .Select(pair => this._userFontConfigParser.ParseBack(pair));

            var configObject = new FontConfigs();
            foreach (var config in configs)
                configObject.Add(config);

            this._fontConfigRepository.WriteAllConfigs(configObject);
        }

        private void OnFontPresetUpdated(object sender, PresetUpdatedEventArgs e)
        {
            string name = e.Name;
            var preset = e.Preset;

            var presetObject = preset == null
                ? null
                : this._fontPresetParser.ParseBack(preset);

            this._fontPresetRepository.WritePreset(name, presetObject);
        }

        private void OpenFontSettingsMenu()
        {
            var menu = this.CreateFontSettingsMenu();

            if (Game1.activeClickableMenu is TitleMenu)
                TitleMenu.subMenu = menu;
            else
                Game1.activeClickableMenu = menu;
        }

        private FontSettingsMenu CreateFontSettingsMenu()
        {
            var gen = new SampleFontGenerator(this._vanillaFontProvider);
            IFontGenerator sampleFontGenerator = gen;
            IAsyncFontGenerator sampleAsyncFontGenerator = gen;

            return new FontSettingsMenu(
                config: this._config,
                vanillaFontProvider: this._vanillaFontProvider,
                sampleFontGenerator: sampleFontGenerator,
                sampleAsyncFontGenerator: sampleAsyncFontGenerator,
                presetManager: this._fontPresetManager,
                registry: this.Helper.ModRegistry,
                fontConfigManager: this._fontConfigManager,
                vanillaFontConfigProvider: this._vanillaFontConfigProvider,
                fontChangerFactory: this._fontChangerFactory,
                fontFileProvider: this._fontFileProvider,
                stagedValues: this._menuContextModel);
        }

        private bool AssertModFileExists(string relativePath, out string? fullPath) // fullPath = null when returns false
        {
            fullPath = Path.Combine(this.Helper.DirectoryPath, relativePath);
            fullPath = PathUtilities.NormalizePath(fullPath);

            if (this.AssertModFileExists(fullPath))
                return true;
            else
            {
                fullPath = null;
                return false;
            }
        }

        private bool AssertModFileExists(string relativePath)
        {
            string fullPath = Path.Combine(this.Helper.DirectoryPath, relativePath);
            fullPath = PathUtilities.NormalizePath(fullPath);

            return this.AssertFileExists(fullPath, I18n.Misc_ModFileNotFound(relativePath));
        }

        private bool AssertFileExists(string filePath, string message)
        {
            if (!File.Exists(filePath))
            {
                this.Monitor.Log(message, LogLevel.Error);
                return false;
            }
            return true;
        }

        private Microsoft.Xna.Framework.Point GetTitleFontButtonPosition()
        {
            var registry = this.Helper.ModRegistry;
            bool gmcm = registry.IsLoaded("spacechase0.GenericModConfigMenu");  // (36, Game1.viewport.Height - 100)
            bool mum = registry.IsLoaded("cat.modupdatemenu");                  // (36, Game1.viewport.Height - 150 - 48)
                                                                                // ()
            int interval = 75 + 24;

            switch (0)
            {
                case { } when !gmcm:
                    return new(36, Game1.viewport.Height - interval);

                case { } when gmcm:
                    return mum
                        ? new(36, Game1.viewport.Height - interval * 3)
                        : new(36, Game1.viewport.Height - interval * 2);

                default:
                    return new(36, Game1.viewport.Height - interval);
            }
        }

        /// <summary>Copied from GMCM source code :D</summary>
        private bool IsTitleMenuInteractable()
        {
            if (Game1.activeClickableMenu is not TitleMenu titleMenu || TitleMenu.subMenu != null)
                return false;

            var method = this.Helper.Reflection.GetMethod(titleMenu, "ShouldAllowInteraction", false);
            if (method != null)
                return method.Invoke<bool>();
            else // method isn't available on Android
                return this.Helper.Reflection.GetField<bool>(titleMenu, "titleInPosition").GetValue();
        }
    }
}
