//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Text Mesh Effect
//             Author: Simon Pederick
//             Date Created: 11th August, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Abstract class for a special text mesh pro effect that can be applied
//      to different sections of the text.
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
	/// Abstract class for a special text mesh pro effect that can be applied
	/// to different sections of the text.
	/// </summary>
	public abstract class TextMeshEffect : ScriptableObject
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Inspector Fields
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		[SerializeField] private string code = string.Empty;

		[SerializeField] private bool bold;
		[SerializeField] private bool italic;

		[SerializeField, HandledInCustomInspector] private bool overrideFadeInSpeed;
		[SerializeField, HandledInCustomInspector] private UILocLabelBase.FadeInSpeeds fadeInSpeed;

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Properties
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~

		/// <summary> The markup code used to specify the section of text to apply this effect to. </summary>
		public string Code => code;

		/// <summary> Whether this text should be bold. </summary>
		public bool Bold => bold;

		/// <summary> Whether this text should be italic. </summary>
		public bool Italic => italic;

		/// <summary> Whether the fade in speed should be overridden for this particular text. </summary>
		public bool OverrideFadeInSpeed => overrideFadeInSpeed;

		/// <summary> If fade in speed is overridden, the new fade in speed used for this particular text. </summary>
		public UILocLabelBase.FadeInSpeeds FadeInSpeeed => fadeInSpeed;

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Sets the Alpha to the value specified and returns the updated Colour. </summary>
		protected static Color32 SetColourAlphaOnly(Color32 _colour, byte _alpha)
		{
			_colour.a = _alpha;
			return _colour;
		}

		/// <summary> Sets the Alpha to the value specified and returns the updated Colour. </summary>
		protected static Color32 SetColourAlphaOnly(Color32 _colour, Color32 _colourToTakeAlphaFrom)
		{
			_colour.a = _colourToTakeAlphaFrom.a;
			return _colour;
		}

		/// <summary>
		/// Apply the effect to the text.
		/// </summary>
		/// <param name="_textInfo">Text info of the text.</param>
		/// <param name="_startCharacterIndex">Index of the first character to apply the effect to.</param>
		/// <param name="_endCharacterIndex">Index of the last character to apply the effect to.</param>
		public abstract void ApplyEffect(TMP_TextInfo _textInfo, int _startCharacterIndex, int _endCharacterIndex);

		/// <summary>
		/// Optional string that will replace the contents of this tag.
		/// (Note: replacement strings currently only supported for single tags, not wrapper tags ({a} ... {/a}))
		/// </summary>
		public virtual string ReplacementString() => string.Empty;
		
		/// <summary>
		/// Optional string that will replace the contents of this tag.
		/// (Note: replacement strings currently only supported for single tags, not wrapper tags ({a} ... {/a}))
		/// Alternative method which passes in a parameter pass from the tag, for example: {tag:MyTagString}
		/// </summary>
		public virtual string ReplacementString(string _parameter) => string.Empty;
	}
}