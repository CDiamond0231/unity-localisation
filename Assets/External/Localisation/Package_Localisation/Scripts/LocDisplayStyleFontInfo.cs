//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Loc Display Style Font Info
//             Author: Christopher Allport
//             Date Created: 20th May, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Contains Serialised Font Information for the Localisation Display Styles:
//			* Header1
//			* Header2
//			* Body
//			* etc.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System.Collections;
using System.Collections.Generic;
using UnityEngine;




namespace Localisation.Localisation
{
	[System.Serializable]
	public class LocDisplayStyleFontInfo 
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Definitions
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		[System.Serializable]
		public class StandardFontEditorOnlyAccessResource : EditorOnlyResource<Font>
		{
		}

		[System.Serializable]
		public class TmpFontAssetEditorOnlyAccessResource : EditorOnlyResource<TMPro.TMP_FontAsset>
		{
		}

		[System.Serializable]
		public class MaterialEditorOnlyAccessResource : EditorOnlyResource<Material>
		{
		}

		[System.Serializable]
		public class FontResourceRef : LocResource<Font, StandardFontEditorOnlyAccessResource>
		{
		}

		[System.Serializable]
		public class TMPFontResourceRef : LocResource<TMPro.TMP_FontAsset, TmpFontAssetEditorOnlyAccessResource>
		{
		}

		[System.Serializable]
		public class MaterialResourceRef : LocResource<Material, MaterialEditorOnlyAccessResource>
		{
		}


		/// <summary> Standard Font for Unity Engine Text Component. </summary>
		[System.Serializable]
		public class StandardLocFontInfo
		{
			/// <summary> Standard Font for Unity Engine Text Component. </summary>
			public FontResourceRef font = new FontResourceRef();

			/// <summary> Modifies the Font Size when using this Font type on a Unity Text Component. You should use this if this Font Asset changes font sizes on a shared text component. Just to keep Font Sizes consistent. </summary>
			public float fontSizePercentage = 1.0f;
		}


		[System.Serializable]
		public class TextMeshProLocFontInfo
		{
#if UNITY_EDITOR
			/// <summary> Editor Only Reference: When the Localisation Atlas Files are generated, use this font size at a minimum. Will continue to increase the font size during generation if the atlas can fit bigger text sizes in the same space. </summary>
			public int editorMinFontSize = 44;
#endif

			/// <summary> TMP Font for TMPro Text Component. </summary>
			public TMPFontResourceRef font = new TMPFontResourceRef();
			/// <summary> Font Material to use when showing text normally. </summary>
			public MaterialResourceRef normalMaterial = new MaterialResourceRef();
			/// <summary> Font Material to use when using stroked text. </summary>
			public MaterialResourceRef strokedMaterial = new MaterialResourceRef();
			/// <summary> Font Material to use when using underlined text. </summary>
			public MaterialResourceRef underlinedMaterial = new MaterialResourceRef();

			/// <summary> Modifies the Font Size when using this Font type on a TMPro Component. You should use this if this Font Asset changes font sizes on a shared text component. Just to keep Font Sizes consistent. </summary>
			public float tmpFontSizePercentage = 1.0f;
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Inspector Fields
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public StandardLocFontInfo standardFontInfo = new StandardLocFontInfo();
		public TextMeshProLocFontInfo textMeshProFontInfo = new TextMeshProLocFontInfo();
	}
}