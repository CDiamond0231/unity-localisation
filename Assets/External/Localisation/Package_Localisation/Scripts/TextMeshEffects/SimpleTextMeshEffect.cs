//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Simple Text Mesh Effect
//             Author: Simon Pederick
//             Date Created: 19th August, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      A simple text mesh effect that allows for specifying if the text
//      is bold, italic and a custom fade in speed.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#nullable enable
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;


namespace Localisation.Localisation
{
	/// <summary>
	/// A simple text mesh effect that allows for specifying if the text
	/// is bold, italic and a custom fade in speed.
	/// </summary>
	[CreateAssetMenu(menuName = "Localisation/Text Mesh Effects/Simple")]
	public class SimpleTextMeshEffect : TextMeshEffect
	{
		public override void ApplyEffect(TMP_TextInfo _textInfo, int _startCharacterIndex, int _endCharacterIndex)
		{
		}
	}
}