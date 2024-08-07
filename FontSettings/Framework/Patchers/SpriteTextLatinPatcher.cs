﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using BmFont;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.BellsAndWhistles;

namespace FontSettings.Framework.Patchers
{
    internal class SpriteTextLatinPatcher
    {
        private static ModConfig _config;
        private static IManifest _manifest;
        private readonly IModHelper _helper;

        private string? _currentFontFile;

        public SpriteTextLatinPatcher(ModConfig config, IManifest manifest, IModHelper helper)
        {
            _config = config;
            _manifest = manifest;
            this._helper = helper;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            string[] latinFontFileNames = FontHelpers.GetAllAvailableLanguages()
                .Where(lang => lang.IsLatinLanguage())
                .Select(lang => FontHelpers.GetFontFileAssetName(lang))
                .ToArray();

            var latinFontFile = latinFontFileNames
                .Where(fontFile => e.NameWithoutLocale.IsEquivalentTo(fontFile))
                .FirstOrDefault();
            if (latinFontFile != null)
            {
                this._currentFontFile = latinFontFile;

                XmlSource latinXml = this._helper.ModContent.Load<XmlSource>("assets/fonts/Latin.fnt");
                latinXml = this.ValidateLatinFontPageFile(latinXml, latinFontFile.Split('/').Last());
                e.LoadFrom(() => latinXml, AssetLoadPriority.High);
            }

            if (this.IsLatinFontPage(e, this._currentFontFile, out _))
            {
                e.LoadFromModFile<Texture2D>($"assets/fonts/Latin_0.png", AssetLoadPriority.High);
            }
        }

