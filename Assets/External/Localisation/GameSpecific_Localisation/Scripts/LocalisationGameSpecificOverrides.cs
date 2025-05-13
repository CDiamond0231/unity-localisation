//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//              Localisation Game Specific Overrides
//              Author: Christopher Allport
//              Date Created: August 7, 2020
//              Last Updated: --------------
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//		  This Script defines the const configurations for the Localisation
//      system. This includes defining paths, which keyboard keys activate
//      debug commands, and which languages are Right-to-Left, etc.
//
//        Additionally, there are two additional static classes besides the
//      configuration class that defines how the Localisation system should
//      output debug logs and how to save. These are using Unity's default
//      logging system and PlayerPrefs system. If your game uses different
//      systems compared to these, please overwrite the functions to use your
//      versions.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

using System.Collections.Generic;
using Localisation.Localisation;

namespace Localisation.LocalisationOverrides
{
    public static class Configuration
    {
        /// <summary> The Context Menu Categorisation String. </summary>
        public const string MenuCategorisation = "Localisation/Localisation " + LocManager.LocVersion + ".../";

        /// <summary> Saving currently selected language with the following Save Key </summary>
        public const string SelectedLanguagePlayerPrefsKey = "CurrentLanguage";

        /// <summary> Default value: Press '`' (or '~') key to switch the visible language at any time. </summary>
        public const UnityEngine.KeyCode SwitchLanguageDebugKeyCode = UnityEngine.KeyCode.BackQuote;

        /// <summary> Path to Root Localisation Repo in Project. </summary>
        public const string PathToRootLocalisationRepo = "Assets/External/Localisation/";

        /// <summary> Path to Local Localisation Repo in Project. </summary>
        public const string PathToPackageLocalisationRepo = PathToRootLocalisationRepo + "Package_Localisation/";

        /// <summary> Path to System Localisation Textures Folder. </summary>
        public const string PathToLocalisationTexturesFolder = PathToPackageLocalisationRepo + "Textures/";


        /// <summary> Path to Local (Game Specific) Localisation Repo in Project. </summary>
        public const string PathToGameSpecificLocalisationRepo = PathToRootLocalisationRepo + "GameSpecific_Localisation/";

        /// <summary> Path to Localisation LocTables Folder. </summary>
        public const string PathToGameSpecificLocalisationTablesFolder = PathToGameSpecificLocalisationRepo + "LocTables/";

        /// <summary> Path to Game Specific Loc Prefabs Folder. </summary>
        public const string PathToGameSpecificLocPrefabs = PathToGameSpecificLocalisationRepo + "Prefabs/";

        /// <summary> Path to GameSpecific Localisation Resources Folder. </summary>
        public const string PathToGameSpecificLocalisationResourcesFolder = PathToGameSpecificLocalisationRepo + "Resources/";

        /// <summary> Path to GameSpecific Localisation Scripts Folder. </summary>
        public const string PathToGameSpecificLocalisationScriptsFolder = PathToGameSpecificLocalisationRepo + "Scripts/";


        /// <summary> Loaded from the resources folder, so filename must not have extension. Also needs to include the folder hierarchy in the filename if it exists. </summary>
        public const string MasterLocStringsTableFileName = "Loc_Strings";
        public const string MasterLocStringsTableFileExt = ".csv";
        public const string MasterLocStringsTableFilePath = PathToGameSpecificLocalisationTablesFolder + MasterLocStringsTableFileName + MasterLocStringsTableFileExt;

        /// <summary> All Loc Strings Tables; This is loaded and checked in order, so if you want to check another Loc doc before the master doc for
        /// a Loc String, change the order so the master doc is lower. This is loaded from the Resources folder, therefore, must not have the file extension included. </summary>
        public static readonly string[] LocStringsTablesFilePaths = new string[]
        {
            MasterLocStringsTableFileName,
        };

        /// <summary> Auto-generated LocIDs enum csharp file name & path. </summary>
        public const string LocalisationIDsFileName = "LocIDs";
        public const string LocalisationIDsFileExt = ".cs";
        public const string LocalisationIDsFilePath = PathToGameSpecificLocalisationScriptsFolder + LocalisationIDsFileName + LocalisationIDsFileExt;

        /// <summary> Auto-generated Game Specific LocIDs enum csharp file name & path. </summary>
        public const string GameSpecificLocalisationIDsFileName = "GameSpecificLocIDData";
        public const string GameSpecificLocalisationIDsFileExt = ".cs";
        public const string GameSpecificLocalisationIDsFilePath = PathToGameSpecificLocalisationScriptsFolder + GameSpecificLocalisationIDsFileName + GameSpecificLocalisationIDsFileExt;

        /// <summary> Auto-generated LocLanguages enum csharp file name & path. </summary>
        public const string LocalisationLanguagesFileName = "LocLanguages";
        public const string LocalisationLanguagesFileExt = ".cs";
        public const string LocalisationLanguagesFilePath = PathToGameSpecificLocalisationScriptsFolder + LocalisationLanguagesFileName + LocalisationLanguagesFileExt;

        /// <summary> Loc Table Icon Path. </summary>
        public const string LocTableIconTextureFileName = "LocTable_Icon";
        public const string LocTableIconTextureFileExt = ".tga";
        public const string LocTableIconTextureFilePath = PathToLocalisationTexturesFolder + LocTableIconTextureFileName + LocTableIconTextureFileExt;

        /// <summary> Localisation Import Configuration File Path. </summary>
        public const string LocalisationImportConfigFileName = "LocalisationImportConfig";
        public const string LocalisationImportConfigFilePath = PathToGameSpecificLocalisationRepo + "Editor/" + LocalisationImportConfigFileName + ".asset";

        /// <summary> Localisation Languages Configuration File Path. </summary>
        public const string LocalisationLanguagesConfigFileName = "LocLanguagesConfig";
        public const string LocalisationLanguagesConfigFilePath = PathToGameSpecificLocalisationRepo + "Editor/" + LocalisationLanguagesConfigFileName + ".asset";
    }


    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    //	* Save System Override
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    public static class SaveSystem
    {
        public static void Save()
        {
            UnityEngine.PlayerPrefs.Save();
        }

        public static int GetInt(string _key, int _defaultValue = 0)
        {
            return UnityEngine.PlayerPrefs.GetInt(_key, _defaultValue);
        }

        public static void SetInt(string _key, int _value)
        {
            UnityEngine.PlayerPrefs.SetInt(_key, _value);
        }

        public static string GetString(string _key, string _defaultValue = "")
        {
            return UnityEngine.PlayerPrefs.GetString(_key, _defaultValue);
        }

        public static void SetString(string _key, string _value)
        {
            UnityEngine.PlayerPrefs.SetString(_key, _value);
        }
    }


    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    //	* Debug Logging Override
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    public static class Debug
    {
        public static void LogInfo(string _msg, UnityEngine.Object _context = null)
        {
            UnityEngine.Debug.Log(_msg, _context);
        }

        public static void LogWarning(string _msg, UnityEngine.Object _context = null)
        {
            UnityEngine.Debug.LogWarning(_msg, _context);
        }

        public static void LogError(string _msg, UnityEngine.Object _context = null)
        {
            UnityEngine.Debug.LogError(_msg, _context);
        }

        public static void Assert(bool _condition, string _msg, UnityEngine.Object _context = null)
        {
            UnityEngine.Debug.Assert(_condition, _msg, _context);
        }
    }
}