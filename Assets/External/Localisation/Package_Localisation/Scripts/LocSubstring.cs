using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Localisation.Localisation
{
    [System.Serializable]
    public partial class LocSubstring
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [SerializeField] public int locHashId;
        [SerializeField] public string userDefinedSubstringText;

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Non-Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [NonSerialized] private LocSubstring[] locSubstringsForLocId = null;

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Properties
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public string SubString
        {
            get
            {
                string output;
                if (locHashId == LocIDs.Empty_Loc_String)
                {
                    if (userDefinedSubstringText == null)
                        return string.Empty;
                    
                    output = userDefinedSubstringText;
                }
                else
                {
                    output = LocManager.GetTextViaLocHashID(locHashId);
                }

                output = LocManager.AppendSubstringToString(output, locSubstringsForLocId);
                return output;
            }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Constructors
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public LocSubstring()
        {
            Set(string.Empty);
        }

        public LocSubstring(int _locHashId)
        {
            Set(_locHashId);
        }

        public LocSubstring(LocID _locId)
        {
            Set(_locId);
        }

        public LocSubstring(string _substring)
        {
            Set(_substring);
        }

        public LocSubstring(LocID _locId, LocSubstring _substringForLocId)
        {
            Set(_locId, _substringForLocId);
        }

		public LocSubstring(LocID _locId, LocSubstring[] _substringsForLocId)
        {
            Set(_locId, _substringsForLocId);
        }

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Setters
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public void Set(int _locHashId)
        {
            locHashId = _locHashId;
            userDefinedSubstringText = string.Empty;
            locSubstringsForLocId = null;
        }

        public void Set(LocID _locId)
        {
            locHashId = _locId.locId;
            userDefinedSubstringText = string.Empty;
            locSubstringsForLocId = null;
        }

        public void Set(string _substring)
        {
            locHashId = LocIDs.Empty_Loc_String;
            userDefinedSubstringText = _substring;
            locSubstringsForLocId = null;
        }

		public void Set(LocID _locId, LocSubstring _substringForLocId)
		{
            locHashId = _locId;
            userDefinedSubstringText = string.Empty;
            locSubstringsForLocId = new LocSubstring[] { _substringForLocId };
        }

		public void Set(LocID _locId, LocSubstring[] _substringsForLocId)
		{
            locHashId = _locId;
            userDefinedSubstringText = string.Empty;
            locSubstringsForLocId = _substringsForLocId;
        }

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Casting Overloads
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public override bool Equals(object obj)
		{
            if (obj is LocSubstring other)
                return string.Equals(SubString, other.SubString);

            return false;
		}

		public override int GetHashCode()
		{
			return SubString.GetHashCode();
		}

		public static implicit operator LocSubstring(int _locHashId)
        {
            return new LocSubstring(_locHashId);
        }

        public static implicit operator LocSubstring(LocID _locId)
        {
            return new LocSubstring(_locId);
        }

        public static implicit operator LocSubstring(string _substring)
        {
            return new LocSubstring(_substring);
        }

        public static implicit operator int(LocSubstring _locSubstringRef)
        {
            return _locSubstringRef.locHashId;
        }

        public static implicit operator LocID(LocSubstring _locSubstringRef)
        {
            return _locSubstringRef.locHashId;
        }

        public static implicit operator string(LocSubstring _locSubstringRef)
        {
            return _locSubstringRef.SubString;
        }
    }





#if UNITY_EDITOR
    public partial class LocSubstring
    {
        [CustomPropertyDrawer(typeof(LocSubstring))]
        private sealed class LocSubstringDrawer : PropertyDrawer
        {
            /// <summary> Store the height that will be used for each individual line displayed </summary>
            private static readonly float LINE_HEIGHT = EditorGUIUtility.singleLineHeight + 2f;

            /// <summary> Define a value that will be used for the indentation of the display elements </summary>
            private const float INDENT = 15f;

            /// <summary> Retrieve a sub-rect of the specified original </summary>
            /// <param name="_original">The original rect are to take the values from</param>
            /// <param name="_line">The line within the original to use</param>
            /// <param name="_indent">Indentation into the line that will be used</param>
            /// <param name="_normalOffset">Normalised (0-1) offset into the line that will be used</param>
            /// <param name="_normalWidth">Normalised (0-1) size of the line that will be used for width</param>
            /// <returns>Returns a Rect conforming to the specified settings</returns>
            private static Rect GetSubRect(Rect _original, int _line, int _indent, float _normalOffset = 0f, float _normalWidth = 1f)
            {
                float width = _original.width - _indent * INDENT;
                return new Rect
                (
                    _original.x + _indent * INDENT + _normalOffset * width,
                    _original.y + _line * LINE_HEIGHT,
                    width * _normalWidth,
                    EditorGUIUtility.singleLineHeight
                );
            }

            /// <summary> Determine the height that will be needed to display this properties information </summary>
            /// <param name="_property">The property that is to be displayed</param>
            /// <param name="_label">The label to be assigned to this element</param>
            /// <returns>Returns the pixel height requirements to show this property in the inspector</returns>
            public override float GetPropertyHeight(SerializedProperty _property, GUIContent _label)
            {
                return 0.0f;
            }

            /// <summary> Display the LocId elements within the inspector for use </summary>
            /// <param name="_position">The position allocated to the property for display</param>
            /// <param name="_property">The property information that is to be displayed</param>
            /// <param name="_label">The label assigned to the information to show</param>
            public override void OnGUI(Rect _position, SerializedProperty _property, GUIContent _label)
            {
                // Using BeginProperty / EndProperty on the parent property means that
                // prefab override logic works on the entire property.
                EditorGUI.BeginProperty(_position, _label, _property);

                SerializedProperty locIdProp = _property.FindPropertyRelative(nameof(LocSubstring.locHashId));
                SerializedProperty userSubstringProp = _property.FindPropertyRelative(nameof(LocSubstring.userDefinedSubstringText));

                int currentlySelectedIndex;
                if (LocIDsEditorData.Combined_EditorHashIdToInspectorPopupIndex.TryGetValue(locIdProp.intValue, out currentlySelectedIndex) == false)
                {
                    currentlySelectedIndex = -1;
                }

                EditorGUILayout.LabelField(_label);
                ++EditorGUI.indentLevel;
                {
                    int newSelectedIndex = EditorGUILayout.Popup(new GUIContent("Substring Loc Id:"), currentlySelectedIndex, LocIDsEditorData.Combined_EditorCategorisedAlphabeticallyOrderedLocStringIDs);
                    if (newSelectedIndex != -1 && newSelectedIndex != currentlySelectedIndex)
                    {
                        Undo.RecordObject(_property.serializedObject.targetObject, "Changed Loc Id");
                        locIdProp.intValue = LocIDsEditorData.Combined_EditorHashIdToInspectorPopupIndex[newSelectedIndex];
                        userSubstringProp.stringValue = string.Empty;
                    }

                    if (locIdProp.intValue != LocIDs.Empty_Loc_String)
                    {
                        GUI.enabled = false;
                        {
                            ++EditorGUI.indentLevel;
                            {
                                EditorGUILayout.TextField("Loc Substring:", LocManager.GetTextViaLocHashID(locIdProp.intValue, LocLanguages.English));
                            }
                            --EditorGUI.indentLevel;
                        }
                        GUI.enabled = true;
                    }
                    else
                    {
                        string newUserDefinedSubstring = EditorGUILayout.TextField("Explicit Substring:", userSubstringProp.stringValue);
                        if (string.Equals(newUserDefinedSubstring, userSubstringProp.stringValue) == false)
                        {
                            Undo.RecordObject(_property.serializedObject.targetObject, "Changed Explicit Substring");
                            userSubstringProp.stringValue = newUserDefinedSubstring;
                        }
                    }
                }
                --EditorGUI.indentLevel;

                EditorGUI.EndProperty();
            }
        }
    }
#endif
}