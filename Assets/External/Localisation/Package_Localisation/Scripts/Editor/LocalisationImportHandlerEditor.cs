using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace Localisation.Localisation.Editor
{
    public partial class LocalisationImportHandler
    {
        [UnityEditor.CustomEditor(typeof(LocalisationImportHandler))]
        public class LocalisationImportHandlerEditor : CustomEditorBase<LocalisationImportHandler>
        {
            protected override void OnInitialised()
            {
                base.OnInitialised();

                if (Target.uniqueCharsLanguageSelect.Length != LocManager.AllLocLanguages.Length)
                {
                    EditorHelper.ResizeArray(ref Target.uniqueCharsLanguageSelect, LocManager.AllLocLanguages.Length);
                }

                // Clearing Context State. No need to show previous import status when reloading the Config File.
                Target.CurrentStateContext = string.Empty;
                Target.FullStateContext = string.Empty;
                EditorHelper.ClearProgressBarWindow();
            }

            public override void SetDirty()
            {
                // Unity doesn't save Scriptable Objects by default (there's an option for it in settings), you have to 'Save Project' first in many cases,
                // so this is just a hack to make changes get saved immediately
                base.SetDirty();
                UnityEditor.AssetDatabase.SaveAssets();
            }

            protected void OnImportFinished()
            {
                if (EditorHelper.IsProgressBarWindowActive)
                {
                    EditorHelper.ClearProgressBarWindow();
                }
            }

            [InspectorRegion("~Guide~")]
            protected void DrawGuideDetails()
            {
                if (EditorHelper.DrawButtonField("Take me to the confluence page"))
                {
                    Application.OpenURL("https://github.com/CDiamond0231/unity-localisation");
                }
            }

            [InspectorRegion("~Import Status~")]
            protected void DrawImportStatus()
            {
                using (new GUIDisable())
                {
                    if (string.IsNullOrEmpty(Target.FullStateContext))
                    {
                        EditorHelper.DrawTextArea("Import Process not started\n\n\n\n\n");
                    }
                    else
                    {
                        EditorHelper.DrawTextArea(Target.FullStateContext);
                    }
                }
            }

            [InspectorRegion("~Import Options~")]
            protected void DrawImportButtons()
            {
                EditorHelper.DrawDescriptionBox("Downloads the spreadsheet from Google, regenerates the LocIDs, then creates the Glyph Atlases for the languages that are marked for Font Repackaging.");

                if (EditorHelper.DrawButtonField("Begin Import Process"))
                {
                    Target.BeginLocalisationImportProcess(OnImportFinished);
                }
            }

            [InspectorRegion("~Google Sheet Details~")]
            protected void DrawGoogleSheetOptions()
            {
                DrawArrayParameters<DocumentFetcherInfo> drawParams = new DrawArrayParameters<DocumentFetcherInfo>()
                {
                    CanReorderArray = false,
                    MinSize = 1,
                    SpacesToAddBetweenElements = 2
                };
                    
                EditorHelper.DrawDescriptionBox("Google Doc Id:\n~~~~~~~~~~~~~~~~~~~~~~\nThe Id of the document. Can be found in the URL Link.\n\n"
                                 + "~~~~~~~~~~~~~~~~~~~~~~\nThis is all explained on the confluence page with images. Please click the guide button above to see step by step instructions.");

                EditorHelper.DrawReferenceArrayElements(ref Target.documentFetcherInfo, drawParams, DrawDocumentInfoElement);
            }

            private void DrawDocumentInfoElement(int _index)
            {
                EditorHelper.DrawStringFieldWithUndoOption("Google Doc Id:", ref Target.documentFetcherInfo[_index].googleDocId, "The Id of the Google Doc we will be downloading/importing.");
                EditorHelper.DrawStringFieldWithUndoOption("CSV File Path:", ref Target.documentFetcherInfo[_index].csvFilePath, "The local file path too be allocated to this LocStrings CSV.");
            }

#if USING_UNITY_LOCALISATION
            private bool isFetchingUniqueCharacters = false;

            [InspectorRegion("~~~~~~~~~~~~~~~~~~~~~~\nUnique Characters List\n~~~~~~~~~~~~~~~~~~~~~~")]
            protected async void DrawUniqueCharactersOptions()
            {
                EditorHelper.DrawDescriptionBox("This will only help you find all characters used in the selected languages from the Localisation Doc to keep Font Atlas Sizes down. If you are allowing players to name themselves. or have any other dynamic characters, you will need to ensure all language characters are usable");
                EditorHelper.DrawLabel("Characters to include in every language.");
                EditorHelper.DrawLabel("~~~This should be currencies, etc.~~~");
                EditorHelper.DrawLabel("Something you'll have in every language.");
                EditorHelper.DrawTextAreaWithUndoOption(ref Target.charactersToIncludeForEveryAtlas);

                EditorHelper.DrawLabel("Fetch Unique Chars for");
                using (new EditorIndentation())
                {
                    for (int languageIndex = 0; languageIndex < LocManager.AllLocLanguages.Length; ++languageIndex)
                    {
                        EditorHelper.DrawToggleFieldWithUndoOption($"{LocManager.AllLocLanguages[languageIndex]}:", ref Target.uniqueCharsLanguageSelect[languageIndex], useEnumPopup: false);
                    }
                }

                if (isFetchingUniqueCharacters)
                {
                    EditorHelper.DrawReadonlyButtonField("Fetch Unique Characters");
                }
                else if (EditorHelper.DrawButtonField("Fetch Unique Characters"))
                {
                    isFetchingUniqueCharacters = true;
                    await FetchAllUniqueCharacters();
                    isFetchingUniqueCharacters = false;
                    Repaint();
                }
                else
                {
                    EditorHelper.DrawLabel("vvvvvvvvvvv");
                    EditorHelper.DrawTextArea(Target.uniqueCharactersForSelectedLanguages);
                }
            }

            private async Task FetchAllUniqueCharacters()
            {
                Target.uniqueCharactersForSelectedLanguages = string.Empty;
                StringBuilder sb = new StringBuilder(1000000);
                List<int> charList = new List<int>();
                for (int languageIndex = 0; languageIndex < LocManager.AllLocLanguages.Length; ++languageIndex)
                {
                    if (Target.uniqueCharsLanguageSelect[languageIndex])
                    {
                        LocLanguages language = LocManager.AllLocLanguages[languageIndex];
                        string cultureId = LocLanguagesInfo.LocLanguageToImportInfo[language].cultureVariantId;
                        CultureInfo cultureInfo = CultureInfo.GetCultureInfo(cultureId);
                        if (string.IsNullOrEmpty(cultureId))
                        {
                            EditorHelper.DrawWrappedText($"No Culture ID specified for {language}.", null, Color.red);
                            return;
                        }

                        float startProgress = (float)languageIndex / LocManager.AllLocLanguages.Length;
                        float targetEndProgress = (float)(languageIndex + 1) / LocManager.AllLocLanguages.Length;
                        int locIdsScanCount = 0;
                        int totalLocIdsCount = LocIDsEditorData.Combined_EditorHashIdToLocStringId.Count;
                        foreach (var pair in LocIDsEditorData.Combined_EditorHashIdToLocStringId)
                        {
                            float progress = Mathf.Lerp(startProgress, targetEndProgress, (float)(locIdsScanCount++) / totalLocIdsCount);
                            EditorHelper.DrawProgressBarWindow("Fetching Unique Chars", $"Fetching {language} Characters from Loc IDs [{locIdsScanCount}/{totalLocIdsCount}]", progress);
                            int locId = pair.Key;
                            string locText = Regex.Replace(Regex.Replace(LocManager.GetTextViaLocHashID(locId, language), $"\\n|\\s|{char.ConvertFromUtf32(10)}", string.Empty), @"[\p{So}\p{Cs}]", string.Empty);
                            
                            int finalIndexOfLocString = locText.Length - 1;
                            int unicode;
                            for (int i = 0; i <= finalIndexOfLocString; ++i)
                            {
                                unicode = locText[i];

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
                                        // These exceptions are raised on Emojis. They can be ignored.
                                        continue;
                                    }

                                    if (string.IsNullOrEmpty(character) || character[0] == 65039)
                                    {
                                        // Unicode Decimal Code (65039) is a character to tell the Encoder which character set to lookup.
                                        // It appears in our scans so we need to ignore it as it isn't visible.
                                        continue;
                                    }

                                    sb.Append(character);
                                    charList.Add(unicode);
                                }
                            }
                            
                            EditorHelper.DrawProgressBarWindow("Fetching Unique Chars", $"Fetching {language} chars [Upper & Lower]", targetEndProgress);
                            for (int charIndex = charList.Count - 1; charIndex > -1; --charIndex)
                            {
                                unicode = charList[charIndex];
                                string character = char.ConvertFromUtf32(unicode);
                                string uppercase = character.ToUpper(cultureInfo);
                                string lowercase = character.ToLower(cultureInfo);
                                
                                unicode = uppercase[0];
                                if (charList.Contains(unicode) == false)
                                {
                                    sb.Append(uppercase);
                                    charList.Add(unicode);
                                }
                                
                                unicode = lowercase[0];
                                if (charList.Contains(unicode) == false)
                                {
                                    sb.Append(lowercase);
                                    charList.Add(unicode);
                                }
                            }
                        }
                        
                        await Task.Delay(10);
                    }
                }

                string uniqueCharsBase = $"{Target.charactersToIncludeForEveryAtlas} \n{char.ConvertFromUtf32(10)}{sb}";
                int uniqueCharsLength = uniqueCharsBase.Length;
                List<string> uniqueCharsList = new(uniqueCharsLength);
                sb.Clear();
                for (int charIndex = 0; charIndex < uniqueCharsLength; ++charIndex)
                {
                    EditorHelper.DrawProgressBarWindow("Fetching Unique Chars", $"Outputting all Unique Characters [{charIndex}/{uniqueCharsLength}]", (float)charIndex / uniqueCharsLength);
                    string asString = uniqueCharsBase[charIndex].ToString();
                    if (uniqueCharsList.Contains(asString) == false)
                    {
                        sb.Append(asString);
                        uniqueCharsList.Add(asString);
                    }
                }

                Target.uniqueCharactersForSelectedLanguages = sb.ToString();
                EditorHelper.ClearProgressBarWindow();
                await Task.Delay(10);
            }
#else
            [InspectorRegion("~Fallback Fonts~")]
            protected void DrawFallbackFontOptions()
            {
                EditorHelper.DrawDescriptionBox("In case a character cannot be found in your assigned Font Sheet. Fallback to the following sheets and try again.");

                DrawArrayParameters<TMPro.TMP_FontAsset> drawParams = new DrawArrayParameters<TMPro.TMP_FontAsset>()
                {
                    CanReorderArray = true,
                    IsArrayResizeable = true,
                };

                EditorHelper.DrawReferenceArrayElements(ref Target.fallbackFontSheets, drawParams, DrawFallbackFontElement);
            }

            private void DrawFallbackFontElement(int index)
            {
                EditorHelper.DrawObjectOptionWithUndoOption($"Fallback Font {EditorHelper.GetZeroedNumPrefixString(index + 1)}:", ref Target.fallbackFontSheets[index]);
            }

            [InspectorRegion("~Characters To Include For Every Atlas~")]
            protected void DrawCurrencyOptions()
            {
                const int UnicodeMax = 255;
                const string Tooltip = "List down all text you want to include in every atlas including English and any Currencies you are using. The currency glyphs may be used in any language, so they all need to be included here.";
                EditorHelper.DrawDescriptionBox(Tooltip);
                EditorHelper.DrawLabel("Characters To Include For Every Atlas:");
                if (EditorHelper.DrawButtonField($"Auto-Generate via Unicodes 1-{UnicodeMax}", Tooltip, ButtonAlignment.Right))
                {                    
                    string defaultForEachLanguage = "$€¥£元₩₹₽₺฿₪₱د.إ﷼";    // Currencies can be displayed at any time. Need them for all languages
                    defaultForEachLanguage += " \n";                        // Must include a space and newline otherwise TMPro doesn't know how to deal with those characters
                    defaultForEachLanguage += "!\"#$%&'’()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz"; // And every language needs English Characters as it is the global language.

                    System.Text.StringBuilder builder = new System.Text.StringBuilder(defaultForEachLanguage.Length + UnicodeMax * 2); // Times 2 for surrogate pairings.
                    builder.Append(defaultForEachLanguage);
                    for (int i = 33; i < UnicodeMax; ++i)
                    {
                        string unicodeChar = Char.ConvertFromUtf32(i);
                        builder.Append(unicodeChar);
                    }
                    Target.charactersToIncludeForEveryAtlas = new string(builder.ToString().Distinct().ToArray());
                    SetDirty();
                }
                EditorHelper.DrawTextAreaWithUndoOption(ref Target.charactersToIncludeForEveryAtlas);
            }
#endif
        }
    }
}
