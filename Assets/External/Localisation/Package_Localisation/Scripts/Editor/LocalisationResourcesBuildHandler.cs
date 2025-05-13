//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Localisation Resources Build Handler
//             Author: Christopher Allport
//             Date Created: 27th May, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Helper Class to handle the resolution of Resources for the Loc
//      System during a build (PreProcess and PostProcess)
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Localisation.LocalisationOverrides;
using TMPro;
using UnityEditor;
using UnityEngine;
using static Localisation.Localisation.LocDisplayStyleFontInfo;
using Debug = UnityEngine.Debug;

namespace Localisation.Localisation.Editor
{
	public static class LocalisationResourcesBuildHandler
	{
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Definitions
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private class ResourceInfo
		{
            /// <summary> The Asset assigned to this Resource (Direct Resource Reference). </summary>
            public UnityEngine.Object asset = null;
            /// <summary> Setter for the Resource Key. Used to load the asset from the resources folder during gameplay. </summary>
            public List<System.Action<string>> resourcesKeySetter = new List<System.Action<string>>();

#if LOCALISATION_ADDRESSABLES
            /// <summary> Setter for the Addressables Key. Used to load the asset from the server/cache during gameplay. </summary>
            public List<System.Action<string>> addressablesKeySetter = new List<System.Action<string>>();
#endif
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Consts
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private static readonly string OriginalResourcePathsTrackerFile = $"{Application.persistentDataPath}/{Application.productName}_{EditorUserBuildSettings.activeBuildTarget}_LocResourcePaths.json";

		private static readonly LocLanguages[] AllLocLanguages = System.Enum.GetValues(typeof(LocLanguages)) as LocLanguages[];
		private static readonly LocalisationTextDisplayStyleTypes[] AllTextStyles = System.Enum.GetValues(typeof(LocalisationTextDisplayStyleTypes)) as LocalisationTextDisplayStyleTypes[];


        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Moves Loc Assets To Resources. </summary>
        public static void AssignLocAssetsToResourcesAndAddressables()
		{
            // ~~~~~~~~~~ Getting Addressables Group ~~~~~~~~~~ //
            // Clearing the Addressable Entries for the Localisation (clearing unused assets if they are hanging around)
#if LOCALISATION_ADDRESSABLES
            const string LocAssetsAddressablesGroup = "LocalisationAssets";
            var addressableSettings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            var addressableGroup = addressableSettings.FindGroup(LocAssetsAddressablesGroup);

            if (addressableGroup == null)
			{
                throw new System.InvalidOperationException($"LOCALISATION: Addressables Group \"{LocAssetsAddressablesGroup}\" could not be located. This is CaseSensitive so please rename if this is the issue. If this is the first time you are hearing about this. Please consult the confluence document on setup steps for Localisation.");
            }

            List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry> existingAddressableEntries = new List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry>(addressableGroup.entries);
            foreach (UnityEditor.AddressableAssets.Settings.AddressableAssetEntry addressableEntry in existingAddressableEntries)
            {
                addressableGroup.RemoveAssetEntry(addressableEntry, false);
            }

            EditorUtility.SetDirty(addressableGroup);
#endif

            // ~~~~~~~~~~ Fetching all assets used by Localisation ~~~~~~~~~~ //
            List<UnityEngine.Object> requiredResources;
            List<ResourceInfo> allLocResourcesInfo;
            FetchEditorLocAssetsForLanguages(AllLocLanguages, out requiredResources, out allLocResourcesInfo);

			if (Directory.Exists(LocalisationOverrides.Configuration.PathToGameSpecificLocalisationResourcesFolder) == false)
			{
				Directory.CreateDirectory(LocalisationOverrides.Configuration.PathToGameSpecificLocalisationResourcesFolder);
				AssetDatabase.Refresh();
			}

            Dictionary<string, string> resourceKeyToOriginalResourcePath = new Dictionary<string, string>();

            

            // ~~~~~~~~~~ Moves the Loc Assets to Resources/Addressables ~~~~~~~~~~ //
            foreach (ResourceInfo resourceInfo in allLocResourcesInfo)
			{
                if (resourceInfo.asset == null)
                    continue;

                string originalPath = AssetDatabase.GetAssetPath(resourceInfo.asset);
                if (string.IsNullOrEmpty(originalPath))
                    continue;

                string extension = Path.GetExtension(originalPath);

                // Resources is provided as the full path because there may be a case where Loc Assets share the same name but are in a different folder. Will cause issues when merged together.
                // Also saved with extension for the same issue in the case that the same folder contains two duplicate names with just different file extensions.
                // Resources requires not using file extensions when loading... so I'm just modifying the file name to include the extension to get around that.
                string resourcesKey = Regex.Replace(originalPath, "\\/|\\\\|\\s|-", "_");
                resourcesKey = resourcesKey.Substring(0, resourcesKey.Length - extension.Length);
                if (string.IsNullOrEmpty(extension) == false)
                    resourcesKey += $"_{extension.Substring(1)}";

#if LOCALISATION_ADDRESSABLES
                string assetGuid = AssetDatabase.AssetPathToGUID(originalPath);
                var entry = addressableSettings.CreateOrMoveEntry(assetGuid, addressableGroup);

                entry.SetAddress(resourcesKey);
                entry.SetLabel("Localisation", true, true);                

                foreach (var addressablesKeySetter in resourceInfo.addressablesKeySetter)
                {
                    addressablesKeySetter?.Invoke(resourcesKey);
                }
#else
                AssetDatabase.MoveAsset(originalPath, LocalisationOverrides.Configuration.PathToGameSpecificLocalisationResourcesFolder + resourcesKey + extension);
                resourceKeyToOriginalResourcePath[resourcesKey + extension] = originalPath;

                foreach (var resourceKeySetter in resourceInfo.resourcesKeySetter)
                {
                    resourceKeySetter?.Invoke(resourcesKey);
                }
#endif
            }

            // ~~~~~~~~~~ Move Default Assets to Resources (even if using addressables) ~~~~~~~~~~ //
            // Even if we are using addressables, move default assets to resources so we can load
            // them in the case that Addressables cannot be loaded so we have something to fallback on
#if LOCALISATION_ADDRESSABLES
            const string LocRequiredAssetsAddressablesGroup = "LocalisationRequiredResources";
            addressableGroup = addressableSettings.FindGroup(LocRequiredAssetsAddressablesGroup);
            if (addressableGroup == null)
			{
                throw new System.InvalidOperationException($"LOCALISATION: Addressables Group \"{LocRequiredAssetsAddressablesGroup}\" could not be located. This is CaseSensitive so please rename if this is the issue. If this is the first time you are hearing about this. Please consult the confluence document on setup steps for Localisation.");
            }
            existingAddressableEntries = new List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry>(addressableGroup.entries);
            foreach (UnityEditor.AddressableAssets.Settings.AddressableAssetEntry addressableEntry in existingAddressableEntries)
            {
                addressableGroup.RemoveAssetEntry(addressableEntry, false);
            }
#endif

            foreach (UnityEngine.Object asset in requiredResources)
            {
                if (asset == null)
                    continue;

                string originalPath = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(originalPath))
                    continue;

                string resourcesKey = Path.GetFileNameWithoutExtension(originalPath);
                string extension = Path.GetExtension(originalPath);

                string newPath = LocalisationOverrides.Configuration.PathToGameSpecificLocalisationResourcesFolder + resourcesKey + extension;
                AssetDatabase.MoveAsset(originalPath, newPath);
                resourceKeyToOriginalResourcePath[resourcesKey + extension] = originalPath;

                // Also adding the resource to addressables. If the Loc System has already moved it to addressables, ignore it.
#if LOCALISATION_ADDRESSABLES
                string assetGuid = AssetDatabase.AssetPathToGUID(newPath);
                var entry = addressableSettings.FindAssetEntry(assetGuid);
                if (entry == null || entry.TargetAsset == null)
                {
                    entry = addressableSettings.CreateOrMoveEntry(assetGuid, addressableGroup);
                    entry.SetAddress(resourcesKey);
                    entry.SetLabel("Localisation", true, true);
                }
#endif
            }
            
            // ~~~~~~~~~~ Move Default Assets to Resources (even if using addressables) ~~~~~~~~~~ //
            // Even if we are using addressables, move default assets to resources so we can load
            // them in the case that Addressables cannot be loaded so we have something to fallback on
#if LOCALISATION_ADDRESSABLES
            const string LocEnglishAssetsAddressablesGroup = "LocalisationEnglishAssets";
            
            FetchEditorLocAssetsForLanguages(new LocLanguages[] { LocLanguages.English }, out requiredResources, out allLocResourcesInfo);
            addressableGroup = addressableSettings.FindGroup(LocEnglishAssetsAddressablesGroup);
            if (addressableGroup == null)
            {
                throw new System.InvalidOperationException($"LOCALISATION: Addressables Group \"{LocEnglishAssetsAddressablesGroup}\" could not be located. This is CaseSensitive so please rename if this is the issue. If this is the first time you are hearing about this. Please consult the confluence document on setup steps for Localisation.");
            }
            existingAddressableEntries = new List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry>(addressableGroup.entries);
            foreach (UnityEditor.AddressableAssets.Settings.AddressableAssetEntry addressableEntry in existingAddressableEntries)
            {
                addressableGroup.RemoveAssetEntry(addressableEntry, false);
            }

            foreach (ResourceInfo resourceInfo in allLocResourcesInfo)
            {
                if (resourceInfo.asset == null)
                    continue;
                
                string originalPath = AssetDatabase.GetAssetPath(resourceInfo.asset);
                if (string.IsNullOrEmpty(originalPath))
                    continue;
                
                string extension = Path.GetExtension(originalPath);
                
                string assetGuid = AssetDatabase.AssetPathToGUID(originalPath);
                var entry = addressableSettings.CreateOrMoveEntry(assetGuid, addressableGroup);

                string resourcesKey = Regex.Replace(originalPath, "\\/|\\\\|\\s|-", "_");
                resourcesKey = resourcesKey.Substring(0, resourcesKey.Length - extension.Length);
                if (string.IsNullOrEmpty(extension) == false)
                    resourcesKey += $"_{extension.Substring(1)}";
                
                entry.SetAddress(resourcesKey);
                entry.SetLabel("Localisation", true, true);                

                foreach (var addressablesKeySetter in resourceInfo.addressablesKeySetter)
                {
                    addressablesKeySetter?.Invoke(resourcesKey);
                }
            }
#endif

            // ~~~~~~~~~~ Store a Temp file in the temp directory ~~~~~~~~~~ //
            // When the PostProcessor runs to return the assets back to their original locations it will read this file.
            LocManager locManager = LocManager.EditorGetInstance();
			EditorUtility.SetDirty( locManager );

            try
			{
                if (File.Exists(OriginalResourcePathsTrackerFile))
                    File.Delete(OriginalResourcePathsTrackerFile);

                using (StreamWriter writer = new StreamWriter(OriginalResourcePathsTrackerFile, false))
                    writer.Write(JsonConvert.SerializeObject(resourceKeyToOriginalResourcePath, Formatting.Indented));

                Debug.Log($"Output Temp Loc File to \"{OriginalResourcePathsTrackerFile}\"");
            }
            catch (System.Exception e)
			{
                Debug.LogError($"Error Writing to [{OriginalResourcePathsTrackerFile}]: {e.Message}");
			}
             

			AssetDatabase.SaveAssets();
			AssetDatabase.Refresh();
		}

		/// <summary> Moves Loc Resources to Assets (previous location). </summary>
		public static void MoveLocResourcesBackToAssets()
		{
            if (Directory.Exists(LocalisationOverrides.Configuration.PathToGameSpecificLocalisationResourcesFolder) == false)
            {
                return;
            }
            if (File.Exists(OriginalResourcePathsTrackerFile) == false)
			{
                return;
			}

            Dictionary<string, string> resourceKeyToOriginalResourcePath = new Dictionary<string, string>();
            try
            {
                using (StreamReader reader = new StreamReader(OriginalResourcePathsTrackerFile))
                {
                    resourceKeyToOriginalResourcePath = JsonConvert.DeserializeObject<Dictionary<string, string>>( reader.ReadToEnd() )!;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error Reading from [{OriginalResourcePathsTrackerFile}]: {e.Message}");
            }

            foreach (KeyValuePair<string, string> pair in resourceKeyToOriginalResourcePath)
			{
                string resourceFileWithExt = pair.Key;
                string originalPath = pair.Value;
                AssetDatabase.MoveAsset($"{LocalisationOverrides.Configuration.PathToGameSpecificLocalisationResourcesFolder}{resourceFileWithExt}", originalPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// Outputs the Font Asset References for the following languages. These will return a struct per Asset Type that has either 
        /// a valid addressables key or resources key depending on the way the Loc System is operating.
        /// </summary>
        /// <param name="_languages"> Languages to query </param>
        /// <param name="_requiredResources"> 
        /// When using Addressables, these assets will also need to be moved to resources. These are default fonts and loc sheets. 
        /// These go to resources so that they can be used in the case that the Addressables don't load so we have something to fallback to.
        /// (Key => Original Path To Asset, Value => Function to set the Resources Key (will be used during gameplay to load the Resource))
        /// </param>
        /// <param name="allLocResourcesInfo"> Outputs Resources that are in use in the Localisation System. These can then be converted to Resources or Addressables. </param>
        private static void FetchEditorLocAssetsForLanguages(LocLanguages[] _languages, out List<UnityEngine.Object> _requiredResources, out List<ResourceInfo> allLocResourcesInfo)
        {
            _requiredResources = new List<Object>();
            allLocResourcesInfo = new List<ResourceInfo>();

            // Moving Loc Sheets to Resources. These are also uploaded to Addressables if that is defined
            for (int i = 0; i < LocIDs.LocDocsInfo.TotalLocDocs; ++i)
            {
                string pathToLocStrings = LocIDs.LocDocsInfo.LocDocsPaths[i];
                TextAsset textAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(pathToLocStrings);
                _requiredResources.Add(textAsset);
            }

            LocManager locManager = LocManager.EditorGetInstance();
            foreach (LocLanguages language in _languages)
            {
                foreach (LocalisationTextDisplayStyleTypes textStyle in AllTextStyles)
                {
                    LocDisplayStyleFontInfo.StandardLocFontInfo standardTextFontInfo = locManager.GetTextLocDisplayStyleInfo(language, textStyle, false);
                    LocDisplayStyleFontInfo.TextMeshProLocFontInfo textMeshFontInfo = locManager.GetTextMeshLocDisplayStyleInfo(language, textStyle, false);

                    // ~~~~~~~~~~~ STANDARD FONT ~~~~~~~~~~ //
                    if (standardTextFontInfo != null)
                    {
                        // Resets the Assets Keys in case things have been changed. Will be fixed later.
                        standardTextFontInfo.font.Editor_SetAddressablesKey(string.Empty);
                        standardTextFontInfo.font.Editor_SetResourcesKey(string.Empty);

                        Font standardFontAsset = standardTextFontInfo.font.editorAssetReference.Asset;
                        if (standardFontAsset != null)
                        {
                            // Only need one instance of a unique asset for our purposes. So making sure we're
                            // reusing the same ResourceInfo if this is the same asset we've already bookmarked.
							ResourceInfo resourceInfo = allLocResourcesInfo.Find(ri => ri.asset == standardFontAsset);
                            if (resourceInfo == null)
							{
                                resourceInfo = new ResourceInfo()
                                {
                                    asset = standardFontAsset
                                };
                                allLocResourcesInfo.Add(resourceInfo);
                            }

                            resourceInfo.resourcesKeySetter.Add(standardTextFontInfo.font.Editor_SetResourcesKey);
#if LOCALISATION_ADDRESSABLES
                            resourceInfo.addressablesKeySetter.Add(standardTextFontInfo.font.Editor_SetAddressablesKey);
#endif
                        }

                    }

                    // ~~~~~~~~~~~ TEXT MESH FONT ~~~~~~~~~~ //
                    if (textMeshFontInfo != null)
                    {
                        textMeshFontInfo.font.Editor_SetAddressablesKey(string.Empty);
                        textMeshFontInfo.font.Editor_SetResourcesKey(string.Empty);

                        LocDisplayStyleFontInfo.MaterialResourceRef[] materialsRefs = new LocDisplayStyleFontInfo.MaterialResourceRef[]
                        {
                            textMeshFontInfo.normalMaterial,
                            textMeshFontInfo.strokedMaterial,
                            textMeshFontInfo.underlinedMaterial,
                        };

                        foreach (LocDisplayStyleFontInfo.MaterialResourceRef materialRef in materialsRefs)
                        {
                            materialRef.Editor_SetAddressablesKey(string.Empty);
                            materialRef.Editor_SetResourcesKey(string.Empty);
                        }


                        TMPro.TMP_FontAsset tmpFontAsset = textMeshFontInfo.font.editorAssetReference.Asset;
                        if (tmpFontAsset != null)
                        {
                            ResourceInfo resourceInfo = allLocResourcesInfo.Find(ri => ri.asset == tmpFontAsset);
                            if (resourceInfo == null)
                            {
                                resourceInfo = new ResourceInfo()
                                {
                                    asset = tmpFontAsset
                                };
                                allLocResourcesInfo.Add(resourceInfo);
                            }

                            resourceInfo.resourcesKeySetter.Add(textMeshFontInfo.font.Editor_SetResourcesKey);
#if LOCALISATION_ADDRESSABLES
                            resourceInfo.addressablesKeySetter.Add(textMeshFontInfo.font.Editor_SetAddressablesKey);
#endif
                        }


                        // ~~~~~~~~~~~ MATERIALS ~~~~~~~~~~ //
                        foreach (LocDisplayStyleFontInfo.MaterialResourceRef materialRef in materialsRefs)
                        {
                            ResourceInfo resourceInfo = allLocResourcesInfo.Find(ri => ri.asset == materialRef.editorAssetReference.Asset);
                            if (resourceInfo == null)
                            {
                                resourceInfo = new ResourceInfo()
                                {
                                    asset = materialRef.editorAssetReference.Asset
                                };
                                allLocResourcesInfo.Add(resourceInfo);
                            }

                            resourceInfo.resourcesKeySetter.Add(materialRef.Editor_SetResourcesKey);
#if LOCALISATION_ADDRESSABLES
                            resourceInfo.addressablesKeySetter.Add(materialRef.Editor_SetAddressablesKey);
#endif

                            materialRef.Editor_SetAddressablesKey(string.Empty);
                            materialRef.Editor_SetResourcesKey(string.Empty);
                        }
                    }
                }
            }
            
            LocalisationImportHandler importHandler = AssetDatabase.LoadAssetAtPath(Configuration.LocalisationImportConfigFilePath, typeof(LocalisationImportHandler)) as LocalisationImportHandler;
            if (importHandler != null)
            {
                foreach (TMP_FontAsset fallbackFont in importHandler.FallbackFontSheets)
                {
                    if (fallbackFont != null)
                    {
                        ResourceInfo resourceInfo = allLocResourcesInfo.Find(ri => ri.asset == fallbackFont);
                        if (resourceInfo == null)
                        {
                            resourceInfo = new ResourceInfo()
                            {
                                asset = fallbackFont
                            };
                            allLocResourcesInfo.Add(resourceInfo);

                            if (fallbackFont.sourceFontFile != null)
                            {
                                resourceInfo = new ResourceInfo()
                                {
                                    asset = fallbackFont.sourceFontFile
                                };
                                allLocResourcesInfo.Add(resourceInfo);
                            }
                        }
                    }
                }
            }
        }
    }
}