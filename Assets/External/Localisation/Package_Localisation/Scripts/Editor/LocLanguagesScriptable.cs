//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//              Localisation Languages Scriptable
//              Author: Christopher Allport
//              Date Created: August 13, 2020
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//		  This Script acts as a middle man for designers/UI crew to edit the
//      Languages being used in the Localisation system. This script essentially
//      Takes input via the LocLanguageScriptable Config File (found in the project)
//      and then outputs those as const values in LocLanguages.cs
//
//        Essentially writing code to the script.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Localisation.LocalisationOverrides;
using UnityEngine;
using UnityEditor;
using Debug = Localisation.LocalisationOverrides.Debug;


namespace Localisation.Localisation.Editor
{
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    // *            Scriptable Object
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    public partial class LocLanguagesScriptable : ScriptableObject
    {
        private class LocLanguageInfo
        {
            /// <summary> Language Name before it was overwritten. </summary>
            public string originalLanguageName;
            /// <summary> Language Name currently. </summary>
            public string newLanguageName;
            /// <summary> System Language this gets converted from. </summary>
            public SystemLanguage systemLanguage;
            /// <summary> Is Right-To-Left Language. </summary>
            public bool isRightToLeftLanguage;

            /// <summary> This is used by the Google Doc Import Tool to determine which settings it should use per language. </summary>
            public LocLanguagesInfo.LanguageImportData langImportData = new LocLanguagesInfo.LanguageImportData();
        }

        private LocLanguageInfo[] locLanguagesData;

        protected void OnEnable()
        {
            RefreshData();
        }

        protected void RefreshData()
        {
            LocLanguages[] allLanguages = (System.Enum.GetValues(typeof(LocLanguages)) as LocLanguages[])!;
            int languagesCount = allLanguages.Length;

            locLanguagesData = new LocLanguageInfo[languagesCount];
            for (int i = 0; i < languagesCount; ++i)
            {
                locLanguagesData[i] = new LocLanguageInfo
                {
                    originalLanguageName = allLanguages[i].ToString(),
                    newLanguageName = allLanguages[i].ToString(),
                    isRightToLeftLanguage = LocLanguagesInfo.RightToLeftLanguages.Contains(allLanguages[i])
                };

                if (LocLanguagesInfo.LocLanguageToImportInfo.TryGetValue(allLanguages[i], out locLanguagesData[i].langImportData) == false)
				{
                    locLanguagesData[i].langImportData = new LocLanguagesInfo.LanguageImportData();
                }

                // Find out which system language this pairs with.
                locLanguagesData[i].systemLanguage = SystemLanguage.Unknown;
                foreach (KeyValuePair<UnityEngine.SystemLanguage, LocLanguages> langPair in LocLanguagesInfo.SystemLangToLocLang)
                {
                    if (langPair.Value == allLanguages[i])
                    {
                        locLanguagesData[i].systemLanguage = langPair.Key;
                        break;
                    }
                }
            }
        }


        [MenuItem(Configuration.MenuCategorisation + "Open Languages Config")]
        public static void SelectLanguagesConfigFile()
        {
            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(Configuration.LocalisationLanguagesConfigFilePath, typeof(UnityEngine.Object));
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }
    }





    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    // *            Custom Editor
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
#if UNITY_EDITOR
    public partial class LocLanguagesScriptable
    {
        [UnityEditor.CustomEditor(typeof(LocLanguagesScriptable))]
        protected class LocLanguagesScriptableEditor : CustomEditorBase<LocLanguagesScriptable>
        {
            private bool hasMadeLocLanguageChanges = false;

            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // *            Languages Edit
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            [InspectorRegion]
            private void DrawLocLanguageGenerationOptions()
            {
                if (hasMadeLocLanguageChanges)
                {
                    EditorHelper.DrawDescriptionBox("Changes have been detected. You must click on \"Generate Localisation Languages\" below for your changes to take effect.");
                }

                string missingSettings = string.Empty;
                for (int i = 0; i < Target.locLanguagesData.Length ; ++i)
                {
                    LocLanguageInfo langSetting = Target.locLanguagesData[i];
                    if (string.IsNullOrEmpty(langSetting.newLanguageName))
                    {
                        missingSettings += $"\nLanguage {i + 1:00}: Missing Language Name.";
                    }

                    if (string.IsNullOrEmpty(langSetting.langImportData.cultureVariantId)
                        || CultureInfo.GetCultures(CultureTypes.AllCultures).Any(culture => string.Equals(culture.Name, langSetting.langImportData.cultureVariantId, StringComparison.OrdinalIgnoreCase)) == false)
                    {
                        missingSettings += $"\nLanguage {i + 1:00}: Invalid Culture ID.";
                    }
                }

                if (string.IsNullOrEmpty(missingSettings))
                {
                    if (EditorHelper.DrawButtonField("Generate Localisation Languages"))
                    {
                        GenerateLocLanguages();
                        hasMadeLocLanguageChanges = false;
                    }
                }
                else
                {
                    EditorHelper.DrawDescriptionBox(missingSettings);
                    EditorHelper.DrawReadonlyButtonField("Generate Localisation Languages");
                }
            }