        public void Patch(Harmony harmony, IMonitor monitor)
        {
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteText), "setUpCharacterMap"),
                postfix: this.HarmonyMethod(nameof(SpriteText_setUpCharacterMap_Postfix)));
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteText), nameof(SpriteText.drawString)),
                transpiler: this.HarmonyMethod(nameof(SpriteText_drawString_Transpiler)),
                finalizer: this.HarmonyMethod(nameof(SpriteText_drawString_Finalizer)));
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteText), $"get_{nameof(SpriteText.color_Default)}"),
                transpiler: this.HarmonyMethod(nameof(SpriteText_color_Default_Transpiler)));
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteText), nameof(SpriteText.IsMissingCharacters)),
                transpiler: this.HarmonyMethod(nameof(SpriteText_IsMissingCharacters_Transpiler)));
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteText), nameof(SpriteText.getIndexOfSubstringBeyondHeight)),
                transpiler: this.HarmonyMethod(nameof(SpriteText_getIndexOfSubstringBeyondHeight_Transpiler)));
            harmony.Patch(
                original: AccessTools.Method(typeof(SpriteText), "isUsingNonSpriteSheetFont"),
                postfix: this.HarmonyMethod(nameof(SpriteText_isUsingNonSpriteSheetFont_Postfix)));
        }

        private static void SpriteText_drawString_Finalizer(Exception __exception)
        {
            //throw __exception;
        }

        private static void SpriteText_setUpCharacterMap_Postfix()
        {
            EnsureSubscribedOnLanguageChange();

            if (!CustomSpriteTextInLatinLanguages())
                return;

            if (LocalizedContentManager.CurrentLanguageLatin && !_hasSubscribedOnLanguageChangeLatin)
            {
                SpriteText.characterMap = new Dictionary<char, FontChar>();
                SpriteText.fontPages = new List<Texture2D>();

                SpriteText.FontFile = LoadFontFile(LatinFontFileAssetName());
                SpriteText.fontPixelZoom = 3f;

                foreach (FontChar fontChar in SpriteText.FontFile.Chars)
                {
                    char key = (char)fontChar.ID;
                    SpriteText.characterMap.Add(key, fontChar);
                }
                foreach (FontPage fontPage in SpriteText.FontFile.Pages)
                {
                    SpriteText.fontPages.Add(
                        Game1.content.Load<Texture2D>($"Fonts/{fontPage.File}"));
                }

                LocalizedContentManager.OnLanguageChange += OnLanguageChange_Latin;
                _hasSubscribedOnLanguageChangeLatin = true;
            }
        }

        private static void OnLanguageChange_Latin(LocalizedContentManager.LanguageCode code)
        {
            if (!CustomSpriteTextInLatinLanguages())
                return;

            if (LocalizedContentManager.CurrentLanguageLatin)
            {
                if (SpriteText.characterMap != null)
                    SpriteText.characterMap.Clear();
                else
                    SpriteText.characterMap = new Dictionary<char, FontChar>();
                if (SpriteText.fontPages != null)
                    SpriteText.fontPages.Clear();
                else
                    SpriteText.fontPages = new List<Texture2D>();

                SpriteText.FontFile = LoadFontFile(LatinFontFileAssetName());
                SpriteText.fontPixelZoom = 3f;

                foreach (FontChar fontChar in SpriteText.FontFile.Chars)
                {
                    char key = (char)fontChar.ID;
                    SpriteText.characterMap.Add(key, fontChar);
                }
                foreach (FontPage fontPage in SpriteText.FontFile.Pages)
                {
                    SpriteText.fontPages.Add(
                        Game1.content.Load<Texture2D>($"Fonts/{fontPage.File}"));
                }
            }
        }

        /// <summary>
        /// Turn
        /// ```
        /// if (((LocalizedContentManager.CurrentLanguageLatin 
        ///     || (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ru && !Game1.options.useAlternateFont) 
        ///     || SpriteText.IsSpecialCharacter(s[i])) | junimoText) 
        /// || SpriteText.forceEnglishFont)
        /// ```
        /// 
        /// into
        /// ```
        /// if (((**CurrentLanguageLatinPatched()**
        ///     || SpriteText.IsSpecialCharacter(s[i])) | junimoText) 
        /// || SpriteText.forceEnglishFont)
        /// ```
        /// </summary>
        private static IEnumerable<CodeInstruction> SpriteText_drawString_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {//809
            var oldInstructions = codeInstructions.ToArray();
            int index = 0;
            bool ispatching = false;
            for (int i = 0; i < oldInstructions.Length; i++)
            {
                var instruction = oldInstructions[i];

                if (instruction.opcode == OpCodes.Call
                    && instruction.operand is MethodInfo { Name: "get_CurrentLanguageLatin" }
                    && oldInstructions[i + 10].opcode == OpCodes.Callvirt
                    && oldInstructions[i + 10].operand is MethodInfo { Name: "get_Chars" }
                    && oldInstructions[i + 11].opcode == OpCodes.Call
                    && oldInstructions[i + 11].operand is MethodInfo { Name: "IsSpecialCharacter" })
                {
                    ispatching = true;
                    index = i;
                    yield return new CodeInstruction(instruction)
                    {
                        opcode = OpCodes.Call,
                        operand = CurrentLanguageLatinPatched_Method.Value
                    };
                }

                // skip russian check (total 6 ilcodes)
                else if (ispatching && (i == index + 2 ||
                                        i == index + 3 ||
                                        i == index + 4 ||
                                        i == index + 5 ||
                                        i == index + 6 ||
                                        i == index + 7))
                { }

                else
                    yield return instruction;
            }
        }

        /// <summary>
        /// Turn
        /// ```
        /// if (!LocalizedContentManager.CurrentLanguageLatin 
        ///     && (LocalizedContentManager.CurrentLanguageCode != LocalizedContentManager.LanguageCode.ru || Game1.options.useAlternateFont))
        /// ```
        /// 
        /// to
        /// ```
        /// if (!CurrentLanguageLatinPatched())
        /// ```
        /// </summary>
        private static IEnumerable<CodeInstruction> SpriteText_color_Default_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var oldInstructions = codeInstructions.ToArray();
            int index = 0;
            bool ispatching = false;
            for (int i = 0; i < oldInstructions.Length; i++)
            {
                var instruction = oldInstructions[i];

                if (instruction.opcode == OpCodes.Call
                    && instruction.operand is MethodInfo { Name: "get_CurrentLanguageLatin" }
                    && oldInstructions[i + 1].opcode == OpCodes.Brtrue_S
                    && oldInstructions[i + 2].opcode == OpCodes.Call
                    && oldInstructions[i + 2].operand is MethodInfo { Name: "get_CurrentLanguageCode" }
                    && oldInstructions[i + 3].opcode == OpCodes.Ldc_I4_2)
                {
                    ispatching = true;
                    index = i;
                    yield return new CodeInstruction(instruction)
                    {
                        opcode = OpCodes.Call,
                        operand = CurrentLanguageLatinPatched_Method.Value
                    };
                }

                // skip russian check (total 6 ilcodes)
                else if (ispatching && (i == index + 2 ||
                                        i == index + 3 ||
                                        i == index + 4 ||
                                        i == index + 5 ||
                                        i == index + 6 ||
                                        i == index + 7))
                { }

                else
                    yield return instruction;
            }
        }

        private static IEnumerable<CodeInstruction> SpriteText_IsMissingCharacters_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var oldInstructions = codeInstructions.ToArray();
            for (int i = 0; i < oldInstructions.Length; i++)
            {
                var instruction = oldInstructions[i];

                if (instruction.opcode == OpCodes.Call
                    && instruction.operand is MethodInfo { Name: "get_CurrentLanguageLatin" }
                    && oldInstructions[i + 1].opcode == OpCodes.Brtrue_S
                    && oldInstructions[i + 2].opcode == OpCodes.Ldsfld
                    && oldInstructions[i + 2].operand is FieldInfo { Name: nameof(SpriteText.forceEnglishFont) })
                    yield return new CodeInstruction(instruction)
                    {
                        opcode = OpCodes.Call,
                        operand = CurrentLanguageLatinPatched_Method.Value
                    };

                else
                    yield return instruction;
            }
        }

        private static IEnumerable<CodeInstruction> SpriteText_getIndexOfSubstringBeyondHeight_Transpiler(IEnumerable<CodeInstruction> codeInstructions)
        {
            var oldInstructions = codeInstructions.ToArray();
            for (int i = 0; i < oldInstructions.Length; i++)
            {
                var instruction = oldInstructions[i];

                if (instruction.opcode == OpCodes.Call
                    && instruction.operand is MethodInfo { Name: "get_CurrentLanguageLatin" }
                    && oldInstructions[i + 1].opcode == OpCodes.Brtrue)
                    yield return new CodeInstruction(instruction)
                    {
                        opcode = OpCodes.Call,
                        operand = CurrentLanguageLatinPatched_Method.Value
                    };

                else
                    yield return instruction;
            }
        }

        private static void SpriteText_isUsingNonSpriteSheetFont_Postfix(ref bool __result)
        {
            __result = !CurrentLanguageLatinPatched();
        }

        private static bool _hasSubscribedOnLanguageChangeLatin;

        private static readonly Lazy<FieldInfo> LocalizedContentManager_OnLanguageChange_Field = new(
            () => typeof(LocalizedContentManager)
                .GetField(nameof(LocalizedContentManager.OnLanguageChange), BindingFlags.Static | BindingFlags.NonPublic));
        private static readonly Lazy<EventInfo> LocalizedContentManager_OnLanguageChange_Event = new(
            () => typeof(LocalizedContentManager).GetEvent(nameof(LocalizedContentManager.OnLanguageChange)));
        private static readonly Lazy<MethodInfo> SpriteText_OnLanguageChange_Method = new(
            () => typeof(SpriteText)
                .GetMethod("OnLanguageChange", BindingFlags.Static | BindingFlags.NonPublic));
        private static void EnsureSubscribedOnLanguageChange()
        {
            var languageEventHandler = (LocalizedContentManager.LanguageChangedHandler)
                LocalizedContentManager_OnLanguageChange_Field.Value
                .GetValue(null);
            var languageEventInfo = LocalizedContentManager_OnLanguageChange_Event.Value;

            bool ok = false;
            foreach (Delegate d in languageEventHandler.GetInvocationList().ToArray())
            {
                if (d.Method is
                    {
                        DeclaringType: { Name: nameof(SpriteText) },
                        Name: "OnLanguageChange"
                    })
                {
                    if (ok)
                        languageEventInfo.RemoveEventHandler(null, d);
                    else
                        ok = true;
                }
            }

            if (!ok)
            {
                var handler = Delegate.CreateDelegate(languageEventInfo.EventHandlerType, SpriteText_OnLanguageChange_Method.Value);
                languageEventInfo.AddEventHandler(null, handler);
            }
        }

        private static readonly Lazy<MethodInfo> CurrentLanguageLatinPatched_Method = new(
            () => AccessTools.Method(typeof(SpriteTextLatinPatcher), nameof(CurrentLanguageLatinPatched)));
        private static bool CurrentLanguageLatinPatched()
        {
            if (CustomSpriteTextInLatinLanguages())
                return false;
            else
                return LocalizedContentManager.CurrentLanguageLatin || LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ru;
        }

        private static string LatinFontFileAssetName()
        {
            return FontHelpers.GetFontFileAssetName();
        }

        private bool IsLatinFontPage(AssetRequestedEventArgs e, string? fontFile, out string pageFile)  // pageFile: without 'Fonts/' prefix
        {
            if (fontFile != null
                && e.NameWithoutLocale.StartsWith(fontFile + '_')
                && e.DataType == typeof(Texture2D))
            {
                pageFile = e.NameWithoutLocale.BaseName.Split('/').Last();
                return true;
            }
            else
            {
                pageFile = null;
                return false;
            }
        }

        private XmlSource ValidateLatinFontPageFile(XmlSource latinXml, string fontFile)
        {
            FontFile latinFont = FontLoader.Parse(latinXml.Source);

            foreach (FontPage fontPage in latinFont.Pages)
                fontPage.File = $"{fontFile}_{fontPage.ID}";

            return FontHelpers.ParseFontFile(latinFont);
        }

        private static FontFile LoadFontFile(string assetName)
        {
            return FontLoader.Parse(
                Game1.content.Load<XmlSource>(assetName).Source);
        }

        private static bool CustomSpriteTextInLatinLanguages()
            => _config.EnableLatinDialogueFont;

        private HarmonyMethod HarmonyMethod(string methodName)
            => new HarmonyMethod(this.GetType(), methodName);
    }
}
