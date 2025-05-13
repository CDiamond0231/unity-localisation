//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Localisation Manager
//             Author: Christopher Allport
//             Date Created: 24th November, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Handles localisation table requests including Loc IDs to Row IDs,
//      language string, etc.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#if UNITY_EDITOR
    #define ALLOW_LOCAL_FILES
#endif



using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using static Localisation.Localisation.LocDisplayStyleFontInfo;
using System.Threading.Tasks;
using Localisation.LocalisationOverrides;
using TMPro;
using Debug = UnityEngine.Debug;

#if USING_UNITY_LOCALISATION
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
#endif


namespace Localisation.Localisation
{
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //       LocManager - Game Class
    //  This code will exist in both editor and builds
    //-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
    public partial class LocManager : MonoBehaviour
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Consts
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public const string LocVersion = "0.0.1";
        public const string LocStringsTableFullFileName = LocalisationOverrides.Configuration.MasterLocStringsTableFileName + LocalisationOverrides.Configuration.MasterLocStringsTableFileExt;
        public const string SelectedLanguagePlayerPrefKey = Configuration.SelectedLanguagePlayerPrefsKey;
        public const int LocTablesCount = LocIDs.LocDocsInfo.TotalLocDocs;

        public static readonly LocLanguages[] AllLocLanguages = System.Enum.GetValues(typeof(LocLanguages)) as LocLanguages[];
        public static readonly string[] AllLocLanguageIds = AllLocLanguages.Select(lang => lang.ToString()).ToArray();
        public static readonly int LocLanguagesCount = AllLocLanguages.Length;

        public static readonly LocalisationTextDisplayStyleTypes[] AllTextDisplayStyles = System.Enum.GetValues(typeof(LocalisationTextDisplayStyleTypes)) as LocalisationTextDisplayStyleTypes[];
        public static readonly int AllTextDisplayStylesCount = AllTextDisplayStyles.Length;
        
        private const string IllegalCharactersRegex = "\\W";




        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Definitions
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public enum EnglishOnlyMode
		{
            /// <summary> Shows English Only in Editor and Live Builds. </summary>
            EnglishOnly,
            /// <summary> Shows English only during Live Builds. In Editor can still switch to other languages. </summary>
            EnglishOnlyDuringLiveRelease,
            /// <summary> All languages are usable in both editor and on live builds. </summary>
            AllLanguages,
		}

        [System.Serializable]
        public class FontUnitTestInfo
        {
            public TMP_FontAsset FontAsset;
            public bool CheckFallbackFontsIfNotFound = true;
            public LocLanguagesInfo.LocLanguageFlags LanguagesToCheckAgainst = LocLanguagesInfo.LocLanguageFlags.English;
        }

        [System.Serializable]
        public class LocManagerSettings
		{
            public EnglishOnlyMode EnglishOnlyMode = EnglishOnlyMode.AllLanguages;
		}

        [System.Serializable]
        public class LocLanguageToLocStyleTypesDict : SerialisableDictionary<string, LocDisplayStyleTypeToLocDisplayStyleInfoDict>
        {
        }

        [System.Serializable]
        public class LocDisplayStyleTypeToLocDisplayStyleInfoDict : SerialisableDictionary<string, LocDisplayStyleFontInfo>
		{
		}

        /// <summary> Text string for each language </summary>
        public class LanguageStrings
        {
            public string[] strings;

            public LanguageStrings(string[] _strings)
            {
                strings = _strings;
            }

            public string Get(LocLanguages _language)
            {
                int langCellColId = LocManager.locLanguageCellIndexIds[(int)_language];
                return strings[langCellColId];
            }
        }

        public enum FontStyles
        {
            Normal,
            Stroked,
            Underlined,
        }

        public enum LocStatus
        {
            Success,
            BadLocHashId,
            BadLocTableLine,
            NoTextFoundForLanguage
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [SerializeField, HandledInCustomInspector] private LocManagerSettings locManagerSettings = new LocManagerSettings();
        [SerializeField, HandledInCustomInspector] private LocLanguageToLocStyleTypesDict locLanguageToLocStyleTypesDict = new LocLanguageToLocStyleTypesDict();
        [SerializeField, HandledInCustomInspector] private FontUnitTestInfo[] fontsToApplyUnitTests = Array.Empty<FontUnitTestInfo>();

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Non-Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private static LocManager instance = null;

        /// <summary> Interpreter class that scans through Addressable Loc Strings CSV files and finds out where the link is between Loc Ids and Row Ids in the CSV file. </summary>
        private static LocIDsToRowIDsInterpreter locIDsToRowIDsInterpreter = new LocIDsToRowIDsInterpreter();

        /// <summary> Localisation Table could be in a different order than our LocLanguages Enum. This converts LocLanguage to the correct Loc Table Column. </summary>
        private static int[] locLanguageCellIndexIds = Array.Empty<int>();

        /// <summary> All text strings in all languages for all Loc Tables. </summary>
        private static LanguageStrings[][] allLanguageStrings;

        private static bool switchingLanguages = false;

        private static LocLanguages? currentLanguage = null;
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Property Accessors
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Currently selected language </summary>
        public static LocLanguages CurrentLanguage
        {
            get 
            {
                if (currentLanguage.HasValue == false)
                {
                    int saveLanguageId = LocalisationOverrides.SaveSystem.GetInt(SelectedLanguagePlayerPrefKey, -1);
                    if (saveLanguageId == -1)
                    {
                        currentLanguage = GetInstalledLanguage();
                        LocalisationOverrides.SaveSystem.SetInt(SelectedLanguagePlayerPrefKey, (int)currentLanguage.Value);
                        LocalisationOverrides.SaveSystem.Save();
                    }
                    else
                    {
                        currentLanguage = (LocLanguages)saveLanguageId;
                    }
                }

                return currentLanguage.Value;
            }
            private set
            {
                // Saves Language to Prefs if different
                if (currentLanguage.HasValue && currentLanguage.Value != value)
                {
                    LocalisationOverrides.SaveSystem.SetInt(SelectedLanguagePlayerPrefKey, (int)value);
                    LocalisationOverrides.SaveSystem.Save();
                }
                
                currentLanguage = value;
            }
        }

        /// <summary> Culture ID for Current Language. </summary>
        public static string CurrentLanguageCultureID
        {
            get { return LocLanguagesInfo.LocLanguageToImportInfo[CurrentLanguage].cultureVariantId; }
        }

        /// <summary> Which CSV do you want the Loc System to query as a priority (assuming you have multiple Loc String CSV Files). </summary>
        public static string CSVPreference { get; set; } = LocalisationOverrides.Configuration.MasterLocStringsTableFileName;

        /// <summary> All text strings in all languages </summary>
        public static LanguageStrings[][] AllLanguageStrings
        {
            get
            {
                if (allLanguageStrings == null)
                    StaticInitialise();

                return allLanguageStrings;
            }
        }

		/// <summary> Returns the Instance of the LocManager in the scene. </summary>
		public static LocManager Instance
		{
            get
			{
                if (instance == null)
				{
#if UNITY_EDITOR
                    if (Application.isPlaying == false)
				    {
                        instance = EditorGetInstance();
                        return instance;
                    }
#endif
                    instance = LocManagerHelpers.FindComponentInHierarchyEvenIfInactive<LocManager>();
                    if (instance == null)
					{
                        throw new Exception("LocManager has not been added to the Scene. Please add it somewhere.");
					}
				}

                return instance;
			}
		}


        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Unity Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
#if UNITY_EDITOR
        protected void Update()
        {
            if (switchingLanguages == false && Input.GetKeyDown(Configuration.SwitchLanguageDebugKeyCode))
            {
                int numLanguages = System.Enum.GetValues(typeof(LocLanguages)).Length;
                LocLanguages nextLanguage = (LocLanguages)((int)(CurrentLanguage + 1) % numLanguages);
#if LOCALISATION_ADDRESSABLES && !USING_UNITY_LOCALISATION
                _ =
#endif
                SetLanguage(nextLanguage);
                LocalisationOverrides.Debug.LogInfo($"Changed language to: {nextLanguage}");
            }
        }
#endif

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //         Public Static Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public static string SanitiseLocId(string _locId)
        {
            string sanitisedLocId = Regex.Replace(_locId, IllegalCharactersRegex, "_");
            while (sanitisedLocId.Length > 0 && Regex.IsMatch($"{sanitisedLocId[0]}", "\\d"))
            {
                // Enums also cannot start with numbers. So keep removing number from first character until no numbers are the first character.
                sanitisedLocId = sanitisedLocId.Substring(1);
            }

            return sanitisedLocId;
        }
        
        /// <summary> Called via an external loader which should pass through the CSV Files that the LocManager can Parse. </summary>
#if LOCALISATION_ADDRESSABLES && !USING_UNITY_LOCALISATION
        public static async Task RuntimeInitialise(List<TextAsset> _locStringsCSVFiles)
#else
        public static void RuntimeInitialise(List<TextAsset> _locStringsCSVFiles)
#endif
        {
            int locCSVsCount = _locStringsCSVFiles.Count;
            allLanguageStrings = new LanguageStrings[locCSVsCount][];

            for (int locTableIndex = 0; locTableIndex < locCSVsCount; ++locTableIndex)
            {
                locIDsToRowIDsInterpreter.ParseLocStringsFile(locTableIndex, _locStringsCSVFiles[locTableIndex]);
                allLanguageStrings[locTableIndex] = LoadLanguageStringsFromLocTable(_locStringsCSVFiles[locTableIndex]);
            }
            
#if LOCALISATION_ADDRESSABLES && !USING_UNITY_LOCALISATION
            await
#endif
            SetLanguage(CurrentLanguage);
        }