            [InspectorRegion("~Languages~")]
            private void DrawLanguagesConfigOptions()
            {
                DrawArrayParameters<LocLanguageInfo> drawParams = new DrawArrayParameters<LocLanguageInfo>()
                {
                    CanReorderArray = true,
                    IsArrayResizeable = true,
                    SpacesToAddBetweenElements = 2,
                    AlphabeticalComparer = AlphabeticalComparer,
                };

                ArrayModificationData arrayModData = EditorHelper.DrawReferenceArrayElements(ref Target.locLanguagesData, drawParams, DrawLanguageElement);
                if (arrayModData.ModResult != ArrayModificationResult.NoChange)
                {
                    hasMadeLocLanguageChanges = true;
                }
            }

            private void DrawLanguageElement(int _index)
            {
                EditorHelper.DrawStringFieldWithUndoOption($"  Language {EditorHelper.GetZeroedNumPrefixString(_index + 1)}:", ref Target.locLanguagesData[_index].newLanguageName);
                EditorHelper.DrawStringFieldWithUndoOption("  Culture Variant Id:", ref Target.locLanguagesData[_index].langImportData.cultureVariantId, "The Culture Variant Id for this Language. Required to convert to Upper/Lower case characters.");
                EditorHelper.DrawEnumFieldWithUndoOption("  System Language:", ref Target.locLanguagesData[_index].systemLanguage, true, "Which System language is this associated with");
                EditorHelper.DrawBoolFieldWithUndoOption("  Is Right To Left:", ref Target.locLanguagesData[_index].isRightToLeftLanguage, "Is this language supposed to be rendered from Right To Left?");
#if !USING_UNITY_LOCALISATION
                AddSpaces(3);
                EditorHelper.DrawEnumFieldWithUndoOption("  Render Mode:", ref Target.locLanguagesData[_index].langImportData.renderMode, drawInAlphabeticalOrder: true, tooltip: "How will this Language Glyph Atlas be Generated/Rendered");
                EditorHelper.DrawChangeableIntegerWithUndoOption("  Padding:", ref Target.locLanguagesData[_index].langImportData.padding, minValue: 0, maxValue: 30, tooltip: "Allowable pixels between the edges of the glyph atlas");
#endif
            }

            private int AlphabeticalComparer(LocLanguageInfo _val1, LocLanguageInfo _val2)
            {
                // English should be on top as it's always the first column in the Localisation Document.
                if (string.Equals(_val1.newLanguageName, "English", StringComparison.OrdinalIgnoreCase))
				{
                    if (string.Equals(_val2.newLanguageName, "English", StringComparison.OrdinalIgnoreCase))
                        return 0;
                    
                    return -1;
				}
                if (string.Equals(_val2.newLanguageName, "English", StringComparison.OrdinalIgnoreCase))
				{
                    return 1;
				}
                return String.CompareOrdinal(_val1.newLanguageName, _val2.newLanguageName);
            }

            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // *            Languages Generation
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            /// <summary> Generates the LocLanguages.cs script file. </summary>
            private void GenerateLocLanguages()
            {
                using (System.IO.StreamWriter writer = new System.IO.StreamWriter(Configuration.LocalisationLanguagesFilePath, false))
                {
                    try
                    {
                        List<string> rightToLeftLanguages = new List<string>();
                        Dictionary<string, string> systemLanguageToLocLanguage = new Dictionary<string, string>();

                        System.Text.StringBuilder scriptCode = new System.Text.StringBuilder();
                        scriptCode.AppendLine("// Game Specific language list. Auto-Generated by LocLanguagesScriptable.cs");
                        scriptCode.AppendLine("using System.Collections.Generic;");
                        scriptCode.AppendLine("using UnityEngine.TextCore.LowLevel;");
                        scriptCode.AppendLine();
                        scriptCode.AppendLine("public enum LocLanguages");
                        scriptCode.AppendLine("{");

                        foreach (LocLanguageInfo languageInfo in Target.locLanguagesData)
                        {
                            if (string.IsNullOrEmpty(languageInfo.newLanguageName))
                            {
                                continue;
                            }

                            const string IllegalCharactersRegex = "\\W";
                            string legalLanguageName = System.Text.RegularExpressions.Regex.Replace(languageInfo.newLanguageName, IllegalCharactersRegex, "_");
                            while (legalLanguageName.Length > 0 && System.Text.RegularExpressions.Regex.IsMatch($"{legalLanguageName[0]}", "\\d"))
                            {
                                // Enums also cannot start with numbers. So keep removing number from first character until no numbers are the first character.
                                legalLanguageName = legalLanguageName.Substring(1);
                            }

                            // Setup for next phase
                            if (languageInfo.isRightToLeftLanguage)
                            {
                                rightToLeftLanguages.Add(legalLanguageName);
                            }

                            // Writing out language to file.
                            scriptCode.AppendLine("    " + legalLanguageName + ',');

                            if (systemLanguageToLocLanguage.ContainsKey(languageInfo.systemLanguage.ToString()) == false)
                            {
                                systemLanguageToLocLanguage[languageInfo.systemLanguage.ToString()] = legalLanguageName;
                            }
                        }
                        scriptCode.AppendLine("}");
                        scriptCode.AppendLine();

                        scriptCode.AppendLine("namespace Localisation");
                        scriptCode.AppendLine("{");
                        {
                            scriptCode.AppendLine("    public static class LocLanguagesInfo");
                            scriptCode.AppendLine("    {");
                            {
                                // Write-in Default Class Details
                                {
                                    scriptCode.AppendLine("        [System.Serializable]");
                                    scriptCode.AppendLine("        public class LanguageImportData");
                                    scriptCode.AppendLine("        {");
                                    scriptCode.AppendLine("            public string cultureVariantId = \"en\";");
                                    scriptCode.AppendLine("            public GlyphRenderMode renderMode = GlyphRenderMode.SDFAA;");
                                    scriptCode.AppendLine("            public int padding = 4;");
                                    scriptCode.AppendLine("        }\n\n");
                                }
                                
                                // Write-in Right to Left Languages
                                {
                                    scriptCode.AppendLine("        /// <summary> Which languages must be drawn from Right To Left. </summary>");
                                    scriptCode.AppendLine("        public static readonly List<LocLanguages> RightToLeftLanguages = new List<LocLanguages>()");
                                    scriptCode.AppendLine("        {");
                                    foreach (string rtlLanguage in rightToLeftLanguages)
                                    {
                                        scriptCode.AppendLine("            LocLanguages." + rtlLanguage + ',');
                                    }
                                    scriptCode.AppendLine("        };");
                                }
                                scriptCode.AppendLine();

                                // Write-in SystemLanguages To Localisation Languages Conversion
                                {
                                    scriptCode.AppendLine("        /// <summary> Which Language should the default System Language of the device convert into. Defaults to English if System Language is not specified here. </summary>");
                                    scriptCode.AppendLine("        public static readonly Dictionary<UnityEngine.SystemLanguage, LocLanguages> SystemLangToLocLang = new Dictionary<UnityEngine.SystemLanguage, LocLanguages>()");
                                    scriptCode.AppendLine("        {");
                                    foreach (KeyValuePair<string, string> sysLangLocLangPair in systemLanguageToLocLanguage)
                                    {
                                        scriptCode.AppendLine("            { UnityEngine.SystemLanguage." + sysLangLocLangPair.Key + ",     LocLanguages." + sysLangLocLangPair.Value + " },");
                                    }
                                    scriptCode.AppendLine("        };");
                                }

                                {
                                    scriptCode.AppendLine();
                                    scriptCode.AppendLine("        /// <summary> Converts Loc Language back to System Language. </summary>");
                                    scriptCode.AppendLine("        public static readonly Dictionary<LocLanguages, UnityEngine.SystemLanguage> LocLangToSystemLang = new System.Func<Dictionary<LocLanguages, UnityEngine.SystemLanguage>>(() =>");
                                    scriptCode.AppendLine("        {");
                                    scriptCode.AppendLine("            Dictionary<LocLanguages, UnityEngine.SystemLanguage> locLangToSystemLang = new(SystemLangToLocLang.Count);");
                                    scriptCode.AppendLine("            foreach (var pair in SystemLangToLocLang)");
                                    scriptCode.AppendLine("                locLangToSystemLang[pair.Value] = pair.Key;");
                                    scriptCode.AppendLine("            return locLangToSystemLang;");
                                    scriptCode.AppendLine("        })();");
                                }

                                {
                                    scriptCode.AppendLine();
                                    scriptCode.AppendLine("        /// <summary> This is used by the Google Doc Import Tool to determine which settings it should use per language. </summary>");
                                    scriptCode.AppendLine("        public static readonly Dictionary<LocLanguages, LanguageImportData> LocLanguageToImportInfo = new Dictionary<LocLanguages, LanguageImportData>()");
                                    scriptCode.AppendLine("        {");
                                    foreach (LocLanguageInfo languageData in Target.locLanguagesData)
                                    {
                                        scriptCode.AppendLine("           { LocLanguages." + languageData.newLanguageName + ", new LanguageImportData()");
                                        scriptCode.AppendLine("                {");
                                        scriptCode.AppendLine("                    cultureVariantId = \""  + languageData.langImportData.cultureVariantId + "\",");
                                        scriptCode.AppendLine("                    renderMode = GlyphRenderMode." + languageData.langImportData.renderMode + ",");
                                        scriptCode.AppendLine("                    padding = " + languageData.langImportData.padding + ",");
                                        scriptCode.AppendLine("                }");
                                        scriptCode.AppendLine("           },");
                                        scriptCode.AppendLine();
                                    }
                                    scriptCode.AppendLine("        };");
                                }
                                
                                scriptCode.AppendLine("        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
                                scriptCode.AppendLine("        //                   Languages Info                       ");
                                scriptCode.AppendLine("        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~");
                                scriptCode.AppendLine("        /// <summary> Internal use flags for languages. You should continue to use LocLanguages for most things if not doing internal system stuff. </summary>");
                                scriptCode.AppendLine("        [System.Flags]");
                                scriptCode.AppendLine("        public enum LocLanguageFlags");
                                scriptCode.AppendLine("        {");
                                int languageFlagCount = 0;
                                foreach (LocLanguageInfo languageInfo in Target.locLanguagesData)
                                {
                                    if (string.IsNullOrEmpty(languageInfo.newLanguageName) == false)
                                    {
                                        scriptCode.AppendLine($"            {languageInfo.newLanguageName} = 1 << {languageFlagCount++},");
                                    }
                                }
                                scriptCode.AppendLine("        }");
                                scriptCode.AppendLine();

                                scriptCode.AppendLine("        /// <summary> Internal use for converting Language Flags for languages to individual Language (array). </summary>");
                                scriptCode.AppendLine("        public static LocLanguages[] LocLanguageFlagsToLocLanguages(LocLanguageFlags flags)");
                                scriptCode.AppendLine("        {");
                                scriptCode.AppendLine("            List<LocLanguages> languages = new List<LocLanguages>();");
                                foreach (LocLanguageInfo languageInfo in Target.locLanguagesData)
                                {
                                    if (string.IsNullOrEmpty(languageInfo.newLanguageName) == false)
                                    {
                                        scriptCode.AppendLine($"            if (flags.HasFlag(LocLanguageFlags.{languageInfo.newLanguageName})) languages.Add(LocLanguages.{languageInfo.newLanguageName});");
                                    }
                                }
                                scriptCode.AppendLine("            return languages.ToArray();");
                                scriptCode.AppendLine("        }");

                            } // End of public static class LocLanguagesInfo
                            scriptCode.AppendLine("    }");

                        } // End of Namespace PlaySide
                        scriptCode.AppendLine("}");

                        // Write the newly created script code to file.
                        writer.Write(scriptCode.ToString());
                    }
                    catch (System.Exception ex)
                    {
                        string msg = $" threw:\n{ex.Message}";
                        Debug.LogError(msg);
                        EditorHelper.ShowDialogueWindow("Error when trying to regenerate class", msg);
                    }
                }
                UnityEditor.AssetDatabase.Refresh();
                Target.RefreshData();
            }
        }
    }
#endif
}
