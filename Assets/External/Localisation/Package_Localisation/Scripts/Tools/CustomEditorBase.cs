//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Custom Editor Base Class
//             Author: Christopher Allport
//             Date Created: November 11, 2014 
//             Last Updated: August 10, 2020
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//		This Script is a base class for all New Custom Editors. It adds some 
//			additional functionality that should be readily available in all
//			Custom Editor scripts.
//
//    A custom editor is used to add additional functionality to the Unity 
//		inspector when dealing with the aforementioned class data.
//
//	  This includes the addition of adding in buttons or calling a method when a 
//		value is changed.
//	  Most importantly, a custom editor is used to make the inspector more 
//		readable and easier to edit.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=

// Turn this on if you are having trouble finding the script in the project files. It'll draw the option as part of the header, same as Unity default behaviour.
#define RENDER_SCRIPT_FILE_HEADER


using System.Diagnostics.CodeAnalysis;
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
// ReSharper disable UnusedMember.Local
#endif



#if UNITY_EDITOR
namespace Localisation.Localisation
{
    [SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:Elements should be ordered by access", Justification = "Readibility")]
    public class CustomEditorBase<K> : UnityEditor.Editor
        where K : UnityEngine.Object        // K for Klass
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	*+ Properties
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public MonoScript OwningScript { get; private set; }
        public CustomEditorHelper<K> EditorHelper { get; private set; }
        public K Target { get; private set; }
        public virtual string ScriptDescription => string.Empty;
        public virtual int DefaultInspectorGameplayFieldsRegionOrder => 1;
        public virtual int DefaultInspectorEditorFieldsRegionOrder => DefaultInspectorGameplayFieldsRegionOrder + 1;

        public long FrameCountInInspector { get; private set; } = 0;        

        public bool IsInitialised
        {
            get { return EditorHelper != null; }
        }

        /// <summary> Fields that will be serialised and used during gameplay. </summary>
        private List<SerialisableFieldData> _serializableGameplayRelatedFields = new List<SerialisableFieldData>();

        /// <summary> Fields that will be serialised and used during Editor Mode Only ( #if UNITY_EDITOR ). </summary>
        private List<SerialisableFieldData> _serializableEditorOnlyFields = new List<SerialisableFieldData>();

        /// <summary> Methods to call automatically to render inspector GUI. </summary>
        private List<InspectorRegionData> _sortedInspectorRegions = new List<InspectorRegionData>();

        private Dictionary<string, SerializedProperty> _arrayElementToSerialisedProperty = new Dictionary<string, SerializedProperty>();

        private bool _isInitialising = false;

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* Unity Methods: On Enabled/Disabled
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected virtual void OnEnable()
        {
            FrameCountInInspector = 0;
            Undo.undoRedoPerformed += OnUndoRedoPerformed;
            EditorApplication.update += EditorUpdate;
        }

        protected virtual void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedoPerformed;
            EditorApplication.update -= EditorUpdate;

            if (Target != null && Target.GetType().IsSubclassOf(typeof(UnityEngine.ScriptableObject)))
            {
                AssetDatabase.SaveAssetIfDirty(Target);
            }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* Unity Method: On Inspector GUI
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public sealed override void OnInspectorGUI()
        {
            if (_isInitialising)
            {
                EditorHelper.DrawLabel("Performing Async Initialisation.", Color.yellow);
                EditorHelper.DrawLabel("Move your mouse over the window to refresh.", Color.yellow);
                return;
            }
            
            if (IsInitialised == false)
            {
                // Initialise being done here instead of OnEnable because it uses GUI functionality.
                //  GUI functions do not work in the constructor or OnEnable. So we do Initialise here instead.
                Initialise();

                if (_isInitialising)
                {
                    return;
                }
            }

            // Update serialized object's representation
            serializedObject.Update();

            EditorHelper.OnGUIBegin();

            // Get it away from the top of the inspector so it's easier to read!
            AddSpaces(2);

            // If we have a script description. Draw it.
            DrawScriptDescription();

            // Draw Script reference
#if RENDER_SCRIPT_FILE_HEADER
            GUI.enabled = false;
            EditorGUILayout.ObjectField("Script:", OwningScript, typeof(MonoScript), false);
            GUI.enabled = true;
            AddSpaces(2);
#endif

            // Draw each Inspector Region that has been assigned.
            foreach (InspectorRegionData inspectorRegion in _sortedInspectorRegions)
            {
                DrawNewInspectorRegion(inspectorRegion);
            }

            // Reserialise Script Instance if things have been changed.
            if (GUI.changed)
            {
                EditorHelper.OnGUIChanged();
                OnGUIChanged();
                SetDirty();

                if (Application.isPlaying == false)
                {
                    UnityEngine.SceneManagement.Scene activeScene;
                    if (EditorHelper.IsActiveSceneObject(Target, out activeScene))
                    {
                        // Scene was edited directly.
                        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(activeScene);
                    }
                }
            }

            // Next Frame.
            EditorHelper.OnGUIEnd();
            serializedObject.ApplyModifiedProperties();
            ++FrameCountInInspector;
        }

        protected virtual void OnDefaultInspectorElementsRendered()
        {
        }

        protected virtual void OnEditorInspectorElementsRendered()
        {
        }
        
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Initialise
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private async void Initialise()
        {
            MethodInfo methodInfo = GetType().GetMethod(nameof(InitialiseAsync), CustomEditorHelper.REFLECTION_SEARCH_FLAGS);
            bool needsToPerformAsyncInit = methodInfo != null && methodInfo.DeclaringType != typeof(CustomEditorBase<K>);
            _arrayElementToSerialisedProperty.Clear();

            _isInitialising = true;
            {
                Target = target as K;

                // Link back to the File that owns this script. Will be rendered as the first field for this custom inspector. Same as Unity's default.
                if (target is MonoBehaviour monoBehaviour)
                {
                    OwningScript = MonoScript.FromMonoBehaviour(monoBehaviour);
                }
                else if (target is ScriptableObject scriptableObject)
                {
                    OwningScript = MonoScript.FromScriptableObject(scriptableObject);
                }

                EditorHelper = new CustomEditorHelper<K>(Target, () => (float)typeof(EditorGUIUtility).GetProperty("contextWidth",
                                                                        BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null, null));

                // Determine which fields are serializable. Display them if 'DrawDefaultInspector' is invoked
                EditorHelper.BuildFieldInfoList(typeof(UnityEngine.Object), FlagVariablesToNotShowAutomatically(), serializedObject, _serializableGameplayRelatedFields, _serializableEditorOnlyFields);

                // Determines which Functions/Methods will be called to display everything used in the Custom Inspector.
                BuildInspectorRegionsList();

                if (needsToPerformAsyncInit)
                {
                    // By default, the Initialise Async Function in this base class doesn't do anything. So don't
                    // call it unless an inherited class decided to populate it with something
                    EditorHelper.DrawProgressBarWindow("Initialising", "Performing Async Initialisation", 0.5f);
                    await InitialiseAsync();
                    EditorHelper.ClearProgressBarWindow();
                }

                OnInitialised();
            }
            _isInitialising = false;

            if (needsToPerformAsyncInit)
            {
                // Repainting when Async Init is finished. Must be done after isInitialising is toggled off.
                Repaint();
            }
        }

        /// <summary> If overriden will sit and wait on the Editor Inspector until this task is completed. </summary>
        protected virtual System.Threading.Tasks.Task InitialiseAsync()
        {
            return System.Threading.Tasks.Task.CompletedTask;
        }

        /// <summary> Is invoked on the first frame when the Editor Inspector is shown before any rendering is performed. </summary>
        protected virtual void OnInitialised()
        {
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* Unity Method: Update
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected virtual void EditorUpdate()
        {
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* Unity Method: Undo/Redo
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected virtual void OnUndoRedoPerformed()
        {
            SetDirty();
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Build Inspector Regions List
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private void BuildInspectorRegionsList()
        {
            _sortedInspectorRegions.Clear();

            // Custom Inspector Regions determined by the overriding class.
            for (System.Type currentClassType = GetType(); currentClassType != typeof(CustomEditorBase<K>); currentClassType = currentClassType.BaseType)
            {
                MethodInfo[] allMethods = currentClassType!.GetMethods(CustomEditorHelper.REFLECTION_SEARCH_FLAGS);
                foreach (MethodInfo methodInfo in allMethods)
                {
                    InspectorRegionAttribute inspectorRegion = methodInfo.GetCustomAttribute<InspectorRegionAttribute>();
                    if (inspectorRegion == null)
                    {
                        continue;
                    }

                    InspectorRegionData existingRegion = _sortedInspectorRegions.Find(m => m.MethodInfo.Name.Equals(methodInfo.Name));
                    if (existingRegion.MethodInfo != null)
                    {
                        // Can grab same inspector region twice if method is marked as a protected function in a base class
                        continue;
                    }

                    InspectorRegionData inspectorRegionData = new InspectorRegionData()
                    {
                        MethodInfo = methodInfo,
                        RegionInfo = inspectorRegion
                    };

                    _sortedInspectorRegions.Add(inspectorRegionData);
                }
            }

            if (_serializableGameplayRelatedFields.Count > 0)
            {
                // Game Related Variables To Draw By Default. Only need to identify this area as "Gameplay Fields" if there are Editor Only fields which will also be shown.
                string displayLabel = _serializableEditorOnlyFields.Count > 0 ? "~Gameplay Fields~." : string.Empty;
                InspectorRegionData defaultInspectorRegionData = new InspectorRegionData()
                {
                    MethodInfo = typeof(CustomEditorBase<K>).GetMethod(nameof(DrawGameplayFields), CustomEditorHelper.REFLECTION_SEARCH_FLAGS),
                    RegionInfo = new InspectorRegionAttribute(displayLabel: displayLabel, drawOrder: DefaultInspectorGameplayFieldsRegionOrder)
                };

                _sortedInspectorRegions.Add(defaultInspectorRegionData);
            }

            if (_serializableEditorOnlyFields.Count > 0)
            {
                // Editor Related Variables To Draw By Default
                InspectorRegionData editorInspectorRegionData = new InspectorRegionData()
                {
                    MethodInfo = typeof(CustomEditorBase<K>).GetMethod(nameof(DrawEditorOnlyFields), CustomEditorHelper.REFLECTION_SEARCH_FLAGS),
                    RegionInfo = new InspectorRegionAttribute(displayLabel: "~Editor Only Fields~ (compiled out during build).", drawOrder: DefaultInspectorEditorFieldsRegionOrder)
                };

                _sortedInspectorRegions.Add(editorInspectorRegionData);
            }

            _sortedInspectorRegions.Sort((InspectorRegionData a, InspectorRegionData b) =>
            {
                if (a.RegionInfo.DrawOrder < b.RegionInfo.DrawOrder)
                {
                    return -1;
                }
                if (a.RegionInfo.DrawOrder > b.RegionInfo.DrawOrder)
                {
                    return 1;
                }
                return 0;
            });

            // Default Inspector Regions that can be overriden by the overriding class. Add these to the Regions List if they have been overriden.
            List<System.Tuple<string, string>> defaultInspectorMethods = new List<System.Tuple<string, string>>()
            {
                new System.Tuple<string, string>("~Audio Options~ (Edit Audio Information)",    nameof(DrawAudioHandlerInfoOptions)),
                new System.Tuple<string, string>("~Debug Options~ (To Help With Testing)",      nameof(DrawDebugOptions)),
            };

            foreach (System.Tuple<string, string> defaultInspectorMethodInfo in defaultInspectorMethods)
            {
                string methodDisplayLabel = defaultInspectorMethodInfo.Item2;
                MethodInfo methodInfo = GetType().GetMethod(methodDisplayLabel, CustomEditorHelper.REFLECTION_SEARCH_FLAGS);
                if (methodInfo == null)
                {
                    continue;
                }
                if (methodInfo.DeclaringType == typeof(CustomEditorBase<K>))
                {
                    // Not Overriden. Therefore, an empty method/function
                    continue;
                }

                InspectorRegionData inspectorRegionData = new InspectorRegionData()
                {
                    MethodInfo = methodInfo,
                    RegionInfo = new InspectorRegionAttribute(defaultInspectorMethodInfo.Item1)
                };

                _sortedInspectorRegions.Add(inspectorRegionData);
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: On GUI Changed / Force Refresh
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected virtual void OnGUIChanged()
        {
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Flag Variables Not To Show Automatically
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Returns Variable names not to show automatically in Editor. This is the same 
        /// as flagging a variable with [HideInInspector], but intended for inherited classes. </summary>
        protected virtual LinkedList<string> FlagVariablesToNotShowAutomatically()
        {
            return new LinkedList<string>();
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Set Dirty
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public new virtual void SetDirty()
        {
            SetDirty(Target);
        }

        public virtual void SetDirty(UnityEngine.Object markedObj)
        {
            if (markedObj != null)
            {
                EditorUtility.SetDirty(markedObj);
            }

            if (markedObj != Target && markedObj.GetType().IsSubclassOf(typeof(UnityEngine.ScriptableObject)))
            {
                AssetDatabase.SaveAssetIfDirty(markedObj);
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Methods: Draw New Inspector Region
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private void DrawNewInspectorRegion(string displayLabel, CallbackMethod inspectorDrawMethod)
        {
            EditorGUILayout.LabelField(displayLabel, EditorStyles.boldLabel);
            using (new EditorIndentation())
            {
                inspectorDrawMethod();
                AddSpaces(3);
            }
        }

        private void DrawNewInspectorRegion(InspectorRegionData newInspectorRegion)
        {
            if (string.IsNullOrEmpty(newInspectorRegion.RegionInfo.FullDisplayLabel) == false)
            {
                if (newInspectorRegion.RegionInfo.LinesCount > 1)
                {
                    GUIStyle s = new GUIStyle(EditorStyles.boldLabel)
                    {
                        alignment = TextAnchor.MiddleCenter
                    };
                    for (int lineIndex = 0; lineIndex < newInspectorRegion.RegionInfo.LinesCount; ++lineIndex)
                    {
                        EditorGUILayout.LabelField(newInspectorRegion.RegionInfo.DisplayLabelPerLine[lineIndex], s);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(newInspectorRegion.RegionInfo.FullDisplayLabel, EditorStyles.boldLabel);
                }

                using (new DrawBackgroundBoxAndIndentEditor())
                {
                    newInspectorRegion.MethodInfo.Invoke(this, null);
                }
            }
            else
            {
                using (new DrawBackgroundBox())
                {
                    newInspectorRegion.MethodInfo.Invoke(this, null);
                }
            }

            AddSpaces(newInspectorRegion.RegionInfo.SpacesAfterRegion);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Script Description
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private void DrawScriptDescription()
        {
            if (string.IsNullOrEmpty(ScriptDescription) == false)
            {
                EditorHelper.DrawDescriptionBox(ScriptDescription);
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Gameplay Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private void DrawGameplayFields()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawSerialisedFieldsObjects(_serializableGameplayRelatedFields);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            OnDefaultInspectorElementsRendered();
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Editor Only Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private void DrawEditorOnlyFields()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawSerialisedFieldsObjects(_serializableEditorOnlyFields);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }

            OnEditorInspectorElementsRendered();
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Serialised Fields Objects
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private void DrawSerialisedFieldsObjects(List<SerialisableFieldData> serialisedFields)
        {
            int serialisedFieldsCount = serialisedFields.Count;
            if (serialisedFieldsCount == 0)
            {
                return;
            }

            for (int i = 0; i < serialisedFieldsCount; ++i)
            {
                if (serialisedFields[i].SpecialisationType == SpecialisedEditorVariable.Boolean)
                {
                    // Instead of drawing a checkbox. Draw a dropdown box with "TRUE"/"FALSE" instead. I think it looks cleaner.
                    bool currentVal = (bool)serialisedFields[i].FieldInfo.GetValue(Target);
                    bool selectedResult = EditorHelper.DrawToggleField(serialisedFields[i].Label.text, currentVal, serialisedFields[i].Label.tooltip, true);
                    if (selectedResult != currentVal)
                    {
                        serialisedFields[i].FieldInfo.SetValue(Target, selectedResult);
                    }
                }
                else if (serialisedFields[i].SpecialisationType == SpecialisedEditorVariable.Addressable)
                {
#if LOCALISATION_ADDRESSABLES
                    string currentVal = (string)serialisedFields[i].FieldInfo.GetValue(Target);
                    AddressableAttribute addressableAttr = (AddressableAttribute)serialisedFields[i].SpecialisationAttr;
                    string selectedResult = EditorHelper.DrawAddressablesDropdownOptions(
                        _displayLabel: serialisedFields[i].Label.text,
                        _type: addressableAttr.TypeToSeek,
                        _addressablesGroupName: addressableAttr.AddressableGroup, 
                        _currentKey: currentVal, 
                        _tooltip: serialisedFields[i].Label.tooltip);
                    if (selectedResult != null && selectedResult.Equals(currentVal) == false)
                    {
                        serialisedFields[i].FieldInfo.SetValue(Target, selectedResult);
                    }
#else
                    EditorGUILayout.PropertyField(serialisedFields[i].SerialisedProperty, serialisedFields[i].Label, true);
#endif
                }
                else if (serialisedFields[i].SpecialisationType == SpecialisedEditorVariable.EnumDropdown)
                {
                    string currentVal = (string)serialisedFields[i].FieldInfo.GetValue(Target);
                    EnumDropdownAttribute enumAttr = (EnumDropdownAttribute)serialisedFields[i].SpecialisationAttr;
                    string selectedResult = EditorHelper.DrawDropdownWithEnumValues(serialisedFields[i].Label.text, enumAttr.EnumType, currentVal, enumAttr.DrawAlphabeticalOrder, serialisedFields[i].Label.tooltip);
                    if (selectedResult != null && selectedResult.Equals(currentVal) == false)
                    {
                        serialisedFields[i].FieldInfo.SetValue(Target, selectedResult);
                    }
                }
                else
                {
                    EditorGUILayout.PropertyField(serialisedFields[i].SerialisedProperty, serialisedFields[i].Label, true);
                }
            }
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Audio Handler Info Options
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected virtual void DrawAudioHandlerInfoOptions()
        {
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Debug Options
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        protected virtual void DrawDebugOptions()
        {
        }



        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Add Spaces
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void AddSpaces(int count = 3)
        {
            EditorHelper.AddSpaces(count);
        }
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Is a Secondary Component?
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary>
        /// Determines whether this component is a secondary component.
        /// Basically if this component is listed with an ID of 2, 4, 6, 8, 10, etc. it 
        /// will be considered a secondary component. It doesn't mean anything; it's just
        /// a way of identifying whether the colours of the font/labels in this component
        /// should change colours
        /// </summary>
        /// <returns>True if secondary component</returns>
        public bool IsSecondaryComponent()
        {
            if (Target is Component secondaryComponent)
            {
                Component[] components = secondaryComponent.gameObject.GetComponents<Component>();
                for (int i = 0; i < components.Length; ++i)
                {
                    if (components[i] == Target)
                    {
                        return i % 2 != 0;
                    }
                }
            }
            return false;
        }
        
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Serialized Object Options
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawSerialisedObjectOptions(string displayLabel, SerializedProperty property, string tooltip = null)
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            GUIContent label = EditorHelper.PerformLabelConversion(displayLabel, tooltip);
            EditorGUILayout.PropertyField(property, label, true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
            }
        }

        public void DrawSerialisedObjectOptions(string displayLabel, string variableDisplayLabel, string tooltip = null)
        {
            SerializedProperty property = serializedObject.FindProperty(variableDisplayLabel);
            DrawSerialisedObjectOptions(displayLabel, property, tooltip);
        }
        
        public void DrawSerialisedObjectOptions(string displayLabel, string arrayVariableName, int arrayIndex, string tooltip = null)
        {
            SerializedProperty property = GetArraySerialisedProperty(arrayVariableName, arrayIndex);
            DrawSerialisedObjectOptions(displayLabel, property, tooltip);
        }

        public SerializedProperty GetArraySerialisedProperty(string arrayVariableName, int arrayIndex)
        {
            string fullInput = $"{arrayVariableName}+[{arrayIndex}]";
            if (_arrayElementToSerialisedProperty.TryGetValue(fullInput, out SerializedProperty property) == false)
            {
                property = serializedObject.FindProperty(arrayVariableName);
                do
                {
                    if (property.Next(true) == false)
                    {
                        return null;
                    }
                }
				while (property.propertyPath.Equals($"{arrayVariableName}.Array.data[{arrayIndex}]") == false);
                
                _arrayElementToSerialisedProperty.Add(fullInput, property);
            }
            
            return property;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* New Method: Draw Array Options
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void DrawSerialisedArrayOptions(string displayLabel, string arrayVariableName, string tooltip = null)
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(arrayVariableName);
            EditorGUI.BeginChangeCheck();
            GUIContent label = EditorHelper.PerformLabelConversion(displayLabel, tooltip);
            EditorGUILayout.PropertyField(property, label, true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                EditorHelper.OnArrayModification(arrayVariableName);
            }
        }
    }
} // namespace

#endif // #if UNITY_EDITOR