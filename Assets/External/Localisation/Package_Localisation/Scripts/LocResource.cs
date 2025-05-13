//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Loc Resource
//             Author: Christopher Allport
//             Date Created: 23rd May, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Defines a Resource for Localisation. Loaded either via
//      * Direct Reference (Editor)
//      * Resources Folder
//      * Addressables (#if LOCALISATION_ADDRESSABLES defined)
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#if UNITY_EDITOR && !DISABLE_LOCALISATION_EDITOR_MODE
	// Defines as if Running via Editor Mode.
	#define RUN_AS_IF_EDITOR_MODE
#endif

#if !RUN_AS_IF_EDITOR_MODE
	#if !LOCALISATION_ADDRESSABLES
		// Can only do an "Act Like Resources" if not using Addressables.
		//#define LOC_ACT_LIKE_RESOURCES_BUILD
	#endif

	#if LOCALISATION_ADDRESSABLES
		// Can only do an "Act Like Addressables" if Addressables are defined.
		//#define LOC_ACT_LIKE_ADDRESSABLES_BUILD
	#endif
#endif

using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
// ReSharper disable Unity.RedundantSerializeFieldAttribute
// ReSharper disable NotAccessedField.Local



namespace Localisation.Localisation
{
	public class LocResource<T, V> where T : UnityEngine.Object where V : EditorOnlyResource<T>, new()
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Statics
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Multiple Loc Resources can references the same Asset. If any of them load an asset, 
		/// chuck it in here. Then before an asset is loaded, query if we already have it. Same goes for de-loading. </summary>
		private static Dictionary<string, T> SharedKeyToAssetsDict = new Dictionary<string, T>();

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Inspector Fields
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
#if UNITY_EDITOR || RUN_AS_IF_EDITOR_MODE
		/// <summary> This is an editor only reference. You can drag the asset onto here via the inspector. It's data will be converted to a Resource/Addressables key during compile. </summary>
		public V editorAssetReference = new V();
#endif
		/// <summary> Filled out during compile time. If not using Addressables, then on device the FontAssets will be automatically moved into the Resources folder. Use this key to load them. </summary>
		[SerializeField] private string resourceName = "";
		/// <summary> Filled out during compile time. If using Addressables, then on device the FontAssets will be automatically moved into the Resources folder. Use this key to load them. </summary>
		[SerializeField] private string addressablesKey = "";

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Properties
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public T RuntimeAsset
		{
			get
			{
#if RUN_AS_IF_EDITOR_MODE && !LOC_ACT_LIKE_RESOURCES_BUILD && !LOC_ACT_LIKE_ADDRESSABLES_BUILD
				return editorAssetReference.Asset;
#else
				T runtimeAsset;
				if (SharedKeyToAssetsDict.TryGetValue(ResourceKey, out runtimeAsset))
					return runtimeAsset;

#if UNITY_EDITOR
				if (Application.isPlaying == false)
				{
					// Editor Mode: Asset may not be loaded as this is done during Loc Load.
					// So changes made in the interim would end up being skipped over.
					_ = LoadAsset();
					if (SharedKeyToAssetsDict.TryGetValue(ResourceKey, out runtimeAsset))
						return runtimeAsset;
				}
#endif
				
				return null;
#endif
			}
		}

		/// <summary> Addressable/Resource Resources key used to load this resource. </summary>
		public string ResourceKey
		{
			get
			{
#if LOCALISATION_ADDRESSABLES && !LOC_ACT_LIKE_RESOURCES_BUILD
				return addressablesKey;
#else
				return resourceName;
#endif
			}
		}


		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
#if LOCALISATION_ADDRESSABLES && !LOC_ACT_LIKE_RESOURCES_BUILD
		public async Task LoadAsset()
		{
#if !RUN_AS_IF_EDITOR_MODE || LOC_ACT_LIKE_ADDRESSABLES_BUILD
			T assetResult;
			if (SharedKeyToAssetsDict.TryGetValue(ResourceKey, out assetResult) == false)
			{
				assetResult = await UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<T>(addressablesKey).Task;
				SharedKeyToAssetsDict.Add(ResourceKey, assetResult);
			}

#endif
			await Task.CompletedTask;
		}

		public void FreeAsset()
		{
#if !RUN_AS_IF_EDITOR_MODE || LOC_ACT_LIKE_ADDRESSABLES_BUILD
			if (SharedKeyToAssetsDict.ContainsKey(ResourceKey))
			{
				T resource = RuntimeAsset;
				SharedKeyToAssetsDict.Remove(ResourceKey);
	
				if (resource != null)
					UnityEngine.AddressableAssets.Addressables.Release( resource );
			}
#endif
		}

#else
		public void LoadAsset()
		{
			// Only loading resource if it isn't already loaded.
#if !RUN_AS_IF_EDITOR_MODE || LOC_ACT_LIKE_RESOURCES_BUILD
			T runtimeAsset;
			if (SharedKeyToAssetsDict.TryGetValue(ResourceKey, out runtimeAsset) == false)
			{
				runtimeAsset = Resources.Load<T>(resourceName);
				SharedKeyToAssetsDict.Add(ResourceKey, runtimeAsset);
			}
#endif
		}

		public void FreeAsset()
		{
#if !RUN_AS_IF_EDITOR_MODE || LOC_ACT_LIKE_RESOURCES_BUILD
			if (SharedKeyToAssetsDict.ContainsKey(ResourceKey))
			{
				Resources.UnloadAsset(SharedKeyToAssetsDict[ResourceKey]);
				SharedKeyToAssetsDict.Remove(ResourceKey);
			}
#endif
		}
#endif

#if UNITY_EDITOR
		public void Editor_SetResourcesKey(string _resourcesKey)
		{
			resourceName = _resourcesKey;
		}

		public void Editor_SetAddressablesKey(string _addressablesKey)
		{
			addressablesKey = _addressablesKey;
		}

		/// <summary> Returns the Addressable Key to be used for the Assigned Editor Resource. </summary>
		public string Editor_GetEditorResourceAddressableKey()
		{
			if (editorAssetReference.Asset == null)
				return string.Empty;
			
			string originalPath = UnityEditor.AssetDatabase.GetAssetPath(editorAssetReference.Asset);
			if (string.IsNullOrEmpty(originalPath))
				return string.Empty;

			string extension = System.IO.Path.GetExtension(originalPath);
			// Resources is provided as the full path because there may be a case where Loc Assets share the same name but are in a different folder. Will cause issues when merged together.
			// Also saved with extension for the same issue in the case that the same folder contains two duplicate names with just different file extensions.
			// Resources requires not using file extensions when loading... so I'm just modifying the file name to include the extension to get around that.
			string addressableKey = System.Text.RegularExpressions.Regex.Replace(originalPath, "\\/|\\\\|\\s|-", "_");
			addressableKey = addressableKey.Substring(0, addressableKey.Length - extension.Length);
			if (string.IsNullOrEmpty(extension) == false)
				addressableKey += $"_{extension.Substring(1)}";

			return addressableKey;
		}

		/// <summary> Refreshes the Keys used for this asset </summary>
		public void Editor_RefreshKeys()
		{
			string addressableKey = Editor_GetEditorResourceAddressableKey();
			Editor_SetAddressablesKey(addressableKey);
		}
#endif
	}
}