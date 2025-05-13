//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Highlight Text Mesh Effect
//             Author: Simon Pederick
//             Date Created: 11th August, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Highlight a piece of text with the given colour.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#nullable enable
using TMPro;
using UnityEngine;

namespace Localisation.Localisation
{
	/// <summary>
	/// Highlight a piece of text with the given colour.
	/// </summary>
	[CreateAssetMenu(menuName="Localisation/Text Mesh Effects/Colour")]
	public class ColourTextMeshEffect : TextMeshEffect
	{
		[SerializeField] private Color colour;

		public override void ApplyEffect(TMP_TextInfo _textInfo, int _startCharacterIndex, int _endCharacterIndex)
		{
			Color32 col = colour;

			for (int i = _startCharacterIndex; i < _endCharacterIndex && i < _textInfo.characterInfo.Length; ++i)
			{
				var info = _textInfo.characterInfo[i];
				if (!info.isVisible) 
					continue;
				
				int materialIndex = info.materialReferenceIndex;
				if (materialIndex >= _textInfo.meshInfo.Length)
					return;
					
				int vertexIndex = info.vertexIndex;
				Color32[] newVertexColors = _textInfo.meshInfo[materialIndex].colors32;
				if (newVertexColors == null || newVertexColors.Length < 4)
					return;

				newVertexColors[vertexIndex] = SetColourAlphaOnly(col, newVertexColors[vertexIndex]);
				newVertexColors[vertexIndex + 1] = SetColourAlphaOnly(col, newVertexColors[vertexIndex + 1]);
				newVertexColors[vertexIndex + 2] = SetColourAlphaOnly(col, newVertexColors[vertexIndex + 2]);
				newVertexColors[vertexIndex + 3] = SetColourAlphaOnly(col, newVertexColors[vertexIndex + 3]);
			}
		}
	}
}