//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             LocManager Helpers
//             Author: Christopher Allport
//             Date Created: 8th August, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Collection of functions to help out the LocManager
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Localisation.Localisation
{
	public static class LocManagerHelpers
	{
		/// <summary> Scans through the GameObjects hierarchy to find the component you are looking for, even if the component is not currently active. </summary>
		public static T FindComponentInHierarchyEvenIfInactive<T>() where T : MonoBehaviour
		{
			int scenesCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
			for (int sceneIndex = 0; sceneIndex < scenesCount; ++sceneIndex)
			{
				UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
				GameObject[] rootObjects = scene.GetRootGameObjects();
				foreach (GameObject rootObj in rootObjects)
				{
					T obj = rootObj.GetComponentInChildren<T>(true);
					if (obj != null)
					{
						return obj;
					}
				}
			}

			List<GameObject> ddolRootObjects = GetDontDestroyOnLoadObjects();
			foreach (GameObject rootObj in ddolRootObjects)
			{
				T obj = rootObj.GetComponentInChildren<T>(true);
				if (obj != null)
				{
					return obj;
				}
			}

			return null;
		}

		/// <summary> Scans through the GameObjects hierarchy to find the components you are looking for, even if the components are not currently active. </summary>
		public static List<T> FindComponentsInHierarchyEvenIfInactive<T>() where T : MonoBehaviour
		{
			List<T> components = new List<T>();
			int scenesCount = UnityEngine.SceneManagement.SceneManager.sceneCount;
			for (int sceneIndex = 0; sceneIndex < scenesCount; ++sceneIndex)
			{
				UnityEngine.SceneManagement.Scene scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(sceneIndex);
				GameObject[] rootObjects = scene.GetRootGameObjects();
				foreach (GameObject rootObj in rootObjects)
				{
					T[] objs = rootObj.GetComponentsInChildren<T>(true);
					if (objs != null && objs.Length > 0)
					{
						components.AddRange(objs);
					}
				}
			}

			List<GameObject> ddolRootObjects = GetDontDestroyOnLoadObjects();
			foreach (GameObject rootObj in ddolRootObjects)
			{
				T[] objs = rootObj.GetComponentsInChildren<T>(true);
				if (objs != null && objs.Length > 0)
				{
					components.AddRange(objs);
				}
			}

			return components;
		}

		/// <summary> Returns Object that are located under the 'Don't Destroy On Load' Scene. </summary>
		public static List<GameObject> GetDontDestroyOnLoadObjects()
		{
#if UNITY_2020_1_OR_NEWER
			// Can't be used outside of play mode.
			if (!Application.isPlaying)
				return new List<GameObject>();

			// No idea if this is possible on older versions, so to be safe only >=2020
			GameObject obj = new GameObject();
			Object.DontDestroyOnLoad(obj);
			var scene = obj.scene;
			Object.Destroy(obj);
			return new List<GameObject>(scene.GetRootGameObjects());
#else

			List<GameObject> result = new List<GameObject>();

			List<GameObject> rootGameObjectsExceptDontDestroyOnLoad = new List<GameObject>();
			for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; ++i)
			{
				rootGameObjectsExceptDontDestroyOnLoad.AddRange(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).GetRootGameObjects());
			}

			List<GameObject> rootGameObjects = new List<GameObject>();
			Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
			for (int i = 0; i < allTransforms.Length; ++i)
			{
				Transform root = allTransforms[i].root;
				if (root.hideFlags == HideFlags.None && rootGameObjects.Contains(root.gameObject) == false)
				{
					rootGameObjects.Add(root.gameObject);
				}
			}

			for (int i = 0; i < rootGameObjects.Count; ++i)
			{
				if (rootGameObjectsExceptDontDestroyOnLoad.Contains(rootGameObjects[i]) == false)
					result.Add(rootGameObjects[i]);
			}

			return result;
#endif
		}

		public delegate bool BoolPredicate<in T>(T _checkVal);
		public static T Find<T>(T[] _array, BoolPredicate<T> _predicate)
		{
			int length = _array.Length;
			for (int i = 0; i < length; ++i)
			{
				if (_predicate(_array[i]))
				{
					return _array[i];
				}
			}
			return default(T);
		}
	}
}