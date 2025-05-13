//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Custom Editor Helper
//             Author: Christopher Allport
//             Date Created: 12th April, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Contains shared Draw Methods (Wrapped Draw Calls) for both the
//          Custom Editor (Custom Inspector) and Custom Editor Window
//          classes.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Object = UnityEngine.Object;


#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnassignedField.Global
// ReSharper disable ClassWithVirtualMembersNeverInherited.Global
// ReSharper disable UnusedMember.Local
// ReSharper disable UnusedAutoPropertyAccessor.Global
#endif

/// <summary> Any method marked with this Attribute is automatically called when the custom inspector is rendered.  </summary>
namespace Localisation.Localisation
{
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.AttributeUsage(System.AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "Big EditorHelper File with multiple classes")]
    public sealed class InspectorRegionAttribute : System.Attribute
    {
        public string FullDisplayLabel { get; private set; }
        public string[] DisplayLabelPerLine { get; private set; }
        public int LinesCount { get; private set; }
        public int DrawOrder { get; private set; }
        public int SpacesAfterRegion { get; private set; }

        private static readonly char[] NewLineSplitters = new char[] { '\n' };

        /// <summary>
        /// Any method inside a class inheriting from the CustomEditorBase class tagged with this attribute will be automatically invoked during inspector render.
        /// </summary>
        /// 
        /// <param name="displayLabel">
        /// The header text to display above the block of contents rendered by the invoking method. Leave as empty if no display label is desired.
        /// </param>
        /// 
        /// <param name="drawOrder">
        /// The order in which this method should be invoked compared to all other methods marked with InspectorRegion. 
        /// If left as -1, method will be invoked in sequential order as listed in your CustomEditor class.
        /// </param>
        /// 
        /// <param name="spacesAfterRegion">
        /// The number of spaces to add after this inspector region is rendered.
        /// </param>
	    public InspectorRegionAttribute(string displayLabel = "", int drawOrder = -1, int spacesAfterRegion = 3)
        {
            FullDisplayLabel = displayLabel.Replace("\t", "    ");
            DrawOrder = drawOrder;
            SpacesAfterRegion = spacesAfterRegion;
            DisplayLabelPerLine = FullDisplayLabel.Split(NewLineSplitters);
            LinesCount = DisplayLabelPerLine.Length;
        }
    }


    /// <summary>
    /// A field marked with this attribute will not be shown in the inspector by default. The field will become your responsibility to show in the inspector.
    /// This attribute is basically the exact same attribute as "HideInInspector", except this one makes more sense to someone else reading the code.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class HandledInCustomInspectorAttribute : System.Attribute
    {
    }
    
    /// <summary>
    /// Any STRING field marked with this attribute will be automatically adjusted to scan through the addressables settings for an addressable corresponding to the specified type.
    /// </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class AddressableAttribute : System.Attribute
    {
        public string AddressableGroup { get; private set; }
        public System.Type TypeToSeek { get; private set; }

        /// <summary>
        /// Any STRING field marked with this attribute will be automatically adjusted to scan through the addressables settings for an addressable corresponding to the specified type.
        /// </summary>
        /// 
        /// <param name="typeToSeek">
        /// The type that this Addressable is seeking. This MUST be either a UnityEngine.Object type or a UnityEngine.Object inherited type.
        /// </param>
        /// 
        /// <param name="addressableGroup">
        /// The addressable group you expect to find this Addressable asset in. You may leave it blank if you do not want this constraint but be aware that without this
        /// constraint the initialisation process may take longer.
        /// </param>
        public AddressableAttribute(System.Type typeToSeek, string addressableGroup = "")
        {
            TypeToSeek = typeToSeek;
            AddressableGroup = addressableGroup;
        }
    }



    /// <summary>
    /// Any STRING field marked with this attribute will be automatically adjusted to use a dropdown list for the Selected Enum type in the Inspector. </summary>
    [System.Diagnostics.Conditional("UNITY_EDITOR")]
    [System.AttributeUsage(System.AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public sealed class EnumDropdownAttribute : System.Attribute
    {
        public System.Type EnumType { get; private set; }
        public bool DrawAlphabeticalOrder { get; private set; }

        /// <summary>
        /// Any STRING field marked with this attribute will be automatically adjusted to use a dropdown list for the Selected Enum type in the Inspector.
        /// </summary>
        /// 
        /// <param name="enumType">
        /// The Enum type that will be used for the dropdown selection.
        /// </param>
        /// 
        /// <param name="drawAlphabeticalOrder">
        /// If true will render the Dropdown via Alphabetical Order.
        /// </param>
        public EnumDropdownAttribute(System.Type enumType, bool drawAlphabeticalOrder = false)
        {
            EnumType = enumType;
            DrawAlphabeticalOrder = drawAlphabeticalOrder;
        }
    }






#if UNITY_EDITOR
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    //	*       Editor Delegate Types
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    public delegate void CallbackMethod();
    public delegate void OneDArrayElementDrawAction(int index);
    public delegate void TwoDArrayElementDrawAction(int outerIndex, int innerIndex);
    public delegate void ThreeDArrayElementDrawAction(int oneDIndex, int twoDIndex, int threeDIndex);
    public delegate int ArraySorter<in T>(T val1, T val2);
    public delegate List<string> AllDropdownOptionsGetter();


    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    //	*       Editor Enum Types
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    public enum BooleanState
    {
        /// <summary> TRUE. </summary>
        TRUE,
        /// <summary> FALSE. </summary>
        FALSE
    }

    public enum SpecialisedEditorVariable
    {
        /// <summary> Normal Editor Value Type. </summary>
        NotSpecialised,
        /// <summary> Boolean Editor Value Type. </summary>
        Boolean,
        /// <summary> Addressable Editor Value Type. </summary>
        Addressable,
        /// <summary> Enum Editor Value Type. </summary>
        EnumDropdown,
    }

    public enum ButtonAlignment
    {
        /// <summary> Inspector Window will show Button from LeftMost side to RightMost side. </summary>
        FullWidth,
        /// <summary> Inspector Window will show Button in center, with gaps from the leftmost and rightmost sides of the window. </summary>
        Centered,
        /// <summary> Inspector Window will show Button in from leftmost side until dead center. </summary>
        Left,
        /// <summary> Inspector Window will show Button in from dead center until rightmost side. </summary>
        Right
    }

    public enum SimpleAlignment
    {
        /// <summary> Text will appear in the centre of the window. </summary>
        Centered,
        /// <summary> Text will appear on the left side of the window. </summary>
        Left,
        /// <summary> Text will appear on the right side of the window. </summary>
        Right
    }

    public enum PlusMinusButtons
    {
        /// <summary> Array '+' Button Pressed. </summary>
        PlusPressed,
        /// <summary> Array '-' Button Pressed. </summary>
        MinusPressed,
        /// <summary> No Array Button Pressed. </summary>
        None
    }

    public enum ArrayModificationResult
    {
        /// <summary> Array was not modified. </summary>
        NoChange,
        /// <summary> Array value was modified. </summary>
        ModifiedValue,
        /// <summary> Array elements were swapped. </summary>
        SwappedItems,
        /// <summary> Array element was deleted. </summary>
        DeletedIndex,
        /// <summary> Last Array Element was deleted. </summary>
        RemovedLastElement,
        /// <summary> New Array element was added. </summary>
        AddedNewElement,
    }


    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    //	*       Editor Storage Classes
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    public struct SerialisableFieldData
    {
        public string Header;
        public FieldInfo FieldInfo;
        public GUIContent Label;
        public SerializedProperty SerialisedProperty;
        public System.Attribute SpecialisationAttr;
        public SpecialisedEditorVariable SpecialisationType;
    }

    public struct ArrayModificationData
    {
        public ArrayModificationResult ModResult;
        public int AffectedIndex;

        public static implicit operator ArrayModificationResult(ArrayModificationData data)
        {
            return data.ModResult;
        }
    }

    public struct InspectorRegionData
    {
        public MethodInfo MethodInfo;
        public InspectorRegionAttribute RegionInfo;
    }

    public struct WrappedStringInfo
    {
        public float EditorWidthAtCalcTime;
        public float MaxLineSize;
        public List<string> WrappedTextLines;

        public string FullText
        {
            get
            {
                string fullText = string.Empty;
                foreach (string line in WrappedTextLines)
                {
                    fullText += line;
                }

                return fullText + "\n";
            }
        }
    }

    public struct AddressableData
    {
        public UnityEngine.Object Asset;
        public UnityEditor.AddressableAssets.Settings.AddressableAssetEntry AddressableInfo;

        public string GUID => AddressableInfo.guid;
        public string Label => AddressableInfo.labels.FirstOrDefault();
        public string Address => AddressableInfo.address;
        public string AssetPath => AddressableInfo.AssetPath;
    }

    public class AddressablesDataContainer
    {
        public AddressableData[] Addressables = null;
        public string[] AllAddresses = null;
    }

    public class ArrayTypeDef<T>
    {
        public T[] StoredArray = null;
        public List<T> StoredList = null;

        public int Count => StoredArray?.Length ?? StoredList.Count;
        public int Length => StoredArray?.Length ?? StoredList.Count;

        public T this[int index]
        {
            get
            {
                return StoredArray != null ? StoredArray[index] : StoredList[index];
            }
            set
            {
                if (StoredArray != null)
                {
                    StoredArray[index] = value;
                }
                else
                {
                    StoredList[index] = value;
                }
            }
        }

        private ArrayTypeDef(T[] array)
        {
            StoredArray = array;
        }
        private ArrayTypeDef(List<T> list)
        {
            StoredList = list;
        }
        public void Sort(ArraySorter<T> sorter)
        {
            if (StoredList != null)
            {
                StoredList.Sort(sorter.Invoke);
            }
            else
            {
                System.Array.Sort(StoredArray, sorter.Invoke);
            }
        }

        public static implicit operator ArrayTypeDef<T>(T[] array)
        {
            return new ArrayTypeDef<T>(array);
        }
        public static implicit operator ArrayTypeDef<T>(List<T> list)
        {
            return new ArrayTypeDef<T>(list);
        }
    }

    public class ArraySorterInfo<T>
    {
        public string ButtonLabel;
        public ArraySorter<T> SorterFunc;
        public string Tooltip = null;
    }

    public class DrawArrayParameters<T>
    {
        public static DrawArrayParameters<T> DefaultParams = new DrawArrayParameters<T>()
        {
        };

        public bool IsArrayResizeable = true;
        public bool CanReorderArray = false;
        public int SpacesToAddBetweenElements = 0;

        public int MaxSize = int.MaxValue;
        public int MinSize = 0;

        public CallbackMethod OnArraySorted = null;
        public ArraySorter<T> AlphabeticalComparer = null;

        public ArraySorterInfo<T> OtherSorter1 = null;
        public ArraySorterInfo<T> OtherSorter2 = null;
        public ArraySorterInfo<T> OtherSorter3 = null;
    }

    public class ArrayCallbackParameters
    {
        /// <summary> The draw method for this 1D array. </summary>
        public OneDArrayElementDrawAction DrawElementCallback1D = null;
        /// <summary> The draw method for this 2D array. </summary>
        public TwoDArrayElementDrawAction DrawElementCallback2D = null;
        /// <summary> The draw method for this 2D array. </summary>
        public ThreeDArrayElementDrawAction DrawElementCallback3D = null;

        /// <summary> When Rendering an array inside an array. We need to know the index of the outer one (2D). </summary>
        public int TwoDOuterArrayIndex = 0;
        /// <summary> When Rendering an array inside an array. We need to know the index of the outer one (3D). </summary>
        public int ThreeDOuterArrayIndex = 0;

        public bool Is1DArray => DrawElementCallback1D != null;
        public bool Is2DArray => DrawElementCallback2D != null;
        public bool Is3DArray => DrawElementCallback3D != null;
    }

    public class ButtonsInfo
    {
        public string ButtonName;
        public CallbackMethod OnClicked;
        public string Tooltip = null;
    }

    public class DropdownChoicesParameters
    {
        /// <summary> The object that this Data is being pulled from. Pass in the Array Element. </summary>
        public object OwningObject;
        /// <summary> The index of the array which is making use of this Param List. If you don't have one. Pass in as zero. </summary>
        public int ArrayIndex;
        /// <summary> Delegate to gets all the dropdown options available as a string. </summary>
        public AllDropdownOptionsGetter AllDropdownOptionsGetter;
        /// <summary> Delegate to get a list of all array items that currently have assigned elements found in the dropdown list. </summary>
        public AllDropdownOptionsGetter AllUsedDropdownOptionsGetter;
        /// <summary> Forces the Dropdown Box Choices backend to refresh/recalculated chosen boxes, etc. This can be intensive, so only refresh when necessary. </summary>
        public bool ForceRefresh = false;
    }


    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    //	*       System Disposable Classes
    //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
    public class UndoBatchRecorder : System.IDisposable
    {
        private static UndoBatchRecorder oldest = null;
        private int _undoGroupIndex;

        public UndoBatchRecorder(string undoGroupName = "Undo Batch")
        {
            // New Batch Undo(s) cannot be done whilst there is an active one currently being batched.
            if (oldest == null)
            {
                UnityEditor.Undo.IncrementCurrentGroup();
                UnityEditor.Undo.SetCurrentGroupName(undoGroupName);
                _undoGroupIndex = UnityEditor.Undo.GetCurrentGroup();

                oldest = this;
            }
        }

        public void Dispose()
        {
            if (oldest == this)
            {
                UnityEditor.Undo.CollapseUndoOperations(_undoGroupIndex);
                oldest = null;
            }
        }
    }

    public class EditorIndentation : System.IDisposable
    {
        private readonly int _indentCount;

        public EditorIndentation()
        {
            _indentCount = 1;
            EditorGUI.indentLevel += _indentCount;
        }

        public EditorIndentation(int indentCount)
        {
            _indentCount = indentCount;
            EditorGUI.indentLevel += _indentCount;
        }

        public EditorIndentation(string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            _indentCount = 1;
            EditorGUI.indentLevel += _indentCount;
        }

        public EditorIndentation(string label, int indentCount)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            _indentCount = indentCount;
            EditorGUI.indentLevel += _indentCount;
        }

        public void Dispose()
        {
            EditorGUI.indentLevel -= _indentCount;
        }
    }

    public readonly struct SetEditorIndentation : System.IDisposable
    {
        private readonly int _priorIndent;

        public SetEditorIndentation(int newIndentLevel)
        {
            _priorIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = newIndentLevel;
        }

        public void Dispose()
        {
            EditorGUI.indentLevel = _priorIndent;
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

    public class EnableDescriptionRendering : System.IDisposable
    {
        // Statics
        private static List<EnableDescriptionRendering> _descriptionRenderersStack = new List<EnableDescriptionRendering>();

        public static bool ShouldRenderDescription
        {
            get { return _descriptionRenderersStack.LastOrDefault()?._showDescription ?? false; }
        }

        // Instance
        private bool _showDescription = false;

        public EnableDescriptionRendering()
        {
            _descriptionRenderersStack.Add(this);
            _showDescription = true;
        }

        public EnableDescriptionRendering(bool condition)
        {
            _descriptionRenderersStack.Add(this);
            _showDescription = condition;
        }

        public void Dispose()
        {
            _descriptionRenderersStack.Remove(this);
        }
    }

    public class DrawBackgroundBox : System.IDisposable
    {
        public DrawBackgroundBox()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.Space();
        }

        public void Dispose()
        {
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }
    }
    
    public class DrawBackgroundBoxAndIndentEditor : System.IDisposable
    {
        private readonly int _indentCount;
        
        public DrawBackgroundBoxAndIndentEditor()
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.Space();
            _indentCount = 1;
            EditorGUI.indentLevel += _indentCount;
        }

        public DrawBackgroundBoxAndIndentEditor(int indentCount)
        {
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.Space();
            _indentCount = indentCount;
            EditorGUI.indentLevel += _indentCount;
        }

        public DrawBackgroundBoxAndIndentEditor(string label)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.Space();
            _indentCount = 1;
            EditorGUI.indentLevel += _indentCount;
        }

        public DrawBackgroundBoxAndIndentEditor(string label, int indentCount)
        {
            EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.Space();
            _indentCount = indentCount;
            EditorGUI.indentLevel += _indentCount;
        }

        public void Dispose()
        {
            EditorGUI.indentLevel -= _indentCount;
            EditorGUILayout.Space();
            EditorGUILayout.EndVertical();
        }
    }


    internal static class SharedCustomEditorValues
    {
        public static readonly string[] MonthsInYear = new string[] 
        { 
            "January", "February", "March", "April", "May", "June", 
            "July", "August", "September", "October", "November", "December" 
        };
        
        public static readonly string[] DaysInMonth = new string[]
		{
		    "1st", "2nd", "3rd", "4th", "5th", "6th", "7th", "8th", "9th", "10th", "11th", "12th", "13th",
            "14th", "15th", "16th", "17th", "18th", "19th", "20th", "21st", "22nd", "23rd", "24th", "25th",
            "26th", "27th", "28th", "29th", "30th", "31st" 
        };
                                                                      
        public static readonly GUIContent LocIdPopupTitle = new GUIContent(string.Empty);
        public static string OpenPathDirectory = @"C:\";
    }

    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:Elements should be ordered by access", Justification = "Main Body Structure Readibility")]
    public abstract class CustomEditorHelper
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* Const Values
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public const BindingFlags REFLECTION_SEARCH_FLAGS = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        public const float TextLayoutPadding = 70.0f;
        public const float ScrollBarPadding = 20.0f;
        public const float PixelsPerIndent = 15.0f;
        public const string DefaultSearchBoxText = "Type To Search...";

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	*{} Class Declarations
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public class FieldModificationResultClamp
        {
            public bool ModificationResult
            {
                get
                {
                    return _modificationResult;
                }
                set
                {
                    if (value)
                    {
                        // Cannot set to false. Can only be set to true and never reverted.
                        _modificationResult = true;
                    }
                }
            }

            private bool _modificationResult = false;

            public static implicit operator FieldModificationResultClamp(bool result)
            {
                return new FieldModificationResultClamp()
                {
                    _modificationResult = result
                };
            }
            public static implicit operator bool(FieldModificationResultClamp clamp)
            {
                return clamp.ModificationResult;
            }
        }

        public class LocIDData
        {
            public int CurrentHashId = 0;
            public int CurrentPopupIndex = -1;

            public string SearchString = string.Empty;
            public string[] SearchResults = null;
            public int[] HashResults = null;
            public int SelectedSearchResultIndex = -1;

            public LocIDData(int locHashId)
            {
                CurrentHashId = locHashId;
            }

            public void SetLocHashValue(int locHashId)
            {
                CurrentHashId = locHashId;
                CurrentPopupIndex = -1;

                SearchString = string.Empty;
                SearchResults = null;
                HashResults = null;
                SelectedSearchResultIndex = -1;
            }
        }

        public class DropdownChoicesData
        {
            public int CurrentSelectionIndex;
            public string[] AllChoices;
            public string[] ChoicesUsedByOtherElements;
            public string[] RemainingSelectableChoices;
            public int[] RemainingChoicesIndexToAllChoicesIndex;
        }

        public class AudioToolkitIDs
        {
            public string[] ActualAudioIds;
            public string[] CategorisedAudioIds;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	*- Private Instance Variables
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private float? _editorWindowContextWidth = null;
        private System.Func<float> _editorWindowContextGetterFunc = null;

        private Dictionary<string, Dictionary<System.Type, AddressablesDataContainer>> _cachedAddressablesContainer = new Dictionary<string, Dictionary<System.Type, AddressablesDataContainer>>();

        private Dictionary<string, bool> _foldoutBooleans = new Dictionary<string, bool>();
        private Dictionary<System.Type, Dictionary<object, bool>> _foldoutOwners = new Dictionary<System.Type, Dictionary<object, bool>>();

        private Dictionary<object, LocIDData> _locIdSearchDataDict = new Dictionary<object, LocIDData>();
        
        private Dictionary<System.Type, string[]> _defaultEnumValueAsString = new Dictionary<System.Type, string[]>();
        private Dictionary<string, Dictionary<System.Type, string[]>> _alphabetisedEnumTypes = new Dictionary<string, Dictionary<System.Type, string[]>>();
        private Dictionary<object, DropdownChoicesData[]> _cachedDropdownChoicesWithUsedElementsRemoved = new Dictionary<object, DropdownChoicesData[]>();
        private Dictionary<string, WrappedStringInfo> _calculatedWrappedStrings = new Dictionary<string, WrappedStringInfo>();

        /// <summary> The width of each ASCII Text Character. </summary>
        private List<float> _textCharacterWidths = new List<float>();
        
        private LocStruct _cachedLocStruct = new LocStruct()
        {
            locId = 0,
            locSubstrings = new LocSubstring[] { "x", "x", "x", "x", "x", "x", "x", "x", "x", "x", },
            highlightedTextColours = new Color[] { Color.blue, Color.blue, Color.blue, Color.blue, Color.blue }
        };

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	*+ Properties
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private UnityEngine.Object BaseTarget { get; set; } = null;
        public bool IsProgressBarWindowActive { get; private set; } = false;

        /// <summary> Position which the value should start being Drawn From (updates with Inspector length). </summary>
        public float StartDrawValueXPos => EditorWindowContextWidth * 0.42f;
        /// <summary> Width of the editable value (updates with Inspector Length). </summary>
        public float DrawValueWidth => EditorWindowContextWidth * 0.58f;

        public virtual Color MainFontColour => new Color32(61, 84, 47, 255);
        public virtual Color SecondaryFontColour => new Color32(137, 107, 47, 255);

        /// <summary> // Everything is fine => BLACK COLOUR. </summary>
        public Color NormalFontColour => new Color(0.000f, 0.000f, 0.000f, 1.000f);
        /// <summary> // Missing Object is Required	=> RED COLOUR. </summary>
        public Color ImportantObjectMissingFontColour => new Color(1.000f, 0.000f, 0.000f, 0.750f);
        /// <summary> // Missing Object => PURPLE COLOUR. </summary>
        public Color ObjectMissingFontColour => new Color(0.627f, 0.121f, 0.729f, 0.750f);

        public bool IsUsingUnityProSkin => EditorGUIUtility.isProSkin;

        public float EditorWindowContextWidth
        {
            get
            {
                if (_editorWindowContextWidth.HasValue == false)
                {
                    _editorWindowContextWidth = _editorWindowContextGetterFunc();
                }
                return _editorWindowContextWidth.Value;
            }
        }

        /// <summary> Gets the Start X Position of the Drag And Drop portion of a Unity Object Reference (EditorGUILayout) that is drawn into the Inspector. </summary>
        public float XPositionOfUnityDrawObjectWithLabel
        {
            get { return EditorWindowContextWidth * 0.4205f; }
        }

        public Vector3? SceneViewMouseViewportPosition
        {
            get
            {
                if (Event.current == null)
                {
                    return null;
                }

                SceneView sceneView = SceneView.currentDrawingSceneView;
                if (sceneView == null)
                {
                    sceneView = SceneView.lastActiveSceneView;
                    if (sceneView == null)
                    {
                        return null;
                    }
                }

                Vector3 mousePosition = Event.current.mousePosition;
                mousePosition.y = sceneView.camera.pixelHeight - mousePosition.y;
                mousePosition = sceneView.camera.ScreenToViewportPoint(mousePosition);
                return mousePosition;
            }
        }

        /// <summary> Returns True if the BaseTarget Object is an Object in the Active Scene hierarchy. False if a Non-Instanced Prefab or Scriptable Object, etc. </summary>
        public bool IsEditingSceneObject
        {
            get { return IsActiveSceneObject(BaseTarget, out _); }
        }

        /// <summary> Returns True if this BaseTarget object is a Prefab which is being edited directly. Returns false if this is not a prefab or if this prefab is being edited as an Instance Object in the Scene Hierarchy. </summary>
        public bool IsEditingPrefabDirectly
        {
            get { return IsOriginalPrefabObject(BaseTarget as MonoBehaviour); }
        }

        /// <summary> Returns True if the BaseTarget Object is a Scriptable Object, False if otherwise. </summary>
        public bool IsEditingScriptableObject
        {
            get { return IsScriptableObject(BaseTarget); }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* {}        Constructor / Destructor
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected CustomEditorHelper(UnityEngine.Object baseTargetInspectorObject, System.Func<float> getEditorWindowContextWidth)
        {
            _editorWindowContextGetterFunc = getEditorWindowContextWidth;

            BaseTarget = baseTargetInspectorObject;

            Undo.undoRedoPerformed += OnUndoRedoPerformed;
        }

        ~CustomEditorHelper()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: On GUI Begin
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void OnGUIBegin()
        {
            if (_textCharacterWidths.Count == 0)
            {
                // Find out the width of each text character we can use (this can be used later to dynamically alter where labels and editable fields are located)
                // This needs to be done duringGUI Rendering. We cannot do this On Initialisation :(
                BuildCharacterWidthsList();
            }

            // Only needs to be updated when called, once per frame.
            _editorWindowContextWidth = null;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: On GUI End
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void OnGUIEnd()
        {
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: On GUI Changed
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void OnGUIChanged()
        {
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Build Field Info List
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Scans through the Fields in the BaseTarget class to determine any fields/variables that should be shown inside the Inspector view. 
        /// This will also ensure that tags have been set up correctly and throw a warning if something is assigned for the inspector but is not serialisable and why. </summary>
        /// <param name="lowestBaseClass"> The lowest class in the inherited classes list we can scan back towards. </param>
        /// <param name="variablesNotToShow"> Variables not to show. </param>
        /// <param name="serialisedObject"> BaseTarget Serialised Object. </param>
        /// <param name="serializableGameplayRelatedFields"> Returns the GamePlay (default inspector) items into this list. </param>
        /// <param name="serializableEditorOnlyFields"> Returns the Editor Only (fields intended for editor use only) items into this list. </param>
        public void BuildFieldInfoList(System.Type lowestBaseClass, LinkedList<string> variablesNotToShow, SerializedObject serialisedObject, List<SerialisableFieldData> serializableGameplayRelatedFields, List<SerialisableFieldData> serializableEditorOnlyFields)
        {
            serializableGameplayRelatedFields.Clear();
            serializableEditorOnlyFields.Clear();


            /// <summary> ~~ Local Function 01 : Check if Inherited Field has already made its way into the list. Make sure we are not adding fields more than once. This happens when looping through base classes which have the same private variable displayLabels (legal). </summary>
            bool HasFieldAlreadyBeenParsed(FieldInfo fieldInfo)
            {
                foreach (SerialisableFieldData s in serializableGameplayRelatedFields)
                {
                    if (string.Equals(s.FieldInfo.Name, fieldInfo.Name))
                    {
                        return true;
                    }
                }

                foreach (SerialisableFieldData s in serializableEditorOnlyFields)
                {
                    if (string.Equals(s.FieldInfo.Name, fieldInfo.Name))
                    {
                        return true;
                    }
                }
                return false;
            }

            /// <summary> ~~ Local Function 02 : Check if allowed to show Field in Inspector. Additionally, check for any potential serialisation issues and output them to console before developer wastes time editing something that won't save. </summary>
            bool IsAllowedToDisplayFieldInEditor(FieldInfo fieldInfo, out SerializedProperty serialisedProperty)
            {
                HandledInCustomInspectorAttribute handledInCustomInspectorAttr = fieldInfo.GetCustomAttribute<HandledInCustomInspectorAttribute>();
                SerializeField attrSerializeField = fieldInfo.GetCustomAttribute<SerializeField>();
                serialisedProperty = null;

                if (variablesNotToShow != null && variablesNotToShow.Contains(fieldInfo.Name))
                {
                    return false;
                }

                if (fieldInfo.IsPublic == false)
                {
                    // If not public, determine if it has the [SerializableField] attribute.
                    if (attrSerializeField == null)
                    {
                        if (handledInCustomInspectorAttr != null)
                        {
                            Debug.LogError($"{BaseTarget.GetType().Name}: {fieldInfo.Name} has been marked for Inspector via the [{nameof(HandledInCustomInspectorAttribute)}] tag. However, {fieldInfo.FieldType.Name} is missing the [{nameof(SerializeField)}] tag.");
                        }
                        return false;
                    }
                }

                // Remove if [HideInInspector]
                HideInInspector attrHideInInspector = fieldInfo.GetCustomAttribute<HideInInspector>();
                if (attrHideInInspector != null)
                {
                    if (handledInCustomInspectorAttr != null)
                    {
                        Debug.LogWarning($"{BaseTarget.GetType().Name}: {fieldInfo.Name} has been marked for Inspector via the [{nameof(HandledInCustomInspectorAttribute)}] tag. However, {fieldInfo.FieldType.Name} is also marked with [{nameof(HideInInspector)}] tag. These both mean the same thing (hiding in Inspector). So you can remove one or the other.");
                    }
                    return false;
                }

                // Delegates cannot be serialised, Except for Unity Events
                if (fieldInfo.FieldType != typeof(UnityEngine.Events.UnityEvent) && fieldInfo.FieldType.IsSubclassOf(typeof(UnityEngine.Events.UnityEvent)) == false)
                {
                    if (fieldInfo.FieldType == typeof(System.Action) || fieldInfo.FieldType.IsSubclassOf(typeof(System.Action))
                        || fieldInfo.FieldType == typeof(System.Delegate) || fieldInfo.FieldType.IsSubclassOf(typeof(System.Delegate)))
                    {
                        if (attrSerializeField != null)
                        {
                            Debug.LogError($"{BaseTarget.GetType().Name}: {fieldInfo.Name} has been marked for Inspector via the [{nameof(SerializeField)}] tag. However, {fieldInfo.FieldType.Name} is of type {{System.Action}} or {{System.Delegate}}. These cannot be serialised. You may serialise a {{UnityEngine.Events.UnityEvent}} instead to do the same thing.");
                        }
                        if (handledInCustomInspectorAttr != null)
                        {
                            Debug.LogError($"{BaseTarget.GetType().Name}: {fieldInfo.Name} has been marked for Inspector via the [{nameof(HandledInCustomInspectorAttribute)}] tag. However, {fieldInfo.FieldType.Name} is of type {{System.Action}} or {{System.Delegate}}. These cannot be serialised. You may serialise a {{UnityEngine.Events.UnityEvent}} instead to do the same thing.");
                        }
                        return false;
                    }
                }

                serialisedProperty = serialisedObject.FindProperty(fieldInfo.Name);
                if (serialisedProperty == null)
                {
                    System.NonSerializedAttribute nonSerialisedAttr = fieldInfo.GetCustomAttribute<System.NonSerializedAttribute>();
                    if (nonSerialisedAttr != null)
                    {
                        // Marked for Non-Serialisation
                        if (handledInCustomInspectorAttr != null)
                        {
                            Debug.LogError($"{BaseTarget.GetType().Name}: {fieldInfo.Name} has been marked for Inspector via the [{nameof(HandledInCustomInspectorAttribute)}] tag. However, {fieldInfo.FieldType.Name} has also been marked with [NonSerializable]. These are two conflicting commands. Please remove one. Going with [NonSerializable] for now.");
                        }
                        if (attrSerializeField != null)
                        {
                            Debug.LogError($"{BaseTarget.GetType().Name}: {fieldInfo.Name} has been marked for Inspector via the [{nameof(SerializeField)}] tag. However, {fieldInfo.FieldType.Name} has also been marked with [NonSerializable]. These are two conflicting commands. Please remove one. Going with [NonSerializable] for now.");
                        }
                        return false;
                    }
                    System.SerializableAttribute serialisableAttr = fieldInfo.FieldType.GetCustomAttribute<System.SerializableAttribute>();
                    if (serialisableAttr == null)
                    {
                        Debug.LogError($"{BaseTarget.GetType().Name}: {fieldInfo.Name} has been marked for Inspector. However, {fieldInfo.FieldType.Name} is missing the [System.Serializable] tag.");
                    }
                    else
                    {
                        if (fieldInfo.FieldType.IsArray)
                        {
                            System.Type elementType = fieldInfo.FieldType.GetElementType();
                            serialisableAttr = elementType!.GetCustomAttribute<System.SerializableAttribute>();
                            if (serialisableAttr == null)
                            {
                                Debug.LogError($"{BaseTarget.GetType().Name}: The array \"{fieldInfo.Name}\" has been marked for Inspector. However, the referenced class type \"{elementType.Name}\" is missing the [System.Serializable] tag.");
                            }
                            else if (elementType.IsGenericType)
                            {
                                Debug.LogError($"{BaseTarget.GetType().Name}: The array \"{fieldInfo.Name}\" has been marked for Inspector. However, the referenced class type \"{elementType.Name}\" cannot be serialised because it is a Generic Class Type.");
                            }
                            else if (elementType.IsAbstract)
                            {
                                Debug.LogError($"{BaseTarget.GetType().Name}: The array \"{fieldInfo.Name}\" has been marked for Inspector. However, the referenced class type \"{elementType.Name}\" cannot be serialised because it is an Abstract Class Type.");
                            }
                            else
                            {
                                Debug.LogError($"{BaseTarget.GetType().Name}: The array \"{fieldInfo.Name}\" has been marked for Inspector. However, this array cannot be serialised... I'm unsure why... if you have an answer, please let Chris Allport know so he can add it to the error checking.");
                            }
                        }
                        else if (fieldInfo.FieldType.IsGenericType)
                        {
                            Debug.LogError($"{BaseTarget.GetType().Name}: \"{fieldInfo.Name}\" has been marked for Inspector. However, this field cannot be serialised because it is a Generic Class Type.");
                        }
                        else if (fieldInfo.FieldType.IsGenericType)
                        {
                            Debug.LogError($"{BaseTarget.GetType().Name}: \"{fieldInfo.Name}\" has been marked for Inspector. However, this field cannot be serialised because it is an Abstract Class Type.");
                        }
                        else
                        {
                            Debug.LogError($"{BaseTarget.GetType().Name}: \"{fieldInfo.Name}\" has been marked for Inspector. However, this field cannot be serialised... I'm unsure why... if you have an answer, please let Chris Allport know so he can add it to the error checking.");
                        }
                    }
                    return false;
                }

                // Should remove this field from the default fields to show? (Check it for serialisation issues first)                
                if (handledInCustomInspectorAttr != null)
                {
                    return false;
                }

                return true;
            }

            /// <summary> ~~ Local Function 03 : Convert Variable Name into Readable Inspector name (removes 'm_' prefix and adds ':' as a suffix) </summary>
            string ResolveVariableDisplayName(FieldInfo fieldInfo, out bool isEditorOnlyField)
            {
                // Get Display displayLabel: Replace ' m_ ' prefix in Variable displayLabel
                string variableDisplayLabel = fieldInfo.Name;
                if (fieldInfo.Name.EndsWith("k__BackingField"))
                {
                    Match propertyNameMatch = Regex.Match(variableDisplayLabel, "<(.+)>", RegexOptions.IgnoreCase);
                    if (propertyNameMatch.Success)
                    {
                        variableDisplayLabel = propertyNameMatch.Groups[1].ToString();
                    }
                }
                
                variableDisplayLabel = Regex.Replace(variableDisplayLabel, "^m?_", string.Empty, RegexOptions.IgnoreCase);
                string replacedEditorVariableDisplayLabel = Regex.Replace(variableDisplayLabel, "^editor", string.Empty, RegexOptions.IgnoreCase);
                isEditorOnlyField = string.Equals(variableDisplayLabel, replacedEditorVariableDisplayLabel) == false;

#if UNITY_2019_1_OR_NEWER
                InspectorNameAttribute inspectorDisplayLabelAttr = fieldInfo.GetCustomAttribute<InspectorNameAttribute>();
                if (inspectorDisplayLabelAttr != null)
                {
                    return inspectorDisplayLabelAttr.displayName.TrimEnd(':') + ':';
                }
                else
#endif
                {
                    string displayLabel = string.IsNullOrEmpty(replacedEditorVariableDisplayLabel) ? $"Unknown Variable:" : $"{char.ToUpper(replacedEditorVariableDisplayLabel[0])}";
                    int varDisplayLabelLength = replacedEditorVariableDisplayLabel.Length;
                    for (int i = 1; i < varDisplayLabelLength; ++i)
                    {
                        if (char.IsUpper(replacedEditorVariableDisplayLabel[i]))
                        {
                            // Capital Letter => Space, then letter. Unless there are two capital letters in a row. In which case leave them next to one another.
                            if (char.IsUpper(replacedEditorVariableDisplayLabel[i - 1]) == false)
                            {
                                displayLabel += $" {replacedEditorVariableDisplayLabel[i]}";
                                continue;
                            }
                        }
                        if (replacedEditorVariableDisplayLabel[i] == '_')
                        {
                            // Underscore => Replace with space
                            displayLabel += ' ';
                            continue;
                        }

                        displayLabel += replacedEditorVariableDisplayLabel[i];
                    }
                    displayLabel += ":";
                    return displayLabel;
                }
            }

            /// <summary> ~~ Local Function 04 : Discover the Inspector Specific Configuration for this Field. </summary>
            void DetermineInspectorFieldSpecialisationType(FieldInfo fieldInfo, out SpecialisedEditorVariable specialisedEditorVariableType, out System.Attribute specialisationAttribute)
            {
                AddressableAttribute addressableAttr = fieldInfo.GetCustomAttribute<AddressableAttribute>();
                if (addressableAttr != null)
                {
                    if (fieldInfo.FieldType == typeof(string) || fieldInfo.FieldType.IsSubclassOf(typeof(string)))
                    {
                        specialisationAttribute = addressableAttr;
                        specialisedEditorVariableType = SpecialisedEditorVariable.Addressable;
                        return;
                    }
                    Debug.LogError($"{BaseTarget.GetType().Name}: {fieldInfo.Name} has been marked as an Addressable Key via the [{nameof(AddressableAttribute)}] tag. However, {fieldInfo.FieldType.Name} is of type \"{fieldInfo.FieldType}\". This must be a string type.");
                }

                EnumDropdownAttribute enumDropdownAttr = fieldInfo.GetCustomAttribute<EnumDropdownAttribute>();
                if (enumDropdownAttr != null)
                {
                    if (fieldInfo.FieldType == typeof(string) || fieldInfo.FieldType.IsSubclassOf(typeof(string)))
                    {
                        specialisationAttribute = enumDropdownAttr;
                        specialisedEditorVariableType = SpecialisedEditorVariable.EnumDropdown;
                        return;
                    }
                    Debug.LogError($"{BaseTarget.GetType().Name}: {fieldInfo.Name} has been marked as an Enum Dropdown via the [{nameof(EnumDropdownAttribute)}] tag. However, {fieldInfo.FieldType.Name} is of type \"{fieldInfo.FieldType}\". This must be a string type.");
                }

                if (fieldInfo.FieldType == true.GetType() || fieldInfo.FieldType == false.GetType())
                {
                    specialisationAttribute = null;
                    specialisedEditorVariableType = SpecialisedEditorVariable.Boolean;
                    return;
                }

                specialisationAttribute = null;
                specialisedEditorVariableType = SpecialisedEditorVariable.NotSpecialised;
            }

            /// <summary> ~~ Local Function 05 : Assign the Valid Inspector Field to the List </summary>
            void AssignInspectorField(FieldInfo fieldInfo, SerializedProperty serialisedProperty, string queuedHeader, string displayLabel, bool isEditorOnlyField, SpecialisedEditorVariable specialisedEditorVariableType, System.Attribute specialisationAttribute)
            {
                GUIContent label;
                TooltipAttribute tooltipAttr = fieldInfo.GetCustomAttribute<TooltipAttribute>();
                if (tooltipAttr != null && string.IsNullOrEmpty(tooltipAttr.tooltip) == false)
                {
                    label = PerformLabelConversion(displayLabel, tooltipAttr.tooltip);
                }
                else
                {
                    label = PerformLabelConversion(displayLabel);
                }

                SerialisableFieldData fieldData = new SerialisableFieldData()
                {
                    Header = queuedHeader,
                    Label = label,
                    FieldInfo = fieldInfo,
                    SpecialisationAttr = specialisationAttribute,
                    SpecialisationType = specialisedEditorVariableType,
                    SerialisedProperty = serialisedProperty,
                };

                if (isEditorOnlyField)
                {
                    serializableEditorOnlyFields.Add(fieldData);
                }
                else
                {
                    serializableGameplayRelatedFields.Add(fieldData);
                }
            }


            // Loop through the Field of the BaseTarget class. Then make your way down the base class chain until you hit Unity.Object
            for (System.Type currentClassType = BaseTarget.GetType(); currentClassType != lowestBaseClass; currentClassType = currentClassType.BaseType)
            {
                FieldInfo[] allFields = currentClassType!.GetFields(REFLECTION_SEARCH_FLAGS);
                string queuedHeader = null;
                foreach (FieldInfo fieldInfo in allFields)
                {
                    if (fieldInfo.DeclaringType != currentClassType)
                    {
                        // We'll process you when we get to you in your respective class.
                        continue;
                    }

                    HeaderAttribute headerAttribute = fieldInfo.GetCustomAttribute<HeaderAttribute>();
                    if (headerAttribute != null)
                    {
                        queuedHeader = headerAttribute.header;
                    }

                    if (fieldInfo.IsLiteral || fieldInfo.IsInitOnly)
                    {
                        // Consts and Static fields not applicable.
                        continue;
                    }

                    // Make sure we are not adding fields more than once. This happens when looping through base classes which have the same private variable displayLabels (legal).
                    if (HasFieldAlreadyBeenParsed(fieldInfo))
                    {
                        continue;
                    }

                    // Outputs any serialisation errors caught. Such as trying to show Fields in the Inspector without having [SerializeField], etc.
                    SerializedProperty serialisedProperty;
                    if (IsAllowedToDisplayFieldInEditor(fieldInfo, out serialisedProperty) == false)
                    {
                        continue;
                    }

                    bool isEditorOnlyField;
                    string displayLabel = ResolveVariableDisplayName(fieldInfo, out isEditorOnlyField);

                    SpecialisedEditorVariable specialisationType;
                    System.Attribute specialisationAttribute;
                    DetermineInspectorFieldSpecialisationType(fieldInfo, out specialisationType, out specialisationAttribute);

                    AssignInspectorField(fieldInfo, serialisedProperty, queuedHeader, displayLabel, isEditorOnlyField, specialisationType, specialisationAttribute);
                    queuedHeader = null;
                }
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Build Character Widths List
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private void BuildCharacterWidthsList()
        {
            _textCharacterWidths.Clear();
            _textCharacterWidths.Add(0.0f); // Null Terminator
            for (int c = 1; c < 256; ++c)
            {
                string s = char.ConvertFromUtf32(c);
                Vector2 textDimensions = GUI.skin.label.CalcSize(new GUIContent(s));
                float charWidth = textDimensions.x;
                _textCharacterWidths.Add(charWidth);
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: When Undo or Redo is Performed
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private void OnUndoRedoPerformed()
        {
            RefreshAddressables();
            
            _cachedDropdownChoicesWithUsedElementsRemoved.Clear();
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Check Object Type
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Returns True if the BaseTarget Object is an Object in the Active Scene hierarchy. False if a Non-Instanced Prefab or Scriptable Object, etc. </summary>
        /// <param name="targetObject"> Object To Query. </param>
        /// <param name="activeScene"> Outputs the active scene this object is a part of (only valid if this function returns true). </param>
        public bool IsActiveSceneObject(UnityEngine.Object targetObject, out UnityEngine.SceneManagement.Scene activeScene)
        {
            activeScene = new UnityEngine.SceneManagement.Scene();

            if (targetObject == null)
            {
                return false;
            }

            // Only Monobehaviours can exist in the scene. Every other type is an Asset type, so can't be a Scene component.
            System.Type targetType = targetObject.GetType();
            if (targetType != typeof(MonoBehaviour) && targetType.IsSubclassOf(typeof(MonoBehaviour)) == false)
            {
                return false;
            }

            // Original Prefab objects also aren't a part of the Active Scene Hierarchy.
            MonoBehaviour asMonoBehaviour = targetObject as MonoBehaviour;
            if (IsOriginalPrefabObject(asMonoBehaviour))
            {
                return false;
            }

            // Now compare the Scenes
            UnityEngine.SceneManagement.Scene targetObjScene = asMonoBehaviour!.gameObject.scene;
            int activeScenesCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int activeSceneIndex = 0; activeSceneIndex < activeScenesCount; ++activeSceneIndex)
            {
                activeScene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(activeSceneIndex);
                if (string.Equals(activeScene.path, targetObjScene.path))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary> Returns True if this BaseTarget object is the Original Prefab Object. Returns false if this is not a prefab or if this prefab is assigned as an Instance Object in the Scene Hierarchy. </summary>
        public bool IsOriginalPrefabObject(MonoBehaviour targetObject)
        {
            if (targetObject == null)
            {
                return false;
            }
            
            // If a PrefabStage is active then a prefab is being modified directly. But that doesn't mean that prefab is this one. Confirm by comparing the Root Objects.
#if UNITY_2021_3_OR_NEWER
            UnityEditor.SceneManagement.PrefabStage prefabStage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#else
            UnityEditor.Experimental.SceneManagement.PrefabStage prefabStage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#endif
            if (prefabStage != null)
            {
                GameObject rootPrefabObject = targetObject.gameObject;
                while (rootPrefabObject.transform.parent != null)
                {
                    rootPrefabObject = rootPrefabObject.transform.parent.gameObject;
                }

                if (prefabStage.prefabContentsRoot == rootPrefabObject)
                {
                    return true;
                }
            }

            // We aren't in the Prefab Scene, but we can still be editing a prefab directly by clicking on it in the project hierarchy and editing it in the inspector window.
            // So just verify both scenes match up. If they do, then we are either an Instance Prefab in an Active Scene hierarchy or not a prefab at all.
            UnityEngine.SceneManagement.Scene targetObjScene = targetObject.gameObject.scene;

            int activeScenesCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
            for (int activeSceneIndex = 0; activeSceneIndex < activeScenesCount; ++activeSceneIndex)
            {
                // False if this Object is found in an Active Scene.
                UnityEngine.SceneManagement.Scene activeScene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(activeSceneIndex);
                if (string.Equals(activeScene.path, targetObjScene.path))
                {
                    return false;
                }
            }

            // We are a prefab that is not being edited in any active scene.
            return true;
        }

        /// <summary> Returns True if the BaseTarget Object is a Scriptable Object, False if otherwise. </summary>
        public bool IsScriptableObject(UnityEngine.Object targetObject)
        {
            return targetObject.GetType() == typeof(ScriptableObject) || targetObject.GetType().IsSubclassOf(typeof(ScriptableObject));
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Add Spaces
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void AddSpaces(int count = 3)
        {
            for (int i = 0; i < count; ++i)
            {
                EditorGUILayout.Space();
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Get Scaled Rect
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public Rect GetScaledRect()
        {
            Rect previousElementRect = GetLastRect();
            float y = previousElementRect.y;
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
            Rect scale = GUILayoutUtility.GetLastRect();
            scale.y = y;
            scale.height = 15;
            return scale;
        }

        public Rect GetLastRect()
        {
            return GUILayoutUtility.GetLastRect();
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Get Tooltip Attribute String
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public string GetTooltipAttributeString(string variableDisplayLabel)
        {
            FieldInfo fieldInfo = BaseTarget.GetType().GetField(variableDisplayLabel, REFLECTION_SEARCH_FLAGS);
            if (fieldInfo != null)
            {
                TooltipAttribute[] tooltips = (fieldInfo.GetCustomAttributes(typeof(TooltipAttribute), true) as TooltipAttribute[])!;
                if (tooltips.Length > 0)
                {
                    return tooltips[0].tooltip;
                }
            }
            return string.Empty;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Perform Label Conversion
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public GUIContent PerformLabelConversion(string displayName, string tooltip = null)
        {
            if (string.IsNullOrEmpty(tooltip) == false)
            {
                if (EnableDescriptionRendering.ShouldRenderDescription)
                {
                    DrawFieldDescription(tooltip, null, SimpleAlignment.Centered);
                }
                return new GUIContent(displayName, tooltip);
            }
            return new GUIContent(displayName);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Foldout Option
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public enum FoldoutResult
        {
            /// <summary> Foldout has not been modified. </summary>
            SameState,
            /// <summary> Foldout has been modified. </summary>
            ChangedState,
        }

        public FoldoutResult DrawFoldoutOption(string displayLabel, ref bool showFoldout, string tooltip = null, Color? drawColour = null)
        {
            GUIContent guiContent = PerformLabelConversion(displayLabel, tooltip);
            bool result;
            if (drawColour.HasValue)
            {
                GUIStyle s = new GUIStyle(EditorStyles.foldout)
                {
                    normal =
                    {
                        textColor = drawColour.Value
                    }
                };
#if UNITY_2017_3_OR_NEWER
                result = EditorGUILayout.Foldout(showFoldout, guiContent, true, s);
#else
                result = EditorGUILayout.Foldout(showFoldout, guiContent, s);
#endif
            }
            else
            {
#if UNITY_2017_3_OR_NEWER
                result = EditorGUILayout.Foldout(showFoldout, guiContent, true);
#else
                result = EditorGUILayout.Foldout(showFoldout, guiContent);
#endif
            }

            if (showFoldout != result)
            {
                showFoldout = result;
                return FoldoutResult.ChangedState;
            }
            return FoldoutResult.SameState;
        }

        public bool DrawFoldoutOption(string displayLabel, bool defaultValue = false, string tooltip = null, Color? drawColour = null)
        {
            bool showFoldout = _foldoutBooleans.GetValueOrDefault(displayLabel, defaultValue);
            FoldoutResult result = DrawFoldoutOption(displayLabel, ref showFoldout, tooltip, drawColour);

            if (result == FoldoutResult.ChangedState)
            {
                _foldoutBooleans[displayLabel] = showFoldout;
            }
            return showFoldout;
        }

        public bool DrawFoldoutOption<T>(string displayLabel, T owner, bool defaultValue = false, string tooltip = null, Color? drawColour = null)
        {
            Dictionary<object, bool> typeDict;
            if (_foldoutOwners.TryGetValue(typeof(T), out typeDict) == false)
            {
                typeDict = new Dictionary<object, bool>();
                _foldoutOwners.Add(typeof(T), typeDict);
            }

            bool showFoldout;
            if (typeDict.TryGetValue(owner, out showFoldout) == false)
            {
                showFoldout = defaultValue;
                typeDict.Add(owner, showFoldout);
            }
            FoldoutResult result = DrawFoldoutOption(displayLabel, ref showFoldout, tooltip, drawColour);

            if (result == FoldoutResult.ChangedState)
            {
                typeDict[owner] = showFoldout;
            }
            return showFoldout;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Splitter
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawSplitter(int pixels = 3, Color? colour = null)
        {
            if (colour.HasValue)
            {
                DrawBox(string.Empty, (int)EditorWindowContextWidth, pixels, colour.Value);
            }
            else
            {
                DrawBox(string.Empty, (int)EditorWindowContextWidth, pixels, new Color32(42, 42, 42, 255));
            }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Box
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawBox(string textToDisplayInBox, int width, int height, Color? backgroundColour)
        {
            EditorGUILayout.Space();
            Rect previousElementRect = GUILayoutUtility.GetLastRect();
            Rect drawRect = new Rect(previousElementRect.x, previousElementRect.y, width, height);

            if (backgroundColour.HasValue)
            {
                Color[] pix = new Color[width * height];
                for (int i = 0; i < pix.Length; ++i)
                {
                    pix[i] = backgroundColour.Value;
                }
                Texture2D backgroundTexture = new Texture2D(width, height);
                backgroundTexture.SetPixels(pix);
                backgroundTexture.Apply();

                GUIStyle style = new GUIStyle(GUI.skin.box)
                {
                    normal =
                    {
                        background = backgroundTexture
                    }
                };

                GUI.Box(drawRect, string.Empty, style);
            }
            else
            {
                GUI.Box(drawRect, string.Empty);
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Progress Bar Window
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawProgressBarWindow(string windowTitle, string oneLineCurrentTask, float progress)
        {
            IsProgressBarWindowActive = true;
            EditorUtility.DisplayProgressBar(windowTitle, oneLineCurrentTask, progress);
        }

        public void ClearProgressBarWindow()
        {
            IsProgressBarWindowActive = false;
            EditorUtility.ClearProgressBar();
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Enum Flags Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public E DrawEnumFlagsField<E>(string displayLabel, E currentValue, string tooltip = null) 
            where E : System.Enum
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return (E)EditorGUILayout.EnumFlagsField(label, currentValue);
        }
        
        public bool DrawEnumFlagsFieldWithUndoOption<E>(string displayLabel, ref E currentValue, string tooltip = null) 
            where E : System.Enum
        {
            E selectedVal = DrawEnumFlagsField(displayLabel, currentValue, tooltip);
            if (selectedVal.Equals(currentValue) == false)
            {
                Undo.RecordObject(BaseTarget, $"Changed: {displayLabel}");
                currentValue = selectedVal;
                return true;
            }
            return false;
        }
        
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Enum Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public E DrawVanillaEnumField<E>(string displayLabel, E currentValue, string tooltip = null) 
            where E : System.Enum
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return (E)EditorGUILayout.EnumPopup(label, currentValue);
        }

        public E DrawEnumField<E>(string displayLabel, E currentValue, bool drawInAlphabeticalOrder = false, string tooltip = null, int? firstVisibleTypeInAlphabeticalOrder = null, params E[] typesNotToShow) 
            where E : System.Enum
        {
            string[] typesToRemove = new string[typesNotToShow.Length];
            for (int i = 0; i < typesNotToShow.Length; ++i)
            {
                typesToRemove[i] = typesNotToShow[i].ToString();
            }

            string result = DrawDropdownWithEnumValues(displayLabel, typeof(E), currentValue.ToString(), drawInAlphabeticalOrder, tooltip, firstVisibleTypeInAlphabeticalOrder, typesToRemove);
            return (E)System.Enum.Parse(typeof(E), result);
        }

        public bool DrawEnumFieldWithUndoOption<E>(string displayLabel, ref E currentValue, bool drawInAlphabeticalOrder = false, string tooltip = null, int? firstVisibleTypeInAlphabeticalOrder = null, params E[] typesNotToShow) 
            where E : System.Enum
        {
            E selectedVal = DrawEnumField(displayLabel, currentValue, drawInAlphabeticalOrder, tooltip, firstVisibleTypeInAlphabeticalOrder, typesNotToShow);
            if (selectedVal.Equals(currentValue) == false)
            {
                Undo.RecordObject(BaseTarget, $"Changed: {displayLabel}");
                currentValue = selectedVal;
                return true;
            }
            return false;
        }

        public void DrawReadonlyEnumField<E>(string displayLabel, E currentValue, string tooltip = null) 
            where E : System.Enum
        {
            using (new GUIDisable())
            {
                DrawVanillaEnumField(displayLabel, currentValue, tooltip);
            }
        }

        public string DrawDropdownWithEnumValues(string displayLabel, System.Type enumType, string currentValue, bool drawInAlphabeticalOrder = false, string tooltip = null, int? firstVisibleTypeInAlphabeticalOrder = null, params string[] typesNotToShow)
        {
            if (drawInAlphabeticalOrder)
            {
                // Caching Alphabetised Enum so we don't have to keep converting every frame.
                Dictionary<System.Type, string[]> alphabetisedEnumDict;
                if (_alphabetisedEnumTypes.TryGetValue(displayLabel, out alphabetisedEnumDict) == false)
                {
                    alphabetisedEnumDict = new Dictionary<System.Type, string[]>();
                    _alphabetisedEnumTypes.Add(displayLabel, alphabetisedEnumDict);
                }

                string[] sortedAlphabetically;
                if (alphabetisedEnumDict.TryGetValue(enumType, out sortedAlphabetically) == false)
                {
                    string[] enumValuesAsStrings;
                    if (_defaultEnumValueAsString.TryGetValue(enumType, out enumValuesAsStrings) == false)
                    {
                        System.Array enumValues = System.Enum.GetValues(enumType);
                        enumValuesAsStrings = new string[enumValues.Length];
                        for (int i = 0; i < enumValuesAsStrings.Length; ++i)
                        {
                            enumValuesAsStrings[i] = enumValues.GetValue(i).ToString();
                        }

                        _defaultEnumValueAsString.Add(enumType, enumValuesAsStrings);
                    }

                    List<string> alphabeticallySorted = new List<string>(enumValuesAsStrings.OrderBy(o => o));
                    if (firstVisibleTypeInAlphabeticalOrder.HasValue)
                    {
                        // Pushing all values back by one space in the array so our first Visible Type appears first.
                        int indexToMoveUp = alphabeticallySorted.IndexOf(enumValuesAsStrings[firstVisibleTypeInAlphabeticalOrder.Value]);
                        if (indexToMoveUp > 0)
                        {
                            for (int i = indexToMoveUp - 1; i > -1; --i)
                            {
                                alphabeticallySorted[i + 1] = alphabeticallySorted[i];
                            }
                            alphabeticallySorted[0] = enumValuesAsStrings[firstVisibleTypeInAlphabeticalOrder.Value];
                        }
                    }
                    foreach (string type in typesNotToShow)
                    {
                        alphabeticallySorted.Remove(type);
                    }

                    sortedAlphabetically = alphabeticallySorted.ToArray();
                    alphabetisedEnumDict.Add(enumType, sortedAlphabetically);
                }

                int selectedIndex = GetIndexOf(sortedAlphabetically, currentValue ?? string.Empty);
                int result = DrawDropdownBox(displayLabel, sortedAlphabetically, selectedIndex, false, tooltip);
                if (result != selectedIndex)
                {
                    if (result == -1)
                    {
                        // Invalid Selection!
                        return currentValue;
                    }
                    return sortedAlphabetically[result];
                }
                return currentValue;
            }
            else
            {
                string[] enumValuesAsStrings;
                if (_defaultEnumValueAsString.TryGetValue(enumType, out enumValuesAsStrings) == false)
                {
                    System.Array enumValues = System.Enum.GetValues(enumType);
                    enumValuesAsStrings = new string[enumValues.Length];
                    for (int i = 0; i < enumValuesAsStrings.Length; ++i)
                    {
                        enumValuesAsStrings[i] = enumValues.GetValue(i).ToString();
                    }

                    _defaultEnumValueAsString.Add(enumType, enumValuesAsStrings);
                }

                string[] enumDropdownValues = enumValuesAsStrings;
                if (firstVisibleTypeInAlphabeticalOrder.HasValue && firstVisibleTypeInAlphabeticalOrder.Value >= 0 && firstVisibleTypeInAlphabeticalOrder.Value < enumDropdownValues.Length)
                {
                    enumDropdownValues = new string[enumValuesAsStrings.Length];
                    System.Array.Copy(enumValuesAsStrings, enumDropdownValues, enumValuesAsStrings.Length);

                    // Pushing all values back by one space in the array so our first Visible Type appears first.
                    string swapTempVal = enumDropdownValues[firstVisibleTypeInAlphabeticalOrder.Value];
                    for (int i = firstVisibleTypeInAlphabeticalOrder.Value - 1; i > -1; --i)
                    {
                        enumDropdownValues[i + 1] = enumDropdownValues[i];
                    }
                    enumDropdownValues[0] = swapTempVal;
                }

                if (typesNotToShow.Length > 0)
                {
                    List<string> enumValuesAsList = new List<string>(enumDropdownValues);
                    foreach (string type in typesNotToShow)
                    {
                        enumValuesAsList.Remove(type);
                    }
                    enumDropdownValues = enumValuesAsList.ToArray();
                }

                int selectedIndex = GetIndexOf(enumDropdownValues, currentValue);
                int result = DrawDropdownBox(displayLabel, enumDropdownValues, selectedIndex, false, tooltip);
                if (result == -1 || selectedIndex == result)
                {
                    // Invalid Selection, or same selection
                    return currentValue;
                }

                return enumDropdownValues[result];
            }
        }

        public bool DrawDropdownWithEnumValuesWithUndoOption(string displayLabel, System.Type enumType, ref string currentValue, bool drawInAlphabeticalOrder = false, string tooltip = null, int? firstVisibleTypeInAlphabeticalOrder = null, params string[] typesNotToShow)
        {
            string result = DrawDropdownWithEnumValues(displayLabel, enumType, currentValue, drawInAlphabeticalOrder, tooltip, firstVisibleTypeInAlphabeticalOrder, typesNotToShow);
            if (string.Equals(result, currentValue) == false)
            {
                currentValue = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Texture
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawSpriteAsTexture(Sprite sprite, float? width = null, float? height = null, SimpleAlignment alignment = SimpleAlignment.Centered)
        {
            if (sprite != null)
            {
                DrawTexture(sprite.texture, width, height, sprite.border, alignment);
            }
            else
            {
                DrawTexture(null, width, height, null, alignment);
            }
        }

        public void DrawImage(Texture texture, float? width = null, float? height = null, Vector4? borderWidths = null, SimpleAlignment alignment = SimpleAlignment.Centered)
        {
            DrawTexture(texture, width, height, borderWidths, alignment);
        }

        public void DrawTexture(Texture texture, float? width = null, float? height = null, Vector4? borderWidths = null, SimpleAlignment alignment = SimpleAlignment.Centered)
        {
            EditorGUILayout.Space();
            Rect rect = GetScaledRect();
            rect.width = width ?? EditorWindowContextWidth * 0.65f;
            rect.height = height ?? Mathf.Min(rect.width, EditorWindowContextWidth * 0.75f);

            if (alignment == SimpleAlignment.Centered)
            {
                rect.x = (EditorWindowContextWidth * 0.5f) - (rect.width * 0.5f);
            }
            else if (alignment == SimpleAlignment.Left)
            {
                rect.x = 0.0f;
            }
            else
            {
                rect.x = EditorWindowContextWidth - rect.width;
            }

            if (texture != null)
            {
                GUI.DrawTexture(rect, texture, ScaleMode.ScaleAndCrop, true, 0, Color.white, borderWidths ?? Vector4.zero, 0);
            }
            else
            {
                Color[] pix = new Color[(int)rect.width * (int)rect.height];
                for (int i = 0; i < pix.Length; ++i)
                {
                    pix[i] = Color.white;
                }
                Texture2D backgroundTexture = new Texture2D((int)rect.width, (int)rect.height);
                backgroundTexture.SetPixels(pix);
                backgroundTexture.Apply();

                GUIStyle style = new GUIStyle(GUI.skin.box)
                {
                    normal =
                    {
                        background = backgroundTexture
                    }
                };

                GUI.Box(rect, string.Empty, style);
            }

            int spacesRequired = Mathf.CeilToInt(rect.height / 6.0f);
            AddSpaces(spacesRequired);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Description Box
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Renders text out in a box. Text will automatically be wrapped to fit the size of the Editor Window. </summary>
        /// <param name="description">Text that will be rendered.</param>
        /// <param name="drawColour">Colour of the text. Leave as null to let the system decide.</param>
        public void DrawDescriptionBox(string description, Color? drawColour = null)
        {
#if UNITY_2019_1_OR_NEWER
            DrawSplitter();
#endif

            // Keep 15% of the Editor context window as padding.
            float acceptableDrawWidth = EditorWindowContextWidth * 0.85f;
            DrawWrappedText(description, acceptableDrawWidth, drawColour, SimpleAlignment.Centered);

            AddSpaces(2);

#if UNITY_2019_1_OR_NEWER
            DrawSplitter();
            AddSpaces(1);
#endif
        }

        /// <summary> Draws a description area. Automatically inserts line breaks when text gets too long. </summary>
        /// <param name="description">Text we intend to render.</param>
        /// <param name="drawColour">Which colour will the text render as. Keep as null to let the default system decide.</param>
        public void DrawFieldDescription(string description, Color? drawColour = null, SimpleAlignment textAlignment = SimpleAlignment.Right)
        {
            if (textAlignment == SimpleAlignment.Centered)
            {
                float startXPos = (EditorGUI.indentLevel * 15.0f) + (TextLayoutPadding * 0.5f);
                float acceptableDrawWidth = (EditorWindowContextWidth - startXPos) - (TextLayoutPadding * 0.5f);
                DrawWrappedText(description, acceptableDrawWidth, null, SimpleAlignment.Centered);
            }
            else if (textAlignment == SimpleAlignment.Left)
            {
                // Keep 15% of the Editor context window as padding.
                float acceptableDrawWidth = EditorWindowContextWidth - TextLayoutPadding;
                DrawWrappedText(description, acceptableDrawWidth, drawColour, textAlignment);
            }
            else
            {
                // Keep 15% of the Editor context window as padding.
                float acceptableDrawWidth = (EditorWindowContextWidth - XPositionOfUnityDrawObjectWithLabel) - TextLayoutPadding;
                DrawWrappedText(description, acceptableDrawWidth, drawColour, textAlignment);
            }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Wrapped Text
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Finds the positions in the string to insert line breaks to keep the flow of the label nice and smooth. </summary>
        /// <param name="text">Text we intend to render.</param>
        /// <param name="maxDrawWidth">Max Draw Width allowed, leave null to draw 90% width of the editor window.</param>
        public WrappedStringInfo GetWrappedText(string text, float? maxDrawWidth = null)
        {
            bool needsToBeCalculated = true;

            if (maxDrawWidth.HasValue == false)
            {
                // 65% Editor Window Width.
                maxDrawWidth = EditorWindowContextWidth * 0.65f;
            }

            WrappedStringInfo wrappedStringData;
            if (_calculatedWrappedStrings.TryGetValue(text, out wrappedStringData))
            {
                // Already calculated Wrapped String. See if it needs to be recalculated.
                const float RecalculateAfterChangeAmount = 0.01f;
                float sizeDiff = Mathf.Abs(wrappedStringData.EditorWidthAtCalcTime - maxDrawWidth.Value);
                if (sizeDiff < RecalculateAfterChangeAmount)
                {
                    // Window has not been resized, or not resized large enough that we need to recalculate.
                    needsToBeCalculated = false;
                }
            }

            // Render Description
            GUIStyle guiStyle = new GUIStyle
            {
                alignment = TextAnchor.UpperCenter
            };

            if (needsToBeCalculated)
            {
                List<string> lines = new List<string>();
                int startIndexOfCurrentLine = 0;
                float maxLineSize = 0.0f;

                while (true)
                {
                    int startIndexOfNextLine;
                    float lineWidth;
                    bool isFinalLine;
                    GetNextWrappedLine(text, maxDrawWidth.Value, startIndexOfCurrentLine, guiStyle, out startIndexOfNextLine, out lineWidth, out isFinalLine);

                    if (lineWidth > maxLineSize)
                    {
                        maxLineSize = lineWidth;
                    }

                    if (startIndexOfCurrentLine >= text.Length)
                    {
                        // Finished. We can get here like this if the text has multiple \n\n\n at the end.
                        break;
                    }

                    int charRange = startIndexOfNextLine - startIndexOfCurrentLine;
                    string line = text.Substring(startIndexOfCurrentLine, charRange);

                    // Replace space at end of line, or newline character if it exists.
                    line = line.TrimEnd(new char[] { '\n', ' ' });

                    if (isFinalLine)
                    {
                        lines.Add(line);
                        break;
                    }
                    else
                    {
                        lines.Add(line + '\n');
                        startIndexOfCurrentLine = startIndexOfNextLine + 1;
                    }
                }

                // Add to dictionary so we don't need to calculate again next frame.
                wrappedStringData = new WrappedStringInfo()
                {
                    EditorWidthAtCalcTime = maxDrawWidth.Value,
                    MaxLineSize = maxLineSize,
                    WrappedTextLines = lines
                };

                _calculatedWrappedStrings[text] = wrappedStringData;
            }

            return wrappedStringData;
        }

        /// <summary> Finds the positions in the string to insert line breaks to keep the flow of the label nice and smooth. </summary>
        /// <param name="text">Text we intend to render.</param>
        /// <param name="maxDrawWidth">Max Draw Width allowed, leave null to draw 90% width of the editor window.</param>
        /// <param name="drawColour">Which colour will the text render as. Keep as null to let the default system decide.</param>
        /// <param name="textAlignment">The alignment of the displayed text.</param>
        public void DrawWrappedText(string text, float? maxDrawWidth = null, Color? drawColour = null, SimpleAlignment textAlignment = SimpleAlignment.Centered)
        {
            GUIStyle guiStyle = new GUIStyle
            {
                alignment = TextAnchor.UpperCenter
            };
            if (drawColour.HasValue == false)
            {
                if (IsUsingUnityProSkin)
                {
                    guiStyle.normal.textColor = new Color32(41, 192, 192, 255);
                }
                else
                {
                    guiStyle.normal.textColor = new Color32(36, 68, 196, 255);
                }
            }
            else
            {
                guiStyle.normal.textColor = drawColour.Value;
            }

            WrappedStringInfo wrappedStringData = GetWrappedText(text, maxDrawWidth);
            if (wrappedStringData.WrappedTextLines.Count > 0)
            {
                foreach (string line in wrappedStringData.WrappedTextLines)
                {
                    AddSpaces(2);
                    Rect pos = GetScaledRect();

                    if (textAlignment == SimpleAlignment.Left)
                    {
                        pos.x = TextLayoutPadding;
                        pos.width = wrappedStringData.MaxLineSize - pos.x;
                    }
                    else if (textAlignment == SimpleAlignment.Right)
                    {
                        pos.x = (EditorWindowContextWidth - wrappedStringData.MaxLineSize) - TextLayoutPadding;
                        pos.width = (EditorWindowContextWidth - pos.x) - TextLayoutPadding;
                    }

                    EditorGUI.LabelField(pos, line, guiStyle);
                }

                AddSpaces(2);
            }
        }

        /// <summary> Returns the Char Index for the next point in the text where we should add a line break to create wrapped text. </summary>
        /// <param name="text">Text we are checking against.</param>
        /// <param name="acceptableDrawWidth">The maximum draw width we are allowing. </param>
        /// <param name="currentCharIndex">The start char index we are looking to find the break point from. </param>
        /// <param name="guiStyle">The GUI Style you intend to draw the label with. </param>
        /// <param name="nextLineCharIndex">Returns the next index in the text where the line break should be added. </param>
        /// <param name="totalLineWidth">Returns the total width of this line. </param>
        /// <param name="isFinalLine">Returns true if this is the final line. </param>
        private void GetNextWrappedLine(string text, float acceptableDrawWidth, int currentCharIndex, GUIStyle guiStyle, out int nextLineCharIndex, out float totalLineWidth, out bool isFinalLine)
        {
            totalLineWidth = 0.0f;
            float lineWidthAtPreviousSpace = 0.0f;
            int lastSpaceIndex = -1;

            isFinalLine = false;

            for (; currentCharIndex < text.Length; ++currentCharIndex)
            {
                if (text[currentCharIndex] == '\n')
                {
                    break;
                }

                // Discovering the Char Size
                string unicodeChar = char.ConvertFromUtf32(text[currentCharIndex]);
                Vector2 textDimensions = guiStyle.CalcSize(new GUIContent(unicodeChar));
                float charSize = textDimensions.x;

                totalLineWidth += charSize;
                if (totalLineWidth > acceptableDrawWidth)
                {
                    // Exceeded range. Go back to previous space if it exists. Otherwise, just cut off the text here and go to the next line.
                    // This will drop us into the "End Of Line" section below.
                    if (lastSpaceIndex != -1)
                    {
                        currentCharIndex = lastSpaceIndex;
                        totalLineWidth = lineWidthAtPreviousSpace;
                    }
                    else
                    {
                        totalLineWidth -= charSize;
                    }
                    break;
                }
                else if (currentCharIndex == (text.Length - 1))
                {
                    // Final character.
                    isFinalLine = true;
                    ++currentCharIndex;
                    break;
                }
                else
                {
                    // Not yet exceeded range.
                    if (text[currentCharIndex] == ' ')
                    {
                        lastSpaceIndex = currentCharIndex;
                        lineWidthAtPreviousSpace = totalLineWidth;
                    }
                }
            }

            // Start of Next Line Index Found.
            nextLineCharIndex = currentCharIndex;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw String/Text Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public string DrawStringField(string displayLabel, string currentValue, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.TextField(label, currentValue);
        }

        public void DrawReadonlyStringField(string displayLabel, string currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawStringField(displayLabel, currentValue, tooltip);
            }
        }

        public string DrawTextField(string displayLabel, string currentValue, string tooltip = null)
        {
            return DrawStringField(displayLabel, currentValue, tooltip);
        }

        public void DrawReadonlyTextField(string displayLabel, string currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawStringField(displayLabel, currentValue, tooltip);
            }
        }

        public string DrawTextArea(string textToDisplay)
        {
            const string NewLinesText = "\n";

            GUIStyle style = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true
            };
            string output = EditorGUILayout.TextArea(textToDisplay + NewLinesText, style);
            return output.Substring(0, output.Length - NewLinesText.Length);
        }

        public void DrawReadonlyTextArea(string textToDisplay)
        {
            using (new GUIDisable())
            {
                DrawTextArea(textToDisplay);
            }
        }

        public bool DrawTextAreaWithUndoOption(ref string currentValue)
        {
            string result = DrawTextArea(currentValue);
            if (string.IsNullOrEmpty(result))
            {
                if (string.IsNullOrEmpty(currentValue) == false)
                {
                    // String is no longer empty/null
                    Undo.RecordObject(BaseTarget, $"Changed Text Area Value");
                    currentValue = result;
                    return true;
                }
            }
            else if (string.Equals(result, currentValue) == false)
            {
                // Needed the 'if' above because we would get a null ref otherwise if we did an equals check here.
                Undo.RecordObject(BaseTarget, $"Changed Text Area Value");
                currentValue = result;
                return true;
            }
            return false;
        }

        public bool DrawStringFieldWithUndoOption(string displayLabel, ref string currentValue, string tooltip = null)
        {
            string result = DrawStringField(displayLabel, currentValue, tooltip);
            if (string.IsNullOrEmpty(result))
            {
                if (string.IsNullOrEmpty(currentValue) == false)
                {
                    // String is no longer empty/null
                    Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                    currentValue = result;
                    return true;
                }
            }
            else if (string.Equals(result, currentValue) == false)
            {
                // Needed the 'if' above because we would get a null ref otherwise if we did an equals check here.
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }

        public bool DrawTextFieldWithUndoOption(string displayLabel, ref string currentValue, string tooltip = null)
        {
            return DrawStringFieldWithUndoOption(displayLabel, ref currentValue, tooltip);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Button
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawReadonlyButtonField(string displayLabel, string tooltip = null, ButtonAlignment alignment = ButtonAlignment.FullWidth)
        {
            bool guiEnabled = GUI.enabled;
            GUI.enabled = false;
            DrawButtonField(displayLabel, tooltip, alignment);
            GUI.enabled = guiEnabled;
        }

        public bool DrawButtonField(string displayLabel, string tooltip = null, ButtonAlignment alignment = ButtonAlignment.FullWidth)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            if (alignment == ButtonAlignment.FullWidth)
            {
                return GUILayout.Button(label);
            }

            const float padding = 30.0f;
            EditorGUILayout.Space();
            float contextWidth = EditorWindowContextWidth;
            Rect rect = GetScaledRect();
            rect.height = 18.0f;
            rect.width = contextWidth * 0.5f;
            if (alignment == ButtonAlignment.Left)
            {
                rect.x = padding;
            }
            else if (alignment == ButtonAlignment.Centered)
            {
                rect.x = contextWidth * 0.25f;
            }
            else
            {
                rect.x = (contextWidth * 0.5f) - padding;
            }

            AddSpaces(3);
            return GUI.Button(rect, label);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Int Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public int DrawIntField(string displayLabel, int currentValue, string tooltip = null, int? minValue = null, int? maxValue = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            int result = EditorGUILayout.IntField(label, currentValue);
            if (minValue.HasValue && result < minValue.Value)
            {
                return minValue.Value;
            }
            if (maxValue.HasValue && result > maxValue.Value)
            {
                return maxValue.Value;
            }
            return result;
        }

        public void DrawReadonlyIntField(string displayLabel, int currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawIntField(displayLabel, currentValue, tooltip, null, null);
            }
        }

        public bool DrawIntFieldWithUndoOption(string displayLabel, ref int currentValue, string tooltip = null, int? minValue = null, int? maxValue = null)
        {
            int result = DrawIntField(displayLabel, currentValue, tooltip, minValue, maxValue);
            if (result != currentValue)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Float Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public float DrawFloatField(string displayLabel, float currentValue, string tooltip = null, float? minValue = null, float? maxValue = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            float result = EditorGUILayout.FloatField(label, currentValue);
            if (minValue.HasValue && result < minValue.Value)
            {
                return minValue.Value;
            }
            if (maxValue.HasValue && result > maxValue.Value)
            {
                return maxValue.Value;
            }
            return result;
        }

        public void DrawReadonlyFloatField(string displayLabel, float currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawFloatField(displayLabel, currentValue, tooltip, null, null);
            }
        }

        public bool DrawFloatFieldWithUndoOption(string displayLabel, ref float currentValue, string tooltip = null, float? minValue = null, float? maxValue = null)
        {
            float result = DrawFloatField(displayLabel, currentValue, tooltip, minValue, maxValue);
            if (Mathf.Approximately(result, currentValue) == false)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Double Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public double DrawDoubleField(string displayLabel, double currentValue, string tooltip = null, double? minValue = null, double? maxValue = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            double result = EditorGUILayout.DoubleField(label, currentValue);
            if (minValue.HasValue && result < minValue.Value)
            {
                return minValue.Value;
            }
            if (maxValue.HasValue && result > maxValue.Value)
            {
                return maxValue.Value;
            }
            return result;
        }

        public void DrawReadonlyDoubleField(string displayLabel, double currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDoubleField(displayLabel, currentValue, tooltip, null, null);
            }
        }

        public bool DrawDoubleFieldWithUndoOption(string displayLabel, ref double currentValue, string tooltip = null, double? minValue = null, double? maxValue = null)
        {
            double result = DrawDoubleField(displayLabel, currentValue, tooltip, minValue, maxValue);
            if (Math.Abs(result - currentValue) > Mathf.Epsilon)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Toggle Field / Bool Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public bool DrawToggleField(string displayLabel, bool currentValue, string tooltip = null, bool useEnumPopup = true)
        {
            return DrawBoolField(displayLabel, currentValue, tooltip, useEnumPopup);
        }

        public void DrawReadonlyToggleField(string displayLabel, bool currentValue, string tooltip = null, bool useEnumPopup = true)
        {
            using (new GUIDisable())
            {
                DrawBoolField(displayLabel, currentValue, tooltip, useEnumPopup);
            }
        }

        public bool DrawToggleFieldWithUndoOption(string displayLabel, ref bool currentValue, string tooltip = null, bool useEnumPopup = true)
        {
            return DrawBoolFieldWithUndoOption(displayLabel, ref currentValue, tooltip, useEnumPopup);
        }

        public bool DrawBoolField(string displayLabel, bool currentValue, string tooltip = null, bool useEnumPopup = true)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            if (useEnumPopup)
            {
                return (BooleanState)EditorGUILayout.EnumPopup(label, currentValue ? BooleanState.TRUE : BooleanState.FALSE) == BooleanState.TRUE;
            }
            return EditorGUILayout.Toggle(label, currentValue);
        }

        public void DrawReadonlyBoolField(string displayLabel, bool currentValue, string tooltip = null, bool useEnumPopup = true)
        {
            using (new GUIDisable())
            {
                DrawBoolField(displayLabel, currentValue, tooltip, useEnumPopup);
            }
        }

        public bool DrawBoolFieldWithUndoOption(string displayLabel, ref bool currentValue, string tooltip = null, bool useEnumPopup = true)
        {
            bool result = DrawToggleField(displayLabel, currentValue, tooltip, useEnumPopup);
            if (result != currentValue)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Addressables Option
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Addressables Option
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void RefreshAddressables()
        {
            _cachedAddressablesContainer.Clear();
        }

        /// <summary> Gets The Addressables Contents of Type from the specified Group. If the Group param is null or empty, will get the Contents of Type from all groups. </summary>
        /// <param name="_type"> The Type of Object you are seeking. </param>
        /// <param name="_addressablesGroupName"> The Specified Group you wish to grab Addressables from. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        public AddressablesDataContainer GetOrAddAddressablesData(System.Type _type, string _addressablesGroupName, string regexPatternMatching)
        {
            _addressablesGroupName ??= string.Empty;

            Dictionary<System.Type, AddressablesDataContainer> addressablesContainerForTypes;
            if (_cachedAddressablesContainer.TryGetValue(_addressablesGroupName, out addressablesContainerForTypes) == false)
            {
                addressablesContainerForTypes = new Dictionary<System.Type, AddressablesDataContainer>();
                _cachedAddressablesContainer.Add(_addressablesGroupName, addressablesContainerForTypes);
            }

            AddressablesDataContainer addressablesDataContainer;
            if (addressablesContainerForTypes.TryGetValue(_type, out addressablesDataContainer) == false)
            {
                bool needsDepthSearch = _type == typeof(MonoBehaviour) || _type.IsSubclassOf(typeof(MonoBehaviour));

                addressablesDataContainer = new AddressablesDataContainer();
                addressablesContainerForTypes.Add(_type, addressablesDataContainer);

                List<UnityEditor.AddressableAssets.Settings.AddressableAssetGroup> assetGroups;
                if (string.IsNullOrEmpty(_addressablesGroupName))
                {
                    assetGroups = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.groups;
                }
                else
                {
                    assetGroups = new List<UnityEditor.AddressableAssets.Settings.AddressableAssetGroup>()
                    {
                        UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.FindGroup(_addressablesGroupName)
                    };
                }

                // Recursively gets all sub entries also
                List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry> allAssetEntries = new List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry>();
                void RecursivelyAddEntries(UnityEditor.AddressableAssets.Settings.AddressableAssetEntry _assetEntry)
                {
                    if (_assetEntry != null)
                    {
                        if (allAssetEntries.Contains(_assetEntry) == false)
                        {
                            allAssetEntries.Add(_assetEntry);
                        }

                        if (_assetEntry.SubAssets != null)
                        {
                            foreach (var subEntry in _assetEntry.SubAssets)
                            {
                                if (subEntry != null && allAssetEntries.Contains(subEntry) == false)
                                {
                                    RecursivelyAddEntries(subEntry);
                                }
                            }
                        }
                    }
                }

                foreach (var assetGroup in assetGroups)
                {
                    List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry> assetEntries = assetGroup.entries.ToList();
                    foreach (var assetEntry in assetEntries)
                    {
                        RecursivelyAddEntries(assetEntry);
                    }
                }


                List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry> allApplicableAssetEntries = new List<UnityEditor.AddressableAssets.Settings.AddressableAssetEntry>(allAssetEntries);
                if (needsDepthSearch)
                {
                    allApplicableAssetEntries.RemoveAll(a =>
                    {
                        if (a.TargetAsset == null)
                            return true;
                        GameObject go = a.TargetAsset as GameObject;
                        if (go != null)
                            return go.GetComponent(_type) == null;
                        return true;
                    });
                }
                else
                {
                    allApplicableAssetEntries.RemoveAll(a =>
                    {
                        if (a.TargetAsset == null)
                            return true;
                        System.Type type = a.TargetAsset.GetType();
                        return (type == _type || type.IsSubclassOf(_type)) == false;
                    });
                }

                allApplicableAssetEntries.Sort((a, b) => string.CompareOrdinal(a.address, b.address));
                if (string.IsNullOrEmpty(regexPatternMatching) == false)
                {
                    allApplicableAssetEntries.RemoveAll(a => Regex.IsMatch(a.address, regexPatternMatching) == false);
                }

                int totalAddressables = allApplicableAssetEntries.Count;
                addressablesDataContainer.Addressables = new AddressableData[totalAddressables + 1];
                addressablesDataContainer.AllAddresses = new string[totalAddressables + 1];
                
                addressablesDataContainer.AllAddresses[0] = "N\\A";
                addressablesDataContainer.Addressables[0] = new AddressableData()
                {
                    Asset = null,
                    AddressableInfo = null,
                };

                for (int index = 0; index < totalAddressables; ++index)
                {
                    UnityEngine.Object asset;
                    if (needsDepthSearch)
                    {
                        asset = (allApplicableAssetEntries[index].TargetAsset as GameObject)!.GetComponent(_type);
                    }
                    else
                    {
                        asset = allApplicableAssetEntries[index].TargetAsset;
                    }

                    AddressableData addressableData = new AddressableData()
                    {
                        Asset = asset,
                        AddressableInfo = allApplicableAssetEntries[index],
                    };

                    addressablesDataContainer.Addressables[index + 1] = addressableData;
                    addressablesDataContainer.AllAddresses[index + 1] = addressableData.Address;
                }
            }

            return addressablesDataContainer;
        }

        /// <summary> Returns the number of addressables using 'x' label. </summary>
        /// <param name="_addressablesLabel"> The label to seek. </param>
        /// <param name="_addressableGroup"> The  addressable group where this addressable can be found. This is not required, but will speed up the search if you know where to look. </param>
        public int GetTotalAddressablesWithLabel(string _addressablesLabel, string _addressableGroup = "", string regexPatternMatching = "")
        {
            AddressablesDataContainer allAddressables = GetOrAddAddressablesData(typeof(UnityEngine.Object), _addressableGroup, regexPatternMatching);
            int addressablesCount = allAddressables.Addressables.Length;

            int assignedLabelsCount = 0;
            for (int i = 0; i < addressablesCount; ++i)
            {
                if (string.Equals(_addressablesLabel, allAddressables.Addressables[i].Label))
                    ++assignedLabelsCount;
            }
            return assignedLabelsCount;
        }

        /// <summary> Returns All Addressables using the specified label</summary>
        /// <param name="_addressablesLabel"> The label to seek. </param>
        /// <param name="_addressableGroup"> The  addressable group where this addressable can be found. This is not required, but will speed up the search if you know where to look. </param>
        public List<UnityEngine.Object> GetAddressablesWithLabel(string _addressablesLabel, string _addressableGroup = "", string regexPatternMatching = "")
        {
            AddressablesDataContainer allAddressables = GetOrAddAddressablesData(typeof(UnityEngine.Object), _addressableGroup, regexPatternMatching);
            int addressablesCount = allAddressables.Addressables.Length;

            List<UnityEngine.Object> assignedObjects = new List<Object>();
            for (int i = 0; i < addressablesCount; ++i)
            {
                if (string.Equals(_addressablesLabel, allAddressables.Addressables[i].Label))
                    assignedObjects.Add(allAddressables.Addressables[i].Asset);
            }
            return assignedObjects;
        }

        /// <summary> Gets The Addressables Contents of Type from the addressables list. </summary>
        /// <typeparam name="T"> The type of object you are seeking. </typeparam>
        /// <param name="_address"> The address where this asset can be located in the Addressables list. </param>
        /// <param name="_addressableGroup"> The  addressable group where this addressable can be found. This is not required, but will speed up the search if you know where to look. </param>
        public T GetAddressableAssetWithAddress<T>(string _address, string _addressableGroup = "") where T : UnityEngine.Object
        {
            object obj = GetAddressableAssetWithAddress(typeof(T), _address, _addressableGroup);
            return obj as T;
        }

        /// <summary> Gets The Addressables Contents of Type from the addressables list. </summary>
        /// <param name="_type"> The type of object you are seeking. </param>
        /// <param name="_address"> The address where this asset can be located in the Addressables list. </param>
        /// <param name="_addressableGroup"> The  addressable group where this addressable can be found. This is not required, but will speed up the search if you know where to look. </param>
        public object GetAddressableAssetWithAddress(System.Type _type, string _address, string _addressableGroup = "", string regexPatternMatching = "")
        {
            AddressablesDataContainer container = GetOrAddAddressablesData(_type, _addressableGroup, regexPatternMatching);
            int assetsCount = container.Addressables.Length;
            if (assetsCount > 0)
            {
                for (int i = 0; i < assetsCount; ++i)
                {
                    if (string.Equals(container.Addressables[i].Address, _address))
                    {
                        return container.Addressables[i].Asset;
                    }
                }
            }

            return null;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group. </summary>
        /// <typeparam name="T"> The Type of items you are looking for. </typeparam>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentSelectedIndex"> What index have you currently selected? </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public int DrawAddressablesDropdownOptions<T>(string _displayLabel, string _addressablesGroupName, int _currentSelectedIndex, string _tooltip = null) where T : UnityEngine.Object
        {
            return DrawAddressablesDropdownOptions(_displayLabel, typeof(T), _addressablesGroupName, _currentSelectedIndex, _tooltip);
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group. </summary>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_type"> The type of object you are seeking. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentSelectedIndex"> What index have you currently selected? </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public int DrawAddressablesDropdownOptions(string _displayLabel, System.Type _type, string _addressablesGroupName, int _currentSelectedIndex, string regexPatternMatching = "", string _tooltip = null)
        {
            AddressablesDataContainer addressableDataContainer = GetOrAddAddressablesData(_type, _addressablesGroupName, regexPatternMatching);
            int result = DrawDropdownBox(_displayLabel, addressableDataContainer.AllAddresses, _currentSelectedIndex, false, _tooltip);
            return result;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group. </summary>
        /// <typeparam name="T"> The Type of items you are looking for. </typeparam>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentKey"> What addressable id do you currently have selected </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public string DrawAddressablesDropdownOptions<T>(string _displayLabel, string _addressablesGroupName, string _currentKey, string _tooltip = null) where T : UnityEngine.Object
        {
            return DrawAddressablesDropdownOptions(_displayLabel, typeof(T), _addressablesGroupName, _currentKey, _tooltip);
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group. </summary>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_type"> The type of object you are seeking. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentKey"> What addressable id do you currently have selected </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public string DrawAddressablesDropdownOptions(string _displayLabel, System.Type _type, string _addressablesGroupName, string _currentKey, string regexPatternMatching = "", string _tooltip = null)
        {
            AddressablesDataContainer addressableDataContainer = GetOrAddAddressablesData(_type, _addressablesGroupName, regexPatternMatching);
            if (addressableDataContainer.AllAddresses == null)
            {
                DrawReadonlyDropdownBox(_displayLabel, new string[] { $"No Addressables Found for [{_type.Name}]." }, 0, false, _tooltip);
                return _currentKey;
            }

            int index = GetIndexOf(addressableDataContainer.AllAddresses, _currentKey);
            int result = DrawDropdownBox(_displayLabel, addressableDataContainer.AllAddresses, index, false, _tooltip);
            if (result > -1)
            {
                return addressableDataContainer.AllAddresses[result];
            }
            return _currentKey;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group. </summary>
        /// <typeparam name="T"> The Type of items you are looking for. </typeparam>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentSelectedIndex"> What index have you currently selected? </param>
        /// <param name="_assetValue"> Returns the Asset Value if applicable </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public int DrawAddressablesDropdownOptions<T>(string _displayLabel, string _addressablesGroupName, int _currentSelectedIndex, out T _assetValue, string _tooltip = null) where T : UnityEngine.Object
        {
            UnityEngine.Object assetObjValue;
            int result = DrawAddressablesDropdownOptions(_displayLabel, typeof(T), _addressablesGroupName, _currentSelectedIndex, out assetObjValue, _tooltip);
            _assetValue = assetObjValue != null ? assetObjValue as T : null;
            return result;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group. </summary>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_type"> The type of object you are seeking. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentSelectedIndex"> What index have you currently selected? </param>
        /// <param name="_assetValue"> Returns the Asset Value if applicable </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public int DrawAddressablesDropdownOptions(string _displayLabel, System.Type _type, string _addressablesGroupName, int _currentSelectedIndex, out UnityEngine.Object _assetValue, string regexPatternMatching = "", string _tooltip = null)
        {
            AddressablesDataContainer addressableDataContainer = GetOrAddAddressablesData(_type, _addressablesGroupName, regexPatternMatching);
            if (addressableDataContainer.AllAddresses == null)
            {
                DrawReadonlyDropdownBox(_displayLabel, new string[] { $"No Addressables Found for [{_type.Name}]." }, 0, false, _tooltip);
                _assetValue = null;
                return _currentSelectedIndex;
            }

            int result = DrawDropdownBox(_displayLabel, addressableDataContainer.AllAddresses, _currentSelectedIndex, false, _tooltip);
            if (result > -1)
                _assetValue = addressableDataContainer.Addressables[result].Asset;
            else
                _assetValue = null;
            return result;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group. </summary>
        /// <typeparam name="T"> The Type of items you are looking for. </typeparam>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentKey"> What addressable id do you currently have selected </param>
        /// <param name="_assetValue"> Returns the Asset Value if applicable </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public string DrawAddressablesDropdownOptions<T>(string _displayLabel, string _addressablesGroupName, string _currentKey, out T _assetValue, string _tooltip = null) where T : UnityEngine.Object
        {
            UnityEngine.Object assetObjValue;
            string result = DrawAddressablesDropdownOptions(_displayLabel, typeof(T), _addressablesGroupName, _currentKey, out assetObjValue, _tooltip);
            _assetValue = assetObjValue != null ? assetObjValue as T : null;
            return result;
        }

        private class SearchData
        {
            public string searchText;
            public string[] searchResults;
            public int searchResultIndex;
        }

        private Dictionary<string, SearchData> addressableQueryToSearchData = new Dictionary<string, SearchData>();
        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group. </summary>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_type"> The type of object you are seeking. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentKey"> What addressable id do you currently have selected </param>
        /// <param name="_assetValue"> Returns the Asset Value if applicable </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        /// <param name="_convertUnderscoreToSubMenu">Whether to convert underscores in the addressable name to a sub-menu.</param>
        public string DrawAddressablesDropdownOptions(string _displayLabel, System.Type _type, string _addressablesGroupName, string _currentKey, out UnityEngine.Object _assetValue, string regexPatternMatching = "", string _tooltip = null, bool _convertUnderscoreToSubMenu = false)
        {
            AddressablesDataContainer addressableDataContainer = GetOrAddAddressablesData(_type, _addressablesGroupName, regexPatternMatching);

            // Draw Search Box
            string addressableQuery = $"{_displayLabel}_{_type.FullName}_{_addressablesGroupName}";
            if (addressableQueryToSearchData.TryGetValue(addressableQuery, out SearchData searchData) == false)
            {
                searchData = new SearchData
                {
                    searchResults = addressableDataContainer.AllAddresses,
                    searchResultIndex = GetIndexOf(addressableDataContainer.AllAddresses, _currentKey)
                };
                
                addressableQueryToSearchData.Add(addressableQuery, searchData);
            }

            if (GUI.enabled)
            {
                // No point in drawing the Search Box if GUI is disabled as there can be no user interaction
                string newSearchText = EditorGUILayout.TextField(string.IsNullOrEmpty(searchData.searchText) ? DefaultSearchBoxText : searchData.searchText);

                if (string.Equals(newSearchText, DefaultSearchBoxText) == false && string.Equals(newSearchText, searchData.searchText) == false)
                {
                    searchData.searchText = newSearchText;
                    
                    // Populate Search Results
                    if (string.IsNullOrEmpty(newSearchText) == false)
                    {
                        List<string> viableSearchResults = new List<string>();
                        string searchPattern = $"{newSearchText.Replace(' ', '_')}";

                        foreach (string address in addressableDataContainer.AllAddresses)
                        {
                            if (Regex.IsMatch(address, searchPattern, RegexOptions.IgnoreCase))
                            {
                                viableSearchResults.Add(address);
                            }
                        }

                        searchData.searchResults = viableSearchResults.ToArray();

                        if (viableSearchResults.Count > 1)
                        {
                            // If there are valid search results, force us to select the first viable one unless we are already matching something that is in that search result.
                            searchData.searchResultIndex = GetIndexOf(viableSearchResults, _currentKey);
                            if (searchData.searchResultIndex == -1)
                            {
                                searchData.searchResultIndex = 0;
                            }
                        }
                        else if (viableSearchResults.Count == 1)
                        {
                            // Only one match. Just auto-assign it
                            searchData.searchResultIndex = 0;
                        }
                        else
                        {
                            searchData.searchResultIndex = -1;
                        }
                    }

                    // Depopulate Search Results
                    else
                    {
                        searchData.searchResults = addressableDataContainer.AllAddresses;
                        searchData.searchResultIndex = GetIndexOf(searchData.searchResults, _currentKey);
                    }
                }
            }
            
            if (_convertUnderscoreToSubMenu)
                searchData.searchResults = searchData.searchResults.Select(s => s.Replace("_", "/").Replace("//", "/")).ToArray();

            searchData.searchResultIndex = DrawDropdownBox(_displayLabel, searchData.searchResults, searchData.searchResultIndex, false, _tooltip);
            if (searchData.searchResultIndex > -1 && searchData.searchResultIndex < searchData.searchResults.Length)
            {
                int addressesCount = addressableDataContainer.Addressables.Length;
                for (int i = 1; i < addressesCount; ++i)
                {
                    if (string.Equals(addressableDataContainer.Addressables[i].Address, searchData.searchResults[searchData.searchResultIndex]))
                    {
                        _assetValue = addressableDataContainer.Addressables[i].Asset;
                        return searchData.searchResults[searchData.searchResultIndex];
                    }
                }
            }

            _assetValue = null;
            return _currentKey;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group and adds the option for an Undo when changed. </summary>
        /// <typeparam name="T"> The Type of items you are looking for. </typeparam>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentSelectedIndex"> What index have you currently selected? </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public bool DrawAddressablesDropdownWithUndoOption<T>(string _displayLabel, string _addressablesGroupName, ref int _currentSelectedIndex, string regexPatternMatching = "", string _tooltip = null) where T : UnityEngine.Object
        {
            return DrawAddressablesDropdownWithUndoOption(_displayLabel, typeof(T), _addressablesGroupName, ref _currentSelectedIndex, regexPatternMatching, _tooltip);
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group and adds the option for an Undo when changed. </summary>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_type"> The type of object you are seeking. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentSelectedIndex"> What index have you currently selected? </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public bool DrawAddressablesDropdownWithUndoOption(string _displayLabel, System.Type _type, string _addressablesGroupName, ref int _currentSelectedIndex, string regexPatternMatching = "", string _tooltip = null)
        {
            int result = DrawAddressablesDropdownOptions(_displayLabel, _type, _addressablesGroupName, _currentSelectedIndex, regexPatternMatching, _tooltip);
            if (result != _currentSelectedIndex)
            {
                Undo.RecordObject(BaseTarget, $"Modified {_displayLabel}");
                _currentSelectedIndex = result;
                return true;
            }
            return false;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group and adds the option for an Undo when changed. </summary>
        /// <typeparam name="T"> The Type of items you are looking for. </typeparam>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentKey"> What addressable id do you currently have selected </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public bool DrawAddressablesDropdownWithUndoOption<T>(string _displayLabel, string _addressablesGroupName, ref string _currentKey, string regexPatternMatching = "", string _tooltip = null) where T : UnityEngine.Object
        {
            return DrawAddressablesDropdownWithUndoOption(_displayLabel, typeof(T), _addressablesGroupName, ref _currentKey, regexPatternMatching, _tooltip);
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group and adds the option for an Undo when changed. </summary>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_type"> The type of object you are seeking. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentKey"> What addressable id do you currently have selected </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public bool DrawAddressablesDropdownWithUndoOption(string _displayLabel, System.Type _type, string _addressablesGroupName, ref string _currentKey, string regexPatternMatching = "", string _tooltip = null)
        {
            string result = DrawAddressablesDropdownOptions(_displayLabel, _type, _addressablesGroupName, _currentKey, regexPatternMatching, _tooltip);
            if (string.Equals(result, _currentKey) == false)
            {
                Undo.RecordObject(BaseTarget, $"Modified {_displayLabel}");
                _currentKey = result;
                return true;
            }
            return false;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group and adds the option for an Undo when changed. </summary>
        /// <typeparam name="T"> The Type of items you are looking for. </typeparam>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentSelectedIndex"> What index have you currently selected? </param>
        /// <param name="_assetValue"> Returns the Asset Value if applicable </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public bool DrawAddressablesDropdownWithUndoOption<T>(string _displayLabel, string _addressablesGroupName, ref int _currentSelectedIndex, out T _assetValue, string regexPatternMatching = "", string _tooltip = null) where T : UnityEngine.Object
        {
            UnityEngine.Object assetObjValue;
            bool result = DrawAddressablesDropdownWithUndoOption(_displayLabel, typeof(T), _addressablesGroupName, ref _currentSelectedIndex, out assetObjValue, regexPatternMatching, _tooltip);
            _assetValue = assetObjValue != null ? assetObjValue as T : null;
            return result;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group and adds the option for an Undo when changed. </summary>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_type"> The type of object you are seeking. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentSelectedIndex"> What index have you currently selected? </param>
        /// <param name="_assetValue"> Returns the Asset Value if applicable </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public bool DrawAddressablesDropdownWithUndoOption(string _displayLabel, System.Type _type, string _addressablesGroupName, ref int _currentSelectedIndex, out UnityEngine.Object _assetValue, string regexPatternMatching = "", string _tooltip = null)
        {
            int result = DrawAddressablesDropdownOptions(_displayLabel, _type, _addressablesGroupName, _currentSelectedIndex, out _assetValue, regexPatternMatching, _tooltip);
            if (result != _currentSelectedIndex)
            {
                Undo.RecordObject(BaseTarget, $"Modified {_displayLabel}");
                _currentSelectedIndex = result;
                return true;
            }
            return false;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group and adds the option for an Undo when changed. </summary>
        /// <typeparam name="T"> The Type of items you are looking for. </typeparam>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentKey"> What addressable id do you currently have selected </param>
        /// <param name="_assetValue"> Returns the Asset Value if applicable </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public bool DrawAddressablesDropdownWithUndoOption<T>(string _displayLabel, string _addressablesGroupName, ref string _currentKey, out T _assetValue, string regexPatternMatching = "", string _tooltip = null) where T : UnityEngine.Object
        {
            UnityEngine.Object assetObjValue;
            bool result = DrawAddressablesDropdownWithUndoOption(_displayLabel, typeof(T), _addressablesGroupName, ref _currentKey, out assetObjValue, regexPatternMatching, _tooltip);
            _assetValue = assetObjValue != null ? assetObjValue as T : null;
            return result;
        }

        /// <summary> Draws a Dropdown Box with the addressable options for the defined Group and adds the option for an Undo when changed. </summary>
        /// <param name="_displayLabel"> The label to display in the inspector for this field. </param>
        /// <param name="_type"> The type of object you are seeking. </param>
        /// <param name="_addressablesGroupName"> Which Addressables group are you querying. If the Group param is null or empty, will get the Contents of Type from all groups. </param>
        /// <param name="_currentKey"> What addressable id do you currently have selected </param>
        /// <param name="_assetValue"> Returns the Asset Value if applicable </param>
        /// <param name="_tooltip"> Renders a tooltip when hovered over. </param>
        public bool DrawAddressablesDropdownWithUndoOption(string _displayLabel, System.Type _type, string _addressablesGroupName, ref string _currentKey, out UnityEngine.Object _assetValue, string regexPatternMatching = "", string _tooltip = null)
        {
            string result = DrawAddressablesDropdownOptions(_displayLabel, _type, _addressablesGroupName, _currentKey, out _assetValue, regexPatternMatching, _tooltip);
            if (string.Equals(result, _currentKey) == false)
            {
                Undo.RecordObject(BaseTarget, $"Modified {_displayLabel}");
                _currentKey = result;
                return true;
            }
            return false;
        }
        
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Dropdown Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public int DrawDropdownBox(string displayLabel, GUIContent[] options, int currentSelectedIndex, bool drawByAlphabeticalOrder = false, string tooltip = null, string emptyDropboxText = "N/A - Empty Dropbox")
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            if (options.Length == 0)
            {
                if (GUI.enabled == false)
                {
                    EditorGUILayout.Popup(label, 0, new GUIContent[] { new(emptyDropboxText) });
                }
                else
                {
                    using (new GUIDisable())
                    {
                        EditorGUILayout.Popup(label, 0, new GUIContent[] { new(emptyDropboxText) });
                    }
                }
                return currentSelectedIndex;
            }
            else
            {
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
                    int result = EditorGUILayout.Popup(label, currentSelectedSortedIndex, asSortedArray);
                    if (result == -1)
                    {
                        return -1;
                    }

                    for (int i = 0; i < asSortedArray.Length; ++i)
                    {
                        // Still need to return the correct index. All we did was change the layout visually.
                        if (string.Equals(asSortedArray[result].text, options[i].text))
                        {
                            return i;
                        }
                    }
                }

                EditorGUILayout.BeginVertical();
                var output = EditorGUILayout.Popup(label, currentSelectedIndex, options);
                EditorGUILayout.EndVertical();
                return output;
            }
        }
        
        public int DrawDropdownBox(string displayLabel, string[] options, int currentSelectedIndex, bool drawByAlphabeticalOrder = false, string tooltip = null, string emptyDropboxText = "N/A - Empty Dropbox")
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            if (options.Length == 0)
            {
                if (GUI.enabled == false)
                {
                    EditorGUILayout.Popup(label, 0, new string[] { emptyDropboxText });
                }
                else
                {
                    using (new GUIDisable())
                    {
                        EditorGUILayout.Popup(label, 0, new string[] { emptyDropboxText });
                    }
                }
                return currentSelectedIndex;
            }
            else
            {
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
                    int result = EditorGUILayout.Popup(label, currentSelectedSortedIndex, asSortedArray);
                    if (result == -1)
                    {
                        return -1;
                    }

                    for (int i = 0; i < asSortedArray.Length; ++i)
                    {
                        // Still need to return the correct index. All we did was change the layout visually.
                        if (string.Equals(asSortedArray[result], options[i]))
                        {
                            return i;
                        }
                    }
                }

                EditorGUILayout.BeginVertical();
                var output = EditorGUILayout.Popup(label, currentSelectedIndex, options);
                EditorGUILayout.EndVertical();
                return output;
            }
        }
        
        public int DrawDropdownBox(string displayLabel, GUIContent[] options, string currentSelection, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int selectedIndex = -1;
            if (string.IsNullOrEmpty(currentSelection) == false)
            {
                int selectionHash = currentSelection.GetHashCode();
                for (int i = 0; i < options.Length; ++i)
                {
                    if (selectionHash == options[i].GetHashCode())
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            return DrawDropdownBox(displayLabel, options, selectedIndex, drawByAlphabeticalOrder, tooltip);
        }

        public int DrawDropdownBox(string displayLabel, string[] options, string currentSelection, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int selectedIndex = -1;
            if (string.IsNullOrEmpty(currentSelection) == false)
            {
                int selectionHash = currentSelection.GetHashCode();
                for (int i = 0; i < options.Length; ++i)
                {
                    if (selectionHash == options[i].GetHashCode())
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            return DrawDropdownBox(displayLabel, options, selectedIndex, drawByAlphabeticalOrder, tooltip);
        }

        public int DrawDropdownBox(string displayLabel, List<string> options, int currentSelectedIndex, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            return DrawDropdownBox(displayLabel, options.ToArray(), currentSelectedIndex, drawByAlphabeticalOrder, tooltip);
        }

        public int DrawDropdownBox(string displayLabel, List<string> options, string currentSelection, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int selectedIndex = -1;
            if (string.IsNullOrEmpty(currentSelection) == false)
            {
                int selectionHash = currentSelection.GetHashCode();
                for (int i = 0; i < options.Count; ++i)
                {
                    if (selectionHash == options[i].GetHashCode())
                    {
                        selectedIndex = i;
                        break;
                    }
                }
            }
            return DrawDropdownBox(displayLabel, options.ToArray(), selectedIndex, drawByAlphabeticalOrder, tooltip);
        }
        
        public void DrawReadonlyDropdownBox(string displayLabel, GUIContent[] options, int currentSelectedIndex, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelectedIndex, drawByAlphabeticalOrder, tooltip);
            }
        }

        public void DrawReadonlyDropdownBox(string displayLabel, GUIContent[] options, string currentSelection, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            }
        }

        public void DrawReadonlyDropdownBox(string displayLabel, string[] options, int currentSelectedIndex, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelectedIndex, drawByAlphabeticalOrder, tooltip);
            }
        }

        public void DrawReadonlyDropdownBox(string displayLabel, string[] options, string currentSelection, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            }
        }

        public void DrawReadonlyDropdownBox(string displayLabel, List<string> options, int currentSelectedIndex, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelectedIndex, drawByAlphabeticalOrder, tooltip);
            }
        }

        public void DrawReadonlyDropdownBox(string displayLabel, List<string> options, string currentSelection, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            }
        }
        
        public bool DrawDropdownBoxWithUndoOption(string displayLabel, GUIContent[] options, ref int currentSelectionIndex, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelectionIndex, drawByAlphabeticalOrder, tooltip);
            if (result != currentSelectionIndex)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentSelectionIndex = result;
                return true;
            }
            return false;
        }

        public bool DrawDropdownBoxWithUndoOption(string displayLabel, GUIContent[] options, ref string currentSelection, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            if (result != -1 && string.Equals(options[result].text, currentSelection) == false)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentSelection = options[result].text;
                return true;
            }
            return false;
        }

        public bool DrawDropdownBoxWithUndoOption(string displayLabel, string[] options, ref int currentSelectionIndex, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelectionIndex, drawByAlphabeticalOrder, tooltip);
            if (result != currentSelectionIndex)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentSelectionIndex = result;
                return true;
            }
            return false;
        }

        public bool DrawDropdownBoxWithUndoOption(string displayLabel, string[] options, ref string currentSelection, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            if (result != -1 && string.Equals(options[result], currentSelection) == false)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentSelection = options[result];
                return true;
            }
            return false;
        }

        public bool DrawDropdownBoxWithUndoOption(string displayLabel, List<string> options, ref int currentSelectionIndex, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelectionIndex, drawByAlphabeticalOrder, tooltip);
            if (result != currentSelectionIndex)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentSelectionIndex = result;
                return true;
            }
            return false;
        }

        public bool DrawDropdownBoxWithUndoOption(string displayLabel, List<string> options, ref string currentSelection, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int result = DrawDropdownBox(displayLabel, options, currentSelection, drawByAlphabeticalOrder, tooltip);
            if (result != -1 && string.Equals(options[result], currentSelection) == false)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentSelection = options[result];
                return true;
            }
            return false;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances. Will return true if a change is made </summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="selectionOutput"> Outputs the selected choice as a string. </param>
        /// <param name="parameters"> The dropdown parameters </param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown.</param>
        public bool DrawDropdownBoxViaDropdownChoicesObject(string displayLabel, out string selectionOutput, DropdownChoicesParameters parameters, string tooltip = null)
        {
            DropdownChoicesData dropdownChoiceData = GetDropdownChoicesWithUsedElementsRemoved(parameters);

            int result = DrawDropdownBox(displayLabel, dropdownChoiceData.RemainingSelectableChoices, dropdownChoiceData.CurrentSelectionIndex, false, tooltip);
            if (result == -1)
            {
                selectionOutput = null;
            }
            else
            {
                selectionOutput = dropdownChoiceData.RemainingSelectableChoices[result];
            }

            if (result != dropdownChoiceData.CurrentSelectionIndex)
            {
                // Dropdown Elements need to be recalculated. Remove from Dictionary so it get recalculated when necessary.
                if (_cachedDropdownChoicesWithUsedElementsRemoved.ContainsKey(parameters.OwningObject))
                {
                    _cachedDropdownChoicesWithUsedElementsRemoved.Remove(parameters.OwningObject);
                }
                return true;
            }

            return false;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances. Will return true if a change is made 
        /// and provides the ability to Undo. </summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="selectionOutput"> Outputs the selected choice as a string. </param>
        /// <param name="parameters"> The dropdown parameters </param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown.</param>
        public bool DrawDropdownBoxViaDropdownChoicesObjectWithUndoOption(string displayLabel, out string selectionOutput, DropdownChoicesParameters parameters, string tooltip = null)
        {
            if (DrawDropdownBoxViaDropdownChoicesObject(displayLabel, out selectionOutput, parameters, tooltip))
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                return true;
            }
            return false;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances. Will return true if a change is made </summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="selectedIndex"> Outputs the selected index. This index correlates to the real index value of the Choices Array. </param>
        /// <param name="parameters"> The dropdown parameters </param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown.</param>
        public bool DrawDropdownBoxViaDropdownChoicesObject(string displayLabel, out int selectedIndex, DropdownChoicesParameters parameters, string tooltip = null)
        {
            DropdownChoicesData dropdownChoiceData = GetDropdownChoicesWithUsedElementsRemoved(parameters);

            int result = DrawDropdownBox(displayLabel, dropdownChoiceData.RemainingSelectableChoices, dropdownChoiceData.CurrentSelectionIndex, false, tooltip);

            if (result == -1)
            {
                selectedIndex = -1;
            }
            else
            {
                // Converts from "Selectable Choices" Index to "All Choices" Index.
                selectedIndex = dropdownChoiceData.RemainingChoicesIndexToAllChoicesIndex[result];
            }

            if (result != dropdownChoiceData.CurrentSelectionIndex)
            {
                // Dropdown Elements need to be recalculated. Remove from Dictionary so it get recalculated when necessary.
                if (_cachedDropdownChoicesWithUsedElementsRemoved.ContainsKey(parameters.OwningObject))
                {
                    _cachedDropdownChoicesWithUsedElementsRemoved.Remove(parameters.OwningObject);
                }

                return true;
            }

            return false;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances. Will return true if a change is made 
        /// and also provides the option to Undo. </summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="selectedIndex"> Outputs the selected index. This index correlates to the real index value of the Choices Array. </param>
        /// <param name="parameters"> The dropdown parameters </param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown.</param>
        public bool DrawDropdownBoxViaDropdownChoicesObjectWithUndoOption(string displayLabel, out int selectedIndex, DropdownChoicesParameters parameters, string tooltip = null)
        {
            if (DrawDropdownBoxViaDropdownChoicesObject(displayLabel, out selectedIndex, parameters, tooltip))
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                return true;
            }
            return false;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances. Will return true if a change is made </summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="selectionOutput"> Outputs the selected choice as a string. </param>
        /// <param name="selectedIndex"> Outputs the selected index. This index correlates to the real index value of the Choices Array. </param>
        /// <param name="parameters"> The dropdown parameters </param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown.</param>
        public bool DrawDropdownBoxViaDropdownChoicesObject(string displayLabel, out string selectionOutput, out int selectedIndex, DropdownChoicesParameters parameters, string tooltip = null)
        {
            DropdownChoicesData dropdownChoiceData = GetDropdownChoicesWithUsedElementsRemoved(parameters);

            int result = DrawDropdownBox(displayLabel, dropdownChoiceData.RemainingSelectableChoices, dropdownChoiceData.CurrentSelectionIndex, false, tooltip);
            if (result == -1)
            {
                selectionOutput = null;
                selectedIndex = -1;
            }
            else
            {
                // Converts from "Selectable Choices" Index to "All Choices" Index.
                selectionOutput = dropdownChoiceData.RemainingSelectableChoices[result];
                selectedIndex = dropdownChoiceData.RemainingChoicesIndexToAllChoicesIndex[result];
            }

            if (result != dropdownChoiceData.CurrentSelectionIndex)
            {
                // Dropdown Elements need to be recalculated. Remove from Dictionary so it get recalculated when necessary.
                if (_cachedDropdownChoicesWithUsedElementsRemoved.ContainsKey(parameters.OwningObject))
                {
                    _cachedDropdownChoicesWithUsedElementsRemoved.Remove(parameters.OwningObject);
                }
                return true;
            }

            return false;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances. Will return true if a change is made 
        /// and also includes the ability to Undo. </summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="selectionOutput"> Outputs the selected choice as a string. </param>
        /// <param name="selectedIndex"> Outputs the selected index. This index correlates to the real index value of the Choices Array. </param>
        /// <param name="parameters"> The dropdown parameters </param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown.</param>
        public bool DrawDropdownBoxViaDropdownChoicesObjectWithUndoOption(string displayLabel, out string selectionOutput, out int selectedIndex, DropdownChoicesParameters parameters, string tooltip = null)
        {
            if (DrawDropdownBoxViaDropdownChoicesObject(displayLabel, out selectionOutput, out selectedIndex, parameters, tooltip))
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                return true;
            }
            return false;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances.</summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="otherInstancesReferencesOptions"> Reference to the dropdown options we should remove, if applicable. </param>
        /// <param name="optionsToDisplay"> Which options should be displayed. Pass in the entire unfiltered Options List. The filtering will be done in this function. </param>
        /// <param name="currentIndex"> The currently selected index. </param>
        /// <param name="drawByAlphabeticalOrder"> Do you want to draw the dropdown in Alphabetical order? If yes, this requires more processing power (might cause lag).</param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown. </param>
        public int DrawDropdownBoxWithElementsAlreadyUsedRemoved(string displayLabel, string[] otherInstancesReferencesOptions, string[] optionsToDisplay, int currentIndex, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int otherInstanceCount = otherInstancesReferencesOptions.Length;
            int optionsCount = optionsToDisplay.Length;

            List<string> remainingElements = new List<string>(optionsToDisplay);
            remainingElements.RemoveAll(optionDisplay =>
            {
                if (string.IsNullOrEmpty(optionDisplay))
                {
                    return true;
                }

                int optionHashCode = optionDisplay.GetHashCode();
                if (currentIndex != -1 && optionHashCode == optionsToDisplay[currentIndex].GetHashCode())
                {
                    return false;
                }

                for (int i = 0; i < otherInstanceCount; ++i)
                {
                    if (string.IsNullOrEmpty(otherInstancesReferencesOptions[i]) == false
                        && otherInstancesReferencesOptions[i].GetHashCode() == optionHashCode)
                    {
                        return true;
                    }
                }
                return false;
            });

            int selectedIndexOfRemainingSelection = 0;
            if (currentIndex != -1)
            {
                selectedIndexOfRemainingSelection = remainingElements.IndexOf(optionsToDisplay[currentIndex]);
            }
            selectedIndexOfRemainingSelection = DrawDropdownBox(displayLabel, remainingElements, selectedIndexOfRemainingSelection, drawByAlphabeticalOrder, tooltip);


            // We have selected an index relative to the "Remaining Elements" list. We need to convert the index BACK to the original List's format
            if (selectedIndexOfRemainingSelection != -1 && remainingElements.Count > 0)
            {
                int hashCode = remainingElements[selectedIndexOfRemainingSelection].GetHashCode();
                for (int i = 0; i < optionsCount; ++i)
                {
                    if (optionsToDisplay[i].GetHashCode() == hashCode)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances.</summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="otherInstancesReferencesOptions"> Reference to the dropdown options we should remove, if applicable. </param>
        /// <param name="optionsToDisplay"> Which options should be displayed. Pass in the entire unfiltered Options List. The filtering will be done in this function. </param>
        /// <param name="currentIndex"> The currently selected index. </param>
        /// <param name="drawByAlphabeticalOrder"> Do you want to draw the dropdown in Alphabetical order? If yes, this requires more processing power (might cause lag).</param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown. </param>
        public int DrawDropdownBoxWithElementsAlreadyUsedRemoved(string displayLabel, int[] otherInstancesReferencesOptions, string[] optionsToDisplay, int currentIndex, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int optionsCount = optionsToDisplay.Length;

            List<int> sortedIndicesToRemove = new List<int>(otherInstancesReferencesOptions);
            sortedIndicesToRemove.Sort((int a, int b) =>
            {
                if (a < b)
                {
                    return -1;
                }
                if (a == b)
                {
                    return 0;
                }
                return 1;
            });

            List<string> remainingElements = new List<string>(optionsToDisplay);
            for (int i = sortedIndicesToRemove.Count - 1; i > -1; --i)
            {
                if (remainingElements.Count > i)
                {
                    remainingElements.RemoveAt(i);
                }
            }

            int selectedIndexOfRemainingSelection = 0;
            if (currentIndex != -1)
            {
                selectedIndexOfRemainingSelection = remainingElements.IndexOf(optionsToDisplay[currentIndex]);
            }
            selectedIndexOfRemainingSelection = DrawDropdownBox(displayLabel, remainingElements, selectedIndexOfRemainingSelection, drawByAlphabeticalOrder, tooltip);


            // We have selected an index relative to the "Remaining Elements" list. We need to convert the index BACK to the original List's format
            if (selectedIndexOfRemainingSelection != -1 && remainingElements.Count > 0)
            {
                int hashCode = remainingElements[selectedIndexOfRemainingSelection].GetHashCode();
                for (int i = 0; i < optionsCount; ++i)
                {
                    if (optionsToDisplay[i].GetHashCode() == hashCode)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances.</summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="otherInstancesReferencesOptions"> Reference to the dropdown options we should remove, if applicable. </param>
        /// <param name="optionsToDisplay"> Which options should be displayed. Pass in the entire unfiltered Options List. The filtering will be done in this function. </param>
        /// <param name="currentVal"> What is the value you have assigned currently. </param>
        /// <param name="drawByAlphabeticalOrder"> Do you want to draw the dropdown in Alphabetical order? If yes, this requires more processing power (might cause lag).</param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown. </param>
        public string DrawDropdownBoxWithElementsAlreadyUsedRemoved(string displayLabel, string[] otherInstancesReferencesOptions, string[] optionsToDisplay, string currentVal, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int currentIndex = -1;
            if (string.IsNullOrEmpty(currentVal) == false)
            {
                int count = optionsToDisplay.Length;
                for (int i = 0; i < count; ++i)
                {
                    if (string.Equals(optionsToDisplay[i], currentVal))
                    {
                        currentIndex = i;
                        break;
                    }
                }
            }

            int result = DrawDropdownBoxWithElementsAlreadyUsedRemoved(displayLabel, otherInstancesReferencesOptions, optionsToDisplay, currentIndex, drawByAlphabeticalOrder, tooltip);
            if (result != -1)
            {
                return optionsToDisplay[result];
            }
            return currentVal;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances. Returns true if a change is made and also includes the ability to undo. </summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="otherInstancesReferencesOptions"> Reference to the dropdown options we should remove, if applicable. </param>
        /// <param name="optionsToDisplay"> Which options should be displayed. Pass in the entire unfiltered Options List. The filtering will be done in this function. </param>
        /// <param name="currentVal"> What is the value you have assigned currently. This value will be modified if a change is made. </param>
        /// <param name="drawByAlphabeticalOrder"> Do you want to draw the dropdown in Alphabetical order? If yes, this requires more processing power (might cause lag).</param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown. </param>
        public bool DrawDropdownBoxWithElementsAlreadyUsedRemovedWithUndoOption(string displayLabel, int[] otherInstancesReferencesOptions, string[] optionsToDisplay, ref int currentVal, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            int result = DrawDropdownBoxWithElementsAlreadyUsedRemoved(displayLabel, otherInstancesReferencesOptions, optionsToDisplay, currentVal, drawByAlphabeticalOrder, tooltip);
            if (result != currentVal)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentVal = result;
                return true;
            }
            return false;
        }

        /// <summary> Draws a Dropdown box with Elements Removed if they have already been selected by other Dropdown instances. Returns true if a change is made and also includes the ability to undo. </summary>
        /// <param name="displayLabel"> The label to display. </param>
        /// <param name="otherInstancesReferencesOptions"> Reference to the dropdown options we should remove, if applicable. </param>
        /// <param name="optionsToDisplay"> Which options should be displayed. Pass in the entire unfiltered Options List. The filtering will be done in this function. </param>
        /// <param name="currentVal"> What is the value you have assigned currently. This value will be modified if a change is made. </param>
        /// <param name="drawByAlphabeticalOrder"> Do you want to draw the dropdown in Alphabetical order? If yes, this requires more processing power (might cause lag).</param>
        /// <param name="tooltip"> The tooltip to show when hovering over the dropdown. </param>
        public bool DrawDropdownBoxWithElementsAlreadyUsedRemovedWithUndoOption(string displayLabel, string[] otherInstancesReferencesOptions, string[] optionsToDisplay, ref string currentVal, bool drawByAlphabeticalOrder = false, string tooltip = null)
        {
            string result = DrawDropdownBoxWithElementsAlreadyUsedRemoved(displayLabel, otherInstancesReferencesOptions, optionsToDisplay, currentVal, drawByAlphabeticalOrder, tooltip);
            if (string.Equals(result, currentVal) == false)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentVal = result;
                return true;
            }
            return false;
        }

        /// <summary> Draws multiple dropdown options on one line. Returns a list of indices for each dropdown box passed in. </summary>
        /// <param name="displayLabel"> The label to show. </param>
        /// <param name="dropdownOptions"> The options available to show per dropdown </param>
        /// <param name="selectedIndices"> The currently selected indices per dropdown. </param>
        /// <param name="tooltips"> The tooltips to show per dropdown </param>
        /// <param name="drawInAlphabeticalOrders"> Do you want to draw the dropdowns in Alphabetical order? If yes, this requires more processing power (might cause lag). </param>
        public List<int> DrawMultipleDropdownBoxesOneLine(string displayLabel, List<string[]> dropdownOptions, List<int> selectedIndices, List<string> tooltips = null, List<bool> drawInAlphabeticalOrders = null)
        {
            List<int> results = new List<int>();
            if (dropdownOptions == null)
            {
                // Nothing to draw.
                return results;
            }
            int dropdownsCount = dropdownOptions.Count;
            if (dropdownsCount == 0)
            {
                // Nothing to draw.
                return results;
            }
            if (dropdownsCount == 1)
            {
                // Just use the default draw method
                string tooltip = tooltips != null && tooltips.Count > 0 ? tooltips[0] : string.Empty;
                bool drawInAlphabeticalOrder = drawInAlphabeticalOrders == null || drawInAlphabeticalOrders.Count == 0 || drawInAlphabeticalOrders[0];
                int currentSelectedIndex = selectedIndices != null && selectedIndices.Count > 0 ? selectedIndices[0] : -1;
                int selectedIndex = DrawDropdownBox(displayLabel, dropdownOptions[0], currentSelectedIndex, drawInAlphabeticalOrder, tooltip);
                results.Add(selectedIndex);
                return results;
            }

            DrawLabel(displayLabel);
            const float pixelSeparationPerBox = 0.0f;
            float totalDrawWidth = DrawValueWidth - ScrollBarPadding;
            float drawWidthPerBox = (totalDrawWidth / dropdownsCount) - pixelSeparationPerBox;

            float startDrawXPos = StartDrawValueXPos; // From Center of Window, same value as totalDrawWidth
            Rect linePos = GUILayoutUtility.GetLastRect();

            for (int i = 0; i < dropdownsCount; ++i)
            {
                string tooltip = tooltips != null && tooltips.Count > i ? tooltips[i] : string.Empty;
                bool drawInAlphabeticalOrder = drawInAlphabeticalOrders == null || drawInAlphabeticalOrders.Count <= i || drawInAlphabeticalOrders[i];
                int currentSelectedIndex = selectedIndices != null && selectedIndices.Count > i ? selectedIndices[i] : -1;
                string[] options = dropdownOptions[i];

                Rect drawRect = new Rect(x: startDrawXPos + ((drawWidthPerBox + pixelSeparationPerBox) * i),
                                         y: linePos.y,
                                         width: drawWidthPerBox,
                                         height: linePos.height);

                GUIContent label = string.IsNullOrEmpty(tooltip) == false ? new GUIContent(string.Empty, tooltip) : new GUIContent(string.Empty);
                if (options.Length > 0)
                {
                    if (drawInAlphabeticalOrder)
                    {
                        List<string> sortedOptions = new List<string>();
                        sortedOptions.AddRange(options.OrderBy(o => o));
                        GUIContent[] asSortedArray = sortedOptions.Select(o => new GUIContent(o)).ToArray();
                        int selectedResult = EditorGUI.Popup(drawRect, label, currentSelectedIndex, asSortedArray);
                        if (selectedResult != -1)
                        {
                            for (int j = 0; j < asSortedArray.Length; ++j)
                            {
                                // Still need to return the correct index. All we did was change the layout visually.
                                if (string.Equals(asSortedArray[selectedResult].text, options[j]))
                                {
                                    selectedResult = j;
                                    break;
                                }
                            }
                        }
                        results.Add(selectedResult);
                    }
                    else
                    {
                        GUIContent[] asArray = options.Select(o => new GUIContent(o)).ToArray();
                        int selectedResult = EditorGUI.Popup(drawRect, label, currentSelectedIndex, asArray);
                        results.Add(selectedResult);
                    }
                }
                else
                {
                    // No options available to show...
                    int selectedResult = EditorGUI.Popup(drawRect, label, currentSelectedIndex, Array.Empty<GUIContent>());
                    results.Add(selectedResult);
                }
            }

            return results;
        }

        /// <summary> Draws multiple dropdown options on one line and includes the ability to undo. Return true if a change was made. </summary>
        /// <param name="displayLabel"> The label to show. </param>
        /// <param name="dropdownOptions"> The options available to show per dropdown </param>
        /// <param name="selectedIndices"> The currently selected indices per dropdown. If modified, these values will be changed </param>
        /// <param name="tooltips"> The tooltips to show per dropdown </param>
        /// <param name="drawInAlphabeticalOrders"> Do you want to draw the dropdowns in Alphabetical order? If yes, this requires more processing power (might cause lag). </param>
        public bool DrawMultipleDropdownBoxesOneLineWithUndoOption(string displayLabel, List<string[]> dropdownOptions, ref List<int> selectedIndices, List<string> tooltips = null, List<bool> drawInAlphabeticalOrders = null)
        {
            int indicesCount = selectedIndices.Count;
            List<int> results = DrawMultipleDropdownBoxesOneLine(displayLabel, dropdownOptions, selectedIndices, tooltips, drawInAlphabeticalOrders);
            for (int i = 0; i < indicesCount; ++i)
            {
                if (results[i] != selectedIndices[i])
                {
                    Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                    selectedIndices = results;
                    return true;
                }
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Multiple Buttons One Line
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawMultipleButtonsOneLine(List<ButtonsInfo> buttonsList, float? startXPosition = null, float endXPadding = 15.0f, float spaceBetweenButtons = 5.0f, bool allowUndoOption = true)
        {
            AddSpaces(1);
            if (buttonsList == null)
            {
                // Nothing to draw.
                return;
            }
            int buttonsCount = buttonsList.Count;
            if (buttonsCount == 0)
            {
                // Nothing to draw.
                return;
            }

            if (buttonsCount == 1)
            {
                // Just use the default draw method
                if (DrawButtonField(buttonsList[0].ButtonName, buttonsList[0].Tooltip))
                {
                    buttonsList[0].OnClicked?.Invoke();
                }
                return;
            }

            float startDrawXPos = startXPosition ?? StartDrawValueXPos; // From Center of Window, same value as totalDrawWidth
            float totalDrawWidth = EditorWindowContextWidth - startDrawXPos - endXPadding;
            float drawWidthPerBox = (totalDrawWidth / buttonsCount) - spaceBetweenButtons;
            Rect linePos = GUILayoutUtility.GetLastRect();

            for (int i = 0; i < buttonsCount; ++i)
            {
                Rect drawRect = new Rect(x: startDrawXPos + ((drawWidthPerBox + spaceBetweenButtons) * i),
                                         y: linePos.y,
                                         width: drawWidthPerBox,
                                         height: 20);

                ButtonsInfo buttonInfo = buttonsList[i];
                GUIContent label = string.IsNullOrEmpty(buttonInfo.Tooltip) == false ? new GUIContent(buttonInfo.ButtonName, buttonInfo.Tooltip) : new GUIContent(buttonInfo.ButtonName);
                if (GUI.Button(drawRect, label))
                {
                    if (buttonInfo.OnClicked != null)
                    {
                        if (allowUndoOption)
                        {
                            Undo.RecordObject(BaseTarget, $"Clicked Button [{buttonInfo.ButtonName}]");
                        }

                        buttonInfo.OnClicked.Invoke();
                    }
                }
            }

            AddSpaces(3);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Date Selection
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public System.DateTime DrawDateSelection(string displayLabel, System.DateTime currentVal, int firstSelectableYear = 2019, int finalSelectableYear = 2050)
        {
            int selectedYear = Mathf.Clamp(currentVal.Year, firstSelectableYear, finalSelectableYear);
            int selectedMonth = currentVal.Month;

            int yearSelectionRange = finalSelectableYear - firstSelectableYear;

            string[] yearsOptions = new string[yearSelectionRange];
            for (int yearIndex = 0; yearIndex < yearSelectionRange; ++yearIndex)
            {
                yearsOptions[yearIndex] = (firstSelectableYear + yearIndex).ToString();
            }

            int numDaysInMonth = System.DateTime.DaysInMonth(selectedYear, selectedMonth);
            string[] dayOptions = new string[numDaysInMonth];
            for (int dayIndex = 0; dayIndex < numDaysInMonth; ++dayIndex)
            {
                System.DateTime day = new System.DateTime(selectedYear, selectedMonth, dayIndex + 1);
                dayOptions[dayIndex] = SharedCustomEditorValues.DaysInMonth[dayIndex] + " - " + day.ToString("ddd");
            }

            List<string[]> dropdownChoices = new List<string[]>() { dayOptions, SharedCustomEditorValues.MonthsInYear, yearsOptions };
            List<int> selectedChoiceIndices = new List<int>() { currentVal.Day - 1, selectedMonth - 1, selectedYear - firstSelectableYear };
            List<bool> drawInAlphabeticalOrder = new List<bool>() { false, false, false };

            List<int> chosenOutput = DrawMultipleDropdownBoxesOneLine(displayLabel, dropdownChoices, selectedChoiceIndices, null, drawInAlphabeticalOrder);

            selectedYear = chosenOutput[2] + firstSelectableYear;
            selectedMonth = chosenOutput[1] + 1;
            numDaysInMonth = System.DateTime.DaysInMonth(selectedYear, selectedMonth);
            int selectedDay = Mathf.Clamp(chosenOutput[0] + 1, 1, numDaysInMonth);
            System.DateTime result = new System.DateTime(selectedYear, selectedMonth, selectedDay);
            return result;
        }

        public void DrawReadonlyDateSelection(string displayLabel, System.DateTime currentVal)
        {
            using (new GUIDisable())
            {
                DrawDateSelection(displayLabel, currentVal, 1, 9999);
            }
        }

        public bool DrawDateSelectionWithUndoOption(string displayLabel, ref System.DateTime currentVal, int firstSelectableYear = 2019, int finalSelectableYear = 2050)
        {
            System.DateTime result = DrawDateSelection(displayLabel, currentVal, firstSelectableYear, finalSelectableYear);
            if (result.Ticks != currentVal.Ticks)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentVal = result;
                return true;
            }
            return false;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Colour Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public Gradient DrawGradientField(string displayLabel, Gradient currentValue, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.GradientField(label, currentValue);
        }

        public bool DrawGradientFieldWithUndoOption(string displayLabel, ref Gradient currentValue, string tooltip = null)
        {
            Gradient result = DrawGradientField(displayLabel, currentValue, tooltip);
            if (result.Equals(currentValue) == false)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Colour Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public Color DrawColourField(string displayLabel, Color currentValue, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.ColorField(label, currentValue);
        }

        public Color DrawColourField(string displayLabel, float currentRed, float currentGreen, float currentBlue, float currentAlpha, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.ColorField(label, new Color(currentRed, currentGreen, currentBlue, currentAlpha));
        }

        public void DrawReadonlyColourField(string displayLabel, Color currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawColourField(displayLabel, currentValue, tooltip);
            }
        }

        public void DrawReadonlyColourField(string displayLabel, float currentRed, float currentGreen, float currentBlue, float currentAlpha, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawColourField(displayLabel, currentRed, currentGreen, currentBlue, currentAlpha, tooltip);
            }
        }

        public bool DrawColourFieldWithUndoOption(string displayLabel, ref Color currentValue, string tooltip = null)
        {
            Color result = DrawColourField(displayLabel, currentValue, tooltip);
            if (result != currentValue)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }

        public bool DrawColourFieldWithUndoOption(string displayLabel, ref float currentRed, ref float currentGreen, ref float currentBlue, ref float currentAlpha, string tooltip = null)
        {
            Color current = new Color(currentRed, currentGreen, currentBlue, currentAlpha);
            Color result = DrawColourField(displayLabel, current, tooltip);
            if (result != current)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentRed = result.r;
                currentGreen = result.g;
                currentBlue = result.b;
                currentAlpha = result.a;
                return true;
            }
            return false;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Vector 2 Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public Vector2 DrawVectorField(string displayLabel, Vector2 currentValue, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.Vector2Field(label, currentValue);
        }

        public void DrawReadonlyVectorField(string displayLabel, Vector2 currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawVectorField(displayLabel, currentValue, tooltip);
            }
        }

        public bool DrawVectorFieldWithUndoOption(string displayLabel, ref Vector2 currentValue, string tooltip = null)
        {
            Vector2 result = DrawVectorField(displayLabel, currentValue, tooltip);
            if (result != currentValue)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }

        public Vector2Int DrawVectorField(string displayLabel, Vector2Int currentValue, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.Vector2IntField(label, currentValue);
        }

        public void DrawReadonlyVectorField(string displayLabel, Vector2Int currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawVectorField(displayLabel, currentValue, tooltip);
            }
        }

        public bool DrawVectorFieldWithUndoOption(string displayLabel, ref Vector2Int currentValue, string tooltip = null)
        {
            Vector2Int result = DrawVectorField(displayLabel, currentValue, tooltip);
            if (result != currentValue)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Vector 3 Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public Vector3 DrawVectorField(string displayLabel, Vector3 currentValue, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.Vector3Field(label, currentValue);
        }

        public void DrawReadonlyVectorField(string displayLabel, Vector3 currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawVectorField(displayLabel, currentValue, tooltip);
            }
        }

        public bool DrawVectorFieldWithUndoOption(string displayLabel, ref Vector3 currentValue, string tooltip = null)
        {
            Vector3 result = DrawVectorField(displayLabel, currentValue, tooltip);
            if (result != currentValue)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }


        public Vector3Int DrawVectorField(string displayLabel, Vector3Int currentValue, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.Vector3IntField(label, currentValue);
        }

        public void DrawReadonlyVectorField(string displayLabel, Vector3Int currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawVectorField(displayLabel, currentValue, tooltip);
            }
        }

        public bool DrawVectorFieldWithUndoOption(string displayLabel, ref Vector3Int currentValue, string tooltip = null)
        {
            Vector3Int result = DrawVectorField(displayLabel, currentValue, tooltip);
            if (result != currentValue)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Vector 4 Field
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public Vector4 DrawVectorField(string displayLabel, Vector4 currentValue, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.Vector4Field(label, currentValue);
        }

        public void DrawReadonlyVectorField(string displayLabel, Vector4 currentValue, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawVectorField(displayLabel, currentValue, tooltip);
            }
        }

        public bool DrawVectorFieldWithUndoOption(string displayLabel, ref Vector4 currentValue, string tooltip = null)
        {
            Vector4 result = DrawVectorField(displayLabel, currentValue, tooltip);
            if (result != currentValue)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw LocId Options
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Draws a Loc ID Option. </summary>
        /// <param name="headerName"> Label for the Loc ID. </param>
        /// <param name="currentHashValue"> Current Loc ID Value. </param>
        /// <param name="renderOnOneLine"> Should render both the Search Field and Loc ID popup on one line?. </param>
        /// <param name="owner"> The owner of this Loc ID. If it is a part of an array, pass in the array element ( array[i] ) as the owner. </param>
        /// <param name="drawEnglishTextAlso"> If true, draws an English Text output on the next line. </param>
        /// <param name="tooltip"> Tooltip to show with this Loc ID Property. </param>
        public int DrawLocIdOption(string headerName, int currentHashValue, bool renderOnOneLine, object owner = null, bool drawEnglishTextAlso = true, string tooltip = null)
        {
            LocIDData currentLocIdData;
            if (_locIdSearchDataDict.TryGetValue(owner ?? BaseTarget, out currentLocIdData) == false)
            {
                currentLocIdData = new LocIDData(currentHashValue);
                _locIdSearchDataDict.Add(owner ?? BaseTarget, currentLocIdData);
            }
            else
            {
                if (currentLocIdData.CurrentHashId != currentHashValue)
                {
                    currentLocIdData.SetLocHashValue(currentHashValue);
                }
            }

            if (renderOnOneLine)
            {
                if (EnableDescriptionRendering.ShouldRenderDescription && string.IsNullOrEmpty(tooltip) == false)
                {
                    DrawWrappedText(tooltip, null, null, SimpleAlignment.Centered);
                }
                DrawLabel(headerName);
            }
            else
            {
                DrawLabelCenterAligned(headerName);
                if (EnableDescriptionRendering.ShouldRenderDescription && string.IsNullOrEmpty(tooltip) == false)
                {
                    DrawWrappedText(tooltip, null, null, SimpleAlignment.Centered);
                }
            }

            // Draw Search Box
            Rect searchBoxDrawPos = GUILayoutUtility.GetLastRect();
            if (GUI.enabled)
            {
                // No point in drawing the Search Box if GUI is disabled as there can be no user interaction
                string searchText;
                if (renderOnOneLine)
                {
                    searchBoxDrawPos.x += Mathf.Min(100.0f, XPositionOfUnityDrawObjectWithLabel);
                    searchBoxDrawPos.width = ((EditorWindowContextWidth - XPositionOfUnityDrawObjectWithLabel) * 0.8f) - ScrollBarPadding;

                    searchText = EditorGUI.TextField(searchBoxDrawPos, string.IsNullOrEmpty(currentLocIdData.SearchString) ? DefaultSearchBoxText : currentLocIdData.SearchString);
                }
                else
                {
                    searchText = EditorGUILayout.TextField(string.IsNullOrEmpty(currentLocIdData.SearchString) ? DefaultSearchBoxText : currentLocIdData.SearchString);
                }

                if (string.Equals(searchText, DefaultSearchBoxText) == false && string.Equals(currentLocIdData.SearchString, searchText) == false)
                {
                    // Only making changes if the search text has changed.
                    currentLocIdData.SearchString = searchText;

                    // Populate Search Results
                    if (string.IsNullOrEmpty(currentLocIdData.SearchString) == false)
                    {
                        List<string> viableSearchResults = new List<string>();
                        List<int> viableHashResults = new List<int>();

                        string searchPattern = $"{currentLocIdData.SearchString.Replace(' ', '_')}";

                        for (int index = 0; index < LocIdsHelper.Loc_Strings_TotalLocIdsCount; ++index)
                        {
                            if (Regex.IsMatch(LocIDsEditorData.Combined_EditorAlphabeticallyOrderedLocStringIDs[index].text, searchPattern, RegexOptions.IgnoreCase))
                            {
                                int hashId = LocIDsEditorData.Combined_EditorLocStringIdToHashId[LocIDsEditorData.Combined_EditorAlphabeticallyOrderedLocStringIDs[index].text];
                                viableSearchResults.Add(LocIDsEditorData.Combined_EditorAlphabeticallyOrderedLocStringIDs[index].text);
                                viableHashResults.Add(hashId);
                            }
                        }

                        currentLocIdData.SearchResults = viableSearchResults.ToArray();
                        currentLocIdData.HashResults = viableHashResults.ToArray();

                        if (viableSearchResults.Count > 1)
                        {
                            // If there are valid search results, force us to select the first viable one unless we are already matching something that is in that search result.
                            currentLocIdData.SelectedSearchResultIndex = viableHashResults.IndexOf(currentLocIdData.CurrentHashId);
                            if (currentLocIdData.SelectedSearchResultIndex == -1)
                            {
                                currentLocIdData.SelectedSearchResultIndex = 0;
                                currentLocIdData.CurrentHashId = viableHashResults[0];
                            }
                        }
                        else if (viableSearchResults.Count == 1)
                        {
                            // Only one match. Just auto-assign it
                            currentLocIdData.SelectedSearchResultIndex = 0;
                            currentLocIdData.CurrentHashId = viableHashResults[0];
                        }
                        else
                        {
                            currentLocIdData.SelectedSearchResultIndex = -1;
                        }
                    }

                    // Depopulate Search Results
                    else
                    {
                        if (LocIDsEditorData.Combined_EditorHashIdToInspectorPopupIndex.TryGetValue(currentLocIdData.CurrentHashId, out currentLocIdData.CurrentPopupIndex) == false)
                        {
                            currentLocIdData.CurrentPopupIndex = -1;
                        }
                    }
                }
            }

            // Draw Loc Popup Box
            if (GUI.enabled && string.IsNullOrEmpty(currentLocIdData.SearchString) == false)
            {
                // Draw Search Results
                if (currentLocIdData.SearchResults.Length > 0)
                {
                    int selectedSearchResultIndex;
                    if (renderOnOneLine)
                    {
                        Rect locPopupDrawPos = searchBoxDrawPos;
                        locPopupDrawPos.x += locPopupDrawPos.width * 0.6f;
                        selectedSearchResultIndex = EditorGUI.Popup(locPopupDrawPos, currentLocIdData.SelectedSearchResultIndex, currentLocIdData.SearchResults);
                    }
                    else
                    {
                        selectedSearchResultIndex = EditorGUILayout.Popup(currentLocIdData.SelectedSearchResultIndex, currentLocIdData.SearchResults);
                    }

                    if (selectedSearchResultIndex != currentLocIdData.SelectedSearchResultIndex)
                    {
                        currentLocIdData.SelectedSearchResultIndex = selectedSearchResultIndex;
                        currentLocIdData.CurrentHashId = currentLocIdData.HashResults[selectedSearchResultIndex];
                        currentLocIdData.CurrentPopupIndex = LocIDsEditorData.Combined_EditorHashIdToInspectorPopupIndex[currentLocIdData.CurrentHashId];
                    }
                }
                else
                {
                    using (new GUIDisable())
                    {
                        if (renderOnOneLine)
                        {
                            Rect locPopupDrawPos = searchBoxDrawPos;
                            locPopupDrawPos.x += locPopupDrawPos.width * 0.6f;
                            EditorGUI.Popup(locPopupDrawPos, 0, new string[] { "No Results Found" });
                        }
                        else
                        {
                            EditorGUILayout.Popup(SharedCustomEditorValues.LocIdPopupTitle, 0, new string[] { "No Results Found" });
                        }
                    }
                }
            }
            else
            {
                // Draw Regular Dropdown Boxes
                if (currentLocIdData.CurrentPopupIndex == -1)
                {
                    if (LocIDsEditorData.Combined_EditorHashIdToInspectorPopupIndex.TryGetValue(currentLocIdData.CurrentHashId, out currentLocIdData.CurrentPopupIndex) == false)
                    {
                        currentLocIdData.CurrentPopupIndex = -1;
                    }
                }

                int newSelectedIndex;
                if (renderOnOneLine)
                {
                    Rect locPopupDrawPos = searchBoxDrawPos;
                    locPopupDrawPos.x += locPopupDrawPos.width * 0.6f;
                    newSelectedIndex = EditorGUI.Popup(locPopupDrawPos, currentLocIdData.CurrentPopupIndex, LocIDsEditorData.Combined_EditorCategorisedAlphabeticallyOrderedLocStringIDs);
                }
                else
                {
                    newSelectedIndex = EditorGUILayout.Popup(SharedCustomEditorValues.LocIdPopupTitle, currentLocIdData.CurrentPopupIndex, LocIDsEditorData.Combined_EditorCategorisedAlphabeticallyOrderedLocStringIDs);
                }

                if (newSelectedIndex != -1 && newSelectedIndex != currentLocIdData.CurrentPopupIndex)
                {
                    // Return new Loc ID Data because this is a new result from previous selection
                    currentLocIdData.CurrentHashId = LocIDsEditorData.Combined_EditorInspectorPopupIndexToHashId[newSelectedIndex];
                    currentLocIdData.CurrentPopupIndex = newSelectedIndex;
                }
            }

            if (drawEnglishTextAlso)
            {
                _cachedLocStruct.locId = currentLocIdData.CurrentHashId;
                DrawLabel("Text (EN):", LocManager.GetTextViaLocHashID(_cachedLocStruct, LocLanguages.English).Replace("\n", "\\n"));
            }

            return currentLocIdData.CurrentHashId;
        }

        /// <summary> Draws a Loc ID Option on One Line. </summary>
        /// <param name="headerName"> Label for the Loc ID. </param>
        /// <param name="currentHashValue"> Current Loc ID Value. </param>
        /// <param name="owner"> The owner of this Loc ID. If it is a part of an array, pass in the array element ( array[i] ) as the owner. </param>
        /// <param name="drawEnglishTextAlso"> If true, draws an English Text output on the next line. </param>
        /// <param name="tooltip"> Tooltip to show with this Loc ID Property. </param>
        public int DrawLocIdOptionOneLine(string headerName, int currentHashValue, object owner = null, bool drawEnglishTextAlso = true, string tooltip = null)
        {
            return DrawLocIdOption(headerName, currentHashValue, true, owner, drawEnglishTextAlso, tooltip);
        }

        /// <summary> Draws a Loc ID Option on Multiple Lines (Search Field one line, Loc ID next line). </summary>
        /// <param name="headerName"> Label for the Loc ID. </param>
        /// <param name="currentHashValue"> Current Loc ID Value. </param>
        /// <param name="owner"> The owner of this Loc ID. If it is a part of an array, pass in the array element ( array[i] ) as the owner. </param>
        /// <param name="drawEnglishTextAlso"> If true, draws an English Text output on the next line. </param>
        /// <param name="tooltip"> Tooltip to show with this Loc ID Property. </param>
        public int DrawLocIdOptionMultiLine(string headerName, int currentHashValue, object owner = null, bool drawEnglishTextAlso = true, string tooltip = null)
        {
            return DrawLocIdOption(headerName, currentHashValue, false, owner, drawEnglishTextAlso, tooltip);
        }

        /// <summary> Draws a Loc ID Option. </summary>
        /// <param name="headerName"> Label for the Loc ID. </param>
        /// <param name="currentHashValue"> Current Loc ID Value. </param>
        /// <param name="owner"> The owner of this Loc ID. If it is a part of an array, pass in the array element ( array[i] ) as the owner. </param>
        /// <param name="drawEnglishTextAlso"> If true, draws an English Text output on the next line. </param>
        /// <param name="tooltip"> Tooltip to show with this Loc ID Property. </param>
        public void DrawReadonlyLocIdOption(string headerName, int currentHashValue, object owner = null, bool drawEnglishTextAlso = true, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawLocIdOption(headerName, currentHashValue, false, owner, drawEnglishTextAlso, tooltip);
            }
        }

        /// <summary> Draws a Loc ID Option. </summary>
        /// <param name="headerName"> Label for the Loc ID. </param>
        /// <param name="currentLocId"> Current Loc ID Value. </param>
        /// <param name="drawEnglishTextAlso"> If true, draws an English Text output on the next line. </param>
        /// <param name="tooltip"> Tooltip to show with this Loc ID Property. </param>
        public void DrawReadonlyLocIdOption(string headerName, LocID currentLocId, bool drawEnglishTextAlso = true, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawLocIdOption(headerName, currentLocId.locId, false, currentLocId, drawEnglishTextAlso, tooltip);
            }
        }

        /// <summary> Draws a Loc ID Option on One Line. </summary>
        /// <param name="headerName"> Label for the Loc ID. </param>
        /// <param name="currentLocId"> Current Loc ID Value. </param>
        /// <param name="drawEnglishTextAlso"> If true, draws an English Text output on the next line. </param>
        /// <param name="tooltip"> Tooltip to show with this Loc ID Property. </param>
        /// <param name="targetObj"> The serialized object that this Loc ID belongs to. If null, will use BaseTarget. </param>
        public bool DrawLocIdOptionWithUndoOptionOneLine(string headerName, ref LocID currentLocId, bool drawEnglishTextAlso = true, string tooltip = null, UnityEngine.Object targetObj = null)
        {
            int result = DrawLocIdOption(headerName, currentLocId, true, currentLocId, drawEnglishTextAlso, tooltip);
            if (result != currentLocId)
            {
                Undo.RecordObject(targetObj != null ? targetObj : BaseTarget, $"Changed {headerName}");
                currentLocId.locId = result;
                return true;
            }
            return false;
        }

        /// <summary> Draws a Loc ID Option on One Line. </summary>
        /// <param name="headerName"> Label for the Loc ID. </param>
        /// <param name="currentHashValue"> Current Loc ID Value. </param>
        /// <param name="owner"> The owner of this Loc ID. If it is a part of an array, pass in the array element ( array[i] ) as the owner. </param>
        /// <param name="drawEnglishTextAlso"> If true, draws an English Text output on the next line. </param>
        /// <param name="tooltip"> Tooltip to show with this Loc ID Property. </param>
        /// <param name="targetObj"> The serialized object that this Loc ID belongs to. If null, will use BaseTarget. </param>
        public bool DrawLocIdOptionWithUndoOptionOneLine(string headerName, ref int currentHashValue, object owner, bool drawEnglishTextAlso = true, string tooltip = null, UnityEngine.Object targetObj = null)
        {
            int result = DrawLocIdOption(headerName, currentHashValue, true, owner, drawEnglishTextAlso, tooltip);
            if (result != currentHashValue)
            {
                Undo.RecordObject(targetObj != null ? targetObj : BaseTarget, $"Changed {headerName}");
                currentHashValue = result;
                return true;
            }
            return false;
        }

        /// <summary> Draws a Loc ID Option on Multiple Lines (Search Field one line, Loc ID next line). </summary>
        /// <param name="headerName"> Label for the Loc ID. </param>
        /// <param name="currentLocId"> Current Loc ID Value. </param>
        /// <param name="drawEnglishTextAlso"> If true, draws an English Text output on the next line. </param>
        /// <param name="tooltip"> Tooltip to show with this Loc ID Property. </param>
        /// <param name="targetObj"> The serialized object that this Loc ID belongs to. If null, will use BaseTarget. </param>
        public bool DrawLocIdOptionWithUndoOptionOnMultipleLines(string headerName, ref LocID currentLocId, bool drawEnglishTextAlso = true, string tooltip = null, UnityEngine.Object targetObj = null)
        {
            int result = DrawLocIdOption(headerName, currentLocId, false, currentLocId, drawEnglishTextAlso, tooltip);
            if (result != currentLocId)
            {
                Undo.RecordObject(targetObj != null ? targetObj : BaseTarget, $"Changed {headerName}");
                currentLocId.locId = result;
                return true;
            }
            return false;
        }

        /// <summary> Draws a Loc ID Option on Multiple Lines (Search Field one line, Loc ID next line). </summary>
        /// <param name="headerName"> Label for the Loc ID. </param>
        /// <param name="currentHashValue"> Current Loc ID Value. </param>
        /// <param name="owner"> The owner of this Loc ID. If it is a part of an array, pass in the array element ( array[i] ) as the owner. </param>
        /// <param name="drawEnglishTextAlso"> If true, draws an English Text output on the next line. </param>
        /// <param name="tooltip"> Tooltip to show with this Loc ID Property. </param>
        /// <param name="targetObj"> The serialized object that this Loc ID belongs to. If null, will use BaseTarget. </param>
        public bool DrawLocIdOptionWithUndoOptionOnMultipleLines(string headerName, ref int currentHashValue, object owner, bool drawEnglishTextAlso = true, string tooltip = null, UnityEngine.Object targetObj = null)
        {
            int result = DrawLocIdOption(headerName, currentHashValue, false, owner, drawEnglishTextAlso, tooltip);
            if (result != currentHashValue)
            {
                Undo.RecordObject(targetObj != null ? targetObj : BaseTarget, $"Changed {headerName}");
                currentHashValue = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Object Option
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public T DrawObjectOption<T>(string displayLabel, T obj, string tooltip = null) 
            where T : UnityEngine.Object
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return DrawObjectOption(label, obj);
        }

        public void DrawReadonlyObjectOption<T>(string displayLabel, T obj, string tooltip = null) 
            where T : UnityEngine.Object
        {
            using (new GUIDisable())
            {
                DrawObjectOption(displayLabel, obj, tooltip);
            }
        }

        public bool DrawObjectOptionWithUndoOption<T>(string displayLabel, ref T obj, string tooltip = null) 
            where T : UnityEngine.Object
        {
            T result = DrawObjectOption(displayLabel, obj, tooltip);
            if (result != obj)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                obj = result;
                return true;
            }
            return false;
        }

        public T DrawObjectOption<T>(string displayLabel, T obj, string tooltipWhenAssignedAnObject, string tooltipWhenNoObjectIsAssigned) 
            where T : UnityEngine.Object
        {
            GUIContent label = null;
            if (obj != null)
            {
                if (string.IsNullOrEmpty(tooltipWhenAssignedAnObject) == false)
                {
                    label = PerformLabelConversion(displayLabel, tooltipWhenAssignedAnObject);
                }
            }
            else
            {
                if (string.IsNullOrEmpty(tooltipWhenNoObjectIsAssigned) == false)
                {
                    label = PerformLabelConversion(displayLabel, tooltipWhenNoObjectIsAssigned);
                }
                else if (string.IsNullOrEmpty(tooltipWhenAssignedAnObject) == false)
                {
                    label = PerformLabelConversion(displayLabel, tooltipWhenAssignedAnObject + "\n\nThis object is required. You must assign something here!");
                }
            }

            label ??= PerformLabelConversion(displayLabel);

            return DrawObjectOption(label, obj);
        }

        public T DrawObjectOption<T>(GUIContent display, T obj) 
            where T : UnityEngine.Object
        {
            if (typeof(T) == typeof(Sprite) || typeof(T).IsSubclassOf(typeof(Sprite)))
            {
                // Sprites no longer seem to work in Unity 2020+. So gotta do a roundabout solution.
#if !UNITY_2020_1_OR_NEWER
                // Can't do Template Specialization in C#. So have to do this hacky copy-paste job instead. If it's a Sprite show all text on the same line as Sprite
                //  Since Unity now shows the sprites in the inspector rather than the object box (like it used to).
                T val = (T)EditorGUILayout.ObjectField(" ", obj, typeof(T), true);
                Rect spriteRect = GUILayoutUtility.GetLastRect();
                Rect drawPosition = new Rect(4.4f * (EditorGUI.indentLevel + 1), spriteRect.y, 300, spriteRect.height);
                EditorGUI.LabelField(drawPosition, display);
                return val;
#else
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.PrefixLabel(display);
                Sprite output = (Sprite)EditorGUILayout.ObjectField(obj, typeof(Sprite), true);
                EditorGUILayout.EndHorizontal();
                DrawSpriteAsTexture(output, 64, 64, SimpleAlignment.Centered);
                return output as T;
#endif
            }
            return (T)EditorGUILayout.ObjectField(display, obj, typeof(T), true);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Label
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawLabel(string text)
        {
            GUIContent label = PerformLabelConversion(text);
            EditorGUILayout.LabelField(label);
        }
        
        public void DrawLabel(string header, string text)
        {
            GUIContent label = new GUIContent(header);
            GUIContent textLabel = PerformLabelConversion(text);
            EditorGUILayout.LabelField(label, textLabel);
        }

        public void DrawLabel(string text, GUIStyle s, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(text, tooltip);
            EditorGUILayout.LabelField(label, s);
        }

        public void DrawLabel(string header, string text, GUIStyle s, string tooltip = null)
        {
            GUIContent headerLabel = new GUIContent(header);
            GUIContent textLabel = PerformLabelConversion(text, tooltip);
            EditorGUILayout.LabelField(headerLabel, textLabel, s);
        }

        public void DrawLabel(string text, Color colour, string tooltip = null)
        {
            GUIStyle s = new GUIStyle
            {
                normal =
                {
                    textColor = colour
                }
            };
            DrawLabel(text, s, tooltip);
        }

        public void DrawLabel(string header, string text, Color colour, string tooltip = null)
        {
            GUIStyle s = new GUIStyle
            {
                normal =
                {
                    textColor = colour
                }
            };
            DrawLabel(header, text, s, tooltip);
        }

        public void DrawLabelCenterAligned(string text, string tooltip = null)
        {
            GUIStyle s = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleCenter
            };
            DrawLabel(text, s, tooltip);
        }

        public void DrawLabelRightAligned(string text, string tooltip = null)
        {
            GUIStyle s = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleRight
            };
            DrawLabel(text, s, tooltip);
        }

        public void DrawBoldLabel(string text, string tooltip = null)
        {
            DrawLabel(text, EditorStyles.boldLabel, tooltip);
        }

        public void DrawBoldLabel(string header, string text, string tooltip)
        {
            DrawLabel(header, text, EditorStyles.boldLabel, tooltip);
        }


        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Scene Label
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawSceneLabel(string text, Vector3 worldPosition, int fontSize = 12)
        {
            GUIStyle style = new GUIStyle()
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = fontSize,
                fontStyle = FontStyle.Bold,
            };
            Handles.Label(worldPosition, text, style);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Changeable Number Option (INT)
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public int DrawChangeableIntegerOption(string displayLabel, int currentNumber, int changingAmount = 1, string tooltip = null, bool indentLabel = false, int? minValue = null, int? maxValue = null)
        {
            if (string.IsNullOrEmpty(displayLabel) == false)
            {
                GUIContent label = PerformLabelConversion(displayLabel, tooltip);
                if (indentLabel)
                {
                    using (new EditorIndentation())
                    {
                        EditorGUILayout.LabelField(label);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(label);
                }
            }
            return DrawChangeableIntegerOption(GetScaledRect(), string.Empty, currentNumber, changingAmount, string.Empty, false, minValue, maxValue);
        }

        public void DrawReadonlyChangeableIntegerOption(string displayLabel, int currentNumber, string tooltip = null)
        {
            DrawReadonlyIntField(displayLabel, currentNumber, tooltip);
        }

        public bool DrawChangeableIntegerWithUndoOption(string displayLabel, ref int currentNumber, int changingAmount = 1, string tooltip = null, bool indentLabel = false, int? minValue = null, int? maxValue = null)
        {
            int result = DrawChangeableIntegerOption(displayLabel, currentNumber, changingAmount, tooltip, indentLabel, minValue, maxValue);
            if (result != currentNumber)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentNumber = result;
                return true;
            }
            return false;
        }

        public int DrawChangeableIntegerOption(Rect drawPos, string label, int currentNumber, int changingAmount = 1, string tooltip = null, bool indentLabel = false, int? minValue = null, int? maxValue = null)
        {
            if (string.IsNullOrEmpty(label) == false)
            {
                GUIContent guiLabel = PerformLabelConversion(label, tooltip);
                if (indentLabel)
                {
                    using (new EditorIndentation())
                    {
                        EditorGUILayout.LabelField(guiLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(guiLabel);
                }
            }

            // Button Width
            float bw = 20;

            // Text Box width
            int currentNumOfDigits = (int)Mathf.Floor(Mathf.Log10(currentNumber) + 1);
            float sizePerDigit = 9.0f;

            float tw = Mathf.Max(30.0f, currentNumOfDigits * sizePerDigit);
            Rect pos = drawPos;
            pos.x = StartDrawValueXPos;
            pos.width = bw;
            if (GUI.Button(pos, "<"))
            {
                currentNumber -= changingAmount;
            }

            pos.x += pos.width + 5;
            pos.width = tw;
            using (new SetEditorIndentation(0))
            {
                currentNumber = int.Parse(EditorGUI.TextField(pos, currentNumber.ToString()));
            }

            pos.x += pos.width + 5;
            pos.width = bw;
            if (GUI.Button(pos, ">"))
            {
                currentNumber += changingAmount;
            }

            if (minValue.HasValue && currentNumber < minValue.Value)
            {
                currentNumber = minValue.Value;
            }

            if (maxValue.HasValue && currentNumber > maxValue.Value)
            {
                currentNumber = maxValue.Value;
            }

            return currentNumber;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Changeable Number Option (FLOAT)
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public float DrawChangeableFloatOption(string displayLabel, float currentNumber, float changingAmount = 0.1f, string tooltip = null, bool indentLabel = false, float? minValue = null, float? maxValue = null)
        {
            if (string.IsNullOrEmpty(displayLabel) == false)
            {
                GUIContent guiLabel = PerformLabelConversion(displayLabel, tooltip);
                if (indentLabel)
                {
                    using (new EditorIndentation())
                    {
                        EditorGUILayout.LabelField(guiLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(guiLabel);
                }
            }
            return DrawChangeableFloatOption(GetScaledRect(), string.Empty, currentNumber, changingAmount, string.Empty, false, minValue, maxValue);
        }

        public void DrawReadonlyChangeableFloatOption(string displayLabel, float currentNumber, string tooltip = null)
        {
            DrawReadonlyFloatField(displayLabel, currentNumber, tooltip);
        }

        public bool DrawChangeableFloatOptionWithUndoOption(string displayLabel, ref float currentNumber, float changingAmount = 0.1f, string tooltip = null, bool indentLabel = false, float? minValue = null, float? maxValue = null)
        {
            float result = DrawChangeableFloatOption(displayLabel, currentNumber, changingAmount, tooltip, indentLabel, minValue, maxValue);
            if (Mathf.Approximately(result, currentNumber) == false)
            {
                Undo.RecordObject(BaseTarget, $"Changed {displayLabel}");
                currentNumber = result;
                return true;
            }
            return false;
        }

        public float DrawChangeableFloatOption(Rect drawPos, string label, float currentNumber, float changingAmount = 0.1f, string tooltip = null, bool indentLabel = false, float? minValue = null, float? maxValue = null)
        {
            if (string.IsNullOrEmpty(label) == false)
            {
                GUIContent guiLabel = PerformLabelConversion(label, tooltip);
                if (indentLabel)
                {
                    using (new EditorIndentation())
                    {
                        EditorGUILayout.LabelField(guiLabel);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(guiLabel);
                }
            }
            float bw = 20; // Button Width
            float tw = 50; // Text Box width
            Rect pos = drawPos;
            pos.x = StartDrawValueXPos;
            pos.width = bw;
            if (GUI.Button(pos, "<"))
            {
                currentNumber -= changingAmount;
            }

            pos.x += pos.width + 5;
            pos.width = tw;

            using (new SetEditorIndentation(0))
            {
                currentNumber = float.Parse(EditorGUI.TextField(pos, currentNumber.ToString(CultureInfo.InvariantCulture)));
            }

            pos.x += pos.width + 5;
            pos.width = bw;
            if (GUI.Button(pos, ">"))
            {
                currentNumber += changingAmount;
            }

            if (minValue.HasValue && currentNumber < minValue.Value)
            {
                currentNumber = minValue.Value;
            }
            if (maxValue.HasValue && currentNumber > maxValue.Value)
            {
                currentNumber = maxValue.Value;
            }

            return currentNumber;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Range
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public float DrawRange(string displayLabel, float currentValue, float min = 0.0f, float max = 1.0f, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.Slider(label, currentValue, min, max);
        }

        public int DrawRange(string displayLabel, int currentValue, int min = -1000, int max = 1000, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.IntSlider(label, currentValue, min, max);
        }

        public void DrawReadonlyRange(string displayLabel, float currentValue, float min = 0.0f, float max = 1.0f, string tooltip = null)
        {
            using (new GUIDisable())
            {
                DrawRange(displayLabel, currentValue, min, max, tooltip);
            }
        }

        public int DrawReadonlyRange(string displayLabel, int currentValue, int min = -1000, int max = 1000, string tooltip = null)
        {
            using (new GUIDisable())
            {
                return DrawRange(displayLabel, currentValue, min, max, tooltip);
            }
        }

        public bool DrawRangeWithUndoOption(string displayLabel, ref float currentValue, float min = 0.0f, float max = 1.0f, string tooltip = null)
        {
            float result = DrawRange(displayLabel, currentValue, min, max, tooltip);
            if (Mathf.Approximately(result, currentValue) == false)
            {
                Undo.RecordObject(BaseTarget, $"Modified {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }

        public bool DrawRangeWithUndoOption(string displayLabel, ref int currentValue, int min = -1000, int max = 1000, string tooltip = null)
        {
            int result = DrawRange(displayLabel, currentValue, min, max, tooltip);
            if (result != currentValue)
            {
                Undo.RecordObject(BaseTarget, $"Modified {displayLabel}");
                currentValue = result;
                return true;
            }
            return false;
        }

        public float DrawFloatRange(string displayLabel, float currentValue, float min = 0.0f, float max = 1.0f, string tooltip = null)
        {
            return DrawRange(displayLabel, currentValue, min, max, tooltip);
        }

        public void DrawReadonlyFloatRange(string displayLabel, float currentValue, float min = 0.0f, float max = 1.0f, string tooltip = null)
        {
            DrawReadonlyRange(displayLabel, currentValue, min, max, tooltip);
        }

        public bool DrawFloatRangeWithUndoOption(string displayLabel, ref float currentValue, float min = 0.0f, float max = 1.0f, string tooltip = null)
        {
            return DrawRangeWithUndoOption(displayLabel, ref currentValue, min, max, tooltip);
        }

        public int DrawIntRange(string displayLabel, int currentValue, int min = -1000, int max = 1000, string tooltip = null)
        {
            return DrawRange(displayLabel, currentValue, min, max, tooltip);
        }

        public void DrawReadonlyIntRange(string displayLabel, int currentValue, int min = -1000, int max = 1000, string tooltip = null)
        {
            DrawReadonlyRange(displayLabel, currentValue, min, max, tooltip);
        }

        public bool DrawIntRangeWithUndoOption(string displayLabel, ref int currentValue, int min = -1000, int max = 1000, string tooltip = null)
        {
            return DrawRangeWithUndoOption(displayLabel, ref currentValue, min, max, tooltip);
        }

        public float DrawSliderRange(string displayLabel, float currentValue, float min = 0.0f, float max = 1.0f, string tooltip = null)
        {
            return DrawRange(displayLabel, currentValue, min, max, tooltip);
        }

        public int DrawSliderRange(string displayLabel, int currentValue, int min = -1000, int max = 1000, string tooltip = null)
        {
            return DrawRange(displayLabel, currentValue, min, max, tooltip);
        }

        public void DrawReadonlySliderRange(string displayLabel, float currentValue, float min = 0.0f, float max = 1.0f, string tooltip = null)
        {
            DrawReadonlyRange(displayLabel, currentValue, min, max, tooltip);
        }

        public void DrawReadonlySliderRange(string displayLabel, int currentValue, int min = -1000, int max = 1000, string tooltip = null)
        {
            DrawReadonlyRange(displayLabel, currentValue, min, max, tooltip);
        }

        public bool DrawSliderRangeWithUndoOption(string displayLabel, ref float currentValue, float min = 0.0f, float max = 1.0f, string tooltip = null)
        {
            return DrawRangeWithUndoOption(displayLabel, ref currentValue, min, max, tooltip);
        }

        public bool DrawSliderRangeWithUndoOption(string displayLabel, ref int currentValue, int min = -1000, int max = 1000, string tooltip = null)
        {
            return DrawRangeWithUndoOption(displayLabel, ref currentValue, min, max, tooltip);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw Plus/Minus Buttons
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public PlusMinusButtons DrawPlusMinusButtons(float yOffset = 20.0f, bool canDrawPlusButton = true, bool canDrawMinusButton = true)
        {
            const float buttonWidth = 60;
            const float spacePixels = 5;
            const float offsetPosition = (buttonWidth * 2.0f) + spacePixels;

            Rect pos = GetScaledRect();
            pos.x = (pos.x + pos.width) - offsetPosition;
            pos.y += yOffset;
            pos.width = buttonWidth;

            EditorGUILayout.Space();

            if (canDrawPlusButton && GUI.Button(pos, "+"))
            {
                return PlusMinusButtons.PlusPressed;
            }

            if (canDrawMinusButton)
            {
                pos.x += pos.width + spacePixels;
                if (GUI.Button(pos, "-"))
                {
                    return PlusMinusButtons.MinusPressed;
                }
            }

            return PlusMinusButtons.None;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Animation Curve
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public AnimationCurve DrawCurve(string displayLabel, ref AnimationCurve curve, string tooltip = null)
        {
            GUIContent label = PerformLabelConversion(displayLabel, tooltip);
            return EditorGUILayout.CurveField(label, curve);
        }

        public bool DrawCurveWithUndoOption(string displayLabel, ref AnimationCurve currentCurve, string tooltip = null)
        {
            AnimationCurve result = DrawCurve(displayLabel, ref currentCurve, tooltip);
            if (result.Equals(currentCurve) == false)
            {
                Undo.RecordObject(BaseTarget, $"Modified {displayLabel}");
                currentCurve = result;
                return true;
            }
            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Array Header - Struct Type
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public bool DrawArrayHeader<T>(string arrayDisplayLabel, ref T[] array, bool drawAsFoldoutOption = true, int numberOfElementsExpected = -1) 
            where T : struct
        {
            array ??= new T[1];

            bool retShowArrayContents;
            ArrayTypeDef<T> definition = array;
            definition = ResolveArrayHeader(arrayDisplayLabel, definition, numberOfElementsExpected, drawAsFoldoutOption, out retShowArrayContents);
            array = definition.StoredArray;
            return retShowArrayContents;
        }

        public bool DrawArrayHeader<T>(string arrayDisplayLabel, ref List<T> list, bool drawAsFoldoutOption = true, int numberOfElementsExpected = -1) 
            where T : struct
        {
            list ??= new List<T>();

            bool retShowArrayContents;
            ArrayTypeDef<T> definition = list;
            ResolveArrayHeader(arrayDisplayLabel, definition, numberOfElementsExpected, drawAsFoldoutOption, out retShowArrayContents);
            return retShowArrayContents;
        }

        private ArrayTypeDef<T> ResolveArrayHeader<T>(string arrayDisplayLabel, ArrayTypeDef<T> arrayDef, int numberOfElementsExpected,
                                                                                bool drawAsFoldoutOption, out bool retShowArrayContents) 
            where T : struct
        {
            retShowArrayContents = true;
            if (drawAsFoldoutOption)
            {
                retShowArrayContents = DrawFoldoutOption(arrayDisplayLabel);
            }
            else
            {
                DrawLabel(arrayDisplayLabel);
            }

            if (arrayDef.Length < numberOfElementsExpected)
            {
                if (arrayDef.StoredArray != null)
                {
                    ResizeArray(ref arrayDef.StoredArray, numberOfElementsExpected);
                }
                else
                {
                    ResizeArray(ref arrayDef.StoredList, numberOfElementsExpected);
                }
            }

            return arrayDef;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Array Header - Class Type
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public bool DrawReferenceArrayHeader<T>(string arrayDisplayLabel, ref T[] array, bool drawAsFoldoutOption = true, int numberOfElementsExpected = -1, bool forceAllElementsToReferenceAnObject = false)
            where T : class
        {
            if (array == null)
            {
                array = new T[1];
                if (CanAutomaticallyAssignNewClassValue(typeof(T)))
                {
                    array[0] = System.Activator.CreateInstance(typeof(T), true) as T;
                }
            }

            bool retShowArrayContents;
            ArrayTypeDef<T> definition = array;
            definition = ResolveReferenceArrayHeader(arrayDisplayLabel, definition, numberOfElementsExpected, drawAsFoldoutOption, forceAllElementsToReferenceAnObject, out retShowArrayContents);
            array = definition.StoredArray;
            return retShowArrayContents;
        }

        public bool DrawReferenceArrayHeader<T>(string arrayDisplayLabel, ref List<T> array, bool drawAsFoldoutOption = true, int numberOfElementsExpected = -1, bool forceAllElementsToReferenceAnObject = false)
            where T : class
        {
            if (array == null)
            {
                array = new List<T>();
                if (CanAutomaticallyAssignNewClassValue(typeof(T)))
                {
                    array.Add(System.Activator.CreateInstance(typeof(T), true) as T);
                }
            }

            bool retShowArrayContents;
            ArrayTypeDef<T> definition = array;
            ResolveReferenceArrayHeader(arrayDisplayLabel, definition, numberOfElementsExpected, drawAsFoldoutOption, forceAllElementsToReferenceAnObject, out retShowArrayContents);
            return retShowArrayContents;
        }

        private ArrayTypeDef<T> ResolveReferenceArrayHeader<T>(string arrayDisplayLabel, ArrayTypeDef<T> arrayDef, int numberOfElementsExpected, bool drawAsFoldoutOption,
                                                                    bool forceAllElementsToReferenceAnObject, out bool retShowArrayContents) 
            where T : class
        {
            retShowArrayContents = true;
            if (drawAsFoldoutOption)
            {
                retShowArrayContents = DrawFoldoutOption(arrayDisplayLabel);
            }
            else
            {
                DrawLabel(arrayDisplayLabel);
            }

            int count = arrayDef.Count;
            if (count < numberOfElementsExpected)
            {
                if (arrayDef.StoredArray != null)
                {
                    ResizeReferenceArray(ref arrayDef.StoredArray, numberOfElementsExpected);
                }
                else
                {
                    ResizeReferenceArray(ref arrayDef.StoredList, numberOfElementsExpected);
                }
            }

            if (forceAllElementsToReferenceAnObject)
            {
                if (CanAutomaticallyAssignNewClassValue(typeof(T)))
                {
                    for (int i = 0; i < count; ++i)
                    {
                        arrayDef[i] ??= System.Activator.CreateInstance(typeof(T), true) as T;
                    }
                }
            }

            return arrayDef;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Array Elements - Struct Type
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public ArrayModificationResult DrawArrayElements<T>(ref T[] array, DrawArrayParameters<T> drawArrayParams, OneDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true) 
            where T : struct
        {
            array ??= new T[1];

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                DrawElementCallback1D = drawElementCallback
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = array;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);
            array = definition.StoredArray;

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeArray(ref array, array.Length + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeArray(ref array, array.Length - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                RemoveArrayElementAt(ref array, requestedDeleteIndex);
            }

            return arrayModificationResult;
        }

        public ArrayModificationResult DrawArrayElements<T>(ref List<T> list, DrawArrayParameters<T> drawArrayParams, OneDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true)
            where T : struct
        {
            list ??= new List<T>();

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                DrawElementCallback1D = drawElementCallback
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = list;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeArray(ref list, list.Count + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeArray(ref list, list.Count - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                list.RemoveAt(requestedDeleteIndex);
            }

            return arrayModificationResult;
        }

        public ArrayModificationResult Draw2DArrayElements<T>(ref T[] array, int twoDOuterArrayIndex, DrawArrayParameters<T> drawArrayParams, TwoDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true)
            where T : struct
        {
            array ??= new T[1];

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                TwoDOuterArrayIndex = twoDOuterArrayIndex,
                DrawElementCallback2D = drawElementCallback,
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = array;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);
            array = definition.StoredArray;

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeArray(ref array, array.Length + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeArray(ref array, array.Length - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                RemoveArrayElementAt(ref array, requestedDeleteIndex);
            }

            return arrayModificationResult;
        }

        public ArrayModificationResult Draw2DArrayElements<T>(ref List<T> list, int twoDOuterArrayIndex, DrawArrayParameters<T> drawArrayParams, TwoDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true) 
            where T : struct
        {
            list ??= new List<T>();

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                TwoDOuterArrayIndex = twoDOuterArrayIndex,
                DrawElementCallback2D = drawElementCallback,
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = list;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeArray(ref list, list.Count + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeArray(ref list, list.Count - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                list.RemoveAt(requestedDeleteIndex);
            }

            return arrayModificationResult;
        }

        public ArrayModificationResult Draw3DArrayElements<T>(ref T[] array, int threeDOuterArrayIndex, int twoDOuterArrayIndex, DrawArrayParameters<T> drawArrayParams, ThreeDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true)
            where T : struct
        {
            array ??= new T[1];

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                ThreeDOuterArrayIndex = threeDOuterArrayIndex,
                TwoDOuterArrayIndex = twoDOuterArrayIndex,
                DrawElementCallback3D = drawElementCallback,
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = array;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);
            array = definition.StoredArray;

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeArray(ref array, array.Length + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeArray(ref array, array.Length - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                RemoveArrayElementAt(ref array, requestedDeleteIndex);
            }

            return arrayModificationResult;
        }

        public ArrayModificationResult Draw3DArrayElements<T>(ref List<T> list, int threeDOuterArrayIndex, int twoDOuterArrayIndex, DrawArrayParameters<T> drawArrayParams, ThreeDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true)
            where T : struct
        {
            list ??= new List<T>();

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                ThreeDOuterArrayIndex = threeDOuterArrayIndex,
                TwoDOuterArrayIndex = twoDOuterArrayIndex,
                DrawElementCallback3D = drawElementCallback,
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = list;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeArray(ref list, list.Count + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeArray(ref list, list.Count - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                list.RemoveAt(requestedDeleteIndex);
            }

            return arrayModificationResult;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Array Elements - Class Type
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public ArrayModificationData DrawReferenceArrayElements<T>(ref T[] array, DrawArrayParameters<T> drawArrayParams, OneDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true) 
            where T : class
        {
            array ??= new T[1];

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                DrawElementCallback1D = drawElementCallback
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = array;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);

            int affectedIndex = -1;
            if (arrayModificationResult != ArrayModificationResult.NoChange)
            {
                if (_cachedDropdownChoicesWithUsedElementsRemoved.ContainsKey(array))
                {
                    _cachedDropdownChoicesWithUsedElementsRemoved.Remove(array);
                }

                if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
                {
                    affectedIndex = array.Length + 1;
                    ResizeReferenceArray(ref array, affectedIndex);
                }
                else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
                {
                    affectedIndex = array.Length;
                    ResizeReferenceArray(ref array, array.Length - 1);
                }
                else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
                {
                    RemoveArrayElementAt(ref array, requestedDeleteIndex);
                    affectedIndex = requestedDeleteIndex;
                }
            }


            return new ArrayModificationData()
            {
                ModResult = arrayModificationResult,
                AffectedIndex = affectedIndex,
            };
        }

        public ArrayModificationData DrawReferenceArrayElements<T>(ref List<T> list, DrawArrayParameters<T> drawArrayParams, OneDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true) 
            where T : class
        {
            list ??= new List<T>();

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                DrawElementCallback1D = drawElementCallback
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = list;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);

            int affectedIndex = -1;
            if (arrayModificationResult != ArrayModificationResult.NoChange)
            {
                if (_cachedDropdownChoicesWithUsedElementsRemoved.ContainsKey(list))
                {
                    _cachedDropdownChoicesWithUsedElementsRemoved.Remove(list);
                }

                if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
                {
                    affectedIndex = list.Count + 1;
                    ResizeReferenceArray(ref list, affectedIndex);
                }
                else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
                {
                    affectedIndex = list.Count;
                    ResizeReferenceArray(ref list, list.Count - 1);
                }
                else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
                {
                    list.RemoveAt(requestedDeleteIndex);
                    affectedIndex = requestedDeleteIndex;
                }
            }


            return new ArrayModificationData()
            {
                ModResult = arrayModificationResult,
                AffectedIndex = affectedIndex,
            };
        }

        public ArrayModificationResult Draw2DReferenceArrayElements<T>(ref T[] array, int twoDOuterArrayIndex, DrawArrayParameters<T> drawArrayParams, TwoDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true) 
            where T : class
        {
            array ??= new T[1];

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                TwoDOuterArrayIndex = twoDOuterArrayIndex,
                DrawElementCallback2D = drawElementCallback,
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = array;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeReferenceArray(ref array, array.Length + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeReferenceArray(ref array, array.Length - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                RemoveArrayElementAt(ref array, requestedDeleteIndex);
            }

            if (arrayModificationResult != ArrayModificationResult.NoChange)
            {
                if (_cachedDropdownChoicesWithUsedElementsRemoved.ContainsKey(array))
                {
                    _cachedDropdownChoicesWithUsedElementsRemoved.Remove(array);
                }
            }

            return arrayModificationResult;
        }

        public ArrayModificationResult Draw2DReferenceArrayElements<T>(ref List<T> list, int twoDOuterArrayIndex, DrawArrayParameters<T> drawArrayParams, TwoDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true) 
            where T : class
        {
            list ??= new List<T>();

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                TwoDOuterArrayIndex = twoDOuterArrayIndex,
                DrawElementCallback2D = drawElementCallback,
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = list;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeReferenceArray(ref list, list.Count + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeReferenceArray(ref list, list.Count - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                list.RemoveAt(requestedDeleteIndex);
            }

            if (arrayModificationResult != ArrayModificationResult.NoChange)
            {
                if (_cachedDropdownChoicesWithUsedElementsRemoved.ContainsKey(list))
                {
                    _cachedDropdownChoicesWithUsedElementsRemoved.Remove(list);
                }
            }

            return arrayModificationResult;
        }

        public ArrayModificationResult Draw3DReferenceArrayElements<T>(ref T[] array, int threeDOuterArrayIndex, int twoDOuterArrayIndex, DrawArrayParameters<T> drawArrayParams, ThreeDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true) 
            where T : class
        {
            array ??= new T[1];

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                ThreeDOuterArrayIndex = threeDOuterArrayIndex,
                TwoDOuterArrayIndex = twoDOuterArrayIndex,
                DrawElementCallback3D = drawElementCallback,
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = array;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);
            array = definition.StoredArray;

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeReferenceArray(ref array, array.Length + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeReferenceArray(ref array, array.Length - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                RemoveArrayElementAt(ref array, requestedDeleteIndex);
            }

            return arrayModificationResult;
        }

        public ArrayModificationResult Draw3DReferenceArrayElements<T>(ref List<T> list, int threeDOuterArrayIndex, int twoDArrayIndex, DrawArrayParameters<T> drawArrayParams, ThreeDArrayElementDrawAction drawElementCallback, bool allowUndoOption = true) 
            where T : class
        {
            list ??= new List<T>();

            ArrayCallbackParameters drawElemParam = new ArrayCallbackParameters()
            {
                ThreeDOuterArrayIndex = threeDOuterArrayIndex,
                TwoDOuterArrayIndex = twoDArrayIndex,
                DrawElementCallback3D = drawElementCallback,
            };

            int requestedDeleteIndex;
            ArrayTypeDef<T> definition = list;
            ArrayModificationResult arrayModificationResult = ResolveDrawArrayElements(ref definition, drawArrayParams, drawElemParam, allowUndoOption, out requestedDeleteIndex);

            if (arrayModificationResult == ArrayModificationResult.AddedNewElement)
            {
                ResizeReferenceArray(ref list, list.Count + 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.RemovedLastElement)
            {
                ResizeReferenceArray(ref list, list.Count - 1);
            }
            else if (arrayModificationResult == ArrayModificationResult.DeletedIndex && requestedDeleteIndex != -1)
            {
                list.RemoveAt(requestedDeleteIndex);
            }

            return arrayModificationResult;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Resolve Draw Array Elements
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Renders the contents of either an Array or a List</summary>
        /// <typeparam name="T">Type of Array/List</typeparam>
        /// <param name="arrayDef">This acts as a buffer between a List and Array. Essentially converting calls between the two so it doesn't matter which type it is.</param>
        /// <param name="drawArrayParams">The parameters for this draw call.</param>
        /// <param name="allowUndoOption">If True, any modifications made to the List/Array itself will be undo-able. Any specific modifications made in the DrawElement callback must be done in there instead.</param>
        /// <param name="requestedDeleteIndex">Returns an index if an index in the array was marked for deletion by user. Returns -1 if otherwise.</param>
        private ArrayModificationResult ResolveDrawArrayElements<T>(ref ArrayTypeDef<T> arrayDef, DrawArrayParameters<T> drawArrayParams, ArrayCallbackParameters drawCallbackParams, bool allowUndoOption, out int requestedDeleteIndex)
        {
            ArrayModificationResult arrayModificationResult = ArrayModificationResult.NoChange;

            int length = arrayDef.Length;
            int finalIndex = length - 1;
            requestedDeleteIndex = -1;

            if (length > 1)
            {
                List<ButtonsInfo> sortButtons = GetDrawArrayParamsButtonList(arrayDef, drawArrayParams);
                if (sortButtons.Count > 0)
                {
                    AddSpaces(2);
                    DrawMultipleButtonsOneLine(sortButtons, 5.0f, allowUndoOption: allowUndoOption);
                }
            }

            // Reorderable Array ~~~~~~~~~~~~~~~~~~~~~~~~~~~
            if (drawArrayParams.CanReorderArray && length > 1)
            {
                using (new EditorIndentation(4))
                {
                    for (int oneDArrayIndex = 0; oneDArrayIndex < length; ++oneDArrayIndex)
                    {
                        if (drawArrayParams.SpacesToAddBetweenElements > 0 && oneDArrayIndex > 0)
                        {
                            AddSpaces(drawArrayParams.SpacesToAddBetweenElements);
                        }

                        // Must be done before drawing array elements. This gets us the mid-point between all the drawn elements in the array to insert
                        if (drawArrayParams.SpacesToAddBetweenElements > 0)
                        {
                            if (oneDArrayIndex == 0)
                            {
                                EditorGUILayout.Space();
                            }
                            Rect previousElementRect = GUILayoutUtility.GetLastRect();
                            Rect drawRect = new Rect(previousElementRect.x, previousElementRect.y, (int)EditorWindowContextWidth, 3);
                            GUI.Box(drawRect, string.Empty);
                        }

                        Rect previousElementPos = GetScaledRect();
                        bool hadGUIChangeEarlier = GUI.changed;

                        if (drawCallbackParams.Is3DArray)
                        {
                            drawCallbackParams.DrawElementCallback3D(drawCallbackParams.ThreeDOuterArrayIndex, drawCallbackParams.TwoDOuterArrayIndex, oneDArrayIndex);
                        }
                        else if (drawCallbackParams.Is2DArray)
                        {
                            drawCallbackParams.DrawElementCallback2D(drawCallbackParams.TwoDOuterArrayIndex, oneDArrayIndex);
                        }
                        else
                        {
                            drawCallbackParams.DrawElementCallback1D(oneDArrayIndex);
                        }

                        if (GUI.changed && hadGUIChangeEarlier == false)
                        {
                            arrayModificationResult = ArrayModificationResult.ModifiedValue;
                        }

                        // Draw Reorderable Elements
                        GUIStyle centeredTextStyle = new GUIStyle(EditorStyles.helpBox)
                        {
                            alignment = TextAnchor.MiddleCenter
                        };

                        Rect pos = GUILayoutUtility.GetLastRect();
                        float indentationOffset = Mathf.Max(EditorGUI.indentLevel * PixelsPerIndent, 1.0f);

                        int linesCount = (int)((pos.y - previousElementPos.y) / EditorGUIUtility.singleLineHeight);
                        Rect deleteElemButtonPos;
                        Rect moveUpButtonPos;
                        Rect moveDownButtonPos;
                        if (linesCount > 1)
                        {
                            const int numButtonsBottomRow = 2;
                            const float spacePadding = 2.5f;   // Space between buttons
                            const float desiredBW = 20.0f;     // Button Width

                            float remainingDrawSpace = indentationOffset - (spacePadding * numButtonsBottomRow);
                            float usableBW = Mathf.Min(desiredBW, remainingDrawSpace / numButtonsBottomRow);
                            float usedSpace = remainingDrawSpace - (usableBW * numButtonsBottomRow);
                            float percentageOfSpaceUsed = usedSpace / indentationOffset;
                            float startDrawXPos = pos.x + (indentationOffset * percentageOfSpaceUsed);

                            // Centering Button (An array element can contain many displayed items. We want to center it between all elements).
                            float centerYPos = previousElementPos.y + ((pos.y - previousElementPos.y) * 0.5f);
                            float yPos = centerYPos - (EditorGUIUtility.singleLineHeight * 0.5f);
                            deleteElemButtonPos = new Rect(startDrawXPos + (desiredBW * 0.5f) + (spacePadding * 0.5f), yPos, desiredBW, EditorGUIUtility.singleLineHeight);
                            yPos += EditorGUIUtility.singleLineHeight * 1.25f;
                            moveUpButtonPos = new Rect(startDrawXPos, yPos, desiredBW, EditorGUIUtility.singleLineHeight);
                            moveDownButtonPos = new Rect(startDrawXPos + desiredBW + spacePadding, yPos, desiredBW, EditorGUIUtility.singleLineHeight);
                        }
                        else
                        {
                            const int numButtons = 3;
                            const float spacePadding = 1.5f;   // Space between buttons
                            const float desiredBW = 20.0f;     // Button Width

                            float remainingDrawSpace = indentationOffset - (spacePadding * numButtons);
                            float usableBW = Mathf.Min(desiredBW, remainingDrawSpace / numButtons);
                            float usedSpace = remainingDrawSpace - (usableBW * numButtons);
                            float percentageOfSpaceUsed = usedSpace / indentationOffset;
                            float startDrawXPos = pos.x + (indentationOffset * percentageOfSpaceUsed);

                            deleteElemButtonPos = new Rect(startDrawXPos, pos.y, usableBW, EditorGUIUtility.singleLineHeight);
                            moveUpButtonPos = new Rect(startDrawXPos + usableBW + spacePadding, pos.y, usableBW, EditorGUIUtility.singleLineHeight);
                            moveDownButtonPos = new Rect(startDrawXPos + ((usableBW + spacePadding) * 2.0f), pos.y, usableBW, EditorGUIUtility.singleLineHeight);
                        }


                        if (GUI.Button(deleteElemButtonPos, new GUIContent("X", "Delete This Element"), centeredTextStyle))
                        {
                            if (allowUndoOption)
                            {
                                Undo.RecordObject(BaseTarget, "Deleted Element");
                            }
                            requestedDeleteIndex = oneDArrayIndex;
                            arrayModificationResult = ArrayModificationResult.DeletedIndex;
                        }

                        if (oneDArrayIndex > 0)
                        {
                            // Cannot move element 0 upwards. Only element 1 onwards.
                            if (GUI.Button(moveUpButtonPos, new GUIContent("↑", "Move Element Up"), centeredTextStyle))
                            {
                                if (allowUndoOption)
                                {
                                    Undo.RecordObject(BaseTarget, "Swapped Element");
                                }

                                (arrayDef[oneDArrayIndex], arrayDef[oneDArrayIndex - 1]) = (arrayDef[oneDArrayIndex - 1], arrayDef[oneDArrayIndex]);
                                drawArrayParams.OnArraySorted?.Invoke();
                                arrayModificationResult = ArrayModificationResult.SwappedItems;
                            }
                        }

                        if (oneDArrayIndex < finalIndex)
                        {
                            // Same deal for downwards. Cannot move final element down.
                            if (GUI.Button(moveDownButtonPos, new GUIContent("↓", "Move Element Down"), centeredTextStyle))
                            {
                                if (allowUndoOption)
                                {
                                    Undo.RecordObject(BaseTarget, "Swapped Element");
                                }

                                (arrayDef[oneDArrayIndex], arrayDef[oneDArrayIndex + 1]) = (arrayDef[oneDArrayIndex + 1], arrayDef[oneDArrayIndex]);
                                drawArrayParams.OnArraySorted?.Invoke();
                                arrayModificationResult = ArrayModificationResult.SwappedItems;
                            }
                        }
                    }
                }
            }

            // Non-Reorderable Array ~~~~~~~~~~~~~~~~~~~~~~~
            else
            {
                using (new EditorIndentation())
                {
                    for (int oneDArrayIndex = 0; oneDArrayIndex < length; ++oneDArrayIndex)
                    {
                        if (drawArrayParams.SpacesToAddBetweenElements > 0 && oneDArrayIndex > 0)
                        {
                            AddSpaces(drawArrayParams.SpacesToAddBetweenElements);
                        }

                        bool hadGUIChangeEarlier = GUI.changed;

                        if (drawCallbackParams.Is3DArray)
                        {
                            drawCallbackParams.DrawElementCallback3D(drawCallbackParams.ThreeDOuterArrayIndex, drawCallbackParams.TwoDOuterArrayIndex, oneDArrayIndex);
                        }
                        else if (drawCallbackParams.Is2DArray)
                        {
                            drawCallbackParams.DrawElementCallback2D(drawCallbackParams.TwoDOuterArrayIndex, oneDArrayIndex);
                        }
                        else
                        {
                            drawCallbackParams.DrawElementCallback1D(oneDArrayIndex);
                        }

                        if (GUI.changed && hadGUIChangeEarlier == false)
                        {
                            arrayModificationResult = ArrayModificationResult.ModifiedValue;
                        }
                    }
                }
            }
            // ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
            if (drawArrayParams.IsArrayResizeable)
            {
                bool canDrawPlusButton = arrayDef.Length < drawArrayParams.MaxSize;
                bool canDrawMinusButton = arrayDef.Length > drawArrayParams.MinSize;

                EditorGUILayout.Space();
                PlusMinusButtons addOrRemoveElementResult = DrawPlusMinusButtons(0.0f, canDrawPlusButton, canDrawMinusButton);
                if (addOrRemoveElementResult == PlusMinusButtons.PlusPressed)
                {
                    if (allowUndoOption)
                    {
                        Undo.RecordObject(BaseTarget, "Added New Element");
                    }

                    arrayModificationResult = ArrayModificationResult.AddedNewElement;
                }
                else if (addOrRemoveElementResult == PlusMinusButtons.MinusPressed)
                {
                    if (allowUndoOption)
                    {
                        Undo.RecordObject(BaseTarget, "Removed Final Element");
                    }

                    arrayModificationResult = ArrayModificationResult.RemovedLastElement;
                }
                EditorGUILayout.Space();
            }

            return arrayModificationResult;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Resize Array - Struct Type
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void ResizeArray<T>(ref T[] arrayField, int newSize, T? defaultValue = null) 
            where T : struct
        {
            if (newSize < 0)
            {
                return;
            }

            T[] newArray = new T[newSize];
            for (int i = 0; i < newSize; ++i)
            {
                if (arrayField.Length > i)
                {
                    OnArrayVariableModification(ref newArray[i], ref arrayField[i]);
                }
                else if (defaultValue.HasValue)
                {
                    newArray[i] = defaultValue.Value;
                }
            }

            Dictionary<object, bool> typeDict;
            bool foldoutState;
            if (_foldoutOwners.TryGetValue(typeof(T), out typeDict) && typeDict.TryGetValue(arrayField, out foldoutState))
            {
                typeDict.Remove(arrayField);
                typeDict.Add(newArray, foldoutState);
            }

            arrayField = newArray;
        }

        public void ResizeArray<T>(ref List<T> listField, int newSize, T? defaultValue = null) 
            where T : struct
        {
            if (newSize < 0)
            {
                return;
            }

            int count = listField.Count;
            for (; count < newSize; ++count)
            {
                if (defaultValue.HasValue)
                {
                    listField.Add(defaultValue.Value);
                }
                else
                {
                    listField.Add(default);
                }
            }

            int overLimit = count - newSize;
            if (overLimit > 0)
            {
                // Too many elements.
                listField.RemoveRange(newSize, overLimit);
            }
        }

        public void ResizeArray<T>(ref ArrayTypeDef<T> arrayDef, int newSize, T? defaultValue = null) 
            where T : struct
        {
            if (arrayDef.StoredArray != null)
            {
                ResizeArray(ref arrayDef.StoredArray, newSize, defaultValue);
            }
            else
            {
                ResizeArray(ref arrayDef.StoredList, newSize, defaultValue);
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Resize Array - Class Type
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void ResizeReferenceArray<T>(ref T[] arrayField, int newSize) 
        where T : class
        {
            if (newSize < 0)
            {
                return;
            }

            T[] newArray = new T[newSize];
            for (int i = 0; i < newSize; ++i)
            {
                if (arrayField.Length > i)
                {
                    OnArrayVariableModification(ref newArray[i], ref arrayField[i]);
                }
                else
                {
                    OnArrayVariableModification(ref newArray[i]);
                }
            }

            Dictionary<object, bool> typeDict;
            bool foldoutState;
            if (_foldoutOwners.TryGetValue(typeof(T), out typeDict) && typeDict.TryGetValue(arrayField, out foldoutState))
            {
                typeDict.Remove(arrayField);
                typeDict.Add(newArray, foldoutState);
            }

            arrayField = newArray;
            OnArrayModification(ref arrayField);
        }

        public void ResizeReferenceArray<T>(ref List<T> listField, int newSize) 
            where T : class
        {
            if (newSize < 0)
            {
                return;
            }

            int count = listField.Count;
            if (CanAutomaticallyAssignNewClassValue(typeof(T)))
            {
                for (; count < newSize; ++count)
                {
                    listField.Add(System.Activator.CreateInstance(typeof(T), true) as T);
                }
            }
            else
            {
                for (; count < newSize; ++count)
                {
                    listField.Add(null);
                }
            }

            int overLimit = count - newSize;
            if (overLimit > 0)
            {
                // Too many elements.
                listField.RemoveRange(newSize, overLimit);
            }
        }

        private void ResizeReferenceArray<T>(ref ArrayTypeDef<T> arrayDef, int newSize)
            where T : class
        {
            if (arrayDef.StoredArray != null)
            {
                ResizeReferenceArray(ref arrayDef.StoredArray, newSize);
            }
            else
            {
                ResizeReferenceArray(ref arrayDef.StoredList, newSize);
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Remove Array Element At Index
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void RemoveArrayElementAt<T>(ref T[] array, int index)
        {
            List<T> list = new List<T>(array);
            list.RemoveAt(index);
            array = list.ToArray();
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Clone Array - Struct Type
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public T[] CloneArray<T>(T[] originalArray) 
            where T : struct
        {
            if (originalArray == null)
            {
                return null;
            }

            int length = originalArray.Length;
            T[] cloneArray = new T[length];
            for (int i = 0; i < length; ++i)
            {
                cloneArray[i] = originalArray[i];
            }
            return cloneArray;
        }

        public List<T> CloneArray<T>(List<T> originalArray) 
            where T : struct
        {
            if (originalArray == null)
            {
                return null;
            }

            T[] clone = CloneArray(originalArray.ToArray());
            return new List<T>(clone);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Clone Array - Class Type
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public T[] CloneReferenceArray<T>(T[] originalArray) 
            where T : class
        {
            if (originalArray == null)
            {
                return null;
            }

            int length = originalArray.Length;
            T[] cloneArray = new T[length];
            if (CanAutomaticallyAssignNewClassValue(typeof(T)))
            {
                PropertyInfo[] properties = typeof(T).GetProperties(REFLECTION_SEARCH_FLAGS);
                FieldInfo[] variables = typeof(T).GetFields(REFLECTION_SEARCH_FLAGS);

                for (int i = 0; i < length; ++i)
                {
                    cloneArray[i] = System.Activator.CreateInstance(typeof(T), true) as T;

                    foreach (PropertyInfo property in properties)
                    {
                        if (property.CanWrite == false)
                        {
                            continue;
                        }

                        object originalValue = property.GetValue(originalArray[i]);
                        property.SetValue(cloneArray[i], originalValue);
                    }


                    foreach (FieldInfo variable in variables)
                    {
                        object originalValue = variable.GetValue(originalArray[i]);
                        variable.SetValue(cloneArray[i], originalValue);
                    }
                }
            }
            else
            {
                for (int i = 0; i < length; ++i)
                {
                    cloneArray[i] = originalArray[i];
                }
            }

            return cloneArray;
        }

        public List<T> CloneReferenceArray<T>(List<T> originalArray)
            where T : class
        {
            if (originalArray == null)
            {
                return null;
            }

            T[] clone = CloneReferenceArray(originalArray.ToArray());
            return new List<T>(clone);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Insert Into Array
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void InsertIntoArray<T>(ref T[] array, int indexToInsert, T newItem)
        {
            int arrayLengthBefore = array.Length;
            T[] newArray = new T[arrayLengthBefore + 1];
            for (int i = 0; i < indexToInsert; ++i)
            {
                newArray[i] = array[i];
            }
            for (int i = indexToInsert; i < arrayLengthBefore; ++i)
            {
                newArray[i + 1] = array[i];
            }

            newArray[indexToInsert] = newItem;
            array = newArray;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Prepend Array
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void PrependArray<T>(ref T[] original, ref T[] prependingArray)
        {
            T[] newArray = prependingArray.Clone() as T[];
            AppendArray(ref newArray, ref original);
            original = newArray;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Append Array
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void AppendArray<T>(ref T[] original, ref T[] appendingArray)
        {
            int originalLength = original?.Length ?? 0;
            int newSize = originalLength + appendingArray.Length;
            T[] newArray = new T[newSize];

            int appendingIndex = 0;
            for (int i = 0; i < newSize; ++i)
            {
                if (originalLength > i)
                {
                    OnArrayVariableModification(ref newArray[i], ref original![i]);
                }
                else
                {
                    newArray[i] = appendingArray[appendingIndex++];
                }
            }

            original = newArray;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Callback Methods: On Array Modification
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void OnArrayVariableModification<T>(ref T destination) 
            where T : class
        {
            // MonoBehaviour is not allow to be instantiated in Code without using the Instantiate Method. This will cause a warning... Hence, the check for 'MonoBehaviour'
            if (CanAutomaticallyAssignNewClassValue(typeof(T)))
            {
                destination = System.Activator.CreateInstance(typeof(T), true) as T;
            }
        }

        // ReSharper disable once RedundantAssignment
        public void OnArrayVariableModification<T>(ref T destination, ref T source)
        {
            destination = source;
        }

        public void OnArrayModification(string whichArray)
        {
        }

        public void OnArrayModification<T>(ref T[] arrayField)
        {
        }


        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Show "Open File Dialogue"
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary>
        /// Opens up the "Open File Dialog" Window on both Mac & PC. Will allow you to select what you want to have selected
        /// </summary>
        /// <param name="filters">Pass in the extensions you wish to allow the user (i.e. you) to be able to select. e.g.: "txt". Pass in with a separating semicolon
        /// for multiple extension types (EG: "ogg;mp3;wav")</param>
        /// <returns>The absolute path to the desired file. Will return an empty string if the user chooses to exit the window without selecting a file</returns>
        public string ShowOpenFileDialogueWindow(string filters)
        {
            string openFileDisplayLabel = EditorUtility.OpenFilePanel("Open File", SharedCustomEditorValues.OpenPathDirectory, filters);
            if (string.IsNullOrEmpty(openFileDisplayLabel) == false)
            {
                SharedCustomEditorValues.OpenPathDirectory = System.IO.Path.GetDirectoryName(openFileDisplayLabel) + '/';
            }
            return openFileDisplayLabel;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Show Dialogue Window
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Show Dialog Window (Message Box) </summary>
        public void ShowDialogueWindow(string title, string message)
        {
            EditorUtility.DisplayDialog(title, message, "OK");
        }
        /// <summary> Show Dialog Window (Message Box) </summary>
        public void ShowMessageBox(string title, string message)
        {
            ShowDialogueWindow(title, message);
        }

        /// <summary> Show Dialog Window (Message Box) </summary>
        public bool ShowYesOrNoDialogueWindow(string title, string message, string ok = "OK", string cancel = "Cancel")
        {
            return EditorUtility.DisplayDialog(title, message, ok, cancel);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Show Confirmation Window
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Show Confirmation Window (Yes/No selectable) </summary>
        public bool ShowConfirmationWindow(string title, string message, string confirm = "Yes", string deny = "No")
        {
            return EditorUtility.DisplayDialog(title, message, confirm, deny);
        }
        /// <summary> Show Confirmation Window (Yes/No selectable) </summary>
        public bool ShowConfirmationMessageBox(string title, string message, string confirm = "Yes", string deny = "No")
        {
            return ShowConfirmationWindow(title, message, confirm, deny);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Option
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Determines the type and automatically draws the option for you. Recommended that you do not use this if you know the type already. </summary>
        /// <typeparam name="T"> Type you wish to render. </typeparam>
        public object DrawOption<T>(string displayLabel, T value, string tooltip = null)
        {
            if (value is int asInt)
            {
                return DrawChangeableIntegerOption(displayLabel, asInt, tooltip: tooltip);
            }

            if (value is float asFloat)
            {
                return DrawChangeableFloatOption(displayLabel, asFloat, tooltip: tooltip);
            }

            if (value is Vector2 asVector2)
            {
                return DrawVectorField(displayLabel, asVector2, tooltip: tooltip);
            }

            if (value is Vector2Int asVector2Int)
            {
                return DrawVectorField(displayLabel, asVector2Int, tooltip: tooltip);
            }

            if (value is Vector3 asVector3)
            {
                return DrawVectorField(displayLabel, asVector3, tooltip: tooltip);
            }

            if (value is Vector3Int asVector3Int)
            {
                return DrawVectorField(displayLabel, asVector3Int, tooltip: tooltip);
            }

            if (value is Vector4 asVector4)
            {
                return DrawVectorField(displayLabel, asVector4, tooltip: tooltip);
            }

            if (value is bool asBool)
            {
                return DrawToggleField(displayLabel, asBool, tooltip: tooltip);
            }

            if (value is string asString)
            {
                return DrawStringField(displayLabel, asString, tooltip: tooltip);
            }

            if (value is System.Enum asEnum)
            {
                return DrawEnumField(displayLabel, asEnum, tooltip: tooltip);
            }

            return DrawObjectOption(displayLabel, value as UnityEngine.Object, tooltip: tooltip);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Get Index Of (Array)
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public int GetIndexOf<T>(T[] array, T value)
        {
            if (array == null)
            {
                return -1;
            }

            int length = array.Length;
            for (int i = 0; i < length; ++i)
            {
                if (array[i].Equals(value))
                {
                    return i;
                }
            }
            return -1;
        }

        public int GetIndexOf<T>(List<T> list, T value)
        {
            int length = list.Count;
            for (int i = 0; i < length; ++i)
            {
                if (list[i].Equals(value))
                {
                    return i;
                }
            }

            return -1;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Check if we can automatically assign a value on the editor's behalf
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public bool CanAutomaticallyAssignNewClassValue(System.Type t)
        {
            if (t.IsAbstract)
            {
                return false;
            }

            if (t == typeof(UnityEngine.Object) || t.IsSubclassOf(typeof(UnityEngine.Object)))
            {
                return false;
            }

            if (t == typeof(string) || t.IsSubclassOf(typeof(string)))
            {
                return false;
            }

            return true;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Get Zeroed Num Prefix String
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary>
        /// Returns a prefixed string base on the leading zeroes required for a string. 
        /// Example 1, (num = 5, max = 15, baseFormat = 10) => 5 is less than BaseFormat of 10, which is also less than max, so end result is "05". 
        /// Example 2, (num = 5, max = 115, baseFormat = 10) => 5 is less than 10, and 5 is less than 100 (max is 115), so end result is "005".
        /// </summary>
        /// <param name="num">Current Number to add prefixes to</param>
        /// <param name="max">Num to compare to</param>
        /// <param name="baseFormat">format to compare against, 10 by default because humans operate in Base 10. Base 2 is used for Binary, etc.</param>
        /// <param name="replaceChar">Char to insert. Zero by default.</param>
        public string GetZeroedNumPrefixString(int num, int max = 10, int baseFormat = 10, char replaceChar = '0')
        {
            string result = string.Empty;
            for (int currentBaseOfMax = baseFormat; currentBaseOfMax <= max; currentBaseOfMax *= baseFormat)
            {
                if (num < currentBaseOfMax)
                {
                    result += replaceChar;
                }
            }

            result += num.ToString();
            return result;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Get Assets
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Gets first Asset of <T> Type. Returns null if Asset Type cannot be found. </summary>
        public T GetAssetOfType<T>()
         where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).FullName);
            if (guids.Length == 0)
            {
                return null;
            }

            string pathToAsset = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<T>(pathToAsset);
        }

        /// <summary> Gets first Asset of <T> Type. Returns null if Asset Type cannot be found. </summary>
        /// <param name="assetName"> Name of Asset to look for. </param>
        /// <returns></returns>
        public T GetAssetOfType<T>(string assetName) 
            where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets(assetName);
            if (guids.Length == 0)
            {
                return null;
            }

            foreach (string guid in guids)
            {
                string pathToAsset = AssetDatabase.GUIDToAssetPath(guid);
                T asset = AssetDatabase.LoadAssetAtPath<T>(pathToAsset);
                if (asset != null)
                {
                    return asset;
                }
            }

            return null;
        }

        /// <summary> Gets all Assets of <T> Type. Returns null if Asset Type cannot be found. </summary>
        public T[] GetAssetsOfType<T>() 
            where T : UnityEngine.Object
        {
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(T).FullName);
            if (guids.Length == 0)
            {
                return null;
            }

            T[] assets = new T[guids.Length];
            for (int i = 0; i < guids.Length; ++i)
            {
                string pathToAsset = AssetDatabase.GUIDToAssetPath(guids[i]);
                assets[i] = AssetDatabase.LoadAssetAtPath<T>(pathToAsset);
            }

            return assets;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Get Dropdown Choices With Used Elements Removed
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public DropdownChoicesData GetDropdownChoicesWithUsedElementsRemoved(DropdownChoicesParameters parameters)
        {
            if (parameters.OwningObject == null)
            {
                throw new System.Exception("Owning Object for Dropdown Parameters has not been assigned a valid value.");
            }

            DropdownChoicesData[] calculatedChoicesData;
            if (parameters.ForceRefresh == false)
            {
                if (_cachedDropdownChoicesWithUsedElementsRemoved.TryGetValue(parameters.OwningObject, out calculatedChoicesData))
                {
                    // Else: Dirty Data. Needs to be calculated again.
                    if (calculatedChoicesData != null && parameters.ArrayIndex < calculatedChoicesData.Length)
                    {
                        return calculatedChoicesData[parameters.ArrayIndex];
                    }
                }
            }

            string[] allDropdownOptions = parameters.AllDropdownOptionsGetter.Invoke().ToArray();
            List<string> allUsedOptions = parameters.AllUsedDropdownOptionsGetter.Invoke();

            int usedOptionsCount = allUsedOptions.Count;
            calculatedChoicesData = new DropdownChoicesData[usedOptionsCount];

            for (int currentIndex = 0; currentIndex < usedOptionsCount; ++currentIndex)
            {
                List<string> remainingElements = new List<string>(allDropdownOptions);
                remainingElements.RemoveAll(optionDisplay =>
                {
                    if (string.IsNullOrEmpty(optionDisplay))
                    {
                        // Don't Show Empty Elements Either
                        return true;
                    }
                    if (string.Equals(optionDisplay, allUsedOptions[currentIndex]))
                    {
                        // Don't Remove Currently Selected Option. Even if another element also has this option selected.
                        return false;
                    }

                    for (int otherInstanceId = 0; otherInstanceId < usedOptionsCount; ++otherInstanceId)
                    {
                        if (currentIndex == otherInstanceId)
                        {
                            // Don't Check Self
                            continue;
                        }

                        if (string.Equals(optionDisplay, allUsedOptions[otherInstanceId]))
                        {
                            // Other Element is using this. Remove ability to select.
                            return true;
                        }
                    }
                    return false;
                });

                List<string> elementsUsedByOthers = new List<string>(allDropdownOptions);
                elementsUsedByOthers.RemoveAll(option => remainingElements.Contains(option) == false);

                int selectedIndexOfRemainingSelection = remainingElements.IndexOf(allUsedOptions[currentIndex]);

                int[] remainingChoicesIndexToAllChoicesIndex;
                int selectableElementsCount = remainingElements.Count;
                if (selectableElementsCount == 0)
                {
                    // No Elements Selectable
                    remainingElements.Add("N\\A (No Selectable Data)");
                    remainingChoicesIndexToAllChoicesIndex = null;
                }
                else
                {
                    // Pairing up Remaining Elements List with their respective locations in the "All Options" List.
                    remainingChoicesIndexToAllChoicesIndex = new int[selectableElementsCount];
                    for (int selectableElementIndex = 0; selectableElementIndex < selectableElementsCount; ++selectableElementIndex)
                    {
                        remainingChoicesIndexToAllChoicesIndex[selectableElementIndex] = GetIndexOf(allDropdownOptions, remainingElements[selectableElementIndex]);
                    }
                }

                calculatedChoicesData[currentIndex] = new DropdownChoicesData()
                {
                    AllChoices = allDropdownOptions,
                    ChoicesUsedByOtherElements = elementsUsedByOthers.ToArray(),
                    CurrentSelectionIndex = selectedIndexOfRemainingSelection,
                    RemainingSelectableChoices = remainingElements.ToArray(),
                    RemainingChoicesIndexToAllChoicesIndex = remainingChoicesIndexToAllChoicesIndex,
                };
            }

            _cachedDropdownChoicesWithUsedElementsRemoved[parameters.OwningObject] = calculatedChoicesData;
            return calculatedChoicesData[parameters.ArrayIndex];
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Get Draw Array Params Button List
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public T CloneContentsOfItem<T>(T original)
            where T : new()
        {
            T clone = new T();
            List<FieldInfo> allFields = new List<FieldInfo>();
            List<PropertyInfo> allProperties = new List<PropertyInfo>();

            System.Type currentClassType = typeof(T);
            while (currentClassType != typeof(object))
            {
                FieldInfo[] fieldsOfClass = currentClassType!.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo fieldInfo in fieldsOfClass)
                {
                    if (fieldInfo.IsLiteral || fieldInfo.IsInitOnly)
                    {
                        // Consts and Static fields not applicable.
                        continue;
                    }

                    bool canAddField = true;
                    foreach (FieldInfo f in allFields)
                    {
                        if (string.Equals(f.Name, fieldInfo.Name))
                        {
                            // Already has Field (inherited variable)
                            canAddField = false;
                            break;
                        }
                    }

                    if (canAddField)
                    {
                        allFields.Add(fieldInfo);
                    }
                }

                PropertyInfo[] propertiesOfClass = currentClassType.GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                foreach (PropertyInfo propertyInfo in propertiesOfClass)
                {
                    if (propertyInfo.CanWrite == false)
                    {
                        continue;
                    }

                    bool canAddProperty = true;
                    foreach (PropertyInfo p in allProperties)
                    {
                        if (string.Equals(p.Name, propertyInfo.Name))
                        {
                            // Already has Property (inherited variable)
                            canAddProperty = false;
                            break;
                        }
                    }

                    if (canAddProperty)
                    {
                        allProperties.Add(propertyInfo);
                    }
                }

                currentClassType = currentClassType.BaseType;
            }

            // Clone Values
            foreach (FieldInfo fieldInfo in allFields)
            {
                object originalValue = fieldInfo.GetValue(original);
                fieldInfo.SetValue(clone, originalValue);
            }
            foreach (PropertyInfo propertyInfo in allProperties)
            {
                object originalValue = propertyInfo.GetValue(original);
                propertyInfo.SetValue(clone, originalValue);
            }

            return clone;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Is String Equals
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Compares String A against several strings and returns true if equal to any. </summary>
        public bool IsStringEqualsAny(string a, params string[] b)
        {
            foreach (string str in b)
            {
                if (string.Equals(a, str))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary> Compares String A against several strings and returns true if equal to any. </summary>
        public bool IsStringEqualsAny<T>(string a, params T[] b)
        {
            foreach (T t in b)
            {
                if (t != null && string.Equals(a, t.ToString()))
                {
                    return true;
                }
            }

            return false;
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Get Random String
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Returns a string filled with random characters. </summary>
        /// <param name="stringLength"> How many characters do you need </param>
        public string GetRandomString(int stringLength = 10)
        {
            string randString = string.Empty;
            while (randString.Length < stringLength)
            {
                randString += System.IO.Path.GetRandomFileName().Replace(".", string.Empty);
            }

            if (randString.Length == stringLength)
            {
                return randString;
            }
            return randString.Substring(0, stringLength);
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Get Draw Array Params Button List
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private List<ButtonsInfo> GetDrawArrayParamsButtonList<T>(ArrayTypeDef<T> arrayTypeDef, DrawArrayParameters<T> drawArrayParams)
        {
            List<ButtonsInfo> buttonsInfo = new List<ButtonsInfo>();
            if (drawArrayParams.AlphabeticalComparer != null)
            {
                buttonsInfo.Add(new ButtonsInfo()
                {
                    ButtonName = "Sort Alphabetically",
                    Tooltip = "Sort List Alphabetically",
                    OnClicked = () =>
                    {
                        arrayTypeDef.Sort(drawArrayParams.AlphabeticalComparer);
                        drawArrayParams.OnArraySorted?.Invoke();
                    },
                });
            }

            foreach (ArraySorterInfo<T> sorterInfo in new List<ArraySorterInfo<T>>() { drawArrayParams.OtherSorter1, drawArrayParams.OtherSorter2, drawArrayParams.OtherSorter3 })
            {
                if (sorterInfo != null && sorterInfo.SorterFunc != null)
                {
                    buttonsInfo.Add(new ButtonsInfo()
                    {
                        ButtonName = sorterInfo.ButtonLabel,
                        Tooltip = sorterInfo.Tooltip,
                        OnClicked = () =>
                        {
                            arrayTypeDef.Sort(sorterInfo.SorterFunc);
                            drawArrayParams.OnArraySorted?.Invoke();
                        },
                    });
                }
            }

            return buttonsInfo;
        }
    }

    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:Elements should be ordered by access", Justification = "Main Body Structure Readibility")]
    public class CustomEditorHelper<K> : CustomEditorHelper
        where K : UnityEngine.Object
    {
        public K Target { get; private set; } = null;
        
        public CustomEditorHelper(K targetInspectorObject, System.Func<float> getEditorWindowContextWidth)
            : base(targetInspectorObject, getEditorWindowContextWidth)
        {
            Target = targetInspectorObject;
        }
    }
#endif
}
