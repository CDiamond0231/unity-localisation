//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//              Reference Finder
//              Author: Christopher Allport
//              Date Created: February 19, 2020
//              Last Updated: -----------------
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//		  This Script searches through the active scene to find any references
//      for a target Unity Object. This is then returned as a list denoting
//      which objects are targeting the specified Unity Object and which
//      field group they belong to (Array, Field, etc.).
//
//        This script is used in conjunction with the UILocTextEditor to 
//      automatically find any references to the UILocText component and replace
//      them with the superior UILocLabelTextMesh component instead.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Localisation.Localisation
{
    public static class ReferenceFinder
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* Declarations
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public struct FieldReferencesData
        {
            public Component target;
            public FieldInfo fieldInfo;

            public List<int> indexIdsReferencingTargetScript;

            public bool isACollectionVariableType
            {
                get { return fieldInfo.GetValue(target) is ICollection; }
            }
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //	* Static Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

        /// <summary> Returns a list of Variables Referencing this Component. This includes prefabs or Scene components if applicable.  </summary>
        public static List<FieldReferencesData> GetFieldsReferencingTarget<T>(T _targetToFindReferencesFor) where T : UnityEngine.Component
        {
            List<Component> scriptsReferencingThis = FindObjectsReferencingObject(_targetToFindReferencesFor);
            List<FieldReferencesData> variablesReferencingThis = new List<FieldReferencesData>();
            if (scriptsReferencingThis == null || scriptsReferencingThis.Count == 0)
            {
                return variablesReferencingThis;
            }

            int length = scriptsReferencingThis.Count;
            for (int i = 0; i < length; ++i)
            {
                Component referencer = scriptsReferencingThis[i];
                System.Reflection.FieldInfo[] scriptVariables = referencer.GetType().GetFields();
                int variablesCount = scriptVariables.Length;
                for (int j = 0; j < variablesCount; ++j)
                {
                    System.Reflection.FieldInfo variableInfo = scriptVariables[j];

                    // Collection Variable ///////////////////////////////
                    if (variableInfo.GetValue(referencer) is ICollection asCollection)
                    {
                        List<int> elementsWithReference;
                        if (TestCollectionElementsForReference(_targetToFindReferencesFor, asCollection, out elementsWithReference))
                        {
                            FieldReferencesData referenceInfo = new FieldReferencesData()
                            {
                                target = referencer,
                                fieldInfo = variableInfo,
                                indexIdsReferencingTargetScript = elementsWithReference
                            };

                            variablesReferencingThis.Add(referenceInfo);
                        }
                        continue;
                    }

                    // Non-Collection Variable /////////////////////////////
                    T refVal = variableInfo.GetValue(referencer) as T;
                    if (refVal == null)
                    {
                        // Not a value of the Selected Type
                        continue;
                    }

                    if (refVal != _targetToFindReferencesFor)
                    {
                        // Referencing a value of Type T. But not the same instance as Target.
                        continue;
                    }

                    FieldReferencesData referenceData = new FieldReferencesData()
                    {
                        target = referencer,
                        fieldInfo = variableInfo
                    };

                    variablesReferencingThis.Add(referenceData);
                }
            }

            return variablesReferencingThis;
        }

        /// <summary> Finds any references to this component via Reflection </summary>
        public static List<Component> FindObjectsReferencingObject<T>(T targetToFindReferencesFor) where T : UnityEngine.Object
        {
            List<Component> refComponents = new List<Component>();
            Component[] allComponents = Resources.FindObjectsOfTypeAll(typeof(Component)) as Component[];
            if (allComponents == null)
            {
                return refComponents;
            }

            foreach (Component obj in allComponents)
            {
                FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                foreach (FieldInfo fieldInfo in fields)
                {
                    if (DoesFieldReferenceObject(obj, fieldInfo, targetToFindReferencesFor))
                    {
                        refComponents.Add(obj);
                    }
                }
            }
            return refComponents;
        }

        /// <summary> Checks a field/variable to determine whether or not a reference to a target object is found </summary>
        private static bool DoesFieldReferenceObject<T>(Component _obj, FieldInfo _fieldInfo, T _targetToFindReferencesFor) where T : UnityEngine.Object
        {
            if (_fieldInfo.GetValue(_obj) is ICollection asCollection)
            {
                return TestCollectionElementsForReference(_targetToFindReferencesFor, asCollection, out _);
            }
            if (_fieldInfo.FieldType == _targetToFindReferencesFor.GetType())
            {
                T val = _fieldInfo.GetValue(_obj) as T;
                return val == _targetToFindReferencesFor;
            }
            
            return false;
        }

        /// <summary> Checks Collection for any elements referencing a target. </summary>
        private static bool TestCollectionElementsForReference<T, K>(T _thingToCheckReferencesFor, K _collection, out List<int> _elementsWithReference) where T : UnityEngine.Object where K : IEnumerable
        {
            _elementsWithReference = new List<int>();
            int elemId = 0;
            foreach (object elem in _collection)
            {
                if (elem != null && elem.GetType() == typeof(T))
                {
                    T o = elem as T;
                    if (o == _thingToCheckReferencesFor)
                    {
                        _elementsWithReference.Add(elemId);
                    }
                }
                ++elemId;
            }

            return _elementsWithReference.Count > 0;
        }
    }
}