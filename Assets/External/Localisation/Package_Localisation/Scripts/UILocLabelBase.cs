//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             UI Loc Label Base
//             Author: Christopher Allport
//             Date Created: 5th March, 2020
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Abstract Loc Label Class for Localisation
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#nullable enable

#if UNITY_EDITOR || !FINAL
	#define LOCALISATION_DEBUG_MODE
#endif

using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Serialization;
using Object = UnityEngine.Object;

#if USING_UNITY_LOCALISATION
	using UnityEngine.Localization;
	using UnityEngine.Localization.Settings;
#endif

#if UNITY_EDITOR
	using System.Linq;
	using UnityEditor;
#endif

#if LOCALISATION_DEBUG_MODE
	using System.Text.RegularExpressions;
#endif


namespace Localisation.Localisation
{
	//-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
	//          Class:  UI LOC LABEL BASE
	//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
	/// <summary> Abstract Loc Label Class for Localisation. </summary>
	[SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:Elements should be ordered by access", Justification = "Readibility")]
	public abstract partial class UILocLabelBase : MonoBehaviour
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Definitions
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public struct CulledTextResolutionChart
		{
			/// <summary> Full Text that was requested To Be Displayed. </summary>
			public string FullTextToBeDisplayed;
			/// <summary> Text that ended up in the canvas. </summary>
			public string TextThatWasDisplayed;
			/// <summary> Suggested Text to display on Canvas instead. </summary>
			public string SuggestedTextToDisplayThisRound;
			/// <summary> Suggested Text to cull for this phase and show on the next phase instead. </summary>
			public string SuggestedTextToDisplayNextRound;
		}

		public enum TextUsageTypes
		{
			/// <summary> Using Loc Strings. </summary>
			IsUsingLocString,
			/// <summary> Using Direct Strings. </summary>
			WasSetDirectly,
		}

		public enum TextStates
		{
			/// <summary> Text Animation => Fading In </summary>
			FadingIn,
			/// <summary> Text Animation => Fully Shown </summary>
			AllTextShown,
		}

		public enum FadeInSpeeds
		{
			/// <summary> Normal FadeIn Speed. </summary>
			Normal,
			/// <summary> Slow FadeIn Speed. </summary>
			Slow,
			/// <summary> Fast FadeIn Speed. </summary>
			Fast,
			/// <summary> No FadeIn. </summary>
			Instant
		}

		public enum FadeInTypes
		{
			/// <summary> TypeWriter Text Intro Display. </summary>
			Typewriter,
			/// <summary> FadeIn Text Intro Display. </summary>
			Fade
		}

		public enum CulledTextStates
		{
			/// <summary> No Text has been culled on canvas. </summary>
			HasNoCulledText,
			/// <summary> Text has been culled on Canvas. </summary>
			HasCulledText,
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Constants
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Slow FadeIn Speed. </summary>
		protected const float SlowFadeInSpeed = 10f;
		/// <summary> Normal FadeIn Speed. </summary>
		protected const float NormalFadeInSpeed = 50f;
		/// <summary> Fast FadeIn Speed. </summary>
		protected const float FastFadeInSpeed = 90f;

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Inspector Fields
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Loc Hash ID that is assigned. </summary>
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("assignedLocHashId")]
		protected int _assignedLocHashId = LocIDs.Empty_Loc_String;
		
		/// <summary> Font Header/Paragraph Style. </summary>
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("assignedFontStyle")]
		protected LocalisationTextDisplayStyleTypes _assignedFontStyle = LocalisationTextDisplayStyleTypes.Default;
		
		/// <summary> Assigned Localisation Substrings. </summary>
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("locSubstituteStrings")]
		protected LocSubstring[]? _locSubstituteStrings = Array.Empty<LocSubstring>();
		
		/// <summary> Assigned Text Colours. </summary>
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("highlightedTextColours")]
		protected Color[]? _highlightedTextColours = Array.Empty<Color>();
		
		/// <summary> Text Mesh Effect Settings. </summary>
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("textMeshEffects")]
		protected TextMeshEffectSet? _textMeshEffects = null;
		
		/// <summary> Do we need to fade in Text? </summary>
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("fadeInText")]
		protected bool _fadeInText = false;
		
		/// <summary> Text FadeIn Type. </summary>
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("fadeInType")]
		protected FadeInTypes _fadeInType = FadeInTypes.Typewriter;

#if LOCALISATION_3_COMPATIBILITY_MODE
		/// <summary> Should Text Be localised? If False This component only controls Font, not text. </summary>
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("localiseText")]
		protected bool _localiseText = true;
		
		/// <summary> Force Text to be upper case? This includes localised text. </summary>
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("forceUpperCase")]
		protected bool _forceUpperCase = false;
#endif
		
		
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Non-Inspector Fields
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Current Text State. </summary>
		private TextStates _textState = TextStates.AllTextShown;

		/// <summary> When text is set, this value is set. It is queried when trying to set again.
		/// If it matches then the text will not update as updating existing text is expensive. </summary>
		private string _assignedText = string.Empty;

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Events
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Event fired each time another character fades in if text fade in is enabled. </summary>
		// ReSharper disable once NotAccessedField.Local
		public System.Action? OnCharacterFadeIn = null;

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Properties
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Text Component Text Color. </summary>
		public abstract Color TextColour { get; set; }
		
		/// <summary> Font Header/Paragraph Style. </summary>
		public LocalisationTextDisplayStyleTypes AssignedFontStyle
		{
			get => _assignedFontStyle;
			set => _assignedFontStyle = value;
		}

		/// <summary> Spacing between Text Characters. </summary>
		public virtual float CharacterSpacing { get; set; }
		/// <summary> Spacing between Words. </summary>
		public virtual float WordSpacing { get; set; }
		/// <summary> Spacing between Text Lines. </summary>
		public virtual float LineSpacing { get; set; }
		/// <summary> Spacing between Paragraphs. </summary>
		public virtual float ParagraphSpacing { get; set; }

#if LOCALISATION_3_COMPATIBILITY_MODE
		/// <summary> Should Text Be localised? If False This component only controls Font, not text. </summary>
		public bool LocaliseText { get => _localiseText; set => _localiseText = value; }
#endif
		
		/// <summary> Current Text Usage Type. LocStrings/DirectStrings </summary>
		public TextUsageTypes TextUsageType { get; private set; } = TextUsageTypes.IsUsingLocString;

		/// <summary> FadeIn Progression (0.0 -> 1.0) </summary>
		public float FadeInAmount { get; private set; } = 0.0f;
		/// <summary> Fade In Speed. </summary>
		public FadeInSpeeds FadeInSpeed { get; set; } = FadeInSpeeds.Normal;
		
		/// <summary> FadeIn Type </summary>
		public FadeInTypes FadeInType 
		{ 
			get => _fadeInType; 
			set => _fadeInType = value; 
		}

		/// <summary> Returns the visible character of this Text. This is only valid if the TextState is set to FadingIn. </summary>
		public int VisibleCharacterCount { get; protected set; } = -1;

		/// <summary> Returns whether or not there is culled text on this component. </summary>
		public virtual CulledTextStates CulledTextState
		{
			get => CulledTextStates.HasNoCulledText;
		}

		/// <summary> Current Text Fade In Speed. </summary>
		public float TextFadeInSpeed
		{
			get => GetFadeInSpeed(FadeInSpeed);
		}

		/// <summary> Font Size. </summary>
		public virtual float FontSize
		{
			get => 0;
			// ReSharper disable once ValueParameterNotUsed
			set { }
		}

		/// <summary> Current Text State. </summary>
		public TextStates TextState
		{
			get => _textState; 
			set
			{
				_textState = value;
				if (_textState == TextStates.FadingIn)
				{
					_fadeInText = true;
					FadeInAmount = 0;
					InitialiseTextFadeIn();
				}
				else
				{
					_fadeInText = false;
					ShowFullTextFadeIn();
				}
			}
		}

		/// <summary> Assigned Localisation ID. </summary>
		public LocID AssignedLocID
		{
			get => _assignedLocHashId;
			set
			{
				_assignedLocHashId = value;
				TextUsageType = TextUsageTypes.IsUsingLocString;
				Refresh();
			}
		}

		/// <summary> Is Text Initialised? </summary>
		protected abstract bool IsTextInitialised { get; }
		/// <summary> How many Text Characters are on Display in this Text Component. </summary>
		protected abstract int CharacterCount { get; }

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Abstract Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Initialised Text Component References. </summary>
		protected abstract void Init();
		/// <summary> Sets the Text Content for this Text Component. </summary>
		/// <param name="text"> Text to display. </param>
		/// <param name="shouldRenderRightToLeft"> For Arabic/RTL Languages. Render Text Right-To-Left? </param>
		protected abstract void SetTextContent(string text, bool shouldRenderRightToLeft = false);
		/// <summary> Enables Text Wrapping/Overflow. </summary>
		protected abstract void ForceTextExpansion();

		/// <summary> Applies Font Override for this Loc Component </summary>
		protected abstract void ApplyFontOverride();


		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//        Unity / Virtual Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Unity: Awake. </summary>
		protected void Awake()
		{
			if (IsTextInitialised == false)
			{
				Init();
				ApplyFontOverride();
			}
		}

		/// <summary> Unity: Start. </summary>
		protected void Start()
		{
			Refresh();
		}

		/// <summary> Unity: OnEnable. </summary>
		protected void OnEnable()
		{
			Refresh();
		}

		/// <summary> Unity: LateUpdate. </summary>
		protected virtual void LateUpdate()
		{
			UpdateTextFadeIn(TextFadeInSpeed);
		}

		/// <summary> Initialises Text FadeIn (Alpha to zero, etc.) </summary>
		protected virtual void InitialiseTextFadeIn()
		{
		}

		/// <summary> Updates Text FadeIn. </summary>
		/// <param name="fadeInAmount"> Current Progress (0.0 -> 1.0). </param>
		protected virtual void UpdateTextFadeInAmount(float fadeInAmount)
		{
		}

		/// <summary> Skip the FaeIn effect and show the final fully Faded In text. </summary>
		protected virtual void ShowFullTextFadeIn()
		{
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//        Public Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Refreshes the Displayed Text. </summary>
		public virtual void Refresh()
		{
			if (IsTextInitialised == false)
			{
				Init();
			}
			
#if !USING_UNITY_LOCALISATION
			ApplyFontOverride();
#endif

			if (_fadeInText)
			{
				SetTextContent(_assignedText, false);
				TextState = TextStates.FadingIn;
			}

			if (TextUsageType == TextUsageTypes.WasSetDirectly)
			{
				return;
			}

			if (AssignedLocID == LocIDs.Empty_Loc_String)
			{
				if (IsTextInitialised)
				{
					_textState = TextStates.AllTextShown;
					_assignedText = string.Empty;
					SetTextContent(_assignedText, false);
				}
				return;
			}

			_assignedText = GetSanitisedText();

			//   Just because a Language is Right-To-Left does not mean that the text is Right-To-Left. If we have a purely English string we should still render via Left-To-Right.
			// We simply check the bytes length of the text, if the bytes length has the same number of bytes as we have text characters, then we have a purely ASCII string.
			// Therefore, No need for Right-To-Left. The currently existing languages that render Right-To-Left all use unicode characters that require more than a single byte
			// per character.
			bool shouldRenderRightToLeft = LocManager.IsLanguageRightToLeft() && System.Text.Encoding.UTF8.GetByteCount(_assignedText) != _assignedText.Length;
			SetTextContent(_assignedText, shouldRenderRightToLeft);

			// Reset our fade in amount.
			if (TextState == TextStates.FadingIn)
			{
				FadeInAmount = 0;
				InitialiseTextFadeIn();
			}
		}

		/// <summary> Fades in the text by a single frame </summary>
		public void UpdateTextFadeIn()
		{
			UpdateTextFadeIn(TextFadeInSpeed);
		}

		/// <summary> Update: Text Fade In </summary>
		/// <param name="fadeInSpeed"> Fade In Speed </param>
		public virtual void UpdateTextFadeIn(float fadeInSpeed)
		{
			if (TextState == TextStates.FadingIn && FadeInSpeed != FadeInSpeeds.Instant && FadeInAmount < 1.0f)
			{
				FadeInAmount += fadeInSpeed * Time.deltaTime / CharacterCount;
				UpdateTextFadeInAmount(FadeInAmount);

				if (FadeInAmount >= 1.0f)
				{
					TextState = TextStates.AllTextShown;
				}
			}
		}

		/// <summary> Sets the localised text directly. </summary>
		public void SetTextDirectly(string? text)
		{
			text ??= string.Empty;
			if (string.Equals(_assignedText, text))
            {
                return;
            }

            _assignedText = text;
			TextUsageType = TextUsageTypes.WasSetDirectly;

			Refresh();
			SetTextContent(text, false);

			// Reset our fade in amount.
			if (TextState == TextStates.FadingIn)
			{
				FadeInAmount = 0;
				InitialiseTextFadeIn();
			}
		}

		/// <summary> Feed in a null value to disable substitute strings. </summary>
		public void SetSubstituteStrings(LocSubstring substituteString)
		{
			SetSubstituteStrings(new LocSubstring[] { substituteString });
		}

		/// <summary> Feed in a null value to disable substitute strings. </summary>
		public void SetSubstituteStrings(LocSubstring[] substituteStrings)
		{
			if (DoArraysHaveMatchingContents(_locSubstituteStrings, substituteStrings) == false)
			{
				_locSubstituteStrings = substituteStrings;
				Refresh();
			}
		}

		/// <summary> Feed in a null value to disable colour highlighting. </summary>
		public void SetHighlightedTextColours(Color[] highlightedTextColours)
		{
			if (DoArraysHaveMatchingContents(_highlightedTextColours, highlightedTextColours) == false)
			{
				_highlightedTextColours = highlightedTextColours;
				Refresh();
			}
		}

		/// <summary> Sets the Displayed Text using the currently selected language. </summary>
		/// <param name="locHashId"> Hash Id for the Loc String. Get this from the static LocIDs class.</param>
		public void Set(int locHashId)
		{
			Set(locHashId, substrings: null, highlightedTextColours: null);
		}

		/// <summary> Sets the Displayed Text using the currently selected language. </summary>
		/// <param name="locId"> Loc Id for the Loc String. Get this from the static LocIDs class.</param>
		public void Set(LocID locId)
		{
			Set(locId.locId, substrings: null, highlightedTextColours: null);
		}

		/// <summary> Sets the Displayed Text using the currently selected language and applies substrings. </summary>
		/// <param name="locHashId"> Hash Id for the Loc String. Get this from the static LocIDs class. </param>
		/// <param name="substrings">Replaces [0]. [1], [2], etc. with the strings in this array in order. </param>
		public void Set(int locHashId, params LocSubstring[] substrings)
		{
			Set(locHashId, substrings, null);
		}

		/// <summary> Sets the Displayed Text using the currently selected language and applies substrings. </summary>
		/// <param name="locId"> Loc Id for the Loc String. Get this from the static LocIDs class. </param>
		/// <param name="substrings">Replaces [0]. [1], [2], etc. with the strings in this array in order. </param>
		public void Set(LocID locId, params LocSubstring[] substrings)
		{
			Set(locId.locId, substrings, null);
		}

		/// <summary> Sets the Displayed Text using the currently selected language and applies colours. </summary>
		/// <param name="locHashId"> Hash Id for the Loc String. Get this from the static LocIDs class. </param>
		/// <param name="highlightedTextColours">Replaces {0}, {1}, {2}, etc. with the colours in this array in order. </param>
		public void Set(int locHashId, params Color[] highlightedTextColours)
		{
			Set(locHashId, null, highlightedTextColours);
		}

		/// <summary> Sets the Displayed Text using the currently selected language and applies colours. </summary>
		/// <param name="locId"> Hash Id for the Loc String. Get this from the static LocIDs class. </param>
		/// <param name="highlightedTextColours">Replaces {0}, {1}, {2}, etc. with the colours in this array in order. </param>
		public void Set(LocID locId, params Color[] highlightedTextColours)
		{
			Set(locId.locId, null, highlightedTextColours);
		}

		/// <summary> Sets the Displayed Text using the currently selected language and applies substrings and colours. </summary>
		/// <param name="locHashId"> Hash Id for the Loc String. Get this from the static LocIDs class. </param>
		/// <param name="substrings">Replaces [0]. [1], [2], etc. with the strings in this array in order. </param>
		/// <param name="highlightedTextColours">Replaces {0}, {1}, {2}, etc. with the colours in this array in order. </param>
		public void Set(int locHashId, LocSubstring[]? substrings, Color[]? highlightedTextColours)
		{
			if (_assignedLocHashId == locHashId
			    && DoArraysHaveMatchingContents(_locSubstituteStrings, substrings)
			    && DoArraysHaveMatchingContents(_highlightedTextColours, highlightedTextColours))
			{
				// Same Contents as already known/rendered. No need for a refresh.
				return;
			}

			_assignedLocHashId = locHashId;
			_locSubstituteStrings = substrings;
			_highlightedTextColours = highlightedTextColours;

			TextUsageType = TextUsageTypes.IsUsingLocString;
			_assignedText = string.Empty;
			Refresh();
		}

		/// <summary> Sets the Displayed Text using the currently selected language and applies substrings and colours. </summary>
		/// <param name="locId"> Hash Id for the Loc String. Get this from the static LocIDs class. </param>
		/// <param name="substrings">Replaces [0]. [1], [2], etc. with the strings in this array in order. </param>
		/// <param name="highlightedTextColours">Replaces {0}, {1}, {2}, etc. with the colours in this array in order. </param>
		public void Set(LocID locId, LocSubstring[]? substrings, Color[]? highlightedTextColours)
		{
			Set(locId.locId, substrings, highlightedTextColours);
		}

		/// <summary> Sets the Displayed Text using the currently selected language and applies substrings and colours. </summary>
		/// <param name="locStruct"> Struct containing the Loc Data for LocID, Substrings, and Highlighted Text. </param>
		public void Set(LocStruct locStruct)
		{
			Set((int)locStruct.locId, locStruct.locSubstrings, locStruct.highlightedTextColours);
		}

		/// <summary> Returns the fade in speed for the given speed enum. </summary>
		public float GetFadeInSpeed(FadeInSpeeds speed)
		{
			if (_textMeshEffects == null)
			{
				return speed switch
				{
					FadeInSpeeds.Normal => NormalFadeInSpeed,
					FadeInSpeeds.Fast	=> FastFadeInSpeed,
					FadeInSpeeds.Slow	=> SlowFadeInSpeed,
					_					=> NormalFadeInSpeed
				};
			}
			
			return speed switch
			{
				FadeInSpeeds.Normal => _textMeshEffects.NormalFadeInSpeed,
				FadeInSpeeds.Fast	=> _textMeshEffects.FastFadeInSpeed,
				FadeInSpeeds.Slow	=> _textMeshEffects.SlowFadeInSpeed,
				_					=> _textMeshEffects.NormalFadeInSpeed
			};
		}

		/// <summary> Returns the CulledTextResolutionChart struct that tells you how to best deal with the Culled Text we are experiencing. </summary>
		public virtual CulledTextResolutionChart GetCulledTextResolutionChart()
		{
			CulledTextResolutionChart result = new CulledTextResolutionChart
			{
				FullTextToBeDisplayed = _assignedText,
				TextThatWasDisplayed = _assignedText,
				SuggestedTextToDisplayThisRound = _assignedText,
				SuggestedTextToDisplayNextRound = string.Empty
			};
			return result;
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//        Private Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Gets the localised text with the substrings and colour tags replaced with the corresponding data. </summary>
		private string GetSanitisedText()
		{
			LocStruct locStruct = new LocStruct(AssignedLocID, _locSubstituteStrings, _highlightedTextColours);

			string text;
			LocManager.LocStatus locStatus = LocManager.TryGetTextViaLocHashID(locStruct, out text);

			if (locStatus != LocManager.LocStatus.Success)
			{
				string fullPath = string.Empty;
				for (Transform parentTrans = gameObject.transform; parentTrans != null; parentTrans = parentTrans.parent)
				{
					if (string.IsNullOrEmpty(fullPath) == false)
					{
						fullPath = $"{parentTrans.name}/" + fullPath;
					}
					else
					{
						fullPath = parentTrans.name;
					}
				}

				LocalisationOverrides.Debug.LogWarning($"'{fullPath}' => {text}", this);

#if !LOCALISATION_DEBUG_MODE
				if (Debug.isDebugBuild == false)
				{
					// This is a Live build. Fallback to English rather than show a Debug Message
					locStatus = LocManager.TryGetTextViaLocHashID(locStruct, LocLanguages.English, out text);
					if (locStatus != LocManager.LocStatus.Success)
					{
						// Better to show nothing... <_<
						text = string.Empty;
					}
				}
				else
				{
#endif
					ForceTextExpansion();
#if !LOCALISATION_DEBUG_MODE
				}
#endif
			}

#if LOCALISATION_DEBUG_MODE
			DebugOutputFoundFormattingErrors(ref text);
#endif

			return text;
		}

		public delegate bool EqualsComparisonDelegate<in T>(T arg1, T arg2);
		/// <summary> Checks Two Arrays of the Same Type to see if they have matching Contents. </summary>
		public bool DoArraysHaveMatchingContents<T>(T[]? aArray, T[]? bArray, EqualsComparisonDelegate<T>? equalComparison = null)
		{
			if (ReferenceEquals(aArray, bArray))
            {
                return false;
            }
			if (aArray == null && bArray != null)
            {
                return false;
            }
			if (aArray != null && bArray == null)
            {
                return false;
            }

			int aLength = aArray!.Length;
			if (aLength != bArray!.Length)
            {
                return false;
            }

            for (int i = 0; i < aLength; ++i)
			{
				if (aArray[i] == null)
				{
					if (bArray[i] == null)
                    {
                        continue;
                    }
                    
					return false;
				}
				
				if (bArray[i] == null)
				{
					return false;
				}

				if (equalComparison != null)
				{
					if (equalComparison.Invoke(aArray[i], bArray[i]) == false)
                    {
                        return false;
                    }
                }
				else
				{
					if (aArray[i]!.Equals(bArray[i]) == false)
                    {
                        return false;
                    }
                }
			}

			return true;
		}
	}



	//-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
	//          Class:  UI LOC LABEL BASE DEBUG SECTION
	//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#if LOCALISATION_DEBUG_MODE
	/// <summary> Abstract Loc Label Class for Localisation. </summary>
	public abstract partial class UILocLabelBase
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//        Debug Mode Only Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Scans through the text and outputs any issues found with missing substrings or missing colour tags. </summary>
		private void DebugOutputFoundFormattingErrors(ref string text)
		{
			// Editor Only. Use Regex to determine if any substring/text colours are missing and output the reason into the text itself.
			MatchCollection substringMatchCol = Regex.Matches(text, "(\\[\\d+?\\])");
			for (int matchId = 0; matchId < substringMatchCol.Count; ++matchId)
			{
				for (int captureId = 0; captureId < substringMatchCol[matchId].Captures.Count; ++captureId)
				{
					string substringTag = substringMatchCol[matchId].Captures[captureId].Value;
					string intIdTag = substringTag[1].ToString();
					text = text.Replace(substringTag, $"<color=#FF0000FF><Error: Missing or Empty Substring for Element [{intIdTag}]></color>");
				}
			}

			MatchCollection colourMatchCol = Regex.Matches(text, "(\\{\\d+?\\})");
			if (colourMatchCol.Count > 0)
			{
				for (int matchId = 0; matchId < colourMatchCol.Count; ++matchId)
				{
					for (int captureId = 0; captureId < colourMatchCol[matchId].Captures.Count; ++captureId)
					{
						string colourTag = colourMatchCol[matchId].Captures[captureId].Value;
						string intIdTag = colourTag[1].ToString();
						text = text.Replace(colourTag, $"<color=#FF0000FF><Error: Missing Colour for Element {{{intIdTag}}}></color>");
					}
				}

				// Neg one for array offset, and neg 8 for </colour> length
				const int ColourTagCharCount = 8;
				int previousColourCloseIndex = -1;
				for (int charIndex = text.Length - ColourTagCharCount; charIndex > -1; --charIndex)
				{
					if (text[charIndex] != '<')
                    {
                        continue;
                    }

                    string substring = text.Substring(charIndex, ColourTagCharCount);
					if (string.Equals(substring, "</color>", System.StringComparison.OrdinalIgnoreCase))
					{
						if (previousColourCloseIndex != -1)
						{
							// Removing Duplicate </color> tag
							if (text.Length > previousColourCloseIndex + ColourTagCharCount + 1)
							{
								string startSubstring = text.Substring(0, previousColourCloseIndex);
								int indexOfEndSubStr = previousColourCloseIndex + ColourTagCharCount;
								string endSubstring = text.Substring(indexOfEndSubStr, text.Length - indexOfEndSubStr);
								text = startSubstring + endSubstring;
							}
							else
							{
								text = text.Substring(0, previousColourCloseIndex);
							}
						}

						previousColourCloseIndex = charIndex;
					}
					else if (string.Equals(substring, "<color=#", System.StringComparison.OrdinalIgnoreCase))
					{
						// Found Matching <color> tag
						previousColourCloseIndex = -1;
					}
				}
			}
		}
	}
#endif


	//-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
	//          Class:  UI LOC LABEL BASE CUSTOM EDITOR
	//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#if UNITY_EDITOR
	/// <summary> Abstract Loc Label Class for Localisation. </summary>
	public abstract partial class UILocLabelBase
	{
		/// <summary> Abstract Loc Label Editor Class for Localisation. </summary>
		protected class UILocLabelBaseEditor<K> : CustomEditorBase<K>
			where K : UILocLabelBase
		{
			//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
			//        Custom Editor Fields
			//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
			private readonly DrawArrayParameters<LocSubstring> _drawLocSubstringParams = new DrawArrayParameters<LocSubstring>()
			{
				CanReorderArray = true,
				IsArrayResizeable = true,
			};

			private readonly DrawArrayParameters<Color> _drawHighlightedTextColourParams = new DrawArrayParameters<Color>()
			{
				CanReorderArray = true,
				IsArrayResizeable = true,
			};
			
			private int _viewLanguage = (int)LocLanguages.English;
			private int _previewEffectIndex = 0;
			private bool _previewingEffect = false;
			
#if USING_UNITY_LOCALISATION
			private Locale? _lastKnownLocale = null;
#endif

			//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
			//        Custom Editor Methods
			//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
			protected override void OnInitialised()
			{
				base.OnInitialised();

				if (Application.isPlaying == false)
				{
					// Don't allow text to be updated automatically if looking at a UILocLabelBase component in the Editor during PlayMode. Will cause Loc De-sync.
					UpdateTextOnTarget();
				}
			}

			protected override void OnGUIChanged()
			{
				base.OnGUIChanged();
				UpdateTextOnTarget();
			}

			protected override void EditorUpdate()
			{
#if USING_UNITY_LOCALISATION
				if (_lastKnownLocale != LocalizationSettings.SelectedLocale)
				{
					UpdateTextOnTarget();
				}
#endif
			}

			[InspectorRegion]
			protected void DrawLocalisationDebugButtons()
			{
				if (EditorHelper.DrawButtonField("Pull Down Loc Doc"))
				{
					System.Type? FindClass(string className)
					{
						System.Reflection.Assembly[] assemblies = System.AppDomain.CurrentDomain.GetAssemblies();
						foreach (System.Reflection.Assembly assembly in assemblies)
						{
							System.Type[] allTypes = assembly.GetTypes();
							System.Type result = LocManagerHelpers.Find(allTypes, t => string.Equals(t.Name, className));

							if (result != null)
							{
								return result;
							}
						}

						return null;
					}

					System.Type? locImportHandler = FindClass("LocalisationImportHandler");
					if (locImportHandler != null)
					{
						System.Reflection.MethodInfo? importLocDocMethod = locImportHandler.GetMethod("BeginImportProcessMenuOption", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
						if (importLocDocMethod != null)
						{
							importLocDocMethod.Invoke(null, null);
						}
					}
				}
			}

			/// <summary> Called automatically Inspector refreshes </summary>
			[InspectorRegion("Loc ID & Substrings")]
			public void DrawLocTableLineOptions()
			{
#if LOCALISATION_3_COMPATIBILITY_MODE
				EditorHelper.DrawBoolFieldWithUndoOption("Localise Text", ref Target._localiseText);
				if (Target._localiseText)
                {
                    EditorHelper.DrawLocIdOptionWithUndoOptionOneLine("Loc ID:", ref Target._assignedLocHashId, Target);
                }
#else
				EditorHelper.DrawLocIdOptionWithUndoOptionOneLine("Loc ID:", ref Target._assignedLocHashId, Target);
#endif


#if !USING_UNITY_LOCALISATION
                var fontStyleOptions = LocManager.AllTextDisplayStyles.Select(textDisplayStyle => 
				{
					string option = textDisplayStyle.ToString();
					LocDisplayStyleFontInfo.TextMeshProLocFontInfo appliedFontInfo = LocManager.Instance.GetTextMeshLocDisplayStyleInfo(LocLanguages.English, textDisplayStyle);
					if (appliedFontInfo != null && appliedFontInfo.normalMaterial.editorAssetReference.Asset != null)
                    {
                        option += " - " + appliedFontInfo.normalMaterial.editorAssetReference.Asset;
                    }

                    return option;
				}).ToArray();

				int fontStyleIndex = (int)Target._assignedFontStyle;
				if (EditorHelper.DrawDropdownBoxWithUndoOption("Font Style:", fontStyleOptions, ref fontStyleIndex))
                {
					Target._assignedFontStyle = (LocalisationTextDisplayStyleTypes)fontStyleIndex;
				}

#endif
				
#if LOCALISATION_3_COMPATIBILITY_MODE
				EditorHelper.DrawBoolFieldWithUndoOption("Force Upper Case:", ref Target._forceUpperCase);
#endif
				
				if (EditorHelper.DrawReferenceArrayHeader("Substrings", ref Target._locSubstituteStrings))
				{
					EditorHelper.DrawReferenceArrayElements(ref Target._locSubstituteStrings, _drawLocSubstringParams, DrawLocSubstringElement);
				}

				if (EditorHelper.DrawArrayHeader("Text Colours", ref Target._highlightedTextColours))
				{
					if (EditorHelper.DrawArrayElements(ref Target._highlightedTextColours, _drawHighlightedTextColourParams, DrawHighlightedTextColourElement) == ArrayModificationResult.AddedNewElement)
					{
						// Making sure the new Color is assigned to something already easily visible. By default it would give you a 
						//  transparent colour otherwise, so people would think the system is broken when it is not.
						float r = UnityEngine.Random.Range(0.0f, 1.0f);
						float g = UnityEngine.Random.Range(0.0f, 1.0f);
						float b = UnityEngine.Random.Range(0.0f, 1.0f);
						Target._highlightedTextColours[^1] = new Color(r, g, b, 1.0f);
					}
				}
			}

			[InspectorRegion("Text Effects")]
			public void DrawTextMeshEffectOptions()
			{
				EditorHelper.DrawBoolFieldWithUndoOption("Fade In Text", ref Target._fadeInText);

				if (Target._fadeInText)
                {
                    EditorHelper.DrawEnumFieldWithUndoOption("Fade In Type", ref Target._fadeInType);
                }

                Target._textMeshEffects = EditorHelper.DrawObjectOption("Text Mesh Effects", Target._textMeshEffects);

				if (Application.isPlaying && Target._textMeshEffects != null && Target._textMeshEffects.Effects.Length > 0)
				{
					string[] effectOptions = Target._textMeshEffects.Effects.Select(e => e.name).ToArray();
					_previewEffectIndex = EditorHelper.DrawDropdownBox("Preview Effect", effectOptions, _previewEffectIndex);

					TextMeshEffect previewEffect = Target._textMeshEffects.Effects[_previewEffectIndex];
					if (EditorHelper.DrawButtonField("Preview " + previewEffect.name))
					{
						Target.SetTextDirectly($"{{{previewEffect.Code}}}{Target.GetSanitisedText()}{{/{previewEffect.Code}}}");
						_previewingEffect = true;
					}
				}
			}

			/// <summary> Renders individual Substrings in the Editor </summary>
			private void DrawLocSubstringElement(int index)
			{
				EditorHelper.DrawLabel($"Substring For [{index}] tag:");
				using (new EditorIndentation())
				{
					EditorHelper.DrawLocIdOptionWithUndoOptionOneLine("Loc Id Substring:", ref Target._locSubstituteStrings![index].locHashId, Target._locSubstituteStrings[index], false, $"The substring that will be displayed in place of [{index}], defined as a Loc Id.");

					if (Target._locSubstituteStrings[index].locHashId != LocIDs.Empty_Loc_String)
					{
						using (new EditorIndentation())
						{
							EditorHelper.DrawReadonlyStringField("Substring:", LocManager.GetTextViaLocHashID(Target._locSubstituteStrings[index].locHashId, LocLanguages.English));
						}
					}
					else
					{
						EditorHelper.DrawStringFieldWithUndoOption($"Explicit Substring:", ref Target._locSubstituteStrings[index].userDefinedSubstringText, $"The substring that will be displayed in place of [{index}] in the Loc String, defined by you.");
					}
				}
			}

			protected override void DrawDebugOptions()
			{
				base.DrawDebugOptions();

				EditorGUILayout.LabelField("Selected Language", ((LocLanguages)_viewLanguage).ToString());
				EditorHelper.DrawSliderRangeWithUndoOption("View Language", ref _viewLanguage, 0, LocManager.AllLocLanguages.Length - 1);
				EditorGUILayout.LabelField("Text Preview (EN)", LocManager.GetTextViaLocHashID((int)Target.AssignedLocID, LocLanguages.English));
			}

			/// <summary> Renders individual Highlighted Text Colour elements in the Editor. </summary>
			private void DrawHighlightedTextColourElement(int index)
			{
				EditorHelper.DrawColourFieldWithUndoOption($"Colour For {{{index}}} tag:", ref Target._highlightedTextColours![index], $"The colour that will be displayed in place of {{{index}}} in the Loc String");
			}

			/// <summary> Finds the Text component and updates it to the current value </summary>
			protected void UpdateTextOnTarget()
			{
				if (!_previewingEffect)
				{
#if USING_UNITY_LOCALISATION
					// Ensures our loc tools are keeping tracking with Unity Locales
					_lastKnownLocale = LocalizationSettings.SelectedLocale;
					
					foreach (var pair in LocLanguagesInfo.LocLangToSystemLang)
					{
						LocLanguages locLanguage = pair.Key;
						SystemLanguage systemLanguage = pair.Value;
						LocaleIdentifier localeIdentifier = new(systemLanguage);
						Locale locale = LocalizationSettings.Instance.GetAvailableLocales().GetLocale(localeIdentifier);
						if (locale == _lastKnownLocale)
						{
							_viewLanguage = (int)locLanguage;
							break;
						}
					}
#endif

#if LOCALISATION_ADDRESSABLES && !USING_UNITY_LOCALISATION
					_ =
#endif
					LocManager.SetLanguage((LocLanguages)_viewLanguage);

					foreach (Object t in targets)
					{
						// Only localise if isLocalised has been selected, this is to preserve in progress unlocalised strings in editor mode
						UILocLabelBase uiLocLabel = (UILocLabelBase)t;
						uiLocLabel.Set(uiLocLabel.AssignedLocID, uiLocLabel._locSubstituteStrings, uiLocLabel._highlightedTextColours);
					}
				}
			}
		}
	} // UI LOC LABEL BASE EDITOR
#endif
}