        /// <summary> Imports the Loc Strings Table file and reads through the contents, filling in strings for each language. </summary>
        /// <param name="_textAsset"> TextAsset to parse LocStrings from. </param>
        public static LanguageStrings[] LoadLanguageStringsFromLocTable(TextAsset _textAsset)
        {
            string locTableContents = _textAsset.text.Replace("\r\n", "\n");

            string[] locTableRows = locTableContents.Split('\n');
            List<LanguageStrings> langStrings = new List<LanguageStrings>();
            int numLanguages = System.Enum.GetValues(typeof(LocLanguages)).Length;

            bool isMissingLanguage = false;
            for (int rowNo = 0; rowNo < locTableRows.Length; ++rowNo)
            {
                string rowContents = locTableRows[rowNo];
                if (string.Equals(rowContents, " ") || string.IsNullOrEmpty(rowContents.Trim()) == false)
                {
                    // Ignore first column of Loc Table, it contains the Loc ID, and we don't need it in langStrings
                    string[] stringsRead = (rowContents.Substring(rowContents.IndexOf('\t') + 1)).Split('\t');

                    if (isMissingLanguage == false && stringsRead.Length < numLanguages)
                    {
                        if (rowNo == 0)
                        {
                            isMissingLanguage = true;
                        }
                        else
                        {
                            Debug.LogError($"Error parsing '{_textAsset.name}{Configuration.MasterLocStringsTableFileExt}' line {rowNo}: Incorrect number of columns (has {stringsRead.Length}, expected {numLanguages})\n{rowContents}");
                        }
                    }

                    // Replace all literal \n's with actual newlines
                    for (int strNo = 0; strNo < stringsRead.Length; ++strNo)
                    {
                        stringsRead[strNo] = stringsRead[strNo].Replace("\\n", "\n");
                    }

                    langStrings.Add(new LanguageStrings(stringsRead));
                }
            }

            if (langStrings.Count > 0)
            {
                // Find out which Cell Column this language belongs to.
                string[] languagesHeader = langStrings[0].strings;
                int numColumns = languagesHeader.Length;
                locLanguageCellIndexIds = new int[numLanguages];
                for (int langId = 0; langId < numLanguages; ++langId)
                {
                    LocLanguages lookingForLanguage = (LocLanguages)langId;
                    if (lookingForLanguage == LocLanguages.English)
                    {
                        // English is always first column
                        locLanguageCellIndexIds[langId] = 0;
                        continue;
                    }

                    // Assigned to English Cell Column by default in case we can't find the language.
                    locLanguageCellIndexIds[langId] = -1;

                    for (int cellColId = 0; cellColId < numColumns; ++cellColId)
                    {
                        if (string.Equals(languagesHeader[cellColId], lookingForLanguage.ToString(), StringComparison.OrdinalIgnoreCase))
                        {
                            // Found our language.
                            locLanguageCellIndexIds[langId] = cellColId;
                            break;
                        }
                    }

                    if (locLanguageCellIndexIds[langId] == -1)
                    {
                        Debug.LogError($"Could not find \"{lookingForLanguage}\" in the Localisation Strings file. Did you rename this language? Please ensure both it and the entry in {nameof(LocLanguages)} match up. Defaulting to English text for this language.");
                        locLanguageCellIndexIds[langId] = 0;
                    }
                }
            }

            return langStrings.ToArray();
        }

        /// <summary> Sets/changes the current language </summary>
        /// <param name="_language"> Language to show </param>
#if LOCALISATION_ADDRESSABLES && !USING_UNITY_LOCALISATION
        public static async Task SetLanguage(LocLanguages _language)
#else
        public static void SetLanguage(LocLanguages _language)
#endif
        {
            if (Instance.locManagerSettings.EnglishOnlyMode == EnglishOnlyMode.EnglishOnly)
			{
                _language = LocLanguages.English;
			}
#if !UNITY_EDITOR && !FINAL_WITH_DEBUG
            else if (Instance.locManagerSettings.EnglishOnlyMode == EnglishOnlyMode.EnglishOnlyDuringLiveRelease)
            {
                _language = LocLanguages.English;
            }
#endif

            switchingLanguages = true;
#if USING_UNITY_LOCALISATION
    #if UNITY_EDITOR
            // Can only switch locales during play mode. Outside of Play Mode we will be in Edit Mode,
            // so if a UI artist changes languages in the debug options, and we set the locale the change will not be tracked correctly.
            if (Application.isPlaying)
    #endif
            {
                SystemLanguage systemLanguage = LocLanguagesInfo.LocLangToSystemLang[_language];
                LocaleIdentifier localeIdentifier = new(systemLanguage);
                Locale locale = LocalizationSettings.Instance.GetAvailableLocales().GetLocale(localeIdentifier);
                if (locale == null)
                {
                    if (_language == LocLanguages.English)
                        throw new Exception($"English Locale [{localeIdentifier.Code}] is not setup as a Unity Localisation Table. Please add it.");

                    Debug.LogError($"Locale [{localeIdentifier.Code}] is not setup as a Unity Localisation Table. Please add it. Defaulting to English");
                    SetLanguage(LocLanguages.English);
                    return;
                }

                LocalizationSettings.SelectedLocale = locale;
            }
#endif
            
#if !USING_UNITY_LOCALISATION
    #if LOCALISATION_ADDRESSABLES
            await 
    #endif
            Instance.ResolveNewLanguageAssetsResources(CurrentLanguage, _language);
#endif
            CurrentLanguage = _language;
            switchingLanguages = false;
            RefreshAllLocalizedText();
        }

        /// <summary> Finds & refreshes all localised text labels in the UI hierarchy </summary>
        public static void RefreshAllLocalizedText()
        {
            List<UILocLabelBase> allLocScripts = LocManagerHelpers.FindComponentsInHierarchyEvenIfInactive<UILocLabelBase>();
            foreach (UILocLabelBase t in allLocScripts)
                t.Refresh();
        }

        public static bool IsLanguageRightToLeft()
        {
            return IsLanguageRightToLeft(CurrentLanguage);
        }

        public static bool IsLanguageRightToLeft(LocLanguages _language)
        {
            return LocLanguagesInfo.RightToLeftLanguages.Contains(_language);
        }

        /// <summary> Gets the specified text string </summary>
        /// <param name="_locHashId"> Hash ID for the Loc Value </param>
        /// <param name="_output"> Output string </param>
        public static LocStatus TryGetTextViaLocHashID(int _locHashId, out string _output)
        {
            return TryGetTextViaLocHashID(_locHashId, CurrentLanguage, out _output);
        }

        /// <summary> Returns the physical string for the given loc ID with the substrings/colour tags applied. </summary>
        /// <param name="_locStruct"> The Loc Data to use. </param>
        /// <param name="_textOutput"> The text output with the Substrings and Colours applied. </param>
        public static LocStatus TryGetTextViaLocHashID(LocStruct _locStruct, out string _textOutput)
        {
            return TryGetTextViaLocHashID(_locStruct, CurrentLanguage, out _textOutput);
        }

        /// <summary> Resolves the Substrings for this text and appends/Inserts the substring values where needed. </summary>
        /// <param name="_text"> The text you wish to insert substrings into. </param>
        /// <param name="_locSubstrings"> The substrings you wish to append </param>
        public static string AppendSubstringToString(string _text, params LocSubstring[] _locSubstrings)
		{
            if (_locSubstrings != null)
            {
                int substituteStringsCount = _locSubstrings.Length;
                for (int i = 0; i < substituteStringsCount; ++i)
                {
                    if (_locSubstrings[i] != null)
                    {
                        string substring = _locSubstrings[i].SubString;
                        if (string.IsNullOrEmpty(substring) == false)
                        {
                            _text = _text.Replace("[" + i + "]", substring);
                        }
                    }
                }
            }
            return _text;
        }

        /// <summary> Returns the physical string for the given loc ID with the substrings/colour tags applied. </summary>
        /// <param name="_locStruct"> The Loc Data to use. </param>
        /// <param name="_language"> The Language to get. </param>
        /// <param name="_textOutput"> The text output with the Substrings and Colours applied. </param>
        public static LocStatus TryGetTextViaLocHashID(LocStruct _locStruct, LocLanguages _language, out string _textOutput)
        {
            LocStatus locStatus = PrivateTryGetTextViaLocHashID((int)_locStruct.locId, _language, out _textOutput);
            if (locStatus != LocStatus.Success)
            {
                return locStatus;
            }

            // Apply Substrings
            _textOutput = AppendSubstringToString(_textOutput, _locStruct.locSubstrings);

            // Apply highlighted text colours
            if (_locStruct.highlightedTextColours != null)
            {
                int coloursCount = _locStruct.highlightedTextColours.Length;
                for (int i = 0; i < coloursCount; ++i)
                {
                    int r = Mathf.CeilToInt(_locStruct.highlightedTextColours[i].r * 255.0f);
                    int g = Mathf.CeilToInt(_locStruct.highlightedTextColours[i].g * 255.0f);
                    int b = Mathf.CeilToInt(_locStruct.highlightedTextColours[i].b * 255.0f);
                    int a = Mathf.CeilToInt(_locStruct.highlightedTextColours[i].a * 255.0f);
                    string hexVal = $"{r:X2}{g:X2}{b:X2}{a:X2}";
                    _textOutput = _textOutput.Replace("{" + i + "}", $"<color=#{hexVal}>");
                }

                _textOutput = _textOutput.Replace("{/}", "</color>");
            }

            return LocStatus.Success;
        }

        /// <summary> Gets the specified text string </summary>
        /// <param name="_locHashId"> Hash ID for the Loc Value </param>
        /// <param name="_language"> Language string to grab </param>
        public static string GetTextViaLocHashID(int _locHashId, LocLanguages _language)
        {
            string text;
            TryGetTextViaLocHashID(_locHashId, _language, out text);
            return text;
        }

        /// <summary> Gets the specified text string </summary>
        /// <param name="_locHashId"> Hash ID for the Loc Value </param>
        public static string GetTextViaLocHashID(int _locHashId)
        {
            string text;
            TryGetTextViaLocHashID(_locHashId, CurrentLanguage, out text);
            return text;
        }

        /// <summary> Returns the physical string for the given loc ID with the substrings/colour tags applied </summary>
        /// <param name="_locStruct"> The Loc Data to use. </param>
        public static string GetTextViaLocHashID(LocStruct _locStruct)
        {
            string textOutput;
            TryGetTextViaLocHashID(_locStruct, CurrentLanguage, out textOutput);
            return textOutput;
        }

        /// <summary> Returns the physical string for the given loc ID with the substrings/colour tags applied </summary>
        /// <param name="_locStruct"> The Loc Data to use. </param>
        /// <param name="_language"> The Language to get. </param>
        public static string GetTextViaLocHashID(LocStruct _locStruct, LocLanguages _language)
        {
            string textOutput;
            TryGetTextViaLocHashID(_locStruct, _language, out textOutput);
            return textOutput;
        }

        /// <summary> Returns the Loc ID matching the provided string.</summary>
        /// <param name="_locIdAsString"> The Loc ID as a string. </param>
        public static LocID GetLocIdWithIdAsString(string _locIdAsString)
        {
            if (string.IsNullOrEmpty(_locIdAsString))
            {
                _locIdAsString = string.Empty;
                Debug.LogError("Cannot get a LocId with a null or empty string.");
            }

            _locIdAsString = SanitiseLocId(_locIdAsString);
            return _locIdAsString.GetHashCode();
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //         Private Static Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Called either when an Editor Object requests a LocString during Editor Mode. Or during normal Initialisation (Awake) during gameplay if not going through the Gameplay version (!!! EDITOR ONLY !!!). </summary>
        private static void StaticInitialise()
        {
            allLanguageStrings = new LanguageStrings[LocTablesCount][];

            // Load all Language strings from CSV Files
            for (int i = 0; i < LocTablesCount; ++i)
            {
#if ALLOW_LOCAL_FILES
                string pathToLocStrings = LocIDs.LocDocsInfo.LocDocsPaths[i];
                TextAsset textAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(pathToLocStrings);
#else
                string pathToLocStrings = LocIDs.LocDocsInfo.LocDocs[i];
                TextAsset textAsset = Resources.Load<TextAsset>( pathToLocStrings );
#endif
                if (textAsset == null)
                {
                    throw new UnityException("Could not open Loc Table file \"" + pathToLocStrings + "\". If it is present, check its icon in Unity- if it doesn't show as a text doc, then try right click->reimport.");
                }
                allLanguageStrings[i] = LoadLanguageStringsFromLocTable(textAsset);
            }
            
#if LOCALISATION_ADDRESSABLES && !USING_UNITY_LOCALISATION
            _ =
#endif
            SetLanguage(CurrentLanguage);
        }

        /// <summary> Checks if Row Index is valid for the given Loc Table</summary>
        /// <param name="_locTableIndex"> Loc Table to check </param>
        /// <param name="_locTableLineNo"> Row Index to Check </param>
        private static bool IsValidLocTableLineNo(int _locTableIndex, int _locTableLineNo)
        {
            if (AllLanguageStrings == null)
                return false;

            if (_locTableLineNo < 1)
                return false;

            if (_locTableIndex >= AllLanguageStrings.Length)
                return false;

            if (AllLanguageStrings[_locTableIndex] == null)
                return false;

            if (_locTableLineNo >= AllLanguageStrings[_locTableIndex].Length)
                return false;

            return true;
        }

        /// <summary> Gets the specified text string. Use the LocManager Instance if you do not </summary>
        /// <param name="_locId"> Loc ID for the Loc Value </param>
        /// <param name="_language"> Language string to grab </param>
        /// <param name="_output"> Output string </param>
        private static LocStatus PrivateTryGetTextViaLocHashID(LocID _locId, LocLanguages _language, out string _output)
        {
            int locTableIndex;
            int locTableLineNo;

#if UNITY_EDITOR
            // Go through the Runtime generate LocIDs->RowIDs values first since these should be the most up-to-date assuming we have been pulling Localisation Files from a server.
            if (Application.isPlaying)
            {
#endif
                if (locIDsToRowIDsInterpreter.TryGetRowID(_locId.locId, out locTableIndex, out locTableLineNo))
                {
                    return TryGetTextViaLocTableLine(locTableIndex, locTableLineNo, _language, out _output);
                }

#if UNITY_EDITOR
            }
#endif

            // Otherwise fallback to our static generated LocIDs->RowIDs which is created during Localisation Generation when pulling down via Google Docs.
            // ~~ First checking preference CSV then going through other CSV files if we can't find this LOC ID ~~
            if (LocIdsHelper.CSVNameToRowsDictionaryIndex.TryGetValue(LocManager.CSVPreference, out locTableIndex))
            {
                if (LocIdsHelper.HashToRowsDictionaryReferences[locTableIndex].TryGetValue(_locId.locId, out locTableLineNo))
                {
                    return TryGetTextViaLocTableLine(locTableIndex, locTableLineNo, _language, out _output);
                }
            }
            
            for (locTableIndex = 0; locTableIndex < LocTablesCount; ++locTableIndex)
            {
                if (LocIdsHelper.HashToRowsDictionaryReferences[locTableIndex].TryGetValue(_locId.locId, out locTableLineNo))
                {
                    return TryGetTextViaLocTableLine(locTableIndex, locTableLineNo, _language, out _output);
                }
            }

            // Value not Found
            _output = $"Bad Loc Hash ID. This Loc Hash ID doesn't seem to exist in the '{string.Join(", ", LocIDs.LocDocsInfo.LocDocs)}' "
                    + $"files anymore. Please double check. You may be able to fix this issue by reimporting the Default Localisation Table File from Google.";
            return LocStatus.BadLocHashId;
        }

        /// <summary> Gets the specified text string. </summary>
        /// <param name="_locTableIndex"> Loc Table to grab from </param>
        /// <param name="_locTableLineNo"> Loc Table Line (Row ID) to grab </param>
        /// <param name="_language"> Language string to grab </param>
        /// <param name="_output"> Output string </param>
        private static LocStatus TryGetTextViaLocTableLine(int _locTableIndex, int _locTableLineNo, LocLanguages _language, out string _output)
        {
            if (_locTableLineNo == 0)
            {
                // Empty Loc String
                _output = string.Empty;
                return LocStatus.Success;
            }

            if (IsValidLocTableLineNo(_locTableIndex, _locTableLineNo) == false)
            {
                string pathToCSV = LocIDs.LocDocsInfo.LocDocs[_locTableIndex] + LocalisationOverrides.Configuration.MasterLocStringsTableFileExt;
                _output = $"Bad Row ID. Searched for Row {_locTableLineNo}, but could not find it in the {pathToCSV} file. Please double check you haven't removed it. You can likely fix this issue by reimporting the Localisation Table File from Google.";
                return LocStatus.BadLocTableLine;
            }

            _output = AllLanguageStrings[_locTableIndex][_locTableLineNo].Get(_language);

            if (string.IsNullOrEmpty(_output))
            {
                _output = $"Row {_locTableLineNo} does not contain any text for Language [{_language}] in the {LocStringsTableFullFileName} file. Please insert some data. You might be able to fix this issue by reimporting the Localisation Table File from Google.";
                return LocStatus.NoTextFoundForLanguage;
            }

            return LocStatus.Success;
        }

        /// <summary> Gets the System Language. Defaults to English if System Language doesn't have any Localisation Data assigned to 
        /// it (e.g. No Localisation found for Scottish_Gaelic; so defaults to English instead).</summary>
        private static LocLanguages GetInstalledLanguage()
        {
            LocLanguages installedLanguage;
            if (LocLanguagesInfo.SystemLangToLocLang.TryGetValue(Application.systemLanguage, out installedLanguage))
            {
                return installedLanguage;
            }

            // System Language Conversion is not defined. Default to English.
            return LocLanguages.English;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Public Instance Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Returns the Text Mesh Pro Localisation Info for the specified Localisation Text Display Style. </summary>
        public LocDisplayStyleFontInfo.StandardLocFontInfo GetTextLocDisplayStyleInfo(LocalisationTextDisplayStyleTypes _textDisplayStyle)
        {
            return GetTextLocDisplayStyleInfo(CurrentLanguage, _textDisplayStyle);
        }

        /// <summary> Returns the Text Mesh Pro Localisation Info for the specified Localisation Text Display Style. </summary>
        public LocDisplayStyleFontInfo.StandardLocFontInfo GetTextLocDisplayStyleInfo(LocLanguages _language, LocalisationTextDisplayStyleTypes _textDisplayStyle, bool _retrieveNullIfNoAsset = true)
        {
            LocDisplayStyleTypeToLocDisplayStyleInfoDict locDisplayStylesDict;
            if (locLanguageToLocStyleTypesDict.TryGetValue(_language.ToString(), out locDisplayStylesDict) == false)
            {
                return null;
            }

            LocDisplayStyleFontInfo locDisplayStyleFontInfo;
            if (locDisplayStylesDict.TryGetValue(_textDisplayStyle.ToString(), out locDisplayStyleFontInfo) == false)
            {
                return null;
            }

#if UNITY_EDITOR
            if (Application.isPlaying)
            {
                if (locDisplayStyleFontInfo.standardFontInfo.font.editorAssetReference.Asset == null
                    && string.IsNullOrEmpty(locDisplayStyleFontInfo.standardFontInfo.font.ResourceKey))
                {
                    return null;
                }
            }
            else if (_retrieveNullIfNoAsset && locDisplayStyleFontInfo.standardFontInfo.font.editorAssetReference.Asset == null)
            {
                return null;
            }
#else
            if (string.IsNullOrEmpty(locDisplayStyleFontInfo.standardFontInfo.font.ResourceKey))
            {
                return null;
            }
#endif


            return locDisplayStyleFontInfo.standardFontInfo;
        }

        /// <summary> Returns the Text Mesh Pro Localisation Info for the specified Localisation Text Display Style. </summary>
        public LocDisplayStyleFontInfo.StandardLocFontInfo GetTextLocDisplayStyleInfoOrDefault(LocalisationTextDisplayStyleTypes _textDisplayStyle)
        {
            return GetTextLocDisplayStyleInfoOrDefault(CurrentLanguage, _textDisplayStyle);
        }

        /// <summary> Returns the Text Mesh Pro Localisation Info for the specified Localisation Text Display Style. </summary>
        public LocDisplayStyleFontInfo.StandardLocFontInfo GetTextLocDisplayStyleInfoOrDefault(LocLanguages _language, LocalisationTextDisplayStyleTypes _textDisplayStyle)
        {
            LocDisplayStyleFontInfo.StandardLocFontInfo result = GetTextLocDisplayStyleInfo(_language, _textDisplayStyle);
            if (result == null)
            {
                result = GetTextLocDisplayStyleInfo(_language, 0);
                result ??= GetTextLocDisplayStyleInfo(LocLanguages.English, 0);
            }
            return result;
        }

        /// <summary> Returns the Text Mesh Pro Localisation Info for the specified Localisation Text Display Style. </summary>
        public LocDisplayStyleFontInfo.TextMeshProLocFontInfo GetTextMeshLocDisplayStyleInfo(LocalisationTextDisplayStyleTypes _textDisplayStyle)
        {
            return GetTextMeshLocDisplayStyleInfo(CurrentLanguage, _textDisplayStyle);
        }

        /// <summary> Returns the Text Mesh Pro Localisation Info for the specified Localisation Text Display Style. </summary>
        public LocDisplayStyleFontInfo.TextMeshProLocFontInfo GetTextMeshLocDisplayStyleInfo(LocLanguages _language, LocalisationTextDisplayStyleTypes _textDisplayStyle, bool _retrieveNullIfNoAsset = true)
        {
            LocDisplayStyleTypeToLocDisplayStyleInfoDict locDisplayStylesDict;
            if (locLanguageToLocStyleTypesDict.TryGetValue(_language.ToString(), out locDisplayStylesDict) == false)
            {
                return null;
            }

            LocDisplayStyleFontInfo locDisplayStyleFontInfo;
            if (locDisplayStylesDict.TryGetValue(_textDisplayStyle.ToString(), out locDisplayStyleFontInfo) == false)
			{
                return null;
			}

#if UNITY_EDITOR
            if (Application.isPlaying)
			{
                if (locDisplayStyleFontInfo.textMeshProFontInfo.font.editorAssetReference.Asset == null
                    && string.IsNullOrEmpty(locDisplayStyleFontInfo.textMeshProFontInfo.font.ResourceKey))
				{
                    return null;
				}
			}
            else if (_retrieveNullIfNoAsset && locDisplayStyleFontInfo.textMeshProFontInfo.font.editorAssetReference.Asset == null)
            {
                return null;
            }
#else
            if (string.IsNullOrEmpty(locDisplayStyleFontInfo.textMeshProFontInfo.font.ResourceKey))
            {
                return null;
            }
#endif

            return locDisplayStyleFontInfo.textMeshProFontInfo;
        }

        /// <summary> Returns the Text Mesh Pro Localisation Info for the specified Localisation Text Display Style. </summary>
        public LocDisplayStyleFontInfo.TextMeshProLocFontInfo GetTextMeshLocDisplayStyleInfoOrDefault(LocalisationTextDisplayStyleTypes _textDisplayStyle)
        {
            return GetTextMeshLocDisplayStyleInfoOrDefault(CurrentLanguage, _textDisplayStyle);
        }

        /// <summary> Returns the Text Mesh Pro Localisation Info for the specified Localisation Text Display Style. </summary>
        public LocDisplayStyleFontInfo.TextMeshProLocFontInfo GetTextMeshLocDisplayStyleInfoOrDefault(LocLanguages _language, LocalisationTextDisplayStyleTypes _textDisplayStyle)
        {
            LocDisplayStyleFontInfo.TextMeshProLocFontInfo result = GetTextMeshLocDisplayStyleInfo(_language, _textDisplayStyle);
            if (result == null)
            {
                result = GetTextMeshLocDisplayStyleInfo(_language, 0);
                result ??= GetTextMeshLocDisplayStyleInfo(LocLanguages.English, 0);
            }
            return result;
        }

        /// <summary>
        /// Outputs the Font Asset References for the following languages. These will return a struct per Asset Type that has either 
        /// a valid addressables key or resources key depending on the way the Loc System is operating.
        /// </summary>
        /// <param name="_language"> Language to query </param>
        /// <param name="_standardFontAssetToReferences"> Outputs the Standard Font Assets in use. </param>
        /// <param name="_tmpFontAssetToReferences"> Outputs the Tmp Font Assets in use. </param>
        /// <param name="_materialAssetToReferences"> Outputs the Materials in use. </param>
        public void FetchLocAssetsForLanguage(LocLanguages _language,
                                                out Dictionary<string, List<LocDisplayStyleFontInfo.FontResourceRef>> _standardFontAssetToReferences,
                                                out Dictionary<string, List<LocDisplayStyleFontInfo.TMPFontResourceRef>> _tmpFontAssetToReferences,
                                                out Dictionary<string, List<LocDisplayStyleFontInfo.MaterialResourceRef>> _materialAssetToReferences)
		{
            FetchLocAssetsForLanguages(new LocLanguages[] { _language }, out _standardFontAssetToReferences, out _tmpFontAssetToReferences, out _materialAssetToReferences);
        }

        /// <summary>
        /// Outputs the Font Asset References for the following languages. These will return a struct per Asset Type that has either 
        /// a valid addressables key or resources key depending on the way the Loc System is operating.
        /// </summary>
        /// <param name="_languages"> Languages to query </param>
        /// <param name="_standardFontAssetToReferences"> Outputs the Standard Font Assets in use. </param>
        /// <param name="_tmpFontAssetToReferences"> Outputs the Tmp Font Assets in use. </param>
        /// <param name="_materialAssetToReferences"> Outputs the Materials in use. </param>
        public void FetchLocAssetsForLanguages(LocLanguages[] _languages,
                                                out Dictionary<string, List<LocDisplayStyleFontInfo.FontResourceRef>> _standardFontAssetToReferences,
                                                out Dictionary<string, List<LocDisplayStyleFontInfo.TMPFontResourceRef>> _tmpFontAssetToReferences,
                                                out Dictionary<string, List<LocDisplayStyleFontInfo.MaterialResourceRef>> _materialAssetToReferences)
        {
            const int MaterialsCount = 3;
            LocDisplayStyleFontInfo.MaterialResourceRef[] materialsRefs = new LocDisplayStyleFontInfo.MaterialResourceRef[MaterialsCount];

            _standardFontAssetToReferences = new Dictionary<string, List<LocDisplayStyleFontInfo.FontResourceRef>>();
            _tmpFontAssetToReferences = new Dictionary<string, List<LocDisplayStyleFontInfo.TMPFontResourceRef>>();
            _materialAssetToReferences = new Dictionary<string, List<LocDisplayStyleFontInfo.MaterialResourceRef>>();

            foreach (LocLanguages language in _languages)
            {
                foreach (LocalisationTextDisplayStyleTypes textStyle in AllTextDisplayStyles)
                {
					LocDisplayStyleFontInfo.StandardLocFontInfo standardTextFontInfo = GetTextLocDisplayStyleInfo(language, textStyle);
					LocDisplayStyleFontInfo.TextMeshProLocFontInfo textMeshFontInfo = GetTextMeshLocDisplayStyleInfo(language, textStyle);

                    if (standardTextFontInfo != null)
                    {
                        string standardFontAssetKey = standardTextFontInfo.font.ResourceKey;
                        if (string.IsNullOrEmpty(standardFontAssetKey) == false)
                        {
                            List<LocDisplayStyleFontInfo.FontResourceRef> fontReferences;
                            if (_standardFontAssetToReferences.TryGetValue(standardFontAssetKey, out fontReferences) == false)
                            {
                                fontReferences = new List<LocDisplayStyleFontInfo.FontResourceRef>();
                                _standardFontAssetToReferences.Add(standardFontAssetKey, fontReferences);
                            }

                            fontReferences.Add(standardTextFontInfo.font);
                        }
                    }

                    if (textMeshFontInfo != null)
                    {
                        string tmpFontAssetKey = textMeshFontInfo.font.ResourceKey;
                        if (string.IsNullOrEmpty(tmpFontAssetKey) == false)
                        {
                            List<LocDisplayStyleFontInfo.TMPFontResourceRef> textMeshFontReferences;
                            if (_tmpFontAssetToReferences.TryGetValue(tmpFontAssetKey, out textMeshFontReferences) == false)
                            {
                                textMeshFontReferences = new List<LocDisplayStyleFontInfo.TMPFontResourceRef>();
                                _tmpFontAssetToReferences.Add(tmpFontAssetKey, textMeshFontReferences);
                            }

                            textMeshFontReferences.Add(textMeshFontInfo.font);
                        }


                        materialsRefs[0] = textMeshFontInfo.normalMaterial;
                        materialsRefs[1] = textMeshFontInfo.strokedMaterial;
                        materialsRefs[2] = textMeshFontInfo.underlinedMaterial;
                        for (int materIndex = 0; materIndex < MaterialsCount; ++materIndex)
                        {
                            string materialAssetKey = materialsRefs[materIndex].ResourceKey;
                            if (string.IsNullOrEmpty(materialAssetKey) == false)
                            {
                                List<LocDisplayStyleFontInfo.MaterialResourceRef> materialReferences;
                                if (_materialAssetToReferences.TryGetValue(materialAssetKey, out materialReferences) == false)
                                {
                                    materialReferences = new List<LocDisplayStyleFontInfo.MaterialResourceRef>();
                                    _materialAssetToReferences.Add(materialAssetKey, materialReferences);
                                }

                                materialReferences.Add(materialsRefs[materIndex]);
                            }
                        }
                    }
                }
            }
        }


        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //        Private Instance Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Will delete any unused assets from the prior language and spawn/fetch required assets for the new language </summary>
        /// <param name="_oldLanguage"> Old Language (Despawn assets) </param>
        /// <param name="_newLanguage"> New language (Spawn Assets) </param>
#if LOCALISATION_ADDRESSABLES
        private async Task ResolveNewLanguageAssetsResources(LocLanguages _oldLanguage, LocLanguages _newLanguage)
#else
        private void ResolveNewLanguageAssetsResources(LocLanguages _oldLanguage, LocLanguages _newLanguage)
#endif
        {
            Dictionary<string, List<LocDisplayStyleFontInfo.FontResourceRef>> spawnStandardFontReferences;
            Dictionary<string, List<LocDisplayStyleFontInfo.TMPFontResourceRef>> spawnTmpFontReferences;
            Dictionary<string, List<LocDisplayStyleFontInfo.MaterialResourceRef>> spawnMaterialReferences;

            if (_oldLanguage == LocLanguages.English && _newLanguage == LocLanguages.English)
			{
                // This happens on LocManager Init. Load up those initial default English resources
                FetchLocAssetsForLanguage(LocLanguages.English, out spawnStandardFontReferences, out spawnTmpFontReferences, out spawnMaterialReferences);
			}
            else
			{
                Dictionary<string, List<LocDisplayStyleFontInfo.FontResourceRef>> despawnStandardFontReferences;
                Dictionary<string, List<LocDisplayStyleFontInfo.TMPFontResourceRef>> despawnTmpFontReferences;
                Dictionary<string, List<LocDisplayStyleFontInfo.MaterialResourceRef>> despawnMaterialReferences;

                FetchLocAssetsForLanguage(_oldLanguage, out despawnStandardFontReferences, out despawnTmpFontReferences, out despawnMaterialReferences);
                FetchLocAssetsForLanguage(_newLanguage, out spawnStandardFontReferences, out spawnTmpFontReferences, out spawnMaterialReferences);

                // Any assets being reused should not be despawned
                foreach (var pair in spawnStandardFontReferences)
                    despawnStandardFontReferences.Remove(pair.Key);

                foreach (var pair in spawnTmpFontReferences)
                    despawnTmpFontReferences.Remove(pair.Key);

                foreach (var pair in spawnMaterialReferences)
                    despawnMaterialReferences.Remove(pair.Key);

                // De-load old Language assets and spawn in the new ones
                // These are all referencing the same asset. So only the first one has to handle the unloading of the asset.
                foreach (var pair in despawnStandardFontReferences)
				    pair.Value[0].FreeAsset();

                foreach (var pair in despawnTmpFontReferences)
                    pair.Value[0].FreeAsset();

                foreach (var pair in despawnMaterialReferences)
                    pair.Value[0].FreeAsset();
            }


            // Spawn Assets for the new language
            // These are all referencing the same asset. So only the first one has to handle the loading of the asset.
            foreach (var pair in spawnStandardFontReferences)
			{
#if LOCALISATION_ADDRESSABLES
                await pair.Value[0].LoadAsset();
#else
                pair.Value[0].LoadAsset();
#endif
            }

            foreach (var pair in spawnTmpFontReferences)
            {
#if LOCALISATION_ADDRESSABLES
                await pair.Value[0].LoadAsset();
#else
                pair.Value[0].LoadAsset();
#endif
            }

            foreach (var pair in spawnMaterialReferences)
            {
#if LOCALISATION_ADDRESSABLES
                await pair.Value[0].LoadAsset();
#else
                pair.Value[0].LoadAsset();
#endif
            }
        }
    }





    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //       LocManager - Editor Class
    //    This code will exist in editor only.
    //-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
#if UNITY_EDITOR
    public partial class LocManager
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Editor Only Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public static LocManager EditorGetInstance()
        {
            string[] assetGUIDs = UnityEditor.AssetDatabase.FindAssets($"t:{typeof(LocManager).FullName}");
            if (assetGUIDs.Length > 0)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGUIDs[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<LocManager>(assetPath);
            }

            assetGUIDs = UnityEditor.AssetDatabase.FindAssets("PF_LocManager (do not rename this)");
            if (assetGUIDs.Length > 0)
            {
                string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(assetGUIDs[0]);
                return UnityEditor.AssetDatabase.LoadAssetAtPath<LocManager>(assetPath);
            }

            string locManagerPath = LocalisationOverrides.Configuration.PathToGameSpecificLocPrefabs + "PF_LocManager (do not rename this)";
            GameObject asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(locManagerPath);
            return asset.GetComponent<LocManager>();
        }

        /// <summary> Returns whether the font will be used in the Loc System. </summary>
        public static bool CheckIfFontIsUsed(TMPro.TMP_FontAsset _fontAsset)
		{
			LocLanguageToLocStyleTypesDict locStyles = EditorGetInstance().locLanguageToLocStyleTypesDict;
            foreach (var locLangPair in locStyles)
			{
                foreach (var pair in locLangPair.Value)
				{
                    if (pair.Value.textMeshProFontInfo.font.editorAssetReference.Asset == _fontAsset)
                        return true;
				}
			}

            return false;
		}

        /// <summary> Returns whether the font will be used in the Loc System. </summary>
        public static bool CheckIfFontIsUsed(Font _fontAsset)
        {
            LocLanguageToLocStyleTypesDict locStyles = EditorGetInstance().locLanguageToLocStyleTypesDict;
            foreach (var locLangPair in locStyles)
            {
                foreach (var pair in locLangPair.Value)
                {
                    if (pair.Value.standardFontInfo.font.editorAssetReference.Asset == _fontAsset)
                        return true;
                }
            }

            return false;
        }

        public static string EditorGetEnglishTextValue(int _locHashId)
		{
            for (int locTableIndex = 0; locTableIndex < LocTablesCount; ++locTableIndex)
            {
                int locTableLineNo;
                if (LocIdsHelper.HashToRowsDictionaryReferences[locTableIndex].TryGetValue(_locHashId, out locTableLineNo))
                {
                    string output;
                    TryGetTextViaLocTableLine(locTableIndex, locTableLineNo, LocLanguages.English, out output);
                    return output;
                }
            }
            return string.Empty;
        }

        /// <summary> Rereads from the Local CSV Files and refreshes the Cached LocStrings. </summary>
        public static void EditorRefreshCachedLocStrings()
        {
            StaticInitialise();
        }

        /// <summary> Attempts to convert a Hash Value into the original String ID that was converted. </summary>
        /// <param name="_locId"> The Loc ID you wish to find the original name for.</param>
        public static bool EditorTryGetLocHashIdAsStringId(LocID _locId, out string _originalStringId)
        {
            int dictionariesCount = LocIDsEditorData.EditorHashToStringIdsDictionaryReferences.Length;
            for (int i = 0; i < dictionariesCount; ++i)
            {
                if (LocIDsEditorData.EditorHashToStringIdsDictionaryReferences[i].TryGetValue(_locId, out _originalStringId))
                    return true;
            }

            _originalStringId = string.Empty;
            return false;
        }

        /// <summary> Attempts to convert a String ID into an existing. It will output a valid Hash ID regardless of whether an existing 
        /// Hash ID in the Localisation docs can be found. However, the function will return false if the Hash ID that it returns is not present in any Loc Doc. </summary>
        public static bool EditorTryConvertStringIdToHashId(string _locStringId, out LocID _outputResult)
        {
            int dictionariesCount = LocIDsEditorData.EditorHashToStringIdsDictionaryReferences.Length;
            for (int i = 0; i < dictionariesCount; ++i)
            {
                int result;
                if (LocIDsEditorData.EditorStringIdsToHashIdsDictionaryReferences[i].TryGetValue(_locStringId, out result))
                {
                    _outputResult = result;
                    return true;
                }
            }

            _outputResult = _locStringId;
            return false;
        }

        public static FontUnitTestInfo[] EditorGetFontsToApplyUnitTests()
        {
            return EditorGetInstance().fontsToApplyUnitTests;
        }






        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Custom Inspector
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [UnityEditor.CustomEditor(typeof(LocManager))]
        private class LocManagerEditor : CustomEditorBase<LocManager>
        {
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //          Custom Editor Consts
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            /// <summary> The separators that can be used in the Scripting Defines Dialogue Field (used to extract existing Scripting Define Symbols). </summary>
            private static readonly char[] ScriptingDefineSeparators = new char[]
            {
                ';',
                ',',
                ' ',
            };

            /// <summary> Sorted Loc Languages. English goes first followed Language Alphabetical order. </summary>
            private static readonly string[] SortedLocLanguageIds = new System.Func<string[]>(() =>
            {
                string[] output = AllLocLanguageIds.Clone() as string[];
                Array.Sort(output!, (a, b) =>
                {
                    if (a.Equals(LocLanguages.English.ToString()))
                    {
                        if (b.Equals(LocLanguages.English.ToString()))
                            return 0;
                        return -1;
                    }
                    if (b.Equals(LocLanguages.English.ToString()))
                    {
                        return 1;
                    }

                    return string.CompareOrdinal(a, b);
                });
                return output;
            })();


            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //          Custom Editor Fields
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            private Dictionary<string, Dictionary<string, bool>> foldoutBools = new Dictionary<string, Dictionary<string, bool>>();

            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //          Custom Editor Methods
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            /// <summary> Adds the desired Scripting Define Symbol to all applicable build targets (ignores unknown and obsolete targets). </summary>
            private void AddAndRemoveDefineSymbolsToAllBuildTargets(List<string> _defineSymbolsToAdd, List<string> _defineSymbolsToRemove)
			{
                bool IsObsoleteTarget(UnityEditor.BuildTargetGroup value)
                {
					System.Reflection.FieldInfo fi = value.GetType().GetField(value.ToString());
					ObsoleteAttribute[] attributes = (ObsoleteAttribute[])fi.GetCustomAttributes(typeof(ObsoleteAttribute), false);
                    return attributes.Length > 0;
                }

                bool hasChanged = false;
                foreach (UnityEditor.BuildTargetGroup buildTarget in System.Enum.GetValues(typeof(UnityEditor.BuildTargetGroup)))
                {
                    // The `iPhone` build group is marked as Obsolete (replaced by `iOS`). But shares the same Enum Value '4' which means iOS gets
                    // attributed with the 'Obsolete' tag due to sharing '4' with iPhone even though it's a valid build target. So for this one instance. Ignore that check 
                    if (buildTarget != UnityEditor.BuildTargetGroup.Unknown 
                        && (buildTarget == UnityEditor.BuildTargetGroup.iOS || IsObsoleteTarget(buildTarget) == false)) 
                    {
                        bool localChange = false;
                        string defineSymbols = UnityEditor.PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTarget);
                        List<string> existingDefineSymbols = new List<string>(defineSymbols.Split(ScriptingDefineSeparators, System.StringSplitOptions.RemoveEmptyEntries));
                        foreach (string includeSymbol in _defineSymbolsToAdd)
                        {
                            if (existingDefineSymbols.Contains(includeSymbol) == false)
                            {
                                existingDefineSymbols.Add(includeSymbol);
                                localChange = true;
                                hasChanged = true;
                            }
                        }
                        foreach (string removeSymbol in _defineSymbolsToRemove)
                        {
                            if (existingDefineSymbols.Contains(removeSymbol))
                            {
                                existingDefineSymbols.Remove(removeSymbol);
                                localChange = true;
                                hasChanged = true;
                            }
                        }

                        if (localChange)
						{
                            UnityEditor.PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTarget, string.Join(";", existingDefineSymbols));
                        }
                    }
                }

                if (hasChanged)
				{
                    UnityEditor.AssetDatabase.SaveAssets();
                    UnityEditor.AssetDatabase.Refresh();
                }
			}

            /// <summary> Adds the desired Scripting Define Symbol to all applicable build targets (ignores unknown and obsolete targets). </summary>
            private void AddDefineSymbolToAllBuildTargets(string _defineSymbol)
			{
                AddAndRemoveDefineSymbolsToAllBuildTargets(new List<string>() { _defineSymbol }, new List<string>());
            }

            /// <summary> Removes the Scripting Define Symbol from all applicable build Targets. </summary>
            private void RemoveDefineSymbolFromAllBuildTargets(string _defineSymbol)
            {
                AddAndRemoveDefineSymbolsToAllBuildTargets(new List<string>(), new List<string>() { _defineSymbol });
            }

            /// <summary> Scans through Assemblies in the Domain via Reflection to find a specific class. </summary>
            /// <param name="_assemblyName"> If you know the assembly name, put it here for quicker/compartmentalised searching. Leave as empty string if you want to search all assemblies. </param>
            private Type FindClassInAssembly(string _assemblyName, string _name)
            {
                System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
                if (string.IsNullOrEmpty(_assemblyName) == false)
                {
                    assemblies = assemblies.OrderBy(assem => assem.GetName().Name).ToArray();
                    assemblies = assemblies.Where(assem => assem.GetName().Name.Equals(_assemblyName)).ToArray();
                }
                foreach (System.Reflection.Assembly assembly in assemblies)
                {
                    Type[] allTypes = assembly.GetTypes();
                    Type result = LocManagerHelpers.Find(allTypes, t => string.Equals(t.Name, _name));

                    if (result != null)
                        return result;
                }
                return null;
            }
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //     Custom Editor Inspector Regions
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            protected override void OnInitialised()
			{
                // Checking if we have serialised data for a non-existent language. Keeping unused data for a language we don't
                // need may result in FontAssets being included in resources/addressables for no benefit.
                LinkedList<string> removedLanguages = new LinkedList<string>();
                foreach (KeyValuePair<string, LocDisplayStyleTypeToLocDisplayStyleInfoDict> pair in Target.locLanguageToLocStyleTypesDict)
                {
                    string languageId = pair.Key;
                    if (AllLocLanguageIds.Contains(languageId) == false)
                    {
                        removedLanguages.AddLast(languageId);
                    }
                }

                if (removedLanguages.Count > 0)
                {
                    foreach (string lang in removedLanguages)
                    {
                        Target.locLanguageToLocStyleTypesDict.Remove(lang);
                    }
                    SetDirty();
                }
			}

            [InspectorRegion]
            protected void DrawLocalisationDebugButtons()
            {
                if (EditorHelper.DrawButtonField("Pull Down Loc Doc"))
                {
                    Type locImportHandler = FindClassInAssembly("", "LocalisationImportHandler");
                    System.Reflection.MethodInfo importLocDocMethod = locImportHandler.GetMethod("BeginImportProcessMenuOption", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (importLocDocMethod == null)
                        LocalisationOverrides.Debug.LogError("Could not locate `LocalisationImportHandler`");
                    else
                        importLocDocMethod.Invoke(null, null);
                }
            }

            [InspectorRegion("Loc Settings")]
            protected void DrawLocSettings()
            {
                using (new DrawBackgroundBox())
                {
                    using (new EnableDescriptionRendering())
                    {
                        const string EnglishOnlyModeTooltip = "Localisation will be shown in English Only. Even if other languages are present. Set this on if you are using Google Translate still."
                                                            + "\n\nEnglishOnly:\nShows English Only in Editor and Live Builds."
                                                            + "\n\nEnglishOnlyDuringLiveRelease:\nShows English only during Live Builds. In Editor can still switch to other languages."
                                                            + "\n\nAllLanguages:\nAll languages are usable in both editor and on live builds.";
                        EditorHelper.DrawEnumFieldWithUndoOption("English Only Mode:", ref Target.locManagerSettings.EnglishOnlyMode, false, EnglishOnlyModeTooltip);
                    }
                }
            }

            [InspectorRegion("Loc Fonts To Unit Test")]
            protected void DrawLocFontsToUnitTest()
            {
                using (new DrawBackgroundBox())
                {
                    using (new EnableDescriptionRendering())
                    {
                        const string UnitTestFontsTooltip = "Localisation will automatically run Unit Tests on these fonts atlases and determine if they are missing any characters. The test is failed if any characters found in the Loc Doc are missing."
                            + "\n\nFont:\nFont to check"
                            + "\n\nCheck Fallback Fonts:\nIf true, will check fallback fonts if this character is missing. The Unit Test is passed if a fallback font has this character included."
                            + "\n\nLanguages:\nLanguages to check against";
                        EditorHelper.DrawDescriptionBox(UnitTestFontsTooltip);
                        EditorHelper.DrawReferenceArrayElements(ref Target.fontsToApplyUnitTests, DrawArrayParameters<FontUnitTestInfo>.DefaultParams, DrawUnitTestFontElement);
                    }
                }
            }

            private void DrawUnitTestFontElement(int index)
            {
                EditorHelper.DrawLabel($"Unit Test Font {index + 1:00}");
                using (new DrawBackgroundBoxAndIndentEditor())
                {
                    EditorHelper.DrawObjectOptionWithUndoOption("Font:", ref Target.fontsToApplyUnitTests[index].FontAsset);
                    EditorHelper.DrawBoolFieldWithUndoOption("Check Fallback Fonts:", ref Target.fontsToApplyUnitTests[index].CheckFallbackFontsIfNotFound);
                    EditorHelper.DrawEnumFlagsFieldWithUndoOption("Languages:", ref Target.fontsToApplyUnitTests[index].LanguagesToCheckAgainst);
                }
            }

#if !USING_UNITY_LOCALISATION
            [InspectorRegion("Loc Fonts in Use")]
            protected void DrawLocFontsInUse()
            {
                Dictionary<Font, List<LocDisplayStyleFontInfo>> usedStandardFontAssetsToInfo = new Dictionary<Font, List<LocDisplayStyleFontInfo>>();
                Dictionary<TMPro.TMP_FontAsset, List<LocDisplayStyleFontInfo>> usedTmpFontAssetsToInfo = new Dictionary<TMPro.TMP_FontAsset, List<LocDisplayStyleFontInfo>>();

                for (int languageIndex = 0; languageIndex < LocLanguagesCount; ++languageIndex)
                {
                    string languageId = SortedLocLanguageIds[languageIndex];
                    LocDisplayStyleTypeToLocDisplayStyleInfoDict locDisplayStyleDict;
                    if (Target.locLanguageToLocStyleTypesDict.TryGetValue(languageId, out locDisplayStyleDict) == false)
                    {
                        continue;
                    }

                    for (int textStyleIndex = 0; textStyleIndex < AllTextDisplayStylesCount; ++textStyleIndex)
                    {
                        string textDisplayStyleId = AllTextDisplayStyles[textStyleIndex].ToString();

                        LocDisplayStyleFontInfo locDisplayStyleInfo;
                        if (locDisplayStyleDict.TryGetValue(textDisplayStyleId, out locDisplayStyleInfo) == false)
                        {
                            continue;
                        }

                        if (locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset != null)
                        {
                            List<LocDisplayStyleFontInfo> locFontsInfo;
                            if (usedStandardFontAssetsToInfo.TryGetValue(locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset, out locFontsInfo) == false)
                            {
                                locFontsInfo = new List<LocDisplayStyleFontInfo>();
                                usedStandardFontAssetsToInfo.Add(locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset, locFontsInfo);
                            }
                            locFontsInfo.Add(locDisplayStyleInfo);
                        }

                        if (locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset != null)
                        {
                            List<LocDisplayStyleFontInfo> locFontsInfo;
                            if (usedTmpFontAssetsToInfo.TryGetValue(locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset, out locFontsInfo) == false)
                            {
                                locFontsInfo = new List<LocDisplayStyleFontInfo>();
                                usedTmpFontAssetsToInfo.Add(locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset, locFontsInfo);
                            }
                            locFontsInfo.Add(locDisplayStyleInfo);
                        }
                    }
                }

                using (new EditorIndentation())
                {
                    EditorHelper.DrawLabel("Used Standard Font Assets");
                    using (new EditorIndentation())
                    {
                        foreach (KeyValuePair<Font, List<LocDisplayStyleFontInfo>> pair in usedStandardFontAssetsToInfo)
                        {
                            EditorHelper.DrawReadonlyObjectOption("Font:", pair.Key);
                        }
                    }

                    EditorHelper.DrawLabel("Used TMP Font Assets");
                    using (new EditorIndentation())
                    {
                        foreach (KeyValuePair<TMPro.TMP_FontAsset, List<LocDisplayStyleFontInfo>> pair in usedTmpFontAssetsToInfo)
                        {
                            EditorHelper.DrawReadonlyObjectOption("Font:", pair.Key);
                        }
                    }
                }
            }

            [InspectorRegion("Loc Font Options")]
            protected void DrawLocFontOptions()
			{
                if (EditorHelper.DrawFoldoutOption("Loc Font Options") == false)
                {
                    return;
                }

                Dictionary<TMPro.TMP_FontAsset, List<LocDisplayStyleFontInfo>> usedFontAssetsToInfo = new Dictionary<TMPro.TMP_FontAsset, List<LocDisplayStyleFontInfo>>();
                for (int languageIndex = 0; languageIndex < LocLanguagesCount; ++languageIndex)
                {
                    string languageId = SortedLocLanguageIds[languageIndex];
                    LocDisplayStyleTypeToLocDisplayStyleInfoDict locDisplayStyleDict;
                    if (Target.locLanguageToLocStyleTypesDict.TryGetValue(languageId, out locDisplayStyleDict) == false)
                    {
                        continue;
                    }

                    for (int textStyleIndex = 0; textStyleIndex < AllTextDisplayStylesCount; ++textStyleIndex)
                    {
                        string textDisplayStyleId = AllTextDisplayStyles[textStyleIndex].ToString();

                        LocDisplayStyleFontInfo locDisplayStyleInfo;
                        if (locDisplayStyleDict.TryGetValue(textDisplayStyleId, out locDisplayStyleInfo) == false)
                        {
                            continue;
                        }

                        if (locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset == null)
                        {
                            continue;
                        }

                        List<LocDisplayStyleFontInfo> locFontsInfo;
                        if (usedFontAssetsToInfo.TryGetValue(locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset, out locFontsInfo) == false)
                        {
                            locFontsInfo = new List<LocDisplayStyleFontInfo>();
                            usedFontAssetsToInfo.Add(locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset, locFontsInfo);
                        }

                        locFontsInfo.Add(locDisplayStyleInfo);
                    }
                }


                using (new EditorIndentation())
                {
                    foreach (KeyValuePair<TMPro.TMP_FontAsset, List<LocDisplayStyleFontInfo>> pair in usedFontAssetsToInfo)
                    {
                        TMPro.TMP_FontAsset fontAsset = pair.Key;
                        List<LocDisplayStyleFontInfo> assignedLocInfos = pair.Value;

                        int minFontSize = assignedLocInfos[0].textMeshProFontInfo.editorMinFontSize;
                        if (EditorHelper.DrawIntFieldWithUndoOption($"{fontAsset.name} Min Font Size:", ref minFontSize))
                        {
                            foreach (LocDisplayStyleFontInfo t in assignedLocInfos)
                                t.textMeshProFontInfo.editorMinFontSize = minFontSize;
                        }
                    }
                }
            }


            [InspectorRegion("Localisation Text Display Styles")]
            protected void DrawLocTextDisplayStyles()
            {
                if (EditorHelper.DrawFoldoutOption("Localisation Text Display Styles") == false)
                {
                    return;
                }

                bool hasShownDescription = false;
                using (new EditorIndentation())
                {
                    for (int languageIndex = 0; languageIndex < LocLanguagesCount; ++languageIndex)
                    {
						// ~~~~~~~~~~~~~~~~~~~~~~~~~ LANGUAGE HEADER ~~~~~~~~~~~~~~~~~~~~~~~~~
						string languageId = SortedLocLanguageIds[languageIndex];

						LocDisplayStyleTypeToLocDisplayStyleInfoDict locDisplayStyleDict;
						if (Target.locLanguageToLocStyleTypesDict.TryGetValue(languageId, out locDisplayStyleDict) == false)
						{
							locDisplayStyleDict = new LocDisplayStyleTypeToLocDisplayStyleInfoDict();
							Target.locLanguageToLocStyleTypesDict.Add(languageId, locDisplayStyleDict);
							SetDirty();
						}

                        bool hasSomethingAssignedToThisLanguage = false;
						Color? foldoutColour = null;
						string displayTitle = languageId;
						for (int textStyleIndex = 0; textStyleIndex < AllTextDisplayStylesCount; ++textStyleIndex)
                        {
                            string textDisplayStyleId = AllTextDisplayStyles[textStyleIndex].ToString();

                            LocDisplayStyleFontInfo locDisplayStyleInfo;
                            if (locDisplayStyleDict.TryGetValue(textDisplayStyleId, out locDisplayStyleInfo) == false)
                            {
                                locDisplayStyleInfo = new LocDisplayStyleFontInfo();
                                locDisplayStyleDict.Add(textDisplayStyleId, locDisplayStyleInfo);
                                SetDirty();
                            }

                            if (locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset != null)
                            {
								hasSomethingAssignedToThisLanguage = true;

								if (locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset == null)
								{
									displayTitle += " - Missing TMP FONT. Please add it";
									foldoutColour = Color.red;
									break;
								}
								else if (locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset.name.Contains(locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset.name) == false)
								{
									displayTitle += " - Font Mismatch: TMPro Font and Font are different";
									foldoutColour = Color.yellow;
									break;
								}
							}
                            else if (locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset != null)
							{
								hasSomethingAssignedToThisLanguage = true;
								displayTitle += " - Missing FONT. Please add it";
								foldoutColour = Color.red;
                                break;
							}							
						}
                        
                        if (hasSomethingAssignedToThisLanguage == false)
                        {
							displayTitle += " - Has Nothing Assigned";
							foldoutColour = Color.yellow;
						}

                        Dictionary<string, bool> langFoldoutBoolsDict;
                        if (foldoutBools.TryGetValue(languageId, out langFoldoutBoolsDict) == false)
						{
                            langFoldoutBoolsDict = new Dictionary<string, bool>();
                            foldoutBools.Add(languageId, langFoldoutBoolsDict);
                        }

						bool foldoutVal;
						if (langFoldoutBoolsDict.TryGetValue("Language Header:", out foldoutVal) == false)
						{
							foldoutVal = false;
						}

						if (EditorHelper.DrawFoldoutOption(displayTitle, ref foldoutVal, "", foldoutColour) == CustomEditorHelper<LocManager>.FoldoutResult.ChangedState)
						{
							langFoldoutBoolsDict["Language Header:"] = foldoutVal;
						}
						if (foldoutVal == false)
						{
							continue;
						}
						// ~~~~~~~~~~~~~~~~~~~~~~~~~ END LANGUAGE HEADER ~~~~~~~~~~~~~~~~~~~~~~~~~



						using (new EditorIndentation())
                        {
                            for (int textStyleIndex = 0; textStyleIndex < AllTextDisplayStylesCount; ++textStyleIndex)
                            {
                                string textDisplayStyleId = AllTextDisplayStyles[textStyleIndex].ToString();

                                LocDisplayStyleFontInfo locDisplayStyleInfo;
                                if (locDisplayStyleDict.TryGetValue(textDisplayStyleId, out locDisplayStyleInfo) == false)
                                {
                                    locDisplayStyleInfo = new LocDisplayStyleFontInfo();
                                    locDisplayStyleDict.Add(textDisplayStyleId, locDisplayStyleInfo);
                                    SetDirty();
                                }

                                string assetName = "";
                                if (locDisplayStyleInfo.textMeshProFontInfo.normalMaterial.editorAssetReference.Asset != null)
								{
                                    assetName = $"~ {locDisplayStyleInfo.textMeshProFontInfo.normalMaterial.editorAssetReference.Asset.name}";
                                }
                                else if (locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset != null)
								{
                                    assetName = $"~ {locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset.name}";
                                }
                                else if (locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset != null)
                                {
                                    assetName = $"~ {locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset.name}";
                                }

                                foldoutColour = null;
                                if (string.IsNullOrEmpty(assetName) == false)
								{
                                    if (locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset == null || locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset == null)
								    {
                                        assetName += " - Missing FONT. Please add it";
                                        foldoutColour = Color.red;
                                    }
                                    else if (locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset.name.Contains(locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset.name) == false)
                                    {
                                        assetName += " - Font Mismatch: TMPro Font and Font are different";
                                        foldoutColour = Color.yellow;
                                    }
                                    else if (assetName.Contains(locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset.name) == false)
									{
                                        assetName += " - Asset Mismatch: FONT and Asset have different names";
                                        foldoutColour = Color.yellow;
                                    }
                                    else if (locDisplayStyleInfo.textMeshProFontInfo.normalMaterial.editorAssetReference.Asset == null
                                        || locDisplayStyleInfo.textMeshProFontInfo.strokedMaterial.editorAssetReference.Asset == null
                                        || locDisplayStyleInfo.textMeshProFontInfo.underlinedMaterial.editorAssetReference.Asset == null)
                                    {
                                        assetName += " - Missing Material";
                                        foldoutColour = Color.yellow;
                                    }
                                }

                                if (langFoldoutBoolsDict.TryGetValue(textDisplayStyleId, out foldoutVal) == false)
								{
                                    foldoutVal = false;
								}
                                
                                if (EditorHelper.DrawFoldoutOption($"{textDisplayStyleId} {assetName}", ref foldoutVal, "", foldoutColour) == CustomEditorHelper<LocManager>.FoldoutResult.ChangedState)
								{
                                    langFoldoutBoolsDict[textDisplayStyleId] = foldoutVal;
                                }
                                if (foldoutVal == false)
								{
                                    continue;
								}

                                using (new EnableDescriptionRendering(hasShownDescription == false))
                                {
                                    hasShownDescription = true;
                                    EditorHelper.DrawBoldLabel("Unity Text Component");
                                    using (new EditorIndentation())
									{
                                        if (locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.DrawEditorOption("Font:", EditorHelper, "This is an editor only reference. You can drag a Font onto here via the inspector. It's data will be converted to a Resource/Addressables key during compile."))
                                            locDisplayStyleInfo.standardFontInfo.font.Editor_RefreshKeys();

                                        EditorHelper.DrawFloatRangeWithUndoOption("Font Size Percentage:", ref locDisplayStyleInfo.standardFontInfo.fontSizePercentage, 0.01f, 2.0f, "Modifies the Font Size when using this Font type on a Unity Text Component. You should use this if this Font Asset changes font sizes on a shared text component. Just to keep Font Sizes consistent.");
                                    }

                                    AddSpaces(2);

                                    EditorHelper.DrawBoldLabel("TMPro Text Component");
                                    using (new EditorIndentation())
                                    {
                                        if (locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.DrawEditorOption("Font:", EditorHelper, "This is an editor only reference. You can drag a TMP_FontAsset onto here via the inspector. It's data will be converted to a Resource/Addressables key during compile."))
                                            locDisplayStyleInfo.textMeshProFontInfo.font.Editor_RefreshKeys();
                                        
                                        EditorHelper.DrawFloatRangeWithUndoOption("Font Size Percentage:", ref locDisplayStyleInfo.textMeshProFontInfo.tmpFontSizePercentage, 0.01f, 2.0f, "Modifies the Font Size when using this Font type on a TMPro Component. You should use this if this Font Asset changes font sizes on a shared text component. Just to keep Font Sizes consistent.");
                                        if (locDisplayStyleInfo.textMeshProFontInfo.normalMaterial.editorAssetReference.DrawEditorOption("Normal Text Material:", EditorHelper, "Font Material to use when showing text normally."))
                                            locDisplayStyleInfo.textMeshProFontInfo.normalMaterial.Editor_RefreshKeys();
                                        if (locDisplayStyleInfo.textMeshProFontInfo.strokedMaterial.editorAssetReference.DrawEditorOption("Stroked Text Material:", EditorHelper, "Font Material to use when using stroked text."))
                                            locDisplayStyleInfo.textMeshProFontInfo.strokedMaterial.Editor_RefreshKeys();
                                        if (locDisplayStyleInfo.textMeshProFontInfo.underlinedMaterial.editorAssetReference.DrawEditorOption("Underlined Text Material:", EditorHelper, "Font Material to use when using underlined text."))
                                            locDisplayStyleInfo.textMeshProFontInfo.underlinedMaterial.Editor_RefreshKeys();
                                    }
                                    AddSpaces(3);
                                    EditorHelper.DrawSplitter();
                                }
                                AddSpaces(3);
                            }
                            AddSpaces(3);
                        }
                    }

                    if (EditorHelper.DrawButtonField("Add New Header"))
                        AddNewTextDisplayStyleEnumValue();
                }
            }

            [InspectorRegion("Compatibility Upgrade")]
            protected void DrawCompatibilityUpgradeOptions()
            {
                if (EditorHelper.DrawButtonField("Populate Text Display Styles Via Obsolete FontOverrideScriptables") == false) 
                    return;
                
                foreach (LocLanguages language in AllLocLanguages)
                {
                    string languageId = language.ToString();
                    FontOverrideScriptable fontOverrideScriptable = Resources.Load<FontOverrideScriptable>($"{languageId}_FontOverride");
                    if (fontOverrideScriptable == null)
                        continue;

                    LocDisplayStyleTypeToLocDisplayStyleInfoDict locDisplayStyleDict;
                    if (Target.locLanguageToLocStyleTypesDict.TryGetValue(languageId, out locDisplayStyleDict) == false)
                    {
                        locDisplayStyleDict = new LocDisplayStyleTypeToLocDisplayStyleInfoDict();
                        Target.locLanguageToLocStyleTypesDict.Add(languageId, locDisplayStyleDict);
                    }
                    for (int textStyleIndex = 0; textStyleIndex < AllTextDisplayStylesCount; ++textStyleIndex)
                    {
                        string textDisplayStyleId = AllTextDisplayStyles[textStyleIndex].ToString();
                        LocDisplayStyleFontInfo locDisplayStyleInfo;
                        if (locDisplayStyleDict.TryGetValue(textDisplayStyleId, out locDisplayStyleInfo) == false)
                        {
                            locDisplayStyleInfo = new LocDisplayStyleFontInfo();
                            locDisplayStyleDict.Add(textDisplayStyleId, locDisplayStyleInfo);
                        }

                        if (fontOverrideScriptable.standardFonts.Length > textStyleIndex)
                        {
                            locDisplayStyleInfo.standardFontInfo.font.editorAssetReference.Asset = fontOverrideScriptable.standardFonts[textStyleIndex].font;
                            locDisplayStyleInfo.standardFontInfo.fontSizePercentage = fontOverrideScriptable.standardFonts[textStyleIndex].fontSizePercentage;
                        }

                        if (fontOverrideScriptable.tmpFonts.Length > textStyleIndex)
                        {
                            locDisplayStyleInfo.textMeshProFontInfo.font.editorAssetReference.Asset = fontOverrideScriptable.tmpFonts[textStyleIndex].tmpFont;
                            locDisplayStyleInfo.textMeshProFontInfo.tmpFontSizePercentage = fontOverrideScriptable.tmpFonts[textStyleIndex].tmpFontSizePercentage;

                            locDisplayStyleInfo.textMeshProFontInfo.normalMaterial.editorAssetReference.Asset = fontOverrideScriptable.tmpFonts[textStyleIndex].normalMaterial;
                            locDisplayStyleInfo.textMeshProFontInfo.strokedMaterial.editorAssetReference.Asset = fontOverrideScriptable.tmpFonts[textStyleIndex].strokedMaterial;
                            locDisplayStyleInfo.textMeshProFontInfo.underlinedMaterial.editorAssetReference.Asset = fontOverrideScriptable.tmpFonts[textStyleIndex].underlinedMaterial;
                        }
                    }
                }
                SetDirty();
            }
#endif

			protected override void DrawDebugOptions()
			{
#if USING_UNITY_LOCALISATION
                EditorHelper.DrawDescriptionBox("No Debug Options to show because you are using Unity Localisation.");
#else
    #if LOCALISATION_ADDRESSABLES
                EditorHelper.DrawWrappedText("Changes the Localisation to convert Localisation assets to addressables during build time and load the assets during runtime via addressables");
                if (EditorHelper.DrawButtonField("Switch Localisation from Addressables to Resources"))
				{
                    if (EditorHelper.ShowYesOrNoDialogueWindow("Are you sure?", "This will modify how the Loc System works and may break your game. Only proceed if you understand what you are doing", "Yes, Switch", "No, don't change"))
                        RemoveDefineSymbolFromAllBuildTargets("LOCALISATION_ADDRESSABLES");
                }
    #else
                EditorHelper.DrawWrappedText("Changes the Localisation to move Localisation assets to the Resources folder during build time and load the assets during runtime via resources");
                if (EditorHelper.DrawButtonField("Switch Localisation from Resources to Addressables"))
                {
                    if (EditorHelper.ShowYesOrNoDialogueWindow("Are you sure?", "This will modify how the Loc System works and may break your game. Only proceed if you understand what you are doing", "Yes, Switch", "No, don't change"))
                        AddDefineSymbolToAllBuildTargets("LOCALISATION_ADDRESSABLES");
                }
    #endif

    #if SHOW_LOC_DEBUG_OPTIONS
                AddSpaces(5);

        #if DISABLE_LOCALISATION_EDITOR_MODE
                EditorHelper.DrawWrappedText("Enables the code that enables the editor to load the localisation resources directly from assets without asynchronous/resources operations. This will make the editor act differently than device.");
                if (EditorHelper.DrawButtonField("Enable Localisation Editor Mode"))
                {
                    if (EditorHelper.ShowYesOrNoDialogueWindow("Are you sure?", "This will modify how the Loc System works and may break your game. Only proceed if you understand what you are doing", "Yes, Switch", "No, don't change"))
                        RemoveDefineSymbolFromAllBuildTargets("DISABLE_LOCALISATION_EDITOR_MODE");
                }
        #else
                EditorHelper.DrawWrappedText("Disables the code that enables the editor to load the localisation resources directly from assets without asynchronous/resources operations. Use this when you want to run Editor as if it were a build.");
                if (EditorHelper.DrawButtonField("Disable Localisation Editor Mode"))
                {
                    if (EditorHelper.ShowYesOrNoDialogueWindow("Are you sure?", "This will modify how the Loc System works and may break your game. Only proceed if you understand what you are doing", "Yes, Switch", "No, don't change"))
                        AddDefineSymbolToAllBuildTargets("DISABLE_LOCALISATION_EDITOR_MODE");
                }
        #endif
                AddSpaces(5);

        #if DISABLE_LOCALISATION_ATLAS_GENERATION_OPTION
                EditorHelper.DrawWrappedText("Enables the prompt for Atlas Generation when pulling down the Localisation from Google.");
                if (EditorHelper.DrawButtonField("Enable Localisation Atlas Generation Option"))
                {
                    if (EditorHelper.ShowYesOrNoDialogueWindow("Are you sure?", "This will modify how the Loc System works and may break your game. Only proceed if you understand what you are doing", "Yes, Switch", "No, don't change"))
                        RemoveDefineSymbolFromAllBuildTargets("DISABLE_LOCALISATION_ATLAS_GENERATION_OPTION");
                }
        #else
                EditorHelper.DrawWrappedText("Disables the prompt for Atlas Generation when pulling down the Localisation from Google.");
                if (EditorHelper.DrawButtonField("Disable Localisation Atlas Generation Option"))
                {
                    if (EditorHelper.ShowYesOrNoDialogueWindow("Are you sure?", "This will modify how the Loc System works and may break your game. Only proceed if you understand what you are doing", "Yes, Switch", "No, don't change"))
                        AddDefineSymbolToAllBuildTargets("DISABLE_LOCALISATION_ATLAS_GENERATION_OPTION");
                }
        #endif

                if (EditorHelper.DrawButtonField("Move Loc Assets to Resources/Make Addressables"))
                {
                    Type locImportHandler = FindClassInAssembly("Localisation.Editor", "LocalisationResourcesBuildHandler");
                    System.Reflection.MethodInfo importLocDocMethod = locImportHandler.GetMethod("AssignLocAssetsToResourcesAndAddressables", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
                    importLocDocMethod.Invoke(null, null);
                }
                if (EditorHelper.DrawButtonField("Move Resources Back to Assets"))
                {
                    Type locImportHandler = FindClassInAssembly("", "LocalisationResourcesBuildHandler");
                    System.Reflection.MethodInfo importLocDocMethod = locImportHandler.GetMethod("MoveLocResourcesBackToAssets", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
                    importLocDocMethod.Invoke(null, null);
                }
    #endif
#endif
            }

            /// <summary> Writes a new Header Enum Value into the LocalisationTextDisplayStyleTypes.cs file </summary>
            private void AddNewTextDisplayStyleEnumValue()
            {
                const string AutomationIdentifier = "~~~~~ AUTOMATION (do not touch this line) ~~~~~";
                const string PathToTextDisplayTypesEnumFile = LocalisationOverrides.Configuration.PathToGameSpecificLocalisationRepo + "Scripts/LocalisationTextDisplayStyleTypes.cs";
			    if (System.IO.File.Exists(PathToTextDisplayTypesEnumFile) == false)
			    {
				    Debug.LogError($"Cannot find {nameof(LocalisationTextDisplayStyleTypes)} at path [{PathToTextDisplayTypesEnumFile}]");
				    return;
			    }

			    string contents = System.IO.File.ReadAllText(PathToTextDisplayTypesEnumFile);
			    string[] contentsSplit = contents.Split($"// {AutomationIdentifier}");

			    using (System.IO.StreamWriter writer = new(PathToTextDisplayTypesEnumFile, false))
			    {
				    try
				    {
					    writer.Write(contentsSplit[0]);
					    writer.Write($"// {AutomationIdentifier}\n");
                        {
                            foreach (LocalisationTextDisplayStyleTypes textDisplayStyleType in AllTextDisplayStyles)
                                writer.WriteLine($"\t\t{textDisplayStyleType},");

                            var lastHeader = AllTextDisplayStyles[AllTextDisplayStyles.Length - 1];
                            Match match = Regex.Match(lastHeader.ToString(), "Header(\\d+)");
                            if (match.Success)
                            {
                                int headerVal = int.Parse(match.Groups[1].ToString(), NumberStyles.Any, CultureInfo.InvariantCulture);
                                writer.WriteLine($"\t\tHeader{headerVal + 1},");
                            }
                            else
                            {
                                writer.WriteLine($"\t\tHeader{AllTextDisplayStyles.Length + 1},");
                            }
                        }
					    writer.Write($"// {AutomationIdentifier}");
					    writer.Write(contentsSplit[2]);
				    }
				    catch (System.Exception ex)
				    {
					    Debug.LogError($" threw:\n{ex.Message}");
				    }
			    }
			
			    UnityEditor.AssetDatabase.Refresh();
            }
        }
    }
#endif
}
