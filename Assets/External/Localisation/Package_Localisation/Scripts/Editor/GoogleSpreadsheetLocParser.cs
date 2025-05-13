//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Google Spreadsheet Loc Parser
//             Author: Christopher Allport
//             Date Created: 16th May, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Parses the data returned from the Google Spreadsheet via
//      the standard Loc format.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

#if USING_UNITY_LOCALISATION
using UnityEditor.Localization;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
#endif


namespace Localisation.Localisation.Editor
{
	public class GoogleSpreadsheetLocParser
	{
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Definitions
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private enum ResolutionStatus
		{
            Completed,
            Failure,
            NotNeeded,
		}

        private class LocIdContents
		{
            public GoogleAPI.SheetData sheetData;
            public string locId = "";
            public string sanitisedLocId = "";
            public string englishText = "";
            public string lineNum = "";
            public Dictionary<string, string> otherLanguageTexts = new Dictionary<string, string>();
        }

        private class LocSheetContents
        {
            public GoogleAPI.SheetData sheetData;
            public List<LocIdContents> locIds = null;
            public List<string> otherLanguagesUsed = new List<string>();
        }

        private class FullLocSpreadsheetContents
        {
            public List<LocSheetContents> locSheets = null;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Consts
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private const string GoogleTranslateIdentifierTag = "(?:Todo:\\s*Translate\\s*Properly)|(?:Google\\s*Translate:)";

        private const string ColumnSeparator = "\t";
        private const string RowSeparator = "\n";

        private const int ColCellIndexForFirstForeignLanguage = 5;
        private static readonly int ColCellIndexForFinalForeignLanguage = ColCellIndexForFirstForeignLanguage + Enum.GetValues(typeof(LocLanguages)).Length - 1;

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private List<string> locLanguagesUsedInSpreadsheet = new List<string>();

        private FullLocSpreadsheetContents fullLocSpreadsheetContents = new FullLocSpreadsheetContents();

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Properties
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public List<string> WarningStatus { get; private set; } = new List<string>();
        public List<string> ErrorStatus { get; private set; } = new List<string>();

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public bool ParseGoogleSpreadsheet(GoogleAPI.SpreadsheetHandler _spreadsheetHandler)
		{
            WarningStatus.Clear();
            ErrorStatus.Clear();
            locLanguagesUsedInSpreadsheet = new List<string>();
            fullLocSpreadsheetContents = new FullLocSpreadsheetContents
            {
                locSheets = new List<LocSheetContents>(_spreadsheetHandler.SpreadsheetData.SheetsData.Count)
            };

            foreach (GoogleAPI.SheetData sheetData in _spreadsheetHandler.SpreadsheetData.SheetsData)
            {
                int rowsCount = sheetData.Cells.Values.Count;

                LocSheetContents sheetContents;
                if (ResolveSheetMetaData(sheetData, rowsCount, out sheetContents) == ResolutionStatus.NotNeeded)
                {
                    continue;
                }
                if (ResolveLocSheetContents(sheetContents, rowsCount) == ResolutionStatus.Failure)
                {
                    return false;
                }
            }

            return ErrorStatus.Count == 0;
        }

