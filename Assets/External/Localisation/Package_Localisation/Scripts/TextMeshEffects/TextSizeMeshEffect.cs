//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Text Size Mesh Effect
//             Author: Christopher Allport
//             Date Created: 26th April, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Text mesh effect that changes the size of the text.
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
	[CreateAssetMenu(menuName = "Localisation/Text Mesh Effects/TextSize")]
	public class TextSizeMeshEffect : TextMeshEffect
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Inspector Fields
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		[SerializeField, Range(0.0f, 1.0f)] private float textScale = 1.0f;
		[SerializeField] private Color textColour = new Color32(58, 63, 87, 255);

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public override void ApplyEffect(TMP_TextInfo _textInfo, int _startCharacterIndex, int _endCharacterIndex)
		{
			for (int i = _startCharacterIndex; i < _endCharacterIndex; ++i)
			{
				var info = _textInfo.characterInfo[i];
				if (info.isVisible)
				{
					int materialIndex = info.materialReferenceIndex;
					int vertexIndex = info.vertexIndex;

					// Condenses the Text closer together to match the Smaller text size.
					info.vertex_BL.position *= textScale;
					info.vertex_BR.position *= textScale;
					info.vertex_TL.position *= textScale;
					info.vertex_TR.position *= textScale;

					Vector3[] vertices = _textInfo.meshInfo[materialIndex].vertices;
					Vector3 centerPos = (info.vertex_BL.position + info.vertex_BR.position + info.vertex_TL.position + info.vertex_TR.position) / 4;
					Vector3 scale = Vector3.one * textScale;

					vertices[vertexIndex] = ScaleRelativePivot(info.vertex_BL.position, centerPos, scale);
					vertices[vertexIndex + 1] = ScaleRelativePivot(info.vertex_TL.position, centerPos, scale);
					vertices[vertexIndex + 2] = ScaleRelativePivot(info.vertex_TR.position, centerPos, scale);
					vertices[vertexIndex + 3] = ScaleRelativePivot(info.vertex_BR.position, centerPos, scale);

					Color32[] newVertexColors = _textInfo.meshInfo[materialIndex].colors32;
					newVertexColors[vertexIndex] = SetColourAlphaOnly(textColour, newVertexColors[vertexIndex]);
					newVertexColors[vertexIndex + 1] = SetColourAlphaOnly(textColour, newVertexColors[vertexIndex + 1]);
					newVertexColors[vertexIndex + 2] = SetColourAlphaOnly(textColour, newVertexColors[vertexIndex + 2]);
					newVertexColors[vertexIndex + 3] = SetColourAlphaOnly(textColour, newVertexColors[vertexIndex + 3]);
				}
			}
		}

		private static Vector3 ScaleRelativePivot(Vector3 point, Vector3 pivot, Vector3 scaleFactor)
		{
			point -= pivot;
			point = Vector3.Scale(point, scaleFactor);
			return point + pivot;
		}
	}
}