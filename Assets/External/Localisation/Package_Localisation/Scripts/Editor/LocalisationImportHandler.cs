//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//              Localisation Import Handler
//              Author: Christopher Allport
//              Date Created: February 21, 2020
//              Last Updated: August 7, 2020
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//		  This Script handles the automatic generation of localisation data by
//      fetching and converting data from a Google Sheet document into a tab 
//      separated spreadsheet, then saving it into the project directory.
//
//        Once completed, this script also then parses the data in the
//      spreadsheet, compiles a new LocIDs enum structure based off that data,
//      and uses the unique characters imported in each language to generate 
//      the glyph atlases that will be used.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine.TextCore.LowLevel;
using TMPro;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Localisation.LocalisationOverrides;
using Debug = UnityEngine.Debug;
using LanguageImportData = Localisation.Localisation.LocLanguagesInfo.LanguageImportData;


#if UNITY_EDITOR
[assembly: UnityEngine.Scripting.Preserve]
#endif

namespace Localisation.Localisation.Editor
{
    public partial class LocalisationImportHandler : ScriptableObject
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *            Declarations
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private delegate StepResult AutoGenerationStepHandlerDelegate();
        private delegate Task<StepResult> AsyncAutoGenerationStepHandlerDelegate();

        [System.Serializable]
        public class DocumentFetcherInfo
        {
            public string googleDocId;
            public string csvFilePath;

            [NonSerialized] public GoogleAPI.SpreadsheetHandler spreadsheetHandler;
            [NonSerialized] public GoogleSpreadsheetLocParser spreadsheetLocParser;
        }

        private enum StepResult
        {
            Running,
            Success,
            Failed
        }

        private class LocFontStyleAssociation
        {
            public LocalisationTextDisplayStyleTypes fontStyle;
            public LocLanguages language;

            public LocDisplayStyleFontInfo.StandardLocFontInfo StandardLocFontInfo
            {
                get { return LocManager.Instance.GetTextLocDisplayStyleInfo(language, fontStyle); }
            }

            public LocDisplayStyleFontInfo.TextMeshProLocFontInfo TextMeshProLocFontInfo
			{
                get { return LocManager.Instance.GetTextMeshLocDisplayStyleInfo(language, fontStyle); }
			}

            public override string ToString()
            {
                return language.ToString();
            }
        }

        private class AutoGenerationStepHandler
        {
            public StepResult status;
            public AutoGenerationStepHandlerDelegate methodToRun;
            public AsyncAutoGenerationStepHandlerDelegate asyncMethodToRun;
        }

        private class LanguageFontData
        {
            public string fontPath;
            public Font sourceLanguageFont;
        }

        private class LanguageAtlasData
        {
            public TMP_FontAsset fontAsset;
            public int fontSize;
            public int atlasWidth;
            public int atlasHeight;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *            Consts
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private const string ProgressBarWindowTitle = "Localisation Import Status";

        private const string FailurePopupMessage = "Failed to import Localisation. Please check the console for errors.";

        private const string SuccessPopupMessage = "      Fetch from Google's Servers successful.\n\n"
                                                 + "                            \\\\ (^_^) //\n"
                                                 + "                        Have a nice day!";

        private static readonly int[] AllowableAtlasSizes = new int[] { 256, 512, 1024, 2048, 4096 };
        private static readonly int MainTexShaderID = Shader.PropertyToID("_MainTex");
        private static readonly int TextureWidthShaderID = Shader.PropertyToID("_TextureWidth");
        private static readonly int TextureHeightShaderID = Shader.PropertyToID("_TextureHeight");
        

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *            Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [SerializeField, HandledInCustomInspector] private DocumentFetcherInfo[] documentFetcherInfo = { new() };

        [SerializeField, HandledInCustomInspector] public TMP_FontAsset[] fallbackFontSheets = Array.Empty<TMP_FontAsset>();
        
        [SerializeField, HandledInCustomInspector] private bool[] uniqueCharsLanguageSelect = new bool[1] { true };
        [SerializeField, HandledInCustomInspector] private string charactersToIncludeForEveryAtlas = "$€¥£元₩₹₽₺฿₪₱";
        [SerializeField, HandledInCustomInspector] private string uniqueCharactersForSelectedLanguages = string.Empty;
        

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *            Non-Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private List<LocLanguages> languagesImported = new List<LocLanguages>();
        
        private System.Action onFinished;
        private bool showFinishedDialog = true;
        private List<AutoGenerationStepHandler> autoGenerationSteps;
        private string charactersMissingDuringGeneration = string.Empty;

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *            Properties
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public bool HasInitialisedFontEngine { get; private set; } = false;

        public int TotalStepsCount { get { return autoGenerationSteps.Count; } }
        public float Progress { get { return (float)CurrentStepIndex / TotalStepsCount; } }

        public string FullStateContext { get; private set; } = "";
        public string CurrentStateContext { get; private set; } = "";

        public int CurrentStepIndex { get; private set; } = 0;
        public DocumentFetcherInfo[] DocumentFetchInfos { get { return documentFetcherInfo; } }
        public TMP_FontAsset[] FallbackFontSheets { get { return fallbackFontSheets; } }
        
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *            Unity Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [MenuItem(Configuration.MenuCategorisation + "Open Localisation Import Settings")]
        public static void SelectLanguageImportConfigFile()
        {
            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(Configuration.LocalisationImportConfigFilePath, typeof(UnityEngine.Object));
            Selection.activeObject = obj;
            EditorGUIUtility.PingObject(obj);
        }

        [MenuItem(Configuration.MenuCategorisation + "Open Google Sheets Document")]
        public static void OpenLocalisationGoogleSheetsDocument()
        {
            LocalisationImportHandler importHandler = AssetDatabase.LoadAssetAtPath(Configuration.LocalisationImportConfigFilePath, typeof(LocalisationImportHandler)) as LocalisationImportHandler;
            if (importHandler == null)
            {
                EditorUtility.DisplayDialog("Failed", $"Cannot find {nameof(LocalisationImportHandler)} at {Configuration.LocalisationImportConfigFilePath}", "Ok");
            }
            else
            {
                foreach (DocumentFetcherInfo documentFetcherInfo in importHandler.documentFetcherInfo)
                {
                    if (string.IsNullOrEmpty(documentFetcherInfo.googleDocId) == false)
                        Application.OpenURL($"https://docs.google.com/spreadsheets/d/{documentFetcherInfo.googleDocId}/edit");
                }
            }
        }

        [MenuItem(Configuration.MenuCategorisation + "Import Loc Table Data from Google", priority = 0)]
        public static void BeginImportProcessMenuOption()
        {
            LocalisationImportHandler importHandler = AssetDatabase.LoadAssetAtPath(Configuration.LocalisationImportConfigFilePath, typeof(LocalisationImportHandler)) as LocalisationImportHandler;
            if (importHandler == null)
            {
                EditorUtility.DisplayDialog("Failed", $"Cannot find {nameof(LocalisationImportHandler)} at {Configuration.LocalisationImportConfigFilePath}", "Ok");
            }
            else
            {
                importHandler.BeginLocalisationImportProcess(null);
            }
        }

        protected void EditorUpdate()
        {
            // This updates the Fetch Request and Automation Process over multiple frames. Each Part of the Automation process 
            //  has it's own methods assigned to it and updated here per frame.
            AutoGenerationStepHandlerDelegate updateMethod = autoGenerationSteps[CurrentStepIndex].methodToRun;
            if (updateMethod != null)
            {
                autoGenerationSteps[CurrentStepIndex].status = updateMethod();
            }

            if (autoGenerationSteps[CurrentStepIndex].status == StepResult.Running)
            {
                return;
            }

            if (autoGenerationSteps[CurrentStepIndex].status == StepResult.Failed)
            {
                // Quit Processing.
                UnityEditor.EditorApplication.update -= EditorUpdate;
                if (showFinishedDialog)
                    EditorUtility.DisplayDialog($"Failure", FailurePopupMessage, "Ok");
                else
                    throw new UnityException(FailurePopupMessage);
                OnProcessingFinished();
                return;
            }

            ++CurrentStepIndex;
            if (CurrentStepIndex >= TotalStepsCount)
            {
                // Finished Processing \(^_^)/
                UnityEditor.EditorApplication.update -= EditorUpdate;
                if (showFinishedDialog)
                    EditorUtility.DisplayDialog($"Success", SuccessPopupMessage, "Ok");
                OnProcessingFinished();
            }
        }

        /// <summary> Invoked when processing finished with either a Success or Failed status. </summary>
        private void OnProcessingFinished()
        {
            if (HasInitialisedFontEngine)
            {
                FontEngine.DestroyFontEngine();
                HasInitialisedFontEngine = false;
            }


            if (onFinished != null)
            {
                onFinished.Invoke();
                onFinished = null;
            }
            
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            LocManager.EditorRefreshCachedLocStrings();
            AssetDatabase.Refresh();
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *            Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Log the status of the Import process. </summary>
        private void SetCurrentStateContext(string _stateContext, LogType _logType = LogType.Log)
        {
            FullStateContext += _stateContext + "\n";
            CurrentStateContext = _stateContext;

            EditorUtility.DisplayProgressBar(ProgressBarWindowTitle, CurrentStateContext, Progress);

            if (_logType == LogType.Assert || _logType == LogType.Exception)
            {
                LocalisationOverrides.Debug.Assert(false, _stateContext);
            }
            else if (_logType == LogType.Error)
            {
                LocalisationOverrides.Debug.LogError(_stateContext);
            }
            else if (_logType == LogType.Warning)
            {
                LocalisationOverrides.Debug.LogWarning(_stateContext);
            }
            else
            {
                LocalisationOverrides.Debug.LogInfo(_stateContext);
            }
        }

        /// <summary> Gets the Language Import Configuration Data for the Selected Language </summary>
        /// <returns> A class containing the configuration data. </returns>
        private LocLanguagesInfo.LanguageImportData GetLanguageImportData(LocLanguages _language)
        {
            LocLanguagesInfo.LanguageImportData langImportData;
            if (LocLanguagesInfo.LocLanguageToImportInfo.TryGetValue(_language, out langImportData) == false)
                langImportData = new LocLanguagesInfo.LanguageImportData();

            return langImportData;
        }

        /// <summary> Gets all the unique characters for a language that could be found in the Localisation Strings file. </summary>
        /// <returns>Returns a list of uint values denoting the Unicode values for each of the unique characters found for the language.</returns>
        private uint[] FindAllUniqueCharsForLanguage(LocLanguages _language, LocLanguagesInfo.LanguageImportData _langImportData)
        {
            CultureInfo languageCulture;
            try
            {
                languageCulture = CultureInfo.GetCultureInfo(_langImportData.cultureVariantId);
            }
            catch
            {
                LocalisationOverrides.Debug.LogError($"{_language} has assigned [ {_langImportData.cultureVariantId} ] as its {nameof(_langImportData.cultureVariantId)}, however this is invalid. Please enter the correct Id.");
                return Array.Empty<uint>();
            }

            // Include default English and Punctuation as well... Commonly found in our Localisation when translating through google translate. So good to have so we can identify.
            //  Also the currencies for common markets, as these can show up in any language.
            string allTextFromLanguage = charactersToIncludeForEveryAtlas.Replace("\n", string.Empty)
                                                                         .Replace(" ", string.Empty)
                                                                         .Replace(char.ConvertFromUtf32(10), string.Empty);

            foreach (DocumentFetcherInfo docFetchInfo in DocumentFetchInfos)
            {
                string pathToLocStrings = docFetchInfo.csvFilePath;
                TextAsset textAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(pathToLocStrings);
                if (textAsset == null)
                {
                    Debug.LogError("Could not open Loc Table file Resources/" + pathToLocStrings + ". If it is present, check its icon in Unity- if it doesn't show as a text doc, then try right click->reimport.");
                    continue;
                }

                LocManager.LanguageStrings[] languageStrings = LocManager.LoadLanguageStringsFromLocTable(textAsset);

                int length = languageStrings.Length;
                for (int i = 0; i < length; ++i)
                {
                    // Get as is, then upper and lower chars because TextMeshPro has the option to force Uppercase/Lowercase which we will need to account for with Glyphs.
                    // Keeping it as is + Upper/Lower because we don't know how other languages behave when converting to/from Upper to Lower. So best to be safe. Duplicates will be filtered out.
                    string languageCellString = languageStrings[i].Get(_language);
                    allTextFromLanguage += languageCellString;
                    allTextFromLanguage += languageCellString.ToUpper(languageCulture);
                    allTextFromLanguage += languageCellString.ToLower(languageCulture);
                }
            }

            int finalIndexOfLanguageString = allTextFromLanguage.Length - 1;
            List<uint> charList = new List<uint>();
            for (int i = 0; i < allTextFromLanguage.Length; ++i)
            {
                uint unicode = allTextFromLanguage[i];

                // Handle surrogate pairs
                if (i < finalIndexOfLanguageString && char.IsHighSurrogate((char)unicode) && char.IsLowSurrogate(allTextFromLanguage[i + 1]))
                {
                    unicode = (uint)char.ConvertToUtf32(allTextFromLanguage[i], allTextFromLanguage[i + 1]);
                    ++i;
                }

                // Check to make sure we don't include duplicates
                if (charList.Contains(unicode) == false)
                {
                    charList.Add(unicode);
                }
            }

            uint[] uniqueCharacters = charList.ToArray();
            return uniqueCharacters;
        }

        /// <summary> Returns as a List of all languages using the Same Glyph Atlas. Unity will auto-unload the Glyph Sheet when function exits. So can only return a List of languages that reuse same glyph. </summary>
        private List<List<LocFontStyleAssociation>> FindWhichLanguagesAreUsingTheSameGlyphAtlas()
        {
            Dictionary<TMP_FontAsset, List<LocFontStyleAssociation>> glyphToLocLanguages = new Dictionary<TMP_FontAsset, List<LocFontStyleAssociation>>();

            LocLanguages[] allLanguages = (System.Enum.GetValues(typeof(LocLanguages)) as LocLanguages[])!;
            foreach (LocLanguages language in allLanguages)
            {
                for (int fontStyleIndex = 0; fontStyleIndex < LocManager.AllTextDisplayStylesCount; ++fontStyleIndex)
                {
                    LocFontStyleAssociation locFontStyleAssociation = new LocFontStyleAssociation()
                    {
                        fontStyle = (LocalisationTextDisplayStyleTypes)fontStyleIndex,
                        language = language,
                    };

					LocDisplayStyleFontInfo.TextMeshProLocFontInfo locFontInfo = locFontStyleAssociation.TextMeshProLocFontInfo;
                    if (locFontInfo == null || locFontInfo.font.editorAssetReference.Asset == null)
                    {
                        continue;
                    }

                    List<LocFontStyleAssociation> languagesUsingThisGlyph;
                    if (glyphToLocLanguages.TryGetValue(locFontInfo.font.editorAssetReference.Asset, out languagesUsingThisGlyph) == false)
                    {
                        languagesUsingThisGlyph = new List<LocFontStyleAssociation>();
                        glyphToLocLanguages.Add(locFontInfo.font.editorAssetReference.Asset, languagesUsingThisGlyph);
                    }

                    languagesUsingThisGlyph.Add(locFontStyleAssociation);
                }
            }

            return glyphToLocLanguages.Values.ToList();
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *          Step 00: Begin Import Process
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Called from the Unity Custom Inspector. Begins the Import Process. </summary>
        public void BeginLocalisationImportProcess(Action _onFinished = null, bool _showFinishedDialog = true)
        {
            onFinished = _onFinished;
            showFinishedDialog = _showFinishedDialog;

            CurrentStateContext = string.Empty;
            FullStateContext = string.Empty;

            charactersMissingDuringGeneration = string.Empty;

            // Setting up the auto generation steps. The System will execute each of the following methods until either all are complete, or one returns a "failed" status.
            autoGenerationSteps = new List<AutoGenerationStepHandler>()
            {
                new AutoGenerationStepHandler() { methodToRun = WaitForGoogleFetchResult },
                new AutoGenerationStepHandler() { methodToRun = ParseGoogleDocument },
                new AutoGenerationStepHandler() { methodToRun = ExportLocStringsTable },
                new AutoGenerationStepHandler() { methodToRun = RegenerateLocIDs },
            };

            // Setup "Generate Glyphs for each language" steps.
#if !DISABLE_LOCALISATION_ATLAS_GENERATION_OPTION && !USING_UNITY_LOCALISATION
            const string GenAtlasYes = "Yes, Generate";
            const string GenAtlasNo = "No, Skip";
            const string GenAtlasDescription = "Do you want to generate the atlases for each language?"
                                             + "\n\nNote:"
                                             + "\nYou only need to do this before you submit your build or when you change the Font Sheets being used for a language."
                                             + "\n\n'" + GenAtlasYes + "' = Slower Import. But safer"
                                             + "\n'" + GenAtlasNo + "' = Faster Import"
                                             + "\n\nIf you are seeing any issues with localisation characters, or if it is a build day, you should allow atlas generation.";

            if (EditorUtility.DisplayDialog("Generate Atlases?", GenAtlasDescription, GenAtlasYes, GenAtlasNo))
            {
                autoGenerationSteps.Add(new AutoGenerationStepHandler() { methodToRun = InitialiseFontEngine });

                List<List<LocFontStyleAssociation>> languagesUsingSameGlyphs = FindWhichLanguagesAreUsingTheSameGlyphAtlas();
                foreach (List<LocFontStyleAssociation> languagesForGlyph in languagesUsingSameGlyphs)
                {
                    autoGenerationSteps.Add(new AutoGenerationStepHandler()
                    {
                        methodToRun = () => GenerateFontAssets(languagesForGlyph)
                    });
                }

                autoGenerationSteps.Add(new AutoGenerationStepHandler()
                {
                    methodToRun = GenerateFallbackGlyph
                });
            }
#endif

            // Request Document from Google.
            CurrentStepIndex = 0;
            SetCurrentStateContext($"Started Import Process at: {System.DateTime.Now}");


            UnityEditor.EditorApplication.update += EditorUpdate;
            autoGenerationSteps[0].status = StepResult.Running;

            foreach (DocumentFetcherInfo documentFetcherElement in documentFetcherInfo)
            {
                SetCurrentStateContext($"Fetching from Google Server [Doc ID: {documentFetcherElement.googleDocId}] ");
                documentFetcherElement.spreadsheetHandler = GoogleAPI.FetchSpreadsheet(documentFetcherElement.googleDocId);
            }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *          Step 01: Wait for Response from Google
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Step 01, waiting for spreadsheet to be received from Google. </summary>
        private StepResult WaitForGoogleFetchResult()
        {
            float totalProgress = 1f;
            string status = "";
            foreach (DocumentFetcherInfo documentFetcherElement in documentFetcherInfo)
            {
                totalProgress *= documentFetcherElement.spreadsheetHandler.Progress;
                
                if (documentFetcherElement.spreadsheetHandler.HasFailed == false
                    && documentFetcherElement.spreadsheetHandler.IsCompleted == false)
                {
                    status += documentFetcherElement.spreadsheetHandler.Status;
                }
            }

            EditorUtility.DisplayProgressBar(ProgressBarWindowTitle, status, totalProgress);
            foreach (DocumentFetcherInfo documentFetcherElement in documentFetcherInfo)
            {
                if (documentFetcherElement.spreadsheetHandler.HasFailed)
                {
                    return StepResult.Failed;
                }
                if (documentFetcherElement.spreadsheetHandler.IsCompleted == false)
                {
                    return StepResult.Running;
                }
            }
            
            return StepResult.Success;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *          Step 02: Parse/Sanitise Google Spreadsheet
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> 
        /// Step 02, scans through the contents of the downloaded spreadsheet, removes the first cell (context) because we don't need it, 
        /// then ensures the LocIDs are not in an illegal format. 
        /// </summary>
        private StepResult ParseGoogleDocument()
        {
            SetCurrentStateContext($"Parsing Spreadsheet(s).");
            foreach (DocumentFetcherInfo documentFetcherElement in documentFetcherInfo)
            {
                documentFetcherElement.spreadsheetLocParser = new GoogleSpreadsheetLocParser();
                bool isParsed = documentFetcherElement.spreadsheetLocParser.ParseGoogleSpreadsheet(documentFetcherElement.spreadsheetHandler);

                foreach (string warning in documentFetcherElement.spreadsheetLocParser.WarningStatus)
                {
                    SetCurrentStateContext(warning, LogType.Warning);
                }

                if (isParsed == false)
                {
                    foreach (string error in documentFetcherElement.spreadsheetLocParser.ErrorStatus)
                    {
                        SetCurrentStateContext(error, LogType.Error);
                    }
                    return StepResult.Failed;
                }
            }

            return StepResult.Success;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *        Step 03: Export Sanitised Spreadsheet to Assets Folder
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Step 03: Export sanitised spreadsheet data to Assets folder. </summary>
        private StepResult ExportLocStringsTable()
        {
            foreach (DocumentFetcherInfo documentFetcherElement in documentFetcherInfo)
            {
                SetCurrentStateContext($"Exporting Data to Loc Table [{documentFetcherElement.csvFilePath}]");
                if (documentFetcherElement.spreadsheetLocParser.ExportLocStringsTable(documentFetcherElement.csvFilePath) == false)
                {
                    return StepResult.Failed;
                }
            }

            return StepResult.Success;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *        Step 04: Regenerate LocIDs Enum Values
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Step 04: Regenerate LocIDs Enum Values from the LocIDs column in the spreadsheet. </summary>
        private StepResult RegenerateLocIDs()
        {
            // Clearing progress bar here just in case the LocIds import cause compilation issues.
            EditorUtility.ClearProgressBar();

            SetCurrentStateContext($"Regenerating Loc Ids");
            if (GenerateLocalisationIDs.CreateLocalisationIDsClassFile() == false)
            {
                return StepResult.Failed;
            }

            return StepResult.Success;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *        Step 05: Initialise Font Engine
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Step 05: Initiate Font Engine. This will be used to help generate the glyph atlases in the following steps. </summary>
        private StepResult InitialiseFontEngine()
        {
            SetCurrentStateContext($"Initialising Font Engine");
            FontEngineError error = FontEngine.InitializeFontEngine();
            if (error != FontEngineError.Success)
            {
                return StepResult.Failed;
            }

            // Collect all TMP_FontAssets also, just before the next step.
            HasInitialisedFontEngine = true;
            return StepResult.Success;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *        Step 06 + : Generate Glyph Atlases
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Step 06 + : Generates the Font/Glyph Assets for the chosen Languages. </summary>
        private StepResult GenerateFontAssets(List<LocFontStyleAssociation> _locFontStyleAssociation)
        {
#if !UNITY_2019_1_OR_NEWER
            SetCurrentStateContext($"Skipping atlas generation for {_selectedLanguage} because this Unity Version is not supported.", LogType.Warning);
            return StepResult.Success;
#else
            List<(LocFontStyleAssociation locFontStyleAssociation, LocLanguagesInfo.LanguageImportData configuration)> usableLanguages = new List<(LocFontStyleAssociation locFontStyleAsscoiation, LocLanguagesInfo.LanguageImportData configuration)>();
            foreach (LocFontStyleAssociation language in _locFontStyleAssociation)
            {
                LocLanguagesInfo.LanguageImportData langImportData = GetLanguageImportData(language.language);
                if (langImportData == null)
                {
                    SetCurrentStateContext($"Could not find {nameof(LocLanguagesInfo.LanguageImportData)} for {language}. Please make sure it's defined in the {nameof(LocalisationImportHandler)} Scriptable (it'll get automatically generated for you if you just select the scriptable in the Unity Editor).", LogType.Error);
                    continue;
                }
                if (language.StandardLocFontInfo == null || language.StandardLocFontInfo.font.editorAssetReference.Asset == null)
                {
                    SetCurrentStateContext($"Could not find LocInfo for {language} for TextDisplay [{language.fontStyle}]. Please make sure it's defined in the {nameof(LocManager)}", LogType.Error);
                    continue;
                }
                if (language.TextMeshProLocFontInfo == null || language.TextMeshProLocFontInfo.font.editorAssetReference.Asset == null)
                {
                    SetCurrentStateContext($"Could not find LocInfo for {language} for TextDisplay [{language.fontStyle}]. Please make sure it's defined in the {nameof(LocManager)}", LogType.Error);
                    continue;
                }

                usableLanguages.Add((language, langImportData));
            }

            if (usableLanguages.Count == 0)
            {
                return StepResult.Failed;
            }

            List<uint> uniqueChars = new List<uint>();
            foreach ((LocFontStyleAssociation languageFontStyleAssociation, LocLanguagesInfo.LanguageImportData configuration) in usableLanguages)
            {
                SetCurrentStateContext($"Fetching all unique characters for {languageFontStyleAssociation.language}");
                uint[] uniqueCharsForLang = FindAllUniqueCharsForLanguage(languageFontStyleAssociation.language, configuration);
                uniqueChars.AddRange(uniqueCharsForLang);
            }

            // Removing same Unicode characters found in the additional languages. Multiple languages may reuse the same characters.
            uniqueChars = uniqueChars.Distinct().ToList();

            // Just need to get the TMP Font data for the first language since all of these languages are using the same settings.
            string selectedLanguagesAsString = string.Join(", ", usableLanguages.Select(x => $"{x.locFontStyleAssociation.language} ({x.locFontStyleAssociation.fontStyle})"));
            LanguageFontData[] languageFontDatas = new LanguageFontData[usableLanguages.Count];
            for (int i = 0; i < languageFontDatas.Length; ++i)
            {
				LocDisplayStyleFontInfo.StandardLocFontInfo textFontResource = usableLanguages[i].locFontStyleAssociation.StandardLocFontInfo;
                if (textFontResource == null || textFontResource.font.editorAssetReference.Asset == null)
				{
                    SetCurrentStateContext($"Standard Font Asset for language(s) [{selectedLanguagesAsString}] not found. This is required Even for TMPro Font Atlases. Please assign it", LogType.Error);
                    return StepResult.Failed;
                }
                
                languageFontDatas[i] = new LanguageFontData()
                {
                    sourceLanguageFont = textFontResource.font.editorAssetReference.Asset,
                    fontPath = AssetDatabase.GetAssetPath(textFontResource.font.editorAssetReference.Asset)
                };
            }

            // Create a new font asset
            string selectedFontsAsString = string.Join(", ", usableLanguages.Select(x => $"{x.locFontStyleAssociation.language} : {x.locFontStyleAssociation.StandardLocFontInfo.font.editorAssetReference.Asset.name} ({x.locFontStyleAssociation.fontStyle})"));
            SetCurrentStateContext($"Generating New Atlas for Font(s) [{selectedFontsAsString}]");
            LanguageAtlasData languageAtlasData = TryGenerateGlyphAtlas(usableLanguages, languageFontDatas[0], uniqueChars.ToArray());
            if (languageAtlasData == null || languageAtlasData.fontAsset.atlasTexture == null || languageAtlasData.fontAsset.material == null)
            {
                // Couldn't generate atlas. Errors will be printed from within the above function.
                return StepResult.Failed;
            }

            // Make it a persistent asset so it will be saved
            string fontFileDirectory = System.IO.Path.GetDirectoryName(languageFontDatas[0].fontPath)!;
            string fontAssetName = System.IO.Path.GetFileNameWithoutExtension(languageFontDatas[0].fontPath);
            string fontAssetPath = System.IO.Path.Combine(fontFileDirectory, fontAssetName);

            AssetDatabase.CreateAsset(languageAtlasData.fontAsset, fontAssetPath + ".asset");

            languageAtlasData.fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

            // Add font atlas to asset so it will be saved
            languageAtlasData.fontAsset.atlasTexture.name = fontAssetName + " Default Atlas";
            AssetDatabase.AddObjectToAsset(languageAtlasData.fontAsset.atlasTexture, languageAtlasData.fontAsset);

            // Add font material to asset so it will be saved
            string defaultMaterialName = fontAssetName + " Default Material";
            languageAtlasData.fontAsset.material.name = defaultMaterialName;
            AssetDatabase.CreateAsset(languageAtlasData.fontAsset.material, System.IO.Path.Combine(fontFileDirectory, defaultMaterialName + ".mat"));


            Material OutputValidMaterial(Material _currentAssignedMat)
            {
                if (_currentAssignedMat != null && string.IsNullOrEmpty(_currentAssignedMat.name) == false)
                {
                    // Checking if a Material is already assigned to the FontStyle. Assuming this material is not the
                    // default Material that gets embedded to the FontAtlas, then change a few key variables to keep it
                    // up to date and return the existing material.
                    if (_currentAssignedMat.name.Equals(defaultMaterialName) == false
                        && _currentAssignedMat.name.StartsWith("Assets") == false)
                    {
                        _currentAssignedMat.SetTexture(MainTexShaderID, languageAtlasData.fontAsset.atlasTexture);
                        _currentAssignedMat.SetFloat(TextureWidthShaderID, languageAtlasData.fontAsset.atlasWidth);
                        _currentAssignedMat.SetFloat(TextureHeightShaderID, languageAtlasData.fontAsset.atlasHeight);
                        EditorUtility.SetDirty(_currentAssignedMat);
                        return _currentAssignedMat;
                    }
                }
                return languageAtlasData.fontAsset.material;
            }

            // Reassigning new font asset back to font override scriptables (must be applied to all)
            for (int i = 0; i < languageFontDatas.Length; ++i)
            {
				LocDisplayStyleFontInfo.TextMeshProLocFontInfo textMeshFont = usableLanguages[i].locFontStyleAssociation.TextMeshProLocFontInfo;
                textMeshFont.font.editorAssetReference.Asset = languageAtlasData.fontAsset;
                textMeshFont.normalMaterial.editorAssetReference.Asset = OutputValidMaterial(textMeshFont.normalMaterial.editorAssetReference.Asset != null ? textMeshFont.normalMaterial.editorAssetReference.Asset : languageAtlasData.fontAsset.material);
                textMeshFont.strokedMaterial.editorAssetReference.Asset = OutputValidMaterial(textMeshFont.strokedMaterial.editorAssetReference.Asset != null ? textMeshFont.strokedMaterial.editorAssetReference.Asset : languageAtlasData.fontAsset.material);
                textMeshFont.underlinedMaterial.editorAssetReference.Asset = OutputValidMaterial(textMeshFont.underlinedMaterial.editorAssetReference.Asset != null ? textMeshFont.underlinedMaterial.editorAssetReference.Asset : languageAtlasData.fontAsset.material);
            }

            // Save all modified assets
            EditorUtility.SetDirty(languageAtlasData.fontAsset);            
            EditorUtility.SetDirty(LocManager.EditorGetInstance());

            SetCurrentStateContext($"Successfully Generated [{selectedLanguagesAsString}] Font Atlas at path [{fontAssetPath}] with FontSize={languageAtlasData.fontSize}, AtlasWidth={languageAtlasData.atlasWidth}, AtlasHeight={languageAtlasData.atlasHeight}, RenderMode={usableLanguages[0].configuration.renderMode}");
            return StepResult.Success;

#endif // !UNITY_2019_1_OR_NEWER
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *        Step 07 : Generate Fallback Glyph Atlases
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Step 07 : Generates the Font/Glyph Assets for the fallback. </summary>
        private StepResult GenerateFallbackGlyph()
        {
#if !UNITY_2019_1_OR_NEWER
            SetCurrentStateContext($"Skipping atlas generation for Fallback Glyphs because this Unity Version is not supported.", LogType.Warning);
            return StepResult.Success;
#else
            string allMissingCharacters = charactersMissingDuringGeneration;
            if (string.IsNullOrEmpty(allMissingCharacters))
            {
                SetCurrentStateContext("No missing characters found in existing Glyph Atlases");
                return StepResult.Success;
            }

            if (fallbackFontSheets == null || fallbackFontSheets.Any(f => f != null) == false)
            {
                SetCurrentStateContext("Characters are missing, but no fallback font sheets were found.", LogType.Error);
                return StepResult.Failed;
            }

            // Getting Unique Missing Chars
            int finalIndexOfLanguageString = allMissingCharacters.Length - 1;
            List<uint> charList = new List<uint>();
            for (int i = 0; i < allMissingCharacters.Length; ++i)
            {
                uint unicode = allMissingCharacters[i];

                // Handle surrogate pairs
                if (i < finalIndexOfLanguageString && char.IsHighSurrogate((char)unicode) && char.IsLowSurrogate(allMissingCharacters[i + 1]))
                {
                    unicode = (uint)char.ConvertToUtf32(allMissingCharacters[i], allMissingCharacters[i + 1]);
                    ++i;
                }

                // Check to make sure we don't include duplicates
                if (charList.Contains(unicode) == false)
                {
                    charList.Add(unicode);
                }
            }

            uint[] uniqueCharacters = charList.ToArray();

            for (int fallbackFontIndex = 0; fallbackFontIndex < fallbackFontSheets.Length; ++fallbackFontIndex)
            {
                TMP_FontAsset fallbackFont = fallbackFontSheets[fallbackFontIndex];
                if (fallbackFont == null)
                {
                    continue;
                }

                if (fallbackFont.sourceFontFile == null)
                {
                    FieldInfo fallbackFieldInfo = fallbackFont.GetType().GetField("m_SourceFontFile_EditorRef", BindingFlags.NonPublic | BindingFlags.Instance);
                    if (fallbackFieldInfo != null)
                    {
                        Font editorFallbackFont = fallbackFieldInfo.GetValue(fallbackFont) as Font;

                        fallbackFieldInfo = fallbackFont.GetType().GetField("m_SourceFontFile", BindingFlags.NonPublic | BindingFlags.Instance);
                        if (fallbackFieldInfo != null)
                        {
                            fallbackFieldInfo.SetValue(fallbackFont, editorFallbackFont);

                            if (fallbackFont.sourceFontFile == null)
                                continue;
                        }
                    }
                }

                if (string.Equals(fallbackFont.sourceFontFile.name, "Arial"))
                {
                    // Using Unity's Default Arial Font Sheet. This has every character. No need to regenerate.
                    return StepResult.Success;
                }

                // Removing Old Glyph without deleting the TMP_FontAsset reference itself.
                string assetPath = AssetDatabase.GetAssetPath(fallbackFont);
                fallbackFont = (AssetDatabase.LoadAssetAtPath(assetPath, typeof(TMP_FontAsset)) as TMP_FontAsset)!;


                uint[] stillMissingCharactersFromThisFont;
                if (fallbackFont.TryAddCharacters(uniqueCharacters, out stillMissingCharactersFromThisFont) == false && stillMissingCharactersFromThisFont != null)
                {
                    string missingGeneratedChars = string.Join(", ", stillMissingCharactersFromThisFont.Select(u => $"{char.ConvertFromUtf32((int)u)}"));
                    SetCurrentStateContext($"FallbackFont [{fallbackFontIndex}] cannot generate the following characters either [{missingGeneratedChars}]...", LogType.Warning);
                }

                assetPath = assetPath.Substring(0, assetPath.Length - ".asset".Length); // Removing .asset at the end.

                // Add font material to asset so it will be saved
                fallbackFont.material.name = assetPath + " Material";
                //AssetDatabase.AddObjectToAsset(fallbackFont.material, fallbackFont);

                // Add font atlas to asset so it will be saved
                fallbackFont.atlasTexture.name = assetPath + " Atlas";
                //AssetDatabase.AddObjectToAsset(fallbackFont.atlasTexture, fallbackFont);

                //AssetDatabase.CreateAsset(fallbackFont, assetPath + ".asset");
                EditorUtility.SetDirty(fallbackFont);

                uniqueCharacters = stillMissingCharactersFromThisFont;
                if (stillMissingCharactersFromThisFont == null || stillMissingCharactersFromThisFont.Length == 0)
                {
                    SetCurrentStateContext($"Missing characters successfully integrated into Fallback Glyphs");
                    return StepResult.Success;
                }
            }

            string remainingMissingCharacters = string.Join(", ", uniqueCharacters.Select(u => $"{char.ConvertFromUtf32((int)u)}"));
            SetCurrentStateContext($"Fallback Glyphs could not generate the following missing characters either [{remainingMissingCharacters}]. This is a problem as you will have missing characters in your game. "
                                + "Please add a new fallback atlas that can contain these characters", LogType.Error);
            return StepResult.Success;
#endif
        }

        private class AtlasGenerationInputOutputStruct
        {
            public List<(LocFontStyleAssociation locFontStyleAsscoiation, LocLanguagesInfo.LanguageImportData configuration)> selectedLanguages;
            public LanguageFontData languageFontData;
            public uint[] uniqueCharsToInsert;
            public int fontSize;
            public int atlasWidth;
            public int atlasHeight;
            public TMP_FontAsset fontAsset;
            public uint[] missingCharacters;
        }
        /// <summary> True to generate a Glyph Atlas for the desired language </summary>
        /// <param name="_selectedLanguages">Languages you wish to generate an atlas for.</param>
        /// <param name="_languageFontData">The Font Data already associated with this Language</param>
        /// <param name="uniqueCharsToInsert">The Unique Characters to add to this Glyph Atlas.</param>
        /// <returns>Returns null if the Atlas could not be generated. Otherwise returns data via the LanguageAtlasData class.</returns>
        private LanguageAtlasData TryGenerateGlyphAtlas(List<(LocFontStyleAssociation locFontStyleAsscoiation, LocLanguagesInfo.LanguageImportData configuration)> _selectedLanguages, LanguageFontData _languageFontData, uint[] _uniqueCharsToInsert)
        {
            // Only need to grab first Language Import Data since each language will reuse the same configuration for this Glyph
            AtlasGenerationInputOutputStruct atlasGenerationInputOutputStruct = new AtlasGenerationInputOutputStruct()
            {
                languageFontData = _languageFontData,
                uniqueCharsToInsert = _uniqueCharsToInsert,
                selectedLanguages = _selectedLanguages,
            };

            AtlasGenerationInputOutputStruct currentBestGenerationAttemptData = null;

            // Make sure we only pass in Unique characters. No need to generate glyphs for characters that already exist.
            int allowableAssetSizesCount = AllowableAtlasSizes.Length;

            // Whichever Text Display Style has the biggest Minimum Font size is our starting point.
            int minFontSize = 14; 
            for (int i = 0; i < _selectedLanguages.Count; ++i)
			{
                minFontSize = Mathf.Max(_selectedLanguages[i].locFontStyleAsscoiation.TextMeshProLocFontInfo.editorMinFontSize, minFontSize);
			}

            for (int allowedWidthIndex = 0; allowedWidthIndex < allowableAssetSizesCount; ++allowedWidthIndex)
            {
                atlasGenerationInputOutputStruct.atlasWidth = AllowableAtlasSizes[allowedWidthIndex];

                // Start from Max Font Size and move down until you reach the smooth spot. If no font at any of the applicable font 
                //   sizes can fit in the atlas at the current atlasWidth/height. Pass though again with a bigger atlas and try again.
                // # Condition : Less than or Equal to allowedWidthIndex, this will attempt to keep assets as close to the same size as possible.
                for (int allowedHeightIndex = 0; allowedHeightIndex <= allowedWidthIndex; ++allowedHeightIndex)
                {
                    bool allCharactersImplemented = false;
                    atlasGenerationInputOutputStruct.fontSize = minFontSize;
                    while (true)
                    {                        
                        atlasGenerationInputOutputStruct.atlasHeight = AllowableAtlasSizes[allowedHeightIndex];
                        StepResult result = TryGenerateGlyphAtlasWithSize(ref atlasGenerationInputOutputStruct);
                        if (result == StepResult.Failed)
                        {
                            // When SUCCESS, details are stored in the AtlasGenerationStruct
                            // When FAILED, since it fail on any generation attempt then it genuinely cannot create an atlas with this dataset. No point trying any further.
                            // In the case of an atlas not being suitable, the code will return RUNNING instead, as it can still create an atlas, just not with the provided atlas size.
                            return new LanguageAtlasData()
                            {
                                fontAsset = atlasGenerationInputOutputStruct.fontAsset,
                                fontSize = atlasGenerationInputOutputStruct.fontSize,
                                atlasWidth = atlasGenerationInputOutputStruct.atlasWidth,
                                atlasHeight = atlasGenerationInputOutputStruct.atlasHeight
                            };
                        }
                        else
                        {
                            if (atlasGenerationInputOutputStruct.missingCharacters == null)
                            {
                                allCharactersImplemented = true;
                                
                                // Store this generation. We have found a winner. But keep trying with higher font sizes to see how far we can push it.
                                currentBestGenerationAttemptData = new AtlasGenerationInputOutputStruct()
                                {
                                    languageFontData = _languageFontData,
                                    uniqueCharsToInsert = _uniqueCharsToInsert,
                                    selectedLanguages = _selectedLanguages,
                                    fontSize = atlasGenerationInputOutputStruct.fontSize,
                                    atlasWidth = atlasGenerationInputOutputStruct.atlasWidth,
                                    atlasHeight = atlasGenerationInputOutputStruct.atlasHeight,
                                    missingCharacters = new List<uint>(atlasGenerationInputOutputStruct.missingCharacters!).ToArray(),
                                };
                            }
                            else
							{
                                if (allCharactersImplemented)
								{
                                    TryGenerateGlyphAtlasWithSize(ref currentBestGenerationAttemptData);
                                    return new LanguageAtlasData()
                                    {
                                        fontAsset = currentBestGenerationAttemptData.fontAsset,
                                        fontSize = currentBestGenerationAttemptData.fontSize,
                                        atlasWidth = currentBestGenerationAttemptData.atlasWidth,
                                        atlasHeight = currentBestGenerationAttemptData.atlasHeight
                                    };
                                }

                                // Best generation so far? Store it! if we continue to have missing characters after trying all atlas sizes. Come back to this one.
                                if (currentBestGenerationAttemptData == null || currentBestGenerationAttemptData.missingCharacters == null || currentBestGenerationAttemptData.missingCharacters.Length > atlasGenerationInputOutputStruct.missingCharacters.Length)
                                {
                                    currentBestGenerationAttemptData = new AtlasGenerationInputOutputStruct()
                                    {
                                        languageFontData = _languageFontData,
                                        uniqueCharsToInsert = _uniqueCharsToInsert,
                                        selectedLanguages = _selectedLanguages,
                                        fontSize = atlasGenerationInputOutputStruct.fontSize,
                                        atlasWidth = atlasGenerationInputOutputStruct.atlasWidth,
                                        atlasHeight = atlasGenerationInputOutputStruct.atlasHeight,
                                        missingCharacters = (new List<uint>(atlasGenerationInputOutputStruct.missingCharacters)).ToArray(),
                                    };
                                }

                                // Try next Font Atlas Size.
                                break;

                            }
                        }

                        ++atlasGenerationInputOutputStruct.fontSize;
                    }
                }
            }

            // Atlas Generation Failure
            if (atlasGenerationInputOutputStruct.missingCharacters != null && atlasGenerationInputOutputStruct.missingCharacters.Length > 0)
            {
				LocDisplayStyleFontInfo.TextMeshProLocFontInfo textMeshFont = _selectedLanguages[0].locFontStyleAsscoiation.TextMeshProLocFontInfo;
                string selectedLanguages = string.Join(", ", _selectedLanguages.Select(x => x.locFontStyleAsscoiation.language.ToString()));
                foreach (uint g in atlasGenerationInputOutputStruct.missingCharacters)
                {
                    charactersMissingDuringGeneration += $"{char.ConvertFromUtf32((int)g)}";
                    LocalisationOverrides.Debug.LogWarning($"{selectedLanguages} Atlas Generation Error: Missing glyph {g:X4} '{char.ConvertFromUtf32((int)g)}' into font {textMeshFont.font.editorAssetReference.Asset.name}");
                }
            }

            TryGenerateGlyphAtlasWithSize(ref currentBestGenerationAttemptData);
            return new LanguageAtlasData()
            {
                fontAsset = currentBestGenerationAttemptData.fontAsset,
                fontSize = currentBestGenerationAttemptData.fontSize,
                atlasWidth = currentBestGenerationAttemptData.atlasWidth,
                atlasHeight = currentBestGenerationAttemptData.atlasHeight
            };
        }

        private StepResult TryGenerateGlyphAtlasWithSize(ref AtlasGenerationInputOutputStruct _atlasGenerationInputOutputStruct)
        {
            _atlasGenerationInputOutputStruct.missingCharacters = null;
            _atlasGenerationInputOutputStruct.fontAsset = null;

            LocLanguagesInfo.LanguageImportData langImportConfigurationData = _atlasGenerationInputOutputStruct.selectedLanguages[0].configuration;

            try
            {
                // Can error when trying to create a Font Asset if the Font Sheet isn't set up correctly.
                _atlasGenerationInputOutputStruct.fontAsset = TMP_FontAsset.CreateFontAsset(font:                   _atlasGenerationInputOutputStruct.languageFontData.sourceLanguageFont,
                                                                                           samplingPointSize:       _atlasGenerationInputOutputStruct.fontSize,
                                                                                           atlasPadding:            langImportConfigurationData.padding,
                                                                                           renderMode:              langImportConfigurationData.renderMode,
                                                                                           atlasWidth:              _atlasGenerationInputOutputStruct.atlasWidth,
                                                                                           atlasHeight:             _atlasGenerationInputOutputStruct.atlasHeight,
                                                                                           atlasPopulationMode:     AtlasPopulationMode.Dynamic,
                                                                                           enableMultiAtlasSupport: false);
            }
            catch (System.Exception e)
            {
                SetCurrentStateContext($"Font asset could not be created for Languages [{string.Join(", ", _atlasGenerationInputOutputStruct.selectedLanguages.Select(a => a.locFontStyleAsscoiation.language))}]: Error = {e.Message}", LogType.Error);
            }
            if (_atlasGenerationInputOutputStruct.fontAsset == null)
            {
                SetCurrentStateContext($"Font asset could not be created for Languages [{string.Join(", ", _atlasGenerationInputOutputStruct.selectedLanguages.Select(a => a.locFontStyleAsscoiation.language))}]", LogType.Error);
                return StepResult.Failed;
            }

			// ~ Including Fallback Fonts for these glyphs atlases ~
			LocDisplayStyleFontInfo.TextMeshProLocFontInfo textMeshFont = _atlasGenerationInputOutputStruct.selectedLanguages[0].locFontStyleAsscoiation.TextMeshProLocFontInfo;
            
            if (textMeshFont.font.editorAssetReference.Asset.fallbackFontAssetTable != null)
            {
                _atlasGenerationInputOutputStruct.fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>(textMeshFont.font.editorAssetReference.Asset.fallbackFontAssetTable);
            }
            else
            {
                _atlasGenerationInputOutputStruct.fontAsset.fallbackFontAssetTable = new List<TMP_FontAsset>();
            }

            foreach (TMPro.TMP_FontAsset fallbackFont in fallbackFontSheets)
            {
                if (fallbackFont != null && _atlasGenerationInputOutputStruct.fontAsset.fallbackFontAssetTable.Contains(fallbackFont) == false)
                {
                    _atlasGenerationInputOutputStruct.fontAsset.fallbackFontAssetTable.Add(fallbackFont);
                }
            }

            // Ensure the settings of the new Font Asset are the same as our current one.
            _atlasGenerationInputOutputStruct.fontAsset.tabSize = textMeshFont.font.editorAssetReference.Asset.tabSize;
            _atlasGenerationInputOutputStruct.fontAsset.creationSettings = new FontAssetCreationSettings()
            {
                atlasWidth = _atlasGenerationInputOutputStruct.atlasWidth,
                atlasHeight = _atlasGenerationInputOutputStruct.atlasHeight,
                renderMode = (int)langImportConfigurationData.renderMode,
                characterSetSelectionMode = 7, // 7 => Custom Characters
                fontStyle = textMeshFont.font.editorAssetReference.Asset.creationSettings.fontStyle,
                fontStyleModifier = textMeshFont.font.editorAssetReference.Asset.creationSettings.fontStyleModifier,
                includeFontFeatures = textMeshFont.font.editorAssetReference.Asset.creationSettings.includeFontFeatures,
                packingMode = 4, // 4 => Optimum
                padding = langImportConfigurationData.padding,
                pointSize = _atlasGenerationInputOutputStruct.fontSize,
                pointSizeSamplingMode = 1, // 1 => Custom Size (use our defined Font Size above)
                sourceFontFileGUID = textMeshFont.font.editorAssetReference.Asset.creationSettings.sourceFontFileGUID,
                sourceFontFileName = textMeshFont.font.editorAssetReference.Asset.creationSettings.sourceFontFileName
            };

            try
            {
                if (_atlasGenerationInputOutputStruct.fontAsset.TryAddCharacters(_atlasGenerationInputOutputStruct.uniqueCharsToInsert, out _atlasGenerationInputOutputStruct.missingCharacters) == false)
                {
                    LocalisationOverrides.Debug.LogInfo($"Failed to generate Languages [{string.Join(", ", _atlasGenerationInputOutputStruct.selectedLanguages.Select(a => a.locFontStyleAsscoiation.language))}] atlas at size {_atlasGenerationInputOutputStruct.atlasWidth} x {_atlasGenerationInputOutputStruct.atlasHeight} at font size {_atlasGenerationInputOutputStruct.fontSize}");
                    return StepResult.Running;
                }
            }
            catch (System.Exception e)
            {
                LocalisationOverrides.Debug.LogError($"Error while generating atlas for Languages [{string.Join(", ", _atlasGenerationInputOutputStruct.selectedLanguages.Select(a => a.locFontStyleAsscoiation.language))}]: {e.Message}");
                return StepResult.Running;
            }

            Texture2D[] atlases = _atlasGenerationInputOutputStruct.fontAsset.atlasTextures;
            if (atlases.Length > 1)
            {
                // Atlas size is not large enough to fit all glyphs on a single texture. Go for next size up.
                return StepResult.Running;
            }

            // Atlas Generation Success. All characters fit on a single texture at the current size.
            return StepResult.Success;
        }
    }
}