        /// <summary> Export sanitised spreadsheet data to Assets folder. </summary>
        public bool ExportLocStringsTable(string _locCSVFilePath)
        {
            WarningStatus.Clear();
            ErrorStatus.Clear();

            try
            {
                // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                // ~~~~~~~~~~ THIS SECTION BUILDS THE LOC_STRINGS CSV FILE ~~~~~~~~~~
                // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                LocIdContents[] sortedLocContents = fullLocSpreadsheetContents.locSheets.SelectMany(locSheet => locSheet.locIds)
                                                            .OrderBy(locIdContents => locIdContents.sanitisedLocId)
                                                            .ToArray();

                // File may be locked, readonly, etc. So Try/Catch and output failure reason if failed.
                if (System.IO.File.Exists(_locCSVFilePath))
                {
                    System.IO.File.Delete(_locCSVFilePath);
                }
                using (var fileWriter = System.IO.File.CreateText(_locCSVFilePath))
                {
                    fileWriter.Write($"LOC_IDS{ColumnSeparator}");
                    fileWriter.Write($"ENGLISH{ColumnSeparator}");

                    List<string> alphabetisedLanguageIds = locLanguagesUsedInSpreadsheet.OrderBy(o => o).ToList();
                    fileWriter.Write(string.Join(ColumnSeparator, alphabetisedLanguageIds) + RowSeparator);

                    foreach (LocIdContents locIdContents in sortedLocContents)
                    {
                        fileWriter.Write($"{locIdContents.sanitisedLocId}{ColumnSeparator}");
                        fileWriter.Write($"{locIdContents.englishText}");

                        foreach (string languageId in alphabetisedLanguageIds)
						{
                            string languageText;
                            if (locIdContents.otherLanguageTexts.TryGetValue(languageId, out languageText) == false)
							{
                                ErrorStatus.Add($"Loc Sheet [{locIdContents.sheetData.Title}] is missing text for Language [{languageId}] on {locIdContents.lineNum}");
                                EditorUtility.DisplayDialog($"Failure", ErrorStatus[0], "Ok");
                                return false;
							}

                            fileWriter.Write($"{ColumnSeparator}{languageText}");
                        }
                        fileWriter.Write(RowSeparator);
                    }
                }
                
                // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
                // ~~~~~~~~~~ THIS SECTION BUILDS THE UNITY LOCALIZATION TABLES ~~~~~~~~~~
                // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
#if USING_UNITY_LOCALISATION
                ExportLocStringsToUnityLocalizationTable(_locCSVFilePath);
#endif
                
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (System.Exception e)
            {
                EditorUtility.DisplayDialog($"Failure", $"Error Occurred\n{e.Message}", "Ok");
                return false;
            }

            return true;
        }

        private bool StringEqualsAny(string origin, params string[] comparisons)
        {
            foreach (string s in comparisons)
            {
                if (string.Equals(origin, s))
                    return true;
            }

            return false;
        }

#if USING_UNITY_LOCALISATION
        private void ExportLocStringsToUnityLocalizationTable(string _locCSVFilePath)
        {
            LocIdContents[] sortedLocContents = fullLocSpreadsheetContents.locSheets.SelectMany(locSheet => locSheet.locIds).OrderBy(locIdContents => locIdContents.sanitisedLocId).ToArray();

            Dictionary<string, StringTable> languageIdToUnityTable = new(locLanguagesUsedInSpreadsheet.Count);
            StringTableCollection tableCollection = GetOrAddUnityLocalisationStringTableCollection(_locCSVFilePath);

            // ~~~~~ ENGLISH ~~~~~
            StringTable englishTable = GetOrAddUnityLocalisationStringTable(_locCSVFilePath, tableCollection, LocLanguages.English);
            englishTable.Clear();
            EditorUtility.SetDirty(englishTable);
            
            // ~~~~~ OTHER LANGUAGES ~~~~~
            foreach (string languageId in locLanguagesUsedInSpreadsheet)
            {
                if (Enum.TryParse(languageId, ignoreCase: true, out LocLanguages locLanguage) == false)
                    throw new Exception($"[{languageId}] is not defined in Loc Languages. Please add it.");

                StringTable languageTable = GetOrAddUnityLocalisationStringTable(_locCSVFilePath, tableCollection, locLanguage);
                languageIdToUnityTable[languageId] = languageTable;
                
                languageTable.Clear();
                EditorUtility.SetDirty(languageTable);
            }

            // ~~~~~ ADDING TO UNITY LOC TABLES ~~~~~
            foreach (LocIdContents locIdContents in sortedLocContents)
            {
                bool containsSubstrings = false;
                string englishEntryText = locIdContents.englishText;
                Match englishRegexMatch = Regex.Match(englishEntryText, "\\[(\\d+)\\]");
                if (englishRegexMatch.Success)
                {
                    containsSubstrings = true;
                    for (int i = 1; i < englishRegexMatch.Groups.Count; ++i)
                        englishEntryText = englishEntryText.Replace($"[{englishRegexMatch.Groups[i]}]", $"{{{englishRegexMatch.Groups[i]}}}");
                }

                StringTableEntry englishEntry = englishTable.AddEntry(locIdContents.sanitisedLocId, englishEntryText);
                englishEntry.IsSmart = containsSubstrings;

                foreach (string languageId in locLanguagesUsedInSpreadsheet)
                {
                    containsSubstrings = false;
                    StringTable locTable = languageIdToUnityTable[languageId];
                    string languageText = locIdContents.otherLanguageTexts[languageId];
                    Match languageRegexMatch = Regex.Match(languageText, "\\[(\\d+)\\]");
                    if (languageRegexMatch.Success)
                    {
                        containsSubstrings = true;
                        for (int i = 1; i < languageRegexMatch.Groups.Count; ++i)
                            languageText = languageText.Replace($"[{languageRegexMatch.Groups[i]}]", $"{{{languageRegexMatch.Groups[i]}}}");
                    }
                    StringTableEntry languageEntry = locTable.AddEntry(locIdContents.sanitisedLocId, languageText);
                    languageEntry.IsSmart = containsSubstrings;
                }
            }
        }

        private Locale GetOrAddUnityLocalisationLocale(LocLanguages _locLanguage)
        {
            SystemLanguage systemLanguage = LocLanguagesInfo.LocLangToSystemLang[_locLanguage];
            return GetOrAddUnityLocalisationLocale(systemLanguage);
        }
        
        private Locale GetOrAddUnityLocalisationLocale(SystemLanguage _systemLanguage)
        {
            LocaleIdentifier localeIdentifier = new(_systemLanguage);
            Locale locale = LocalizationSettings.Instance.GetAvailableLocales().GetLocale(localeIdentifier);
            if (locale == null)
            {
                locale = Locale.CreateLocale(_systemLanguage);
                
                string unityLocalisationFolder = $"{LocalisationOverrides.Configuration.PathToGameSpecificLocalisationRepo}/UnityLocalisation";
                if (System.IO.Directory.Exists(unityLocalisationFolder) == false)
                    System.IO.Directory.CreateDirectory(unityLocalisationFolder);
                
                string localesFolderPath = $"{unityLocalisationFolder}/Locales";
                if (System.IO.Directory.Exists(localesFolderPath) == false)
                    System.IO.Directory.CreateDirectory(localesFolderPath);
                
                string pathToFile = $"{localesFolderPath}/{locale.Identifier.CultureInfo.EnglishName} ({locale.Identifier.Code}).asset";
                AssetDatabase.CreateAsset(locale, pathToFile);
                LocalizationEditorSettings.AddLocale(locale);
            }
            return locale;
        }

        private StringTableCollection GetOrAddUnityLocalisationStringTableCollection(string _locCSVFilePath)
        {
            string csvFileName = System.IO.Path.GetFileNameWithoutExtension(_locCSVFilePath);
            StringTableCollection tableCollection = LocalizationEditorSettings.GetStringTableCollection(csvFileName);
            if (tableCollection == null)
            {
                string unityLocalisationFolder = $"{LocalisationOverrides.Configuration.PathToGameSpecificLocalisationRepo}/UnityLocalisation";
                if (System.IO.Directory.Exists(unityLocalisationFolder) == false)
                    System.IO.Directory.CreateDirectory(unityLocalisationFolder);
                
                string csvFolderPath = $"{unityLocalisationFolder}/LocTables";
                if (System.IO.Directory.Exists(csvFolderPath) == false)
                    System.IO.Directory.CreateDirectory(csvFolderPath);
                
                tableCollection = LocalizationEditorSettings.CreateStringTableCollection(csvFileName, csvFolderPath);
            }
            return tableCollection;
        }

        private StringTable GetOrAddUnityLocalisationStringTable(string _locCSVFilePath, StringTableCollection _tableCollection, LocLanguages _locLanguage)
        {
            SystemLanguage systemLanguage = LocLanguagesInfo.LocLangToSystemLang[_locLanguage];
            return GetOrAddUnityLocalisationStringTable(_locCSVFilePath, _tableCollection, systemLanguage);
        }
        
        private StringTable GetOrAddUnityLocalisationStringTable(string _locCSVFilePath, StringTableCollection _tableCollection, SystemLanguage _systemLanguage)
        {
            Locale locale = GetOrAddUnityLocalisationLocale(_systemLanguage);
            string csvFileName = System.IO.Path.GetFileNameWithoutExtension(_locCSVFilePath);
            StringTable table = LocalizationSettings.StringDatabase.GetTable(csvFileName, locale);
            if (table == null)
            {
                string unityLocalisationFolder = $"{LocalisationOverrides.Configuration.PathToGameSpecificLocalisationRepo}/UnityLocalisation";
                if (System.IO.Directory.Exists(unityLocalisationFolder) == false)
                    System.IO.Directory.CreateDirectory(unityLocalisationFolder);
                
                string csvFolderPath = $"{unityLocalisationFolder}/LocTables";
                if (System.IO.Directory.Exists(csvFolderPath) == false)
                    System.IO.Directory.CreateDirectory(csvFolderPath);
                
                string tableFilePath = $"{csvFolderPath}/{csvFileName}_{locale.Identifier.Code}.asset";
                table = ScriptableObject.CreateInstance<StringTable>();
                table.LocaleIdentifier = locale.Identifier;
                table.SharedData = _tableCollection.SharedData;
                
                // Must be saved as an asset (Persistent) first before adding to TableCollection
                AssetDatabase.CreateAsset(table, tableFilePath);
                _tableCollection.AddTable(table);
            }
            return table;
        }
#endif

        private ResolutionStatus ResolveSheetMetaData(GoogleAPI.SheetData _sheetData, int _rowsCount, out LocSheetContents _sheetContents)
        {
            // Fetching the Languages being used.
            if (_rowsCount < 2)
            {
                _sheetContents = null;
                return ResolutionStatus.NotNeeded;
            }

            if (_sheetData.Sheet.Properties.Hidden ?? false)
            {
                _sheetContents = null;
                return ResolutionStatus.NotNeeded;
            }

            if (StringEqualsAny(_sheetData.Title, "HUB", "IMPORT", "[HIDDEN]Key", "[HIDDEN]Export", "TemplateSheet (do not use)"))
            {
                _sheetContents = null;
                return ResolutionStatus.NotNeeded;
            }

            _sheetContents = new LocSheetContents()
            {
                sheetData = _sheetData,
                locIds = new List<LocIdContents>(_rowsCount),
            };
            fullLocSpreadsheetContents.locSheets.Add(_sheetContents);

            // Converting downloaded Loc Table contents from Google into Unicode format (We are dealing with non-ASCII languages, so it's essential).
            IList<object> rowData = _sheetData.Cells.Values[0];
            int colsCount = Math.Min(ColCellIndexForFinalForeignLanguage, rowData.Count);
            for (int columnIndex = ColCellIndexForFirstForeignLanguage; columnIndex < colsCount; ++columnIndex)
            {
                string cellData = rowData[columnIndex].ToString();
                byte[] bytes = System.Text.Encoding.Default.GetBytes(cellData);
                string language = System.Text.Encoding.UTF8.GetString(bytes);

                if (locLanguagesUsedInSpreadsheet.Contains(language) == false)
                {
                    locLanguagesUsedInSpreadsheet.Add(language);
                }

                if (_sheetContents.otherLanguagesUsed.Contains(language) == false)
                {
                    _sheetContents.otherLanguagesUsed.Add(language);
                }
            }

            return ResolutionStatus.Completed;
        }

        private ResolutionStatus ResolveLocSheetContents(LocSheetContents _sheetContents, int _rowsCount)
		{
            for (int rowIndex = 1; rowIndex < _rowsCount; ++rowIndex)
            {
                LocIdContents locIdContents = new LocIdContents
                {
                    sheetData = _sheetContents.sheetData,
                    lineNum = (rowIndex + 2).ToString("00")
                };

                IList<object> rowData = _sheetContents.sheetData.Cells.Values[rowIndex];
                int colsCount = rowData.Count;

                if (colsCount > 0)
                {
                    // Converting downloaded Loc Table contents from google into Unicode format (We are dealing with non-ASCII languages, so it's essential).
                    string cellData = rowData[0].ToString();
                    if (string.IsNullOrEmpty(cellData))
					{
                        ErrorStatus.Add($"No Loc Id: LocSheet [{_sheetContents.sheetData.Title}] at Line {locIdContents.lineNum}: No Loc ID has been assigned");
                        continue;
                    }

                    byte[] bytes = System.Text.Encoding.Default.GetBytes(cellData);
                    locIdContents.locId = System.Text.Encoding.UTF8.GetString(bytes);
                }

                if (colsCount > 1)
				{
                    locIdContents.sanitisedLocId = LocManager.SanitiseLocId(locIdContents.locId);

                    if (string.IsNullOrEmpty(locIdContents.sanitisedLocId))
                    {
                        ErrorStatus.Add($"No Loc Id: LocSheet [{_sheetContents.sheetData.Title}] at Line {locIdContents.lineNum}: The Loc ID is invalid. Please fix this and try again.");
                        continue;
                    }

                    LocSheetContents duplicatedSheet;
                    LocIdContents duplicateLocContents;
                    if (CheckForDuplicateLocId(locIdContents.sanitisedLocId, out duplicatedSheet, out duplicateLocContents))
                    {
                        ErrorStatus.Add($"Duplicated Loc Id [{locIdContents.locId}]: LocSheet [{duplicatedSheet.sheetData.Title}] at Line {duplicateLocContents.lineNum} is a duplicate of LocSheet [{_sheetContents.sheetData.Title}] at Line {locIdContents.lineNum}");
                    }

                    _sheetContents.locIds.Add(locIdContents);

                    string cellData = rowData[1].ToString();
                    byte[] bytes = System.Text.Encoding.Default.GetBytes(cellData);
                    locIdContents.englishText = SanitiseText( System.Text.Encoding.UTF8.GetString(bytes) );
                }
                else
				{
                    WarningStatus.Add($"Missing English Text: LocSheet [{_sheetContents.sheetData.Title}] at Line {locIdContents.lineNum} has Loc ID [{locIdContents.locId}] but no English Text. Please add some.");
                    continue;
                }

                for (int languageIndex = 0; languageIndex < _sheetContents.otherLanguagesUsed.Count; ++languageIndex)
                {
                    // Converting downloaded Loc Table contents from google into Unicode format (We are dealing with non-ASCII languages, so it's essential).
                    string languageId = _sheetContents.otherLanguagesUsed[languageIndex];
                    int columnIndex = ColCellIndexForFirstForeignLanguage + languageIndex;
                    if (colsCount > columnIndex)
                    {
                        string cellData = rowData[columnIndex].ToString();
                        byte[] bytes = System.Text.Encoding.Default.GetBytes(cellData);
                        locIdContents.otherLanguageTexts[languageId] = SanitiseText( System.Text.Encoding.UTF8.GetString(bytes) );

                        if (string.IsNullOrEmpty( locIdContents.otherLanguageTexts[languageId] ))
						{
                            ErrorStatus.Add($"Missing {languageId} Text: LocSheet [{_sheetContents.sheetData.Title}] at Line {locIdContents.lineNum} has Loc ID [{locIdContents.locId}] but no {languageId} Text. Please add some.");
                        }
                    }
                    else
					{
                        ErrorStatus.Add($"Missing {languageId} Text: LocSheet [{_sheetContents.sheetData.Title}] at Line {locIdContents.lineNum} has Loc ID [{locIdContents.locId}] but no {languageId} Text. Please add some.");
                    }
                }
            }

            return ResolutionStatus.Completed;
        }

        private bool CheckForDuplicateLocId(string _locId, out LocSheetContents _sheet, out LocIdContents _existingLocIdContents)
		{
            foreach (LocSheetContents sheet in fullLocSpreadsheetContents.locSheets)
			{
                _existingLocIdContents = sheet.locIds.Find(locContents => string.Equals(locContents.locId, _locId));
                if (_existingLocIdContents != null)
				{
                    _sheet = sheet;
                    return true;
                }
			}

            _sheet = null;
            _existingLocIdContents = null;
            return false;
		}

        private string SanitiseText(string _origin)
		{
            _origin = _origin.Replace("\r", "");

            // Remove Google Translate Identifier Tag if it exists, Two Spaces because there might be a newline added.
            _origin = Regex.Replace(_origin, $"^{GoogleTranslateIdentifierTag}{(char)10}*\\s*", "");

            // Google sheets adds two spaces to represent a newline
            _origin = _origin.Replace("  ", "\\n");

            // Unicode character for newline that can sometimes be used (usually comes from Google Translate).
            _origin = _origin.Replace($"{(char)10}", "\\n");

            return _origin;
        }
	}
}