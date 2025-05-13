using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ReSharper disable UnusedMember.Local

namespace Localisation.Localisation
{
    //-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
    //          Class:  FONT OVERRIDE SCRIPTABLE
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    //[CreateAssetMenu(fileName = "New Font Override", menuName = "PlaySide/Localisation/FontOverride", order = 0)]
    public partial class FontOverrideScriptable : ScriptableObject
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Definitions
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [System.Serializable]
        public class StandardFontOverrideData
        {
            public Font font = null;
            public float fontSizePercentage = 1.0f;
        }

        [System.Serializable]
        public class TMPFontOverrideData
        {
            public TMPro.TMP_FontAsset tmpFont = null;
            public Material normalMaterial = null;
            public Material strokedMaterial = null;
            public Material underlinedMaterial = null;
            public float tmpFontSizePercentage = 1.0f;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [HandledInCustomInspector] public StandardFontOverrideData[] standardFonts = new StandardFontOverrideData[1];
        [HandledInCustomInspector] public TMPFontOverrideData[] tmpFonts = new TMPFontOverrideData[1];

    }




    //-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
    //          Class:  FONT OVERRIDE SCRIPTABLE EDITOR
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#if UNITY_EDITOR
    public partial class FontOverrideScriptable
    {
        [UnityEditor.CustomEditor(typeof(FontOverrideScriptable))]
        private class FontOverrideScriptableEditor : CustomEditorBase<FontOverrideScriptable>
        {
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //          Fields
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            private DrawArrayParameters<StandardFontOverrideData> standardFontArrayDrawParams = new DrawArrayParameters<StandardFontOverrideData>()
            {
                CanReorderArray = false,
                IsArrayResizeable = false,
                SpacesToAddBetweenElements = 2,
            };
            
            private DrawArrayParameters<TMPFontOverrideData> tmpFontArrayDrawParams = new DrawArrayParameters<TMPFontOverrideData>()
            {
                CanReorderArray = false,
                IsArrayResizeable = false,
                SpacesToAddBetweenElements = 2,
            };
            
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //          Methods
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            [InspectorRegion]
            private void DrawStandardFontOptions()
            {
                EditorHelper.DrawReferenceArrayHeader("~Standard Fonts {" + nameof(UILocLabelText) + "}~", ref Target.standardFonts, false, LocManager.AllTextDisplayStylesCount);
                EditorHelper.DrawReferenceArrayElements(ref Target.standardFonts, standardFontArrayDrawParams, DrawStandardFontDataElement);
            }

            private void DrawStandardFontDataElement(int index)
            {
                EditorHelper.DrawBoldLabel($"{(LocalisationTextDisplayStyleTypes)index} {nameof(UILocLabelText)} Font Data");
                using (new EditorIndentation())
                {
                    EditorHelper.DrawObjectOptionWithUndoOption("Font Sheet:", ref Target.standardFonts[index].font, $"The Font Sheet that will be assigned for {nameof(UILocLabelText)} components.");
                    EditorHelper.DrawSliderRangeWithUndoOption("Font Size Multiplier:", ref Target.standardFonts[index].fontSizePercentage, 0.1f, 5.0f, "When displayed in-game the font size will be multiplied by this amount. Useful if the characters in this font are smaller/larger than other font sheets.");
                }
            }



            [InspectorRegion]
            private void DrawTMPFontOptions()
            {
                EditorHelper.DrawReferenceArrayHeader("~TMP Fonts {" + nameof(UILocLabelTextMesh) + "}~", ref Target.tmpFonts, false, LocManager.AllTextDisplayStylesCount);
                EditorHelper.DrawReferenceArrayElements(ref Target.tmpFonts, tmpFontArrayDrawParams, DrawTMPFontDataElement);
            }

            private void DrawTMPFontDataElement(int index)
            {
                EditorHelper.DrawBoldLabel($"{(LocalisationTextDisplayStyleTypes)index} {nameof(UILocLabelTextMesh)} Font Data");
                using (new EditorIndentation())
                {
                    EditorHelper.DrawObjectOptionWithUndoOption("Font Atlas:", ref Target.tmpFonts[index].tmpFont, $"The Font Sheet that will be assigned for {nameof(UILocLabelTextMesh)} components.");
                    EditorHelper.DrawSliderRangeWithUndoOption("Font Size Multiplier:", ref Target.tmpFonts[index].tmpFontSizePercentage, 0.1f, 5.0f, "When displayed in-game the font size will be multiplied by this amount. Useful if the characters in this font are smaller/larger than other font sheets.");
                    EditorHelper.DrawObjectOptionWithUndoOption("Normal Material:", ref Target.tmpFonts[index].normalMaterial, "Normal Material to be assigned if this TMP Component is using the normal style.");
                    EditorHelper.DrawObjectOptionWithUndoOption("Stroked Material:", ref Target.tmpFonts[index].strokedMaterial, "Stroked Material to be assigned if this TMP Component is using the normal style.");
                    EditorHelper.DrawObjectOptionWithUndoOption("Underlined Material:", ref Target.tmpFonts[index].underlinedMaterial, "Underlined Material to be assigned if this TMP Component is using the normal style.");
                }
            }
        }
    }
#endif
}