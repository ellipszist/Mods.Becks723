﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BmFont;
using FontSettings.Framework.FontPatching.Editors;
using FontSettings.Framework.FontPatching.Loaders;
using FontSettings.Framework.FontPatching.Replacers;
using StardewModdingAPI.Events;
using StardewValley;

namespace FontSettings.Framework.FontPatching
{
    internal class MainFontPatcher
    {
        private readonly FontConfigManager _fontConfigManager;
        private readonly FontPatchResolverFactory _resolverFactory;
        private readonly FontPatchInvalidatorManager _invalidatorManager;

        private bool _bypassFontPatch;

        public event EventHandler<FontPixelZoomOverrideEventArgs> FontPixelZoomOverride;

        public MainFontPatcher(FontConfigManager fontConfigManager, FontPatchResolverFactory resolverFactory,
            FontPatchInvalidatorManager invalidatorManager)
        {
            this._fontConfigManager = fontConfigManager;
            this._resolverFactory = resolverFactory;
            this._invalidatorManager = invalidatorManager;
        }

        public void OnAssetRequested(AssetRequestedEventArgs e)
        {
            if (this._bypassFontPatch)
                return;

            if (e.NameWithoutLocale.IsEquivalentTo("Fonts/SmallFont"))
            {
                this.PatchCommonFont(e, GameFontType.SmallFont);
            }

            else if (e.NameWithoutLocale.IsEquivalentTo("Fonts/SpriteFont1"))
            {
                this.PatchCommonFont(e, GameFontType.DialogueFont);
            }

            else
            {
                this.PatchBmFont(e);
            }
        }

        public void OnAssetReady(AssetReadyEventArgs e)
        {
            this.UpdateFontFile(e);
        }

        public void PauseFontPatch()
        {
            this._bypassFontPatch = true;
        }

        public void ResumeFontPatch()
        {
            this._bypassFontPatch = false;
        }

        private void PatchCommonFont(AssetRequestedEventArgs e, GameFontType fontType)
        {
            var patch = this.ResolvePatch(fontType);
            if (patch != null)
            {
                this.PatchCommonFontCore(e, patch);
            }
        }

        private IFontPatch? ResolvePatch(GameFontType fontType)
        {
            var invalidator = this._invalidatorManager.GetInvalidator(fontType);

            // patch data is prepared in advance.
            if (invalidator.IsInProgress && invalidator.Patch != null)
            {
                return invalidator.Patch;
            }

            // we need resolve manually.
            if (this._fontConfigManager.TryGetFontConfig(FontHelpers.GetCurrentLanguage(), fontType, out var config))
            {
                var resolver = this.GetResolver(fontType);
                var result = resolver.Resolve(config,
                    new FontPatchContext(FontHelpers.GetCurrentLanguage(), fontType));
                if (result.IsSuccess)
                {
                    return result.GetData();
                }
                else
                {
                    Exception exception = result.GetError();
                    throw exception;  // TODO
                }
            }

            return null;
        }

        private void PatchCommonFontCore(AssetRequestedEventArgs e, IFontPatch patch)
        {
            if (patch.Loader != null)
            {
                this.LoadAsset(e, patch.Loader);
            }

            if (patch.Editor != null)
            {
                this.EditAsset(e, patch.Editor);
            }
        }

        private IBmFontPatch _bmFontPatch;
        private void PatchBmFont(AssetRequestedEventArgs e)
        {
            string fontFileName = FontHelpers.GetFontFileAssetName();
            if (e.NameWithoutLocale.IsEquivalentTo(fontFileName))
            {
                this.PatchFontFile(e);
            }

            else if (this._bmFontPatch != null)
            {
                this.PatchFontPages(e);
            }
        }

        private void PatchFontFile(AssetRequestedEventArgs e)
        {
            var bmFontPatch = this.ResolvePatch(GameFontType.SpriteText) as IBmFontPatch;
            if (bmFontPatch != null)
            {
                this.PatchCommonFontCore(e, bmFontPatch);

                if (bmFontPatch.PageLoaders == null)
                    this.RaiseFontPixelZoomOverride(bmFontPatch.FontPixelZoom);
            }

            this._bmFontPatch = bmFontPatch;
        }

        private void PatchFontPages(AssetRequestedEventArgs e)
        {
            var bmFontPatch = this._bmFontPatch;

            if (bmFontPatch.PageLoaders != null)
            {
                var pairs = bmFontPatch.PageLoaders
                    .Where(pair => e.NameWithoutLocale.IsEquivalentTo(pair.Key));
                if (pairs.Any())
                {
                    var pair = pairs.First();

                    string pageKey = pair.Key;
                    var loader = pair.Value;
                    if (loader != null)
                        this.LoadAsset(e, loader);

                    bmFontPatch.PageLoaders.Remove(pageKey);
                    if (bmFontPatch.PageLoaders.Count == 0)
                    {
                        this._bmFontPatch = null;

                        // 设置缩放，放在最后。
                        this.RaiseFontPixelZoomOverride(bmFontPatch.FontPixelZoom);
                    }
                }
            }
        }

        private void LoadAsset(AssetRequestedEventArgs e, IFontLoader loader, AssetLoadPriority priority = AssetLoadPriority.Exclusive)
        {
            e.LoadFrom(() => loader.Load(), priority);
        }

        private void EditAsset(AssetRequestedEventArgs e, IFontEditor editor, AssetEditPriority priority = AssetEditPriority.Default)
        {
            if (editor is IFontReplacer replacer)
                e.Edit(asset => asset.ReplaceWith(replacer.Replacement), priority);
            else
                e.Edit(asset => editor.Edit(asset.Data), priority);
        }

        private void UpdateFontFile(AssetReadyEventArgs e)
        {
            string fontFileName = FontHelpers.GetFontFileAssetName();
            if (e.NameWithoutLocale.IsEquivalentTo(fontFileName))
            {
                XmlSource xml = Game1.content.Load<XmlSource>(fontFileName);
                var newFontFile = FontLoader.Parse(xml.Source);

                // update for invalidator.
                var spriteTextInvalidator = this._invalidatorManager.GetInvalidator(GameFontType.SpriteText) as ISpriteTextPatchInvalidator;
                if (spriteTextInvalidator != null)
                    spriteTextInvalidator.UpdateFontFile(newFontFile);
            }
        }

        private IFontPatchResolver GetResolver(GameFontType fontType)
            => this._resolverFactory.CreateResolver(fontType);

        private void RaiseFontPixelZoomOverride(float pixelZoom)
        {
            this.RaiseFontPixelZoomOverride(
                new FontPixelZoomOverrideEventArgs(true, pixelZoom));
        }

        protected virtual void RaiseFontPixelZoomOverride(FontPixelZoomOverrideEventArgs e)
        {
            FontPixelZoomOverride?.Invoke(this, e);
        }
    }

    internal class FontPixelZoomOverrideEventArgs : EventArgs
    {
        public bool NeedsOverride { get; }

        public float PixelZoom { get; }

        public FontPixelZoomOverrideEventArgs(bool needsOverride, float pixelZoom)
        {
            this.NeedsOverride = needsOverride;
            this.PixelZoom = pixelZoom;
        }
    }
}
