//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Shake Text Mesh Effect
//             Author: Simon Pederick
//             Date Created: 11th August, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Text mesh effect that shakes the text.
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
	/// Text mesh effect that shakes the text.
	/// </summary>
	[CreateAssetMenu(menuName = "Localisation/Text Mesh Effects/Shake")]
	public class ShakeTextMeshEffect : ColourTextMeshEffect
	{
		[SerializeField] private float shakeFrequency;
		[SerializeField] private float shakeAmplitude;

		[SerializeField] private float baseScale = 1;
		[SerializeField] private float scaleShakeFrequency;
		[SerializeField] private float scaleShakeAmplitude;

		public override void ApplyEffect(TMP_TextInfo _textInfo, int _startCharacterIndex, int _endCharacterIndex)
		{
			base.ApplyEffect(_textInfo, _startCharacterIndex, _endCharacterIndex);

			for (int i = _startCharacterIndex; i < _endCharacterIndex && i < _textInfo.characterInfo.Length; ++i)
			{
				var info = _textInfo.characterInfo[i];
				if (info.isVisible)
				{
					int materialIndex = info.materialReferenceIndex;
					int vertexIndex = info.vertexIndex;
					var vertices = _textInfo.meshInfo[materialIndex].vertices;

					Vector3 offset = new Vector2(((Mathf.PerlinNoise(Time.time * shakeFrequency, i * 100) * 2) - 1) * shakeAmplitude,
												 ((Mathf.PerlinNoise(Time.time * shakeFrequency, 2000 + (i * 100)) * 2) - 1) * shakeAmplitude);
					Vector3 centerPos = (info.vertex_BL.position + info.vertex_BR.position + info.vertex_TL.position + info.vertex_TR.position) / 4;

					float scaleAmount = baseScale;
					scaleAmount += ((Mathf.PerlinNoise(Time.time * scaleShakeFrequency, i * 100) * 2) - 1) * scaleShakeAmplitude;
					Vector3 scale = Vector3.one * scaleAmount;

					vertices[vertexIndex] = ScaleRelativePivot(info.vertex_BL.position, centerPos, scale) + offset;
					vertices[vertexIndex + 1] = ScaleRelativePivot(info.vertex_TL.position, centerPos, scale) + offset;
					vertices[vertexIndex + 2] = ScaleRelativePivot(info.vertex_TR.position, centerPos, scale) + offset;
					vertices[vertexIndex + 3] = ScaleRelativePivot(info.vertex_BR.position, centerPos, scale) + offset;
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