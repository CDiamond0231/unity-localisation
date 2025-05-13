using UnityEngine;
using System.Collections.Generic;
using UnityEngine.UI;
using Debug = Localisation.LocalisationOverrides.Debug;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Localisation.Localisation
{
    //-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
    //          Class:  UI LOC LABEL TEXT
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
    [RequireComponent(typeof(Text))]
    public partial class UILocLabelText : UILocLabelBase
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Non-Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private Font originalFont;
        private int originalTextSize;

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Properties
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public Text TextLabel { get; private set; }

        protected override bool IsTextInitialised
        {
            get { return TextLabel != null; }
        }

        protected override int CharacterCount
        {
            get { return TextLabel.text.Length; }
        }

		public override Color TextColour
        {
            get { return TextLabel.color; }
            set { TextLabel.color = value; }
        }

        public override float FontSize
        {
            get { return TextLabel.fontSize; }
            set { TextLabel.fontSize = (int)value; }
        }

        /// <summary> Returns whether or not there is culled text on this component. </summary>
		public override CulledTextStates CulledTextState
        {
            get { return CulledTextStates.HasNoCulledText; }
        }


        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Called from Awake() </summary>
        protected override void Init()
        {
            TextLabel = GetComponent<Text>();
            if (TextLabel == null)
                throw new UnityException($"Could not find Text component attached to '{gameObject.name}'");

            originalFont = TextLabel.font;
            originalTextSize = TextLabel.fontSize;
        }

        protected override void SetTextContent(string text, bool shouldRenderRightToLeft)
        {
#if LOCALISATION_3_COMPATIBILITY_MODE
            if (!_localiseText)
                return;
           
            if (_forceUpperCase)
                text = text.ToUpper();
#endif

            TextLabel.text = text;

#if UNITY_EDITOR
            if (shouldRenderRightToLeft)
            {
                Debug.LogError($"{name}: {nameof(UILocLabelText)} could not display the text as Right To Left because {nameof(Text)} "
                               + $"does not support it. I suggest you change from using a {nameof(Text)} component to {nameof(TMPro.TextMeshPro)} component instead. "
                               + $"There should be an option to auto-convert via this component's inspector.");
            }
#endif
        }

        protected override void ForceTextExpansion()
        {
            TextLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            TextLabel.verticalOverflow = VerticalWrapMode.Overflow;
        }

        /// <summary> Applies Font Override for this Loc Component </summary>
		protected override void ApplyFontOverride()
        {
            LocDisplayStyleFontInfo.StandardLocFontInfo textDisplayStyleInfo = LocManager.Instance.GetTextLocDisplayStyleInfoOrDefault(_assignedFontStyle);
            if (textDisplayStyleInfo != null && textDisplayStyleInfo.font.RuntimeAsset != null)
            {
                TextLabel.font = textDisplayStyleInfo.font.RuntimeAsset;
                TextLabel.fontSize = (int)(originalTextSize * textDisplayStyleInfo.fontSizePercentage);
            }
            else
            {
                TextLabel.font = originalFont;
                TextLabel.fontSize = originalTextSize;
            }
        }
    }



    //-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
    //          Class:  UI LOC LABEL TEXT EDITOR
    //=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#if UNITY_EDITOR
    #region UI LOC LABEL TEXT EDITOR
    public partial class UILocLabelText
    {
        [CanEditMultipleObjects]
        [CustomEditor(typeof(UILocLabelText))]
        protected class UILocLabelTextEditor : UILocLabelBaseEditor<UILocLabelText>
        {
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //        Custom Editor Methods
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            [InspectorRegion]
            protected void DrawScriptConversionRecommendation()
            {
                Color descriptionTextColour;
                if (EditorGUIUtility.isProSkin)
                {
                    descriptionTextColour = Color.cyan;
                }
                else
                {
                    descriptionTextColour = new Color32(36, 68, 196, 255);
                }

                string conversionRecommendationText = $"It is recommended that you switch over to using"
                                                   + $"\n{nameof(UILocLabelTextMesh)} instead of using {nameof(UILocLabelText)}."
                                                   + $"\n\n{nameof(UILocLabelTextMesh)} supports writing Right-To-Left"
                                                   + $"\ntext for languages such as Arabic."
                                                   + $"\n\nYou can do so with the debug button below.";

                EditorHelper.DrawDescriptionBox(conversionRecommendationText, descriptionTextColour);
            }

            protected override void DrawDebugOptions()
            {
                base.DrawDebugOptions();

                if (GUILayout.Button($"Convert to {nameof(UILocLabelTextMesh)}")
                    && EditorUtility.DisplayDialog("Are you sure?", $"This will replace your {nameof(UILocLabelText)} component with a {nameof(UILocLabelTextMesh)} component", "Go Ahead", "Abort"))
                {
                    ConvertTextComponentToTextMeshPro(out _, out _);
                }
            }

            protected bool ConvertTextComponentToTextMeshPro(out UILocLabelTextMesh textMeshProLoc, out TMPro.TextMeshProUGUI textMeshPro)
            {
                textMeshProLoc = null;
                textMeshPro = null;

                UILocLabelText labelTarget = (target as UILocLabelText)!;
                GameObject targetObject = labelTarget.gameObject;

                // Finding any script that is referencing this UILocLabelText so we can replace it with the Newly created UILocLabelTextMesh
                List<ReferenceFinder.FieldReferencesData> referencesToScript = ReferenceFinder.GetFieldsReferencingTarget(labelTarget);
                foreach (ReferenceFinder.FieldReferencesData refData in referencesToScript)
                {
                    if ((refData.isACollectionVariableType && refData.fieldInfo.FieldType == typeof(UILocLabelText[]))
                        || refData.fieldInfo.FieldType == typeof(UILocLabelText))
                    {
                        // Abort! Abort!
                        if (EditorUtility.DisplayDialog("Issue discovered", $"{refData.target.name} has a reference to {nameof(UILocLabelText)} with name {refData.fieldInfo.Name}.\nThis cannot be converted to {nameof(UILocLabelTextMesh)}. Would you like to abort conversion?", "Abort", "Keep Going"))
                        {
                            return false;
                        }
                    }
                }

                // Storing Text Component Data /////////////////////////
                Text textCom = targetObject.GetComponent<Text>();
                string text = textCom.text;
                FontStyle fontStyle = textCom.fontStyle;
                float lineSpacing = textCom.lineSpacing;
                int fontSize = textCom.fontSize;
                bool richText = textCom.supportRichText;
                HorizontalWrapMode horizontalOverflow = textCom.horizontalOverflow;
                TextAnchor alignment = textCom.alignment;
                Color colour = textCom.color;
                Material material = textCom.material;
                bool rayCastTarget = textCom.raycastTarget;

                // Storing UILocLabelText Data /////////////////////////
                int assignedLocHashId = labelTarget._assignedLocHashId;

                LocSubstring[] substrings = null;
                if (labelTarget._locSubstituteStrings != null && labelTarget._locSubstituteStrings.Length > 0)
                {
                    substrings = new LocSubstring[labelTarget._locSubstituteStrings.Length];
                    for (int i = 0; i < labelTarget._locSubstituteStrings.Length; ++i)
                    {
                        substrings[i] = labelTarget._locSubstituteStrings[i];
                    }
                }

                Color[] highlightedTextColours = null;
                if (labelTarget._highlightedTextColours != null && labelTarget._highlightedTextColours.Length > 0)
                {
                    highlightedTextColours = new Color[labelTarget._highlightedTextColours.Length];
                    for (int i = 0; i < labelTarget._highlightedTextColours.Length; ++i)
                    {
                        highlightedTextColours[i] = labelTarget._highlightedTextColours[i];
                    }
                }

                // ReSharper disable once ConvertToConstant.Local
#if LOCALISATION_3_COMPATIBILITY_MODE
                bool forceUpperCase = labelTarget._forceUpperCase;
#else
                bool forceUpperCase = false;
#endif
                
                // Remove old Components so we can replace with new /////////////////////////
                DestroyImmediate(labelTarget);
                DestroyImmediate(textCom);

                // Convert Text Component To TextMeshPro /////////////////////////
                textMeshPro = targetObject.AddComponent<TMPro.TextMeshProUGUI>();
                textMeshPro.text = text;
                textMeshPro.fontSize = fontSize;
                textMeshPro.lineSpacing = lineSpacing;
                textMeshPro.richText = richText;
                textMeshPro.overflowMode = TMPro.TextOverflowModes.Overflow;
                textMeshPro.enableWordWrapping = horizontalOverflow == HorizontalWrapMode.Wrap;
                textMeshPro.color = colour;
                textMeshPro.material = material;
                textMeshPro.raycastTarget = rayCastTarget;

				LocDisplayStyleFontInfo.TextMeshProLocFontInfo tmpFontInfo = LocManager.Instance.GetTextMeshLocDisplayStyleInfo(labelTarget._assignedFontStyle);
                if (tmpFontInfo != null)
                {
                    textMeshPro.font = tmpFontInfo.font.editorAssetReference.Asset;
                }

                switch (fontStyle)
                {
                    case FontStyle.Bold: textMeshPro.fontStyle = TMPro.FontStyles.Bold; break;
                    case FontStyle.BoldAndItalic: textMeshPro.fontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.Italic; break;
                    case FontStyle.Italic: textMeshPro.fontStyle = TMPro.FontStyles.Italic; break;
                    case FontStyle.Normal: textMeshPro.fontStyle = TMPro.FontStyles.Normal; break;
                }

                switch (alignment)
                {
                    case TextAnchor.LowerLeft: textMeshPro.alignment = TMPro.TextAlignmentOptions.BottomLeft; break;
                    case TextAnchor.LowerCenter: textMeshPro.alignment = TMPro.TextAlignmentOptions.Bottom; break;
                    case TextAnchor.LowerRight: textMeshPro.alignment = TMPro.TextAlignmentOptions.BottomRight; break;
                    case TextAnchor.MiddleLeft: textMeshPro.alignment = TMPro.TextAlignmentOptions.MidlineLeft; break;
                    case TextAnchor.MiddleCenter: textMeshPro.alignment = TMPro.TextAlignmentOptions.Midline; break;
                    case TextAnchor.MiddleRight: textMeshPro.alignment = TMPro.TextAlignmentOptions.MidlineRight; break;
                    case TextAnchor.UpperLeft: textMeshPro.alignment = TMPro.TextAlignmentOptions.TopLeft; break;
                    case TextAnchor.UpperCenter: textMeshPro.alignment = TMPro.TextAlignmentOptions.Top; break;
                    case TextAnchor.UpperRight: textMeshPro.alignment = TMPro.TextAlignmentOptions.TopRight; break;
                }

                // Convert UILocLabelText to UILocLabelTextMesh /////////////////////////
                textMeshProLoc = targetObject.AddComponent<UILocLabelTextMesh>();
                
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                textMeshProLoc.EditorAssignDefaultValues(assignedLocHashId, substrings, highlightedTextColours, forceUpperCase);


                // Reassigning References from Old LocLabel to new one /////////////////////////
                foreach (ReferenceFinder.FieldReferencesData refData in referencesToScript)
                {
                    if (refData.isACollectionVariableType)
                    {
                        if (refData.fieldInfo.GetValue(refData.target) is List<UILocLabelBase> list)
                        {
                            foreach (int index in refData.indexIdsReferencingTargetScript)
                            {
                                list[index] = textMeshProLoc;
                            }
                            refData.fieldInfo.SetValue(refData.target, list);
                        }
                        else
                        {
                            if (refData.fieldInfo.GetValue(refData.target) is UILocLabelBase[] array)
                            {
                                foreach (int index in refData.indexIdsReferencingTargetScript)
                                {
                                    array[index] = textMeshProLoc;
                                }
                                refData.fieldInfo.SetValue(refData.target, array);
                            }
                        }
                    }
                    else
                    {
                        refData.fieldInfo.SetValue(refData.target, textMeshProLoc);
                    }

                    EditorUtility.SetDirty(refData.target);
                    EditorUtility.SetDirty(refData.target.gameObject);
                }


                EditorUtility.SetDirty(textMeshPro);
                EditorUtility.SetDirty(textMeshProLoc);
                EditorUtility.SetDirty(targetObject);

                return true;
            }
        }
    }
    #endregion // UI LOC LABEL TEXT EDITOR
#endif
}