using System.Collections;
using System.Collections.Generic;
using UnityEditor;

#if UNITY_EDITOR
using UnityEngine;
using System.Text.RegularExpressions;
#endif

namespace Localisation.Localisation
{
    [System.Serializable]
    public partial class LocID
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *        Serialised Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public int locId;

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *        Constructors
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public LocID()
        {
        }

        public LocID(int _locIdAsHashValue)
        {
            locId = _locIdAsHashValue;
        }

        public LocID(string _locIdAsString)
        {
            locId = _locIdAsString.GetHashCode();
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // * Operator Overloads:  From LocID -> To *
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public static implicit operator int(LocID _locIdRef)
        {
            if (_locIdRef == null)
            {
                LocalisationOverrides.Debug.LogError($"LocID is unassigned. Defaulting to Empty_Loc_String.");
                return 0;
            }
            return _locIdRef.locId;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // * Operator Overloads:  From * -> To LocID
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public static implicit operator LocID(int _intVal)
        {
            return new LocID(_intVal);
        }

        public static implicit operator LocID(string _stringVal)
        {
            return new LocID(_stringVal);
        }

        public static implicit operator LocID(LocStruct _locStructVal)
        {
            return new LocID()
            {
                locId = _locStructVal.locId,
            };
        }
    }


#if UNITY_EDITOR
    public partial class LocID
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        // *        Editor Only Properties
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Editor Only: Returns the LOC ID String for this Class. </summary>
        public string EditorOnly_LocIdName
		{
            get
            {
                foreach (Dictionary<int, string> locDictionary in LocIDsEditorData.EditorHashToStringIdsDictionaryReferences)
                {
                    string result;
                    if (locDictionary.TryGetValue(locId, out result))
                        return result;
                }
                return string.Empty;
            }
		}

        /// <summary> Editor Only: Returns the English Text for this Loc. </summary>
        public string EditorOnly_EnglishText
        {
            get { return LocManager.GetTextViaLocHashID(locId, LocLanguages.English); }
        }


        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //               Custom Property Inspector                        
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [CustomPropertyDrawer(typeof(LocID))]
        private sealed class LocIDDrawer : CustomPropertyDrawerBase
        {
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //               Definitions        
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            public class LocIDData
            {
                public int currentHashId = 0;
                public int currentPopupIndex = -1;

                public string searchString = "";
                public GUIContent[] searchResults = null;
                public int[] hashResults = null;
                public int selectedSearchResultIndex = -1;

                public LocIDData(int _locHashId)
                {
                    currentHashId = _locHashId;
                }
            }

            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //               Constants        
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            private static readonly GUIContent LocIdDisplayTitle = new GUIContent("Loc Id:");

            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //            Properties
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            protected override int NumberOfLinesNeeded
            {
                get { return 4; }
            }

            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //            Private Fields
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            // (Key => Property Path | Value => Loc Struct)
            private Dictionary<string, LocIDData> propertyToLocContainer = new Dictionary<string, LocIDData>();
            private LocIDData currentLocData = null;

            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            //               Methods
            //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            protected override void RenderProperty()
            {
                SerializedProperty locIdProp = SerializedProperty.FindPropertyRelative(nameof(LocID.locId));
                
                // This same class is used multiple times for multiple instances of LocID. So need to store data per instance using the SerializedProperty 
                //  as that is out only way of knowing which instance we are editing right now.
                if (propertyToLocContainer.TryGetValue(locIdProp.propertyPath, out currentLocData) == false)
                {
                    currentLocData = new LocIDData(locIdProp.intValue);
                    propertyToLocContainer.Add(locIdProp.propertyPath, currentLocData);
                }
                else if (currentLocData.currentHashId != locIdProp.intValue)
                {
                    // Property paths are relative. So if this was a Loc ID assigned to an array and it was reordered; we now have dirty data and must recache.
                    propertyToLocContainer.Clear();
                    currentLocData = new LocIDData(locIdProp.intValue);
                    propertyToLocContainer.Add(locIdProp.propertyPath, currentLocData);
                }

                if (DrawLocField())
                {
                    locIdProp.intValue = currentLocData.currentHashId;
                }
            }

            private bool DrawLocField()
            {
                bool changesMade = false;

                DrawLabel(Label);
                using (new EditorIndentation())
                {
                    DrawLabel("Text (EN):", LocManager.GetTextViaLocHashID(currentLocData.currentHashId, LocLanguages.English), "The text that will be rendered (English).");
                    
                    if (DrawSearchField())
                    {
                        changesMade = true;
                    }

                    using (new EditorIndentation())
                    {
                        if (string.IsNullOrEmpty(currentLocData.searchString) == false)
                        {
                            if (DrawSearchResults())
                            {
                                changesMade = true;
                            }
                        }
                        else
                        {
                            if (DrawRegularLocIdDropdown())
                            {
                                changesMade = true;
                            }
                        }
                    }
                }

                return changesMade;
            }

            private bool DrawSearchField()
            {
                if (DrawStringFieldWithUndoOption("Search Field:", ref currentLocData.searchString) == false)
                {
                    return false;
                }

                // Populate Search Results
                if (string.IsNullOrEmpty(currentLocData.searchString) == false)
                {
                    List<GUIContent> viableSearchResults = new List<GUIContent>();
                    List<int> viableHashResults = new List<int>();

                    string searchPattern = $"{currentLocData.searchString.Replace(' ', '_')}";

                    for (int index = 0; index < LocIdsHelper.Loc_Strings_TotalLocIdsCount; ++index)
                    {
                        if (Regex.IsMatch(LocIDsEditorData.Combined_EditorAlphabeticallyOrderedLocStringIDs[index].text, searchPattern, RegexOptions.IgnoreCase))
                        {
                            string stringID = LocIDsEditorData.Combined_EditorAlphabeticallyOrderedLocStringIDs[index].text;
                            int hashId = LocIDsEditorData.Combined_EditorLocStringIdToHashId[stringID];
                            viableSearchResults.Add(LocIDsEditorData.Combined_EditorAlphabeticallyOrderedLocStringIDs[index]);
                            viableHashResults.Add(hashId);
                        }
                    }

                    currentLocData.searchResults = viableSearchResults.ToArray();
                    currentLocData.hashResults = viableHashResults.ToArray();

                    if (viableSearchResults.Count > 1)
                    {
                        // If there are valid search results, force us to select the first viable one unless we are already matching something that is in that search result.
                        currentLocData.selectedSearchResultIndex = viableHashResults.IndexOf(currentLocData.currentHashId);
                        if (currentLocData.selectedSearchResultIndex == -1)
                        {
                            currentLocData.selectedSearchResultIndex = 0;
                            currentLocData.currentHashId = viableHashResults[0];
                            return true;
                        }
                    }
                    else if (viableSearchResults.Count == 1)
                    {
                        // Only one match. Just auto-assign it
                        currentLocData.selectedSearchResultIndex = 0;
                        currentLocData.currentHashId = viableHashResults[0];
                        return true;

                    }
                    else
                    {
                        currentLocData.selectedSearchResultIndex = -1;
                    }
                }

                // Depopulate Search Results
                else
                {
                    if (LocIDsEditorData.Combined_EditorHashIdToInspectorPopupIndex.TryGetValue(currentLocData.currentHashId, out currentLocData.currentPopupIndex) == false)
                    {
                        currentLocData.currentPopupIndex = -1;
                    }
                }

                return false;
            }

            private bool DrawSearchResults()
            {
                if (currentLocData.searchResults.Length > 0)
                {
                    int selectedSearchResultIndex = DrawDropdownBox(LocIdDisplayTitle, currentLocData.searchResults, currentLocData.selectedSearchResultIndex, false);
                    if (selectedSearchResultIndex != currentLocData.selectedSearchResultIndex)
                    {
                        currentLocData.selectedSearchResultIndex = selectedSearchResultIndex;
                        currentLocData.currentHashId = currentLocData.hashResults[selectedSearchResultIndex];
                        currentLocData.currentPopupIndex = LocIDsEditorData.Combined_EditorHashIdToInspectorPopupIndex[currentLocData.currentHashId];
                        return true;
                    }
                }
                else
                {
                    using (new GUIDisable())
                    {
                        DrawDropdownBox(LocIdDisplayTitle, new GUIContent[] { new GUIContent("No Results Found") }, 0, false);
                    }
                }
                return false;
            }

            private bool DrawRegularLocIdDropdown()
            {
                if (currentLocData.currentPopupIndex == -1)
                {
                    if (LocIDsEditorData.Combined_EditorHashIdToInspectorPopupIndex.TryGetValue(currentLocData.currentHashId, out currentLocData.currentPopupIndex) == false)
                    {
                        currentLocData.currentPopupIndex = -1;
                    }
                }

                int newSelectedIndex = DrawDropdownBox(LocIdDisplayTitle, LocIDsEditorData.Combined_EditorCategorisedAlphabeticallyOrderedLocStringIDs, currentLocData.currentPopupIndex, false);

                if (newSelectedIndex != -1 && newSelectedIndex != currentLocData.currentPopupIndex)
                {
                    // Return new Loc ID Data because this is a new result from previous selection
                    currentLocData.currentHashId = LocIDsEditorData.Combined_EditorInspectorPopupIndexToHashId[newSelectedIndex];
                    currentLocData.currentPopupIndex = newSelectedIndex;
                    return true;
                }
                return false;
            }
        }
    }
#endif
}