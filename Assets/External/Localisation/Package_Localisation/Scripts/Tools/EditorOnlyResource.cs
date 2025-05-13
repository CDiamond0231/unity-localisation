//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Editor Only Resource
//             Author: Christopher Allport
//             Date Created: 30th June, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      A resource that is serialized via the guid rather than direct reference.
//      This is only useful for serializing an asset for use in Editor but to
//      remove the reference to the serialized asset during build time
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

// ReSharper disable Unity.RedundantSerializeFieldAttribute


namespace Localisation.Localisation
{	
	[System.Serializable]
	public class EditorOnlyResource<T> 
		where T : UnityEngine.Object
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Inspector Fields
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		[SerializeField, FormerlySerializedAs("serialisedGUID")] private string _serialisedGUID = string.Empty;

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Properties
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Gets the corresponding asset path for the supplied GUID, or an empty string if the GUID can't be found. </summary>
		public string AssetPath
		{
			get 
			{
#if UNITY_EDITOR
				if (string.IsNullOrEmpty(_serialisedGUID))
				{
					return string.Empty;
				}

				return AssetDatabase.GUIDToAssetPath(_serialisedGUID);
#else
				return string.Empty;
#endif
			}
		}

		/// <summary> Returns the asset or null if unassigned or can't be found. </summary>
		public T Asset
		{
#if UNITY_EDITOR
			get
			{
				string assetPath = AssetPath;
				if (string.IsNullOrEmpty(assetPath))
                {
                    return null;
                }

                return AssetDatabase.LoadAssetAtPath<T>(assetPath);
			}
			set
			{
				if (value == null)
				{
					_serialisedGUID = string.Empty;
				}
				else
				{
					string newAssetPath = AssetDatabase.GetAssetPath(value);
					_serialisedGUID = AssetDatabase.GUIDFromAssetPath(newAssetPath).ToString();
				}
			}
#else
			get
			{
				return null;
			}
#endif
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Overloads/Conversion
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public static implicit operator T (EditorOnlyResource<T> editorOnlyResource)
		{
			return editorOnlyResource.Asset;
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Editor Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
#if UNITY_EDITOR
		/// <summary> Invoked this function via the Custom Editor Helper class. Pass in self when invoked. </summary>
		public bool DrawEditorOption(string displayTitle, CustomEditorHelper editorHelper, string tooltip = "") 
		{
			T asset = Asset;
			if (editorHelper.DrawObjectOptionWithUndoOption(displayTitle, ref asset, tooltip))
			{
				Asset = asset;
				return true;
			}

			return false;
		}
#endif
	}
}