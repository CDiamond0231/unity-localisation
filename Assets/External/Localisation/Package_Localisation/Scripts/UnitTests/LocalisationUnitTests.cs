//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Localisation Unit Tests
//             Author: Christopher Allport
//             Date Created: 17th January, 2025
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Unit Tests for Localisation
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Localisation;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Localisation.Localisation.UnitTests
{
    public class LocalisationUnitTests
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private static readonly string[] FontsToTest = new Func<string[]>(() => 
        {
            List<string> fontsToTest = new List<string>();
            LocManager.FontUnitTestInfo[] fontsForUnitTests = LocManager.EditorGetFontsToApplyUnitTests();
            for (int unitTestIndex = 0; unitTestIndex < fontsForUnitTests.Length; ++unitTestIndex)
            {
                LocManager.FontUnitTestInfo fontUnitTest = fontsForUnitTests[unitTestIndex];
                if (fontUnitTest.FontAsset != null && fontUnitTest.LanguagesToCheckAgainst != 0)
                {
                    LocLanguages[] languagesToCheck = LocLanguagesInfo.LocLanguageFlagsToLocLanguages(fontUnitTest.LanguagesToCheckAgainst);
                    foreach (LocLanguages language in languagesToCheck)
                        fontsToTest.Add($"{fontUnitTest.FontAsset.name} -> {language}");
                }
            }

            return fontsToTest.ToArray();
        })();

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [Test]
        [Category("Localisation")]
        public void AllRequiredCharactersAssignedOnFontAtlas([ValueSource(nameof(FontsToTest))] string fontTest)
        {
            LocManager.FontUnitTestInfo[] fontsForUnitTests = LocManager.EditorGetFontsToApplyUnitTests();
            string[] testNameSplit = fontTest.Split(" -> ", StringSplitOptions.RemoveEmptyEntries);
            if (testNameSplit.Length < 2)
                return;

            string fontNameToTest = testNameSplit[0];
            LocManager.FontUnitTestInfo? fontUnitTest = fontsForUnitTests.FirstOrDefault(x => x.FontAsset != null && string.Equals(x.FontAsset.name, fontNameToTest));
            if (fontUnitTest == null)
                return;

            if (Enum.TryParse(testNameSplit[1], out LocLanguages languageToTest) == false)
                return;

            List<string> foundIssues = new();
            List<int> charList = new List<int>();
            string cultureId = LocLanguagesInfo.LocLanguageToImportInfo[languageToTest].cultureVariantId;
            CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureId);
            
            StringBuilder sb = new StringBuilder(100000);
            Dictionary<string, string> foundCharToLocId = new();
            foreach (var pair in LocIDsEditorData.Combined_EditorHashIdToLocStringId)
            {
                // First removes lines and spaces, then removes Emojis from Text (second statement)
                int locId = pair.Key;
                string locText = Regex.Replace(LocManager.GetTextViaLocHashID(locId, languageToTest), $"\\n|\\s|{char.ConvertFromUtf32(10)}", string.Empty);
                locText = Regex.Replace(locText, @"[\p{So}\p{Cs}]", string.Empty); 
                
                int finalIndexOfLocString = locText.Length - 1;
                for (int i = 0; i <= finalIndexOfLocString; ++i)
                {
                    int unicode = locText[i];

                    // Handle surrogate pairs
                    if (i < finalIndexOfLocString && char.IsHighSurrogate((char)unicode) && char.IsLowSurrogate(locText[i + 1]))
                    {
                        unicode = char.ConvertToUtf32(locText[i], locText[i + 1]);
                        ++i;
                    }

                    // Check to make sure we don't include duplicates
                    if (charList.Contains(unicode) == false)
                    {
                        string character;
                        try
                        {
                            character = char.ConvertFromUtf32(unicode);
                        }
                        catch (System.Exception)
                        {
                            foundIssues.Add($"{fontUnitTest.FontAsset.name}: Could not convert Unicode [{unicode}] to a valid character. Original Character scanned was \"  {locText[i]}  \"");
                            continue;
                        }
                        sb.Append(character);
                        charList.Add(unicode);
                        foundCharToLocId.TryAdd(character, LocIDsEditorData.Combined_EditorHashIdToLocStringId[locId]);
                    }
                }
                
                for (int charIndex = charList.Count - 1; charIndex > -1; --charIndex)
                {
                    int unicode = charList[charIndex];
                    string character;
                    try
                    {
                        character = char.ConvertFromUtf32(unicode);
                    }
                    catch (System.Exception)
                    {
                        // No helpful error can be raised here because we're only dealing with numbers, and we shouldn't
                        // be able to make it here due to checks above in the String section. But the Try/Catch is here for safety sake.
                        continue;
                    }
                    string uppercase = character.ToUpper(cultureInfo);
                    string lowercase = character.ToLower(cultureInfo);
                    
                    unicode = uppercase[0];
                    if (charList.Contains(unicode) == false)
                    {
                        sb.Append(uppercase);
                        charList.Add(unicode);
                        foundCharToLocId.TryAdd(uppercase, LocIDsEditorData.Combined_EditorHashIdToLocStringId[locId]);
                    }
                    
                    unicode = lowercase[0];
                    if (charList.Contains(unicode) == false)
                    {
                        sb.Append(lowercase);
                        charList.Add(unicode);
                        foundCharToLocId.TryAdd(lowercase, LocIDsEditorData.Combined_EditorHashIdToLocStringId[locId]);
                    }
                }
            }
            
            string uniqueCharsBase = $" {sb}"; // Space is required
            int uniqueCharsLength = uniqueCharsBase.Length;
            List<string> uniqueCharsList = new(uniqueCharsLength);
            sb.Clear();
            for (int charIndex = 0; charIndex < uniqueCharsLength; ++charIndex)
            {
                string asString = uniqueCharsBase[charIndex].ToString();
                if (uniqueCharsList.Contains(asString) == false)
                {
                    sb.Append(asString);
                    uniqueCharsList.Add(asString);
                }
            }

            string fullCharacterSet = sb.ToString();
            int finalIndexOfCharacterSet = fullCharacterSet.Length - 1;
            for (int i = 0; i <= finalIndexOfCharacterSet; ++i)
            {
                int unicode = fullCharacterSet[i];
                string character;
                try
                {
                    character = char.ConvertFromUtf32(unicode);
                }
                catch (System.Exception)
                {
                    foundIssues.Add($"{fontUnitTest.FontAsset.name}: Could not convert Unicode [{unicode}] to a valid character. Original Character scanned was \"  {fullCharacterSet[i]}  \"");
                    continue;
                }

                if (string.IsNullOrEmpty(character) || character[0] == 65039)
                {
                    // Unicode Decimal Code (65039) is a character to tell the Encoder which character set to lookup.
                    // It appears in our scans so we need to ignore it as it isn't visible.
                    continue;
                }

                if (TestIfFontAtlasHasCharacter(fontUnitTest.FontAsset, unicode, fontUnitTest.CheckFallbackFontsIfNotFound) == false)
                {
                    string locIDStr = foundCharToLocId.GetValueOrDefault(character, "N/A");
                    if (fontUnitTest.CheckFallbackFontsIfNotFound)
                    {
                        foundIssues.Add($"{fontUnitTest.FontAsset.name}: Missing {languageToTest} Character: \"  {character}  \" found in Loc ID [{locIDStr}]. Either add this character to the font atlas, or Add a fallback font which has this character");
                    }
                    else
                    {
                        foundIssues.Add($"{fontUnitTest.FontAsset.name}: Missing {languageToTest} Character: \"  {character}  \" found in Loc ID [{locIDStr}]");
                    }
                            
                }
            }

            if (foundIssues.Count > 0)
            {
                Assert.Fail(string.Join(",\n", foundIssues));
            }
        }

        private bool TestIfFontAtlasHasCharacter(TMP_FontAsset font, int unicode, bool canCheckFallbackFonts, List<TMP_FontAsset>? scannedFonts = null)
        {
            if (font == null)
                return false;

            if (font.HasCharacter(unicode))
                return true;

            if (canCheckFallbackFonts == false)
                return false;
            
            if (scannedFonts == null)
                scannedFonts = new List<TMP_FontAsset>();
            else if (scannedFonts.Contains(font))
                return false;
            
            scannedFonts.Add(font);
            foreach (TMP_FontAsset fallbackFontAsset in font.fallbackFontAssetTable)
            {
                if (TestIfFontAtlasHasCharacter(fallbackFontAsset, unicode, true, scannedFonts))
                    return true;
            }

            return false;
        }
    }
}
