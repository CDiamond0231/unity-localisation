//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Custom Property Drawer Base Class
//             Author: Christopher Allport
//             Date Created: March 5th, 2021 
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//		This Script is a base class for Custom Property Drawers. It adds some 
//			additional functionality that should be readily available in all
//			Custom Property Drawer scripts.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace Localisation.Localisation
{
    public abstract class CustomPropertyDrawerBase : PropertyDrawer
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //               Const Data
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Define a value that will be used for the indentation of the display elements. </summary>
        public const float IndentPixels = 15f;

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //               Definitions
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public class EditorIndentation : System.IDisposable
        {
            private readonly int m_indentCount;

            public EditorIndentation()
            {
                m_indentCount = 1;
                EditorGUI.indentLevel += m_indentCount;
            }

            public EditorIndentation(int indentCount)
            {
                m_indentCount = indentCount;
                EditorGUI.indentLevel += m_indentCount;
            }

            public void Dispose()
            {
                EditorGUI.indentLevel -= m_indentCount;
            }
        }

        public readonly struct SetEditorIndentation : System.IDisposable
        {
            private readonly int m_priorIndent;

            public SetEditorIndentation(int newIndentLevel)
            {
                m_priorIndent = EditorGUI.indentLevel;
                EditorGUI.indentLevel = newIndentLevel;
            }

            public void Dispose()
            {
                EditorGUI.indentLevel = m_priorIndent;
            }
        }

        public class GUIDisable : System.IDisposable
        {
            public GUIDisable()
            {
                GUI.enabled = false;
            }

            public void Dispose()
            {
                GUI.enabled = true;
            }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //               Properties
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public Rect FullPropertyBounds { get; private set; }
        public SerializedProperty SerializedProperty { get; private set; } = null;
        public GUIContent Label { get; private set; } = null;

        public int RenderedLinesCount { get; private set; } = 0;

        public float LineHeight
        {
            get { return EditorGUIUtility.singleLineHeight + 2.0f; }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //               Abstract Items
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> How Many Properties/Fields will be rendered with this target? (i.e. how many lines of space do you need for this?) </summary>
        protected abstract int NumberOfLinesNeeded
        {
            get;
        }

        protected abstract void RenderProperty();

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //              Unity Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Determine the height that will be needed to display this properties information </summary>
        /// <param name="_property">The property that is to be displayed</param>
        /// <param name="_label">The label to be assigned to this element</param>
        /// <returns>Returns the pixel height requirements to show this property in the inspector</returns>
        public sealed override float GetPropertyHeight(SerializedProperty _property, GUIContent _label)
        {
            return LineHeight * NumberOfLinesNeeded;
        }

        /// <summary> Display the LocId elements within the inspector for use </summary>
        /// <param name="_position">The position allocated to the property for display</param>
        /// <param name="_property">The property information that is to be displayed</param>
        /// <param name="_label">The label assigned to the information to show</param>
        public sealed override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label)
        {
            FullPropertyBounds = _position;
            SerializedProperty = _property;
            Label = _label;
            RenderedLinesCount = 0;

            // Using BeginProperty / EndProperty on the parent property means that prefab override logic works on the entire property.
            EditorGUI.BeginProperty(_position, _label, _property);
            RenderProperty();
            EditorGUI.EndProperty();
        }

        private void OnPostLineRender()
        {
            ++RenderedLinesCount;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //               Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Retrieve a sub-rect of the specified original </summary>
        /// <param name="_original">The original rect are to take the values from</param>
        /// <param name="_line">The line within the original to use</param>
        /// <param name="_indent">Indentation into the line that will be used</param>
        /// <param name="_normalOffset">Normalised (0-1) offset into the line that will be used</param>
        /// <param name="_normalWidth">Normalised (0-1) size of the line that will be used for width</param>
        /// <returns>Returns a Rect conforming to the specified settings</returns>
        protected Rect GetSubRect(int _line, int _indent, float _normalOffset = 0f, float _normalWidth = 1f)
        {
            float width = FullPropertyBounds.width - _indent * IndentPixels;
            return new Rect
            (
                FullPropertyBounds.x + _indent * IndentPixels + _normalOffset * width,
                FullPropertyBounds.y + _line * LineHeight,
                width * _normalWidth,
                EditorGUIUtility.singleLineHeight
            );
        }

        /// <summary> Retrieve a sub-rect for the current/next line you will be rendering </summary>
        /// <param name="_normalOffset">Normalised (0-1) offset into the line that will be used</param>
        /// <param name="_normalWidth">Normalised (0-1) size of the line that will be used for width</param>
        /// <returns>Returns a Rect conforming to the specified settings</returns>
        protected Rect GetNextLineSubRect(float _normalOffset = 0f, float _normalWidth = 1f)
        {
            return GetSubRect(RenderedLinesCount, EditorGUI.indentLevel, _normalOffset, _normalWidth);
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* Perform Label Conversion
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public GUIContent PerformLabelConversion(string displayName, string tooltip = null)
        {
            if (string.IsNullOrEmpty(tooltip) == false)
            {
                // Might do something here later. Thus, keeping this as a method
                return new GUIContent(displayName, tooltip);
            }
            return new GUIContent(displayName);
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	*   Draw Label Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected void DrawLabel(GUIContent displayLabel)
        {
            EditorGUI.LabelField(GetNextLineSubRect(), displayLabel);
            OnPostLineRender();
        }

        protected void DrawLabel(GUIContent displayLabelLeft, GUIContent displayLabelRight)
        {
            EditorGUI.LabelField(GetNextLineSubRect(), displayLabelLeft, displayLabelRight);
            OnPostLineRender();
        }

        protected void DrawLabel(string displayTitle, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayTitle, tooltip);
            DrawLabel(label);
        }

        protected void DrawLabel(string displayTitle, string displayBody, string tooltip)
        {
            GUIContent labelLeft = PerformLabelConversion(displayTitle, tooltip);
            GUIContent labelRight = PerformLabelConversion(displayBody, tooltip);
            DrawLabel(labelLeft, labelRight);
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	*   Draw String Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected string DrawStringField(GUIContent displayLabel, string currentValue)
        {
            string result = EditorGUI.TextField(GetNextLineSubRect(), displayLabel, currentValue);
            OnPostLineRender();
            return result;
        }

        protected string DrawStringField(string displayTitle, string currentValue, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayTitle, tooltip);
            return DrawStringField(label, currentValue);
        }

        protected bool DrawStringFieldWithUndoOption(GUIContent displayLabel, ref string currentValue)
        {
            string result = DrawStringField(displayLabel, currentValue);
            if (result.Equals(currentValue) == false)
            {
                Undo.RecordObject(SerializedProperty.serializedObject.targetObject, $"Changed: {displayLabel.text}");
                currentValue = result;
                return true;
            }
            return false;
        }

        protected bool DrawStringFieldWithUndoOption(string displayTitle, ref string currentValue, string tooltip = null)
        {
            string result = DrawStringField(displayTitle, currentValue, tooltip);
            if (result.Equals(currentValue) == false)
            {
                Undo.RecordObject(SerializedProperty.serializedObject.targetObject, $"Changed: {displayTitle}");
                currentValue = result;
                return true;
            }
            return false;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	*   Draw Enum Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected E DrawEnumField<E>(GUIContent displayLabel, E currentValue) where E : System.Enum
        {
            E result = (E)EditorGUI.EnumPopup(GetNextLineSubRect(), displayLabel, currentValue);
            OnPostLineRender();
            return result;
        }

        protected bool DrawEnumFieldWithUndoOption<E>(GUIContent displayLabel, ref E currentValue) where E : System.Enum
        {
            E result = (E)EditorGUI.EnumPopup(GetNextLineSubRect(), displayLabel, currentValue);
            OnPostLineRender();
            if (result.Equals(currentValue) == false)
            {
                Undo.RecordObject(SerializedProperty.serializedObject.targetObject, $"Changed: {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	*   Draw Dropdown Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public int DrawDropdownBox(GUIContent displayLabel, GUIContent[] options, int currentSelectedIndex, bool drawByAlphabeticalOrder = true)
        {
            if (options.Length == 0)
            {
                return -1;
            }

            PerformLabelConversion(displayLabel.text, displayLabel.tooltip);
            if (drawByAlphabeticalOrder)
            {
                List<GUIContent> sortedOptions = new List<GUIContent>();
                sortedOptions.AddRange(options.OrderBy(o => o.text));
                GUIContent[] asSortedArray = sortedOptions.ToArray();
                int currentSelectedSortedIndex = -1;
                if (currentSelectedIndex != -1)
                {
                    currentSelectedSortedIndex = sortedOptions.IndexOf(options[currentSelectedIndex]);
                }
                int result = EditorGUI.Popup(GetNextLineSubRect(), displayLabel, currentSelectedSortedIndex, asSortedArray);
                OnPostLineRender();
                if (result == -1)
                {
                    return -1;
                }

                for (int i = 0; i < asSortedArray.Length; ++i)
                {
                    // Still need to return the correct index. All we did was change the layout visually.
                    if (asSortedArray[result].Equals(options[i]))
                    {
                        return i;
                    }
                }

                // Won't be reached But C# complains if doesn't exist.
                return -1;
            }
            else
            {
                int result = EditorGUI.Popup(GetNextLineSubRect(), displayLabel, currentSelectedIndex, options);
                OnPostLineRender();
                return result;
            }
        }

        public int DrawDropdownBox(string displayLabel, string[] options, int currentSelectedIndex, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            if (options.Length == 0)
            {
                return -1;
            }

            PerformLabelConversion(displayLabel, tooltip);
            if (drawByAlphabeticalOrder)
            {
                List<string> sortedOptions = new List<string>();
                sortedOptions.AddRange(options.OrderBy(o => o));
                string[] asSortedArray = sortedOptions.ToArray();
                int currentSelectedSortedIndex = -1;
                if (currentSelectedIndex != -1)
                {
                    currentSelectedSortedIndex = sortedOptions.IndexOf(options[currentSelectedIndex]);
                }
                int result = EditorGUI.Popup(GetNextLineSubRect(), displayLabel, currentSelectedSortedIndex, asSortedArray);
                OnPostLineRender();
                if (result == -1)
                {
                    return -1;
                }

                for (int i = 0; i < asSortedArray.Length; ++i)
                {
                    // Still need to return the correct index. All we did was change the layout visually.
                    if (asSortedArray[result].Equals(options[i]))
                    {
                        return i;
                    }
                }

                // Won't be reached But C# complains if doesn't exist.
                return -1;
            }
            else
            {
                int result = EditorGUI.Popup(GetNextLineSubRect(), displayLabel, currentSelectedIndex, options);
                OnPostLineRender();
                return result;
            }
        }

        public int DrawDropdownBox(string displayLabel, string[] options, string currentSelection, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            int selectedIndex = -1;
            if (string.IsNullOrEmpty(currentSelection) == false)
            {
                for (int i = 0; i < options.Length; ++i)
                {
                    if (currentSelection.Equals(options[i]))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            return DrawDropdownBox(displayLabel, options, selectedIndex, drawByAlphabeticalOrder, tooltip);
        }

        public int DrawDropdownBox(string displayLabel, List<string> options, int currentSelectedIndex, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            return DrawDropdownBox(displayLabel, options.ToArray(), currentSelectedIndex, drawByAlphabeticalOrder, tooltip);
        }

        public int DrawDropdownBox(string displayLabel, List<string> options, string currentSelection, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            int selectedIndex = -1;
            if (string.IsNullOrEmpty(currentSelection) == false)
            {
                for (int i = 0; i < options.Count; ++i)
                {
                    if (currentSelection.Equals(options[i]))
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            return DrawDropdownBox(displayLabel, options.ToArray(), selectedIndex, drawByAlphabeticalOrder, tooltip);
        }

        public void DrawReadonlyDropdownBox(string displayLabel, string[] options, int currentSelectedIndex, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelectedIndex, drawByAlphabeticalOrder, tooltip);
            }
        }

        public void DrawReadonlyDropdownBox(string displayLabel, string[] options, string currentSelection, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            }
        }

        public void DrawReadonlyDropdownBox(string displayLabel, List<string> options, int currentSelectedIndex, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelectedIndex, drawByAlphabeticalOrder, tooltip);
            }
        }

        public void DrawReadonlyDropdownBox(string displayLabel, List<string> options, string currentSelection, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            }
        }

        public bool DrawDropdownBoxWithUndoOption(string displayLabel, string[] options, ref int currentSelectionIndex, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelectionIndex, drawByAlphabeticalOrder, tooltip);
            if (result != currentSelectionIndex)
            {
                Undo.RecordObject(SerializedProperty.serializedObject.targetObject, $"Changed {displayLabel}");
                currentSelectionIndex = result;
                return true;
            }
            return false;
        }

        public bool DrawDropdownBoxWithUndoOption(string displayLabel, string[] options, ref string currentSelection, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            if (result != -1 && options[result].Equals(currentSelection) == false)
            {
                Undo.RecordObject(SerializedProperty.serializedObject.targetObject, $"Changed {displayLabel}");
                currentSelection = options[result];
                return true;
            }
            return false;
        }

        public bool DrawDropdownBoxWithUndoOption(string displayLabel, List<string> options, ref int currentSelectionIndex, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelectionIndex, drawByAlphabeticalOrder, tooltip);
            if (result != currentSelectionIndex)
            {
                Undo.RecordObject(SerializedProperty.serializedObject.targetObject, $"Changed {displayLabel}");
                currentSelectionIndex = result;
                return true;
            }
            return false;
        }

        public bool DrawDropdownBoxWithUndoOption(string displayLabel, List<string> options, ref string currentSelection, bool drawByAlphabeticalOrder = true, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            if (result != -1 && options[result].Equals(currentSelection) == false)
            {
                Undo.RecordObject(SerializedProperty.serializedObject.targetObject, $"Changed {displayLabel}");
                currentSelection = options[result];
                return true;
            }
            return false;
        }
    }
}
#endif