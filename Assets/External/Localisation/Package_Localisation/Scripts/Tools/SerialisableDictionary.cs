//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Serialisable Dictionary
//             Author: Christopher Allport
//             Date Created: 30th June, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Defines a Dictionary class that can be serialised
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
// ReSharper disable Unity.RedundantSerializeFieldAttribute




namespace Localisation.Localisation
{
    public class SerialisableDictionary<T, K> : Dictionary<T, K>, ISerializationCallbackReceiver
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Serialised Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        [SerializeField] private List<T> keys = new List<T>();
        [SerializeField] private List<K> values = new List<K>();
            
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Unity Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public void OnBeforeSerialize()
        {
            keys.Clear();
            values.Clear();
            foreach (KeyValuePair<T, K> pair in this)
            {
                keys.Add(pair.Key);
                values.Add(pair.Value);
            }
        }
            
        public void OnAfterDeserialize()
        {
            this.Clear();
                
            int keysCount = keys.Count;
            if (keysCount != values.Count)
                throw new System.Exception($"There are {keysCount} keys and {values.Count} values after deserialisation. Make sure that both key and value types are serialisable.");

            for (int i = 0; i < keysCount; ++i)
                this.Add(keys[i], values[i]);
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Constructors
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public SerialisableDictionary()
        {
        }

        public SerialisableDictionary(IDictionary<T, K> _other) : base(_other)
        {
        }
    }
}