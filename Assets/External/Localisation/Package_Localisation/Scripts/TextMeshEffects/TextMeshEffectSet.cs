//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Text Mesh Effect Set
//             Author: Simon Pederick
//             Date Created: 13th August, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Contains a set of text mesh effects that can be applied to various
//      ui loc labels.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace Localisation.Localisation
{
    /// <summary>
    /// Contains a set of text mesh effects that can be applied to various
    /// ui loc labels.
    /// </summary>
    [CreateAssetMenu(menuName = "Localisation/Text Mesh Effects/Text Mesh Effect Set")]
    public class TextMeshEffectSet : ScriptableObject
    {
        [SerializeField] public float SlowFadeInSpeed = 10f;
        [SerializeField] public float NormalFadeInSpeed = 50f;
        [SerializeField] public float FastFadeInSpeed = 90f;

        [SerializeField] public TextMeshEffect[] Effects = Array.Empty<TextMeshEffect>();
    }
}