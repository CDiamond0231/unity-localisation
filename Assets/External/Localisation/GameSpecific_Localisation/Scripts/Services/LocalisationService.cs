//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Localisation Service
//             Author: Christopher Allport
//             Date Created: 24th November, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Service for Passing through Localisation Tables through to the
//      LocManager via Addressables
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Localisation.Localisation;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMember.Local

namespace Localisation.GameSpecificLocalisation.Services
{
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //       LocalisationService - Game Class
    //  This code will exist in both editor and builds
    //-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
    public partial class LocalisationService : MonoBehaviour, ILocalisationService
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [SerializeField, HandledInCustomInspector] private string[] addressesForIndividualLocTables = Array.Empty<string>();

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Properties
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Returns if localisation has fully initialised. </summary>
        public bool LocalisationInitialised { get; private set; }


        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          IPreDownloadRemoteAssets
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public async Task<IList<IResourceLocation>> GetPreDownloadAssetAddresses()
		{
            var handle = Addressables.LoadResourceLocationsAsync((IEnumerable)addressesForIndividualLocTables, Addressables.MergeMode.Union);
            var results = await handle.Task;
            if (handle.Status != AsyncOperationStatus.Succeeded)
                throw new System.InvalidOperationException("Failed to fetch localisation service addressables");

            Addressables.Release(handle);
            return results;
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Service Init
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected async void Start()
        {
            await InitialiseLocalisationViaAddressables();
        }
        
		public async Task InitialiseLocalisationViaAddressables()
        {
#if UNITY_EDITOR && !DISABLE_LOCALISATION_EDITOR_MODE
            await Task.CompletedTask;
#else
            int locTablesCount = addressesForIndividualLocTables.Length;
            List<TextAsset> locTableAssets = new List<TextAsset>(locTablesCount);

            for (int i = 0; i < locTablesCount; ++i)
            {
                string address = addressesForIndividualLocTables[i];

                TextAsset locTable = await UnityEngine.AddressableAssets.Addressables.LoadAssetAsync<TextAsset>(address).Task;
                if (locTable != null)
                {
                    locTableAssets.Add(locTable);
                }
            }

    #if LOCALISATION_ADDRESSABLES && !USING_UNITY_LOCALISATION
            await 
    #endif
            LocManager.RuntimeInitialise(locTableAssets);
#endif

            LocalisationInitialised = true;
        }
    }
	
	
	
	
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //       LocalisationService - Editor Class
    //    This code will exist in editor only.
    //-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
#if UNITY_EDITOR
    public partial class LocalisationService
    {
        [UnityEditor.CustomEditor(typeof(LocalisationService))]
        private class LocalisationServiceEditor : CustomEditorBase<LocalisationService>
        {
            protected override void OnInitialised()
            {
#if !USING_UNITY_LOCALISATION
                if (Target.addressesForIndividualLocTables.Length != LocManager.LocTablesCount)
                {
                    EditorHelper.ResizeReferenceArray(ref Target.addressesForIndividualLocTables, LocManager.LocTablesCount);
                }
#endif
            }

            [InspectorRegion("Loc Table Addressables")]
            protected void DrawLocTableAddressables()
            {
#if USING_UNITY_LOCALISATION
                EditorHelper.DrawDescriptionBox("You are using Unity Localisation."
                                                + $"\n{nameof(LocalisationService)} will setup addressable assets for you but there is nothing here for you to modify."
                                                + "\nIf you turn off Unity Localisation (not recommended) and come back there will be options here for you to fill in.");
#elif LOCALISATION_ADDRESSABLES
                for (int i = 0; i < LocManager.LocTablesCount; ++i)
                {
                    string locTableFileName = LocalisationOverrides.Configuration.LocStringsTablesFilePaths[i];
                    EditorHelper.DrawAddressablesDropdownWithUndoOption($"{locTableFileName} Address:", typeof(TextAsset), "", ref Target.addressesForIndividualLocTables[i]);
                }
#else
                EditorHelper.DrawDescriptionBox("Your Localisation is currently set to `Use Resources`."
                                                + $"\n{nameof(LocalisationService)} will not do anything unless you are in Addressables Mode."
                                                + "\nIf you switch over to addressables and come back there will be options here for you to fill in.");
#endif
            }
        }
    }
#endif
}