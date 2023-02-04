﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FontSettings.Framework.FontPatching.Editors;
using FontSettings.Framework.FontPatching.Loaders;
using FontSettings.Framework.Models;
using Microsoft.Xna.Framework.Graphics;

namespace FontSettings.Framework.FontPatching.Resolving
{
    internal class FontPatchFactory
    {
        private readonly BmFontLoadHelper _bmFontLoadHelper = new();

        public IFontPatch ForBypassSpriteFont()
            => this.CreatePatch();

        public IFontPatch ForLoadSpriteFont(FontConfig_ config)
            => this.CreatePatch(new SpriteFontLoader(config));

        public IFontPatch ForLoadSpriteFont(SpriteFont spriteFont)
            => this.CreatePatch(new SimpleFontLoader(spriteFont));

        public IFontPatch ForEditSpriteFont(FontConfig_ config)
            => this.CreatePatch(new SpriteFontEditor(config));

        public IBmFontPatch ForBypassBmFont()
            => this.CreateBmPatch();

        public IBmFontPatch ForLoadBmFont(BmFontData bmFont, float fontPixelZoom)
        {
            this._bmFontLoadHelper.GetLoaders(bmFont,
                out IFontLoader fontFileLoader,
                out IDictionary<string, IFontLoader> pageLoaders);
            return this.CreateBmPatch(fontFileLoader, pageLoaders, fontPixelZoom);
        }

        public IBmFontPatch ForEditBmFont(FontConfig_ config)
            => this.CreateBmPatch(new BmFontFileEditor(config));

        private IFontPatch CreatePatch() => new FontPatch(null, null);
        private IFontPatch CreatePatch(IFontLoader loader) => new FontPatch(loader, null);
        private IFontPatch CreatePatch(IFontEditor editor) => new FontPatch(null, editor);
        private IFontPatch CreatePatch(IFontLoader loader, IFontEditor editor) => new FontPatch(loader, editor);

        private IBmFontPatch CreateBmPatch() => new BmFontPatch(null, null, null);
        private IBmFontPatch CreateBmPatch(IFontLoader loader, IDictionary<string, IFontLoader> pageLoaders, float fontPixelZoom) => new BmFontPatch(loader, null, pageLoaders, fontPixelZoom);
        private IBmFontPatch CreateBmPatch(IFontEditor editor) => new BmFontPatch(null, editor, null);
        private IBmFontPatch CreateBmPatch(IFontLoader loader, IFontEditor editor, IDictionary<string, IFontLoader> pageLoaders) => new BmFontPatch(loader, editor, pageLoaders);
    }
}