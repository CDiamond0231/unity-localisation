//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             UI Loc Label TextMesh
//             Author: Christopher Allport
//             Date Created: 5th March, 2020
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Loc Label Class for TMPro.TextMeshUGUI
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;
using UnityEngine.UIElements;

#if USING_UNITY_LOCALISATION
	using UnityEngine.Localization;
#endif

#if UNITY_EDITOR
	using UnityEditor;
#endif

namespace Localisation.Localisation
{
	//-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
	//          Class:  UI LOC LABEL TEXT MESH
	//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
	/// <summary> Loc Label Class for TMPro.TextMeshUGUI. </summary>
	[RequireComponent(typeof(TextMeshProUGUI))]
	[SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1202:Elements should be ordered by access", Justification = "Readibility")]
	[SuppressMessage("StyleCop.CSharp.OrderingRules", "SA1201:Elements should appear in the correct order", Justification = "Readibility")]
	public partial class UILocLabelTextMesh : UILocLabelBase
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Structs
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		protected class TextEffect
		{
			public TextMeshEffect? Effect = null;
			public int StartCharacterIndex;
			public int EndCharacterIndex;
		}

		protected enum AutoScrollSelectType
		{
			DontScroll = 0,
			ScrollRightToLeft,
		}

		[System.Serializable]
		protected class AutoScrollSettings
		{
			public AutoScrollSelectType ScrollType = AutoScrollSelectType.DontScroll;
			public float ScrollSpeed = 20f;
			public float ScrollBoundsSizeMultiplier = 1.3f;
		}

		[System.Serializable]
		protected class ScrollSelectDict : SerialisableDictionary<LocLanguages, AutoScrollSettings>
		{
		}

		protected class ActiveScrollInfo
		{
			public GameObject? ScrollParent = null;
			public Vector3 OriginalPos = Vector3.zero;
			public bool WasMaskable = false;
			public bool WasWordWrapping = false;
			public TextOverflowModes PriorOverflowMode = TextOverflowModes.Overflow;
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Inspector Fields
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		[SerializeField, HandledInCustomInspector, FormerlySerializedAs("translatedFontStyle")]
		protected LocManager.FontStyles _translatedFontStyle = LocManager.FontStyles.Stroked;
		
		[SerializeField, HandledInCustomInspector]
		protected ScrollSelectDict _scrollSelectDict = new ScrollSelectDict();

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Non-Inspector Fields
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		private static readonly char[] TextEffectClosingTags = new[] { '>', '}' };
		private static readonly AutoScrollSettings DefaultScrollSettings = new AutoScrollSettings() { ScrollType = AutoScrollSelectType.DontScroll };
		
		private TMP_FontAsset? _originalFont = null;
		private float _originalTextSize = 1f;
		private Material? _originalMaterial = null;
		private List<TextEffect>? _textEffects = null;
		private TextMeshProUGUI? _textMesh = null;
		private bool _isInitialised = false;
		private ActiveScrollInfo _activeScrollInfo = new();

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Properties
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> TextMesh Component Reference. </summary>
		public TextMeshProUGUI TextMesh
        {
			get
            {
				if (_textMesh == null)
                {
                    _textMesh = GetComponent<TextMeshProUGUI>();
                }
				return _textMesh;
			}
        }

		protected AutoScrollSettings CurrentScrollSettings 
		{
			get { return _scrollSelectDict.GetValueOrDefault(LocManager.CurrentLanguage, DefaultScrollSettings); }
		}

		/// <summary> Start Scroll Pos </summary>
		protected float OffscreenScrollSpawnPos 
		{
			get { return TextMesh.textBounds.size.x * CurrentScrollSettings.ScrollBoundsSizeMultiplier; }
		}

		/// <summary> Is Text Initialised? </summary>
		protected override bool IsTextInitialised
		{
			get { return _isInitialised; }
		}

		/// <summary> How many Text Characters are on Display in this Text Component. </summary>
		protected override int CharacterCount
		{
			get { return TextMesh.text.Length; }
		}

		/// <summary> Returns whether or not there is culled text on this component. </summary>
		public override CulledTextStates CulledTextState
		{
			get
			{
				if (TextMesh.firstOverflowCharacterIndex > -1)
                {
                    return CulledTextStates.HasCulledText;
                }
				return CulledTextStates.HasNoCulledText;
			}
		}

		/// <summary> Text Component Text Color. </summary>
		public override Color TextColour
		{
			get { return TextMesh.color; }
			set { TextMesh.color = value; }
		}

		/// <summary> Font Size. </summary>
		public override float FontSize 
		{ 
			get { return TextMesh.fontSize; }
			set { TextMesh.fontSize = value; }
		}

		/// <summary> Spacing between Text Characters. </summary>
		public override float CharacterSpacing
		{
			get { return TextMesh.characterSpacing; }
			set { TextMesh.characterSpacing = value; }
		}

		/// <summary> Spacing between Words. </summary>
		public override float WordSpacing
		{
			get { return TextMesh.wordSpacing; }
			set { TextMesh.wordSpacing = value; }
		}

		/// <summary> Spacing between Text Lines. </summary>
		public override float LineSpacing
		{
			get { return TextMesh.lineSpacing; }
			set { TextMesh.lineSpacing = value; }
		}

		/// <summary> Spacing between Paragraphs. </summary>
		public override float ParagraphSpacing
		{
			get { return TextMesh.paragraphSpacing; }
			set { TextMesh.paragraphSpacing = value; }
		}


		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Called from Awake() </summary>
		protected override void Init()
		{
			if (TextMesh == null)
            {
                throw new UnityException("Could not find Text component attached to '" + gameObject.name + "'");
            }

            _originalFont = TextMesh.font;
			_originalTextSize = TextMesh.fontSize;
			_originalMaterial = TextMesh.fontSharedMaterial;
			_isInitialised = true;
		}

		protected override void SetTextContent(string text, bool shouldRenderRightToLeft = false)
		{
#if LOCALISATION_3_COMPATIBILITY_MODE
			if (_localiseText == false)
			{
				return;
			}
#endif
			
			// Extract special text effect tags if enabled if any are available.
			string sanitisedText = ParseTextEffects(text);

			// Same Text. Don't need any fancy Canvas updates
			if (string.Equals(sanitisedText, text))
			{
#if LOCALISATION_3_COMPATIBILITY_MODE
				if (_forceUpperCase)
				{
					sanitisedText = sanitisedText.ToUpper();
				}
#endif

				if (string.Equals(sanitisedText, TextMesh.text))
				{
					return;
				}
			}
			else
			{
#if LOCALISATION_3_COMPATIBILITY_MODE
				if (_forceUpperCase)
				{
					sanitisedText = sanitisedText.ToUpper();
				}
#endif
			}


			TextMesh.text = sanitisedText;
			TextMesh.isRightToLeftText = shouldRenderRightToLeft;

			TextMesh.ForceMeshUpdate();
			if (TextState == TextStates.AllTextShown)
			{
				ShowFullTextFadeIn();
			}
		}

		/// <summary> Returns the CulledTextResolutionChart struct that tells you how to best deal with the Culled Text we are experiencing. </summary>
		public override CulledTextResolutionChart GetCulledTextResolutionChart()
		{
			CulledTextResolutionChart result = base.GetCulledTextResolutionChart();
			if (CulledTextState == CulledTextStates.HasCulledText)
			{
				int acceptedWordBreakIndex;
				for (acceptedWordBreakIndex = TextMesh.firstOverflowCharacterIndex - 1; acceptedWordBreakIndex > -1; --acceptedWordBreakIndex)
				{
					if (result.FullTextToBeDisplayed[acceptedWordBreakIndex] == ' '
						|| result.FullTextToBeDisplayed[acceptedWordBreakIndex] == '\n')
					{
						break;
					}
				}

				result.TextThatWasDisplayed = result.FullTextToBeDisplayed.Substring(0, TextMesh.firstOverflowCharacterIndex);
				result.SuggestedTextToDisplayThisRound = result.FullTextToBeDisplayed.Substring(0, acceptedWordBreakIndex);
				result.SuggestedTextToDisplayNextRound = result.FullTextToBeDisplayed.Substring(acceptedWordBreakIndex + 1);
			}
			return result;
		}

		protected override void ForceTextExpansion()
		{
			TextMesh.enableWordWrapping = true;
			TextMesh.overflowMode = TextOverflowModes.Overflow;
		}

		protected override void LateUpdate()
		{
			if (_textEffects != null && _textEffects.Count > 0)
			{
				float fadeInSpeed = TextFadeInSpeed;
				int fadeInCharacter = Mathf.RoundToInt(TextMesh.textInfo.characterCount * FadeInAmount);

				foreach (TextEffect? effect in _textEffects)
				{
					if (effect != null && effect.Effect != null)
					{
						effect.Effect.ApplyEffect(TextMesh.textInfo, effect.StartCharacterIndex, effect.EndCharacterIndex);
						if (effect.Effect.OverrideFadeInSpeed && fadeInCharacter >= effect.StartCharacterIndex && fadeInCharacter <= effect.EndCharacterIndex)
                        {
                            fadeInSpeed = GetFadeInSpeed(effect.Effect.FadeInSpeeed);
                        }
                    }
				}

				UpdateTextFadeIn(fadeInSpeed);

				// ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
				TextMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32 | TMP_VertexDataUpdateFlags.Vertices);
			}
			else
			{
				base.LateUpdate();

				// ReSharper disable once BitwiseOperatorOnEnumWithoutFlags
				if (TextState == TextStates.FadingIn)
                {
                    TextMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32 | TMP_VertexDataUpdateFlags.Vertices);
                }
            }
			
			UpdateAutoScroll();
		}

		protected void UpdateAutoScroll()
		{
			if (_activeScrollInfo.ScrollParent != null)
			{
				float scrollSpawnPos = OffscreenScrollSpawnPos;
				Vector3 localPos = transform.localPosition;
				if ((_activeScrollInfo.OriginalPos.x - localPos.x) > scrollSpawnPos)
				{
					transform.localPosition = new Vector3(_activeScrollInfo.OriginalPos.x + scrollSpawnPos, localPos.y, localPos.z);
				}
				else
				{
					localPos.x -= CurrentScrollSettings.ScrollSpeed * Time.deltaTime;
					transform.localPosition = localPos;
				}
			}
		}

		public override void Refresh()
		{
			base.Refresh();

#if UNITY_EDITOR
			// This messes up text during edit mode. So cease and desist.
			if (Application.isPlaying == false)
			{
				return;
			}
#endif

			// Adds a new parent object to the Loc Text which contains a Rect Mask to enable scrolling
			if (CurrentScrollSettings.ScrollType != AutoScrollSelectType.DontScroll)
			{
				if (_activeScrollInfo.ScrollParent == null)
				{
					int siblingIndex = transform.GetSiblingIndex();
					RectTransform rectTrans = gameObject.GetComponent<RectTransform>();
					_activeScrollInfo.ScrollParent = new GameObject("Text - Scroll Mask");
					_activeScrollInfo.ScrollParent.transform.SetParent(transform.parent, false);
					_activeScrollInfo.ScrollParent.transform.position = transform.position;
					_activeScrollInfo.ScrollParent.transform.SetSiblingIndex(siblingIndex);
					_activeScrollInfo.ScrollParent.AddComponent<RectMask2D>();
					RectTransform parentRectTrans = _activeScrollInfo.ScrollParent.GetComponent<RectTransform>();
					if (parentRectTrans == null)
					{
						parentRectTrans = _activeScrollInfo.ScrollParent.AddComponent<RectTransform>();
					}

					parentRectTrans.anchoredPosition = rectTrans.anchoredPosition;
					parentRectTrans.sizeDelta = rectTrans.sizeDelta;
					parentRectTrans.pivot = rectTrans.pivot;
					parentRectTrans.anchorMax = rectTrans.anchorMax;
					parentRectTrans.anchorMin = rectTrans.anchorMin;
					parentRectTrans.offsetMax = rectTrans.offsetMax;
					parentRectTrans.offsetMin = rectTrans.offsetMin;
					parentRectTrans.anchoredPosition3D = rectTrans.anchoredPosition3D;

					LayoutElement layoutElement = gameObject.GetComponent<LayoutElement>();
					if (layoutElement != null)
					{
						LayoutElement parentLayoutElem = _activeScrollInfo.ScrollParent.AddComponent<LayoutElement>();
						parentLayoutElem.ignoreLayout = layoutElement.ignoreLayout;
						parentLayoutElem.layoutPriority = layoutElement.layoutPriority;
						parentLayoutElem.flexibleHeight = layoutElement.flexibleHeight;
						parentLayoutElem.flexibleWidth = layoutElement.flexibleWidth;
						parentLayoutElem.minHeight = layoutElement.minHeight;
						parentLayoutElem.minWidth = layoutElement.minWidth;
						parentLayoutElem.preferredHeight = layoutElement.preferredHeight;
						parentLayoutElem.preferredWidth = layoutElement.preferredWidth;
						parentLayoutElem.enabled = layoutElement.enabled;
					}

					transform.parent = _activeScrollInfo.ScrollParent.transform;
					_activeScrollInfo.OriginalPos = transform.localPosition;

					_activeScrollInfo.WasMaskable = TextMesh.maskable;
					_activeScrollInfo.WasWordWrapping = TextMesh.enableWordWrapping;
					_activeScrollInfo.PriorOverflowMode = TextMesh.overflowMode;

					TextMesh.maskable = true;
					TextMesh.enableWordWrapping = false;
					TextMesh.overflowMode = TextOverflowModes.Overflow;
				}
			}

			// Removes the RectMask Parent object if we are switching back to a language which doesn't require it.
			else if (_activeScrollInfo.ScrollParent != null)
			{
				transform.localPosition = _activeScrollInfo.OriginalPos;
				int siblingIndex = _activeScrollInfo.ScrollParent.transform.GetSiblingIndex();
				transform.parent = _activeScrollInfo.ScrollParent.transform.parent;
				transform.SetSiblingIndex(siblingIndex);
				
				TextMesh.maskable = _activeScrollInfo.WasMaskable;
				TextMesh.enableWordWrapping = _activeScrollInfo.WasWordWrapping;
				TextMesh.overflowMode = _activeScrollInfo.PriorOverflowMode;
				
				Destroy(_activeScrollInfo.ScrollParent);
				_activeScrollInfo.ScrollParent = null;
			}
		}

		protected override void InitialiseTextFadeIn()
        {
			VisibleCharacterCount = -1;
			if (TextMesh != null)
			{
				System.Reflection.FieldInfo? isAwakeBool = TextMesh.GetType().GetField("m_isAwake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
				if (isAwakeBool != null && (bool)isAwakeBool.GetValue(TextMesh))
				{
					System.Reflection.MethodInfo? genMeshMethodInfo = TextMesh.GetType().GetMethod("GenerateTextMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
					if (genMeshMethodInfo != null)
					{
						genMeshMethodInfo.Invoke(TextMesh, null);
					}

					if (TextMesh.textInfo != null && TextMesh.textInfo.characterCount > 0)
					{
						for (int i = 0; i < TextMesh.textInfo.characterCount; ++i)
						{
							SetCharacterAlpha(i, 0.0f);
						}

						TextMesh.SetAllDirty();
						UpdateTextFadeIn(TextFadeInSpeed);
						TextMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
						TextMesh.ForceMeshUpdate(true, false);
						TextMesh.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
					}
				}
				VisibleCharacterCount = 0;
			}
		}

        protected override void UpdateTextFadeInAmount(float fadeInAmount)
		{
			if (TextMesh != null && TextMesh.textInfo != null)
			{
				if (FadeInType == FadeInTypes.Typewriter)
				{
					int visibleCharacters = 0;
					for (int i = 0; i < TextMesh.textInfo.characterCount; ++i)
					{
						float amount = Mathf.InverseLerp(0, TextMesh.textInfo.characterCount - 1, i);
						float alpha;
						if (amount < fadeInAmount)
						{
							++visibleCharacters;
							alpha = 1;
						}
						else
						{
							alpha = 0;
						}
						
						SetCharacterAlpha(i, alpha);
					}

					if (visibleCharacters > VisibleCharacterCount)
					{
						OnCharacterFadeIn?.Invoke();
						VisibleCharacterCount = visibleCharacters;
					}
				}
				else if (FadeInType == FadeInTypes.Fade)
				{
					const float FadeInSpread = 0.05f;

					for (int i = 0; i < TextMesh.textInfo.characterCount; ++i)
					{
						float amount = Mathf.InverseLerp(0, TextMesh.textInfo.characterCount - 1, i);
						float alpha = Mathf.InverseLerp(Mathf.Max(0, amount - FadeInSpread), Mathf.Min(1, amount + FadeInSpread), fadeInAmount);
						SetCharacterAlpha(i, alpha);
					}
				}
				else
                {
                    throw new InvalidOperationException($"Invalid fade in type: {FadeInType}");
                }
            }
		}

		protected override void ShowFullTextFadeIn()
		{
			if (TextMesh != null && TextMesh.textInfo != null)
			{
				for (int i = 0; i < TextMesh.textInfo.characterCount; ++i)
				{
					SetCharacterAlpha(i, 1.0f);
				}

				TextMesh.ForceMeshUpdate();
			}
		}

		private void SetCharacterAlpha(int characterIndex, float alpha)
		{
			if (TextMesh.textInfo.characterInfo[characterIndex].isVisible)
			{
				int materialIndex = TextMesh.textInfo.characterInfo[characterIndex].materialReferenceIndex;
				var newVertexColors = TextMesh.textInfo.meshInfo[materialIndex].colors32;
				int vertexIndex = TextMesh.textInfo.characterInfo[characterIndex].vertexIndex;

				byte byteAlpha = (byte)(alpha * 255);
				newVertexColors[vertexIndex].a = byteAlpha;
				newVertexColors[vertexIndex + 1].a = byteAlpha;
				newVertexColors[vertexIndex + 2].a = byteAlpha;
				newVertexColors[vertexIndex + 3].a = byteAlpha;
			}
		}

		/// <summary> Applies Font Override for this Loc Component </summary>
		protected override void ApplyFontOverride()
		{
			LocDisplayStyleFontInfo.TextMeshProLocFontInfo tmpFontData = LocManager.Instance.GetTextMeshLocDisplayStyleInfoOrDefault(_assignedFontStyle);
			if (tmpFontData != null && tmpFontData.font.RuntimeAsset != null)
			{
				TextMesh.fontSize = (int)(_originalTextSize * tmpFontData.tmpFontSizePercentage);
				TextMesh.font = tmpFontData.font.RuntimeAsset;

				switch (_translatedFontStyle)
				{
					case LocManager.FontStyles.Normal:
						if (tmpFontData.normalMaterial.RuntimeAsset != null)
                        {
                            TextMesh.fontSharedMaterial = tmpFontData.normalMaterial.RuntimeAsset;
                        }
						break;
					
					case LocManager.FontStyles.Stroked:
						if (tmpFontData.strokedMaterial.RuntimeAsset != null)
                        {
                            TextMesh.fontSharedMaterial = tmpFontData.strokedMaterial.RuntimeAsset;
                        }
						break;
					
					case LocManager.FontStyles.Underlined:
						if (tmpFontData.underlinedMaterial.RuntimeAsset != null)
                        {
                            TextMesh.fontSharedMaterial = tmpFontData.underlinedMaterial.RuntimeAsset;
                        }
						break;
				}
			}
			else
			{
				TextMesh.font = _originalFont;
				TextMesh.fontSize = _originalTextSize;
				TextMesh.fontSharedMaterial = _originalMaterial;
			}
		}

		private bool SubStringEquals(string str, string subStr, int offset = 0)
		{
			int endPos = offset + subStr.Length;
			int strLen = str.Length;
			for (int i = offset; i < endPos; i++)
			{
				if (i < 0 || i >= strLen)
				{
					return false;
				}

				char c1 = ToLowerFast(str[i]);
				char c2 = ToLowerFast(subStr[i - offset]);

				if (c1 != c2)
				{
					return false;
				}
			}

			static char ToLowerFast(char c)
			{
				if (c >= 'A' && c <= 'Z')
				{
					return (char)(c + 32);
				}
				
				return c;
			}

			return true;
		}

		/// <summary> Parses all text effect codes in the loc string and replaces them
		/// as well as creating text effect objects foreach substring. </summary>
		private string ParseTextEffects(string text)
		{
			if (_textMeshEffects != null)
			{
				_textEffects?.Clear();

				// Look for every type of effect
				foreach (TextMeshEffect effect in _textMeshEffects.Effects)
				{
					if (effect == null)
					{
						Debug.LogError("Text effect in effects list is null");
						continue;
					}

					_textEffects ??= new List<TextEffect>();

					// Keep looking in case this effect appears more than once.
					bool lookForTags = true;
					while(lookForTags)
					{
						lookForTags = false;

						string openCode = $"{{{effect.Code}}}";
						string openCodeParameterized = $"{{{effect.Code}:";
						string closeCode = $"{{/{effect.Code}}}";
						int depth = 0;
						int openingIndex = -1;
						int closingIndex = -1;
						string parameter = string.Empty;

						// Search through the entire string, accounting for nested tags.
						for (int i = 0; i < text.Length; ++i)
						{
							// Check for an opening tag
							if (SubStringEquals(text, openCode, i))
							{
								if (depth == 0)
								{
									openingIndex = i;
									parameter = string.Empty;
								}

								depth++;
							}
							else if (SubStringEquals(text, openCodeParameterized, i))
							{
								if (depth == 0)
								{
									// Find parameter in tag
									openingIndex = i;
									int endParameterIndex = text.IndexOf('}', i);

									if (endParameterIndex == -1)
                                    {
                                        throw new Exception("Cannot find end tag for parameterized dialogue code!");
                                    }

                                    int start = i + openCodeParameterized.Length;
									parameter = text.Substring(i + openCodeParameterized.Length, endParameterIndex - start);
								}

								++depth;
							}

							// Check for a closing tag
							if (SubStringEquals(text, closeCode, i))
							{
								// Only apply tags if it's at depth 0 to account for nested tags
								--depth;
								if (depth != 0)
                                {
                                    continue;
                                }

                                closingIndex = i;
								lookForTags = true;
										
								// Replace opening tag
								text = text.Remove(openingIndex, openCode.Length);
								closingIndex -= openCode.Length;

								// Replace closing tag
								text = text.Remove(closingIndex, closeCode.Length);

								// Insert bold/ italic new tags if they exist
								if(effect.Bold || effect.Italic)
								{
									string insertStart = $"{(effect.Bold ? "<b>" : string.Empty)}{(effect.Italic ? "<i>" : string.Empty)}";
									string insertEnd = $"{(effect.Bold ? "</b>" : string.Empty)}{(effect.Italic ? "</i>" : string.Empty)}";

									text = text.Insert(openingIndex, insertStart);
									openingIndex += insertStart.Length;
									closingIndex += insertStart.Length;
									text = text.Insert(closingIndex, insertEnd);
								}

								_textEffects.Add(new TextEffect
								{
									Effect = effect,
									StartCharacterIndex = CalculateRealIndex(openingIndex),
									EndCharacterIndex = CalculateRealIndex(closingIndex)
								});

								break;
							}
						}

						// Only found an opening tag, possibly a replacement without closing tag.
						// for example: {n} is the player's first name, it adds the name
						if(openingIndex != -1 && closingIndex == -1)
						{
							// Only get replacement string if it's required.
							string replacementString;
							if (string.IsNullOrEmpty(parameter))
                            {
                                replacementString = effect.ReplacementString();
                            }
                            else
                            {
                                replacementString = effect.ReplacementString(parameter);
                            }

                            // Replacement tags only have an opening tag.
                            if (!string.IsNullOrEmpty(replacementString))
							{
								lookForTags = true;

								// Replace tag
								if (string.IsNullOrEmpty(parameter))
								{
									text = text.Remove(openingIndex, openCode.Length);
								}
								else
								{
									text = text.Remove(openingIndex, openCodeParameterized.Length + parameter.Length + 1);
								}
								
								text = text.Insert(openingIndex, replacementString);
								OffsetTextEffectIndexes(openingIndex, replacementString.Length);
								closingIndex = openingIndex + replacementString.Length;

								// Insert bold/ italic new tags if they exist
								if (effect.Bold || effect.Italic)
								{
									string insertStart = $"{(effect.Bold ? "<b>" : string.Empty)}{(effect.Italic ? "<i>" : string.Empty)}";
									string insertEnd = $"{(effect.Bold ? "</b>" : string.Empty)}{(effect.Italic ? "</i>" : string.Empty)}";

									text = text.Insert(openingIndex, insertStart);
									openingIndex += insertStart.Length;
									closingIndex += insertStart.Length;
									text = text.Insert(closingIndex, insertEnd);
								}

								_textEffects.Add(new TextEffect
								{
									Effect = effect,
									StartCharacterIndex = CalculateRealIndex(openingIndex),
									EndCharacterIndex = CalculateRealIndex(closingIndex)
								});
							}
						}
					}
				}

				// Converts a text character ID into an effect text index that ignores tags.
				int CalculateRealIndex(int index)
				{
					int realUpto = 0;
					for (int i = 0; i < text.Length && i < index; ++i)
					{
						// Opening tag, find closing tag
						if (text[i] == '{' || text[i] == '<')
						{
							int endIndex = text.IndexOfAny(TextEffectClosingTags, i);
							if(endIndex != -1)
							{
								i = endIndex;
								continue;
							}
						}

						// Visible character
						realUpto++;
					}

					return realUpto;
				}
			}

			if (text.Contains("{"))
			{
				int effectCount = 0;
				if (_textMeshEffects != null)
                {
                    effectCount = _textMeshEffects.Effects.Length;
                }

                GameObject go = gameObject;
				Debug.LogWarning($"May not have successfully replaced all text tags in text: {text}, using {effectCount} effects on {go.name}", go);
			}

			return text;
		}

		/// <summary> Handles offsetting the start and end character indexes for
		/// text effects when substrings are added or removed from the main loc string.</summary>
		/// <param name="index">The index a substring was added / removed.</param>
		/// <param name="offsetAmount">The length of the substring added or removed.</param>
		private void OffsetTextEffectIndexes(int index, int offsetAmount)
		{
			if (_textEffects == null)
            {
                return;
            }

            foreach (var textEffect in _textEffects)
			{
				if (textEffect == null)
                {
                    continue;
                }

                if (textEffect.StartCharacterIndex > index)
                {
                    textEffect.StartCharacterIndex += offsetAmount;
                }

                if (textEffect.EndCharacterIndex > index)
                {
                    textEffect.EndCharacterIndex += offsetAmount;
                }
            }
		}
		
		private string RemoveFirstOccurence(string text, string search)
		{
			return ReplaceFirstOccurence(text, search, string.Empty);
		}

		private string ReplaceFirstOccurence(string text, string search, string replacement)
		{
			int pos = text.IndexOf(search, StringComparison.Ordinal);
			if (pos < 0)
			{
				return text;
			}
			return $"{text.Substring(0, pos)}{replacement}{text.Substring(pos + search.Length)}";
		}
	}



	//-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-
	//          Class:  UI LOC LABEL TEXT MESH EDITOR ONLY
	//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#if UNITY_EDITOR
	/// <summary> Loc Label Class for TMPro.TextMeshUGUI. </summary>
	public partial class UILocLabelTextMesh
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Editor Only Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public void EditorAssignDefaultValues(int assignedLocHashId, LocSubstring[] assignedSubstrings, Color[] highlightedTextColours, bool forceUpperCase)
		{
			_assignedLocHashId = assignedLocHashId;
			_locSubstituteStrings = assignedSubstrings;
			_highlightedTextColours = highlightedTextColours;
			
#if LOCALISATION_3_COMPATIBILITY_MODE
			_forceUpperCase = forceUpperCase;
#endif
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Custom Editor
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		[CanEditMultipleObjects]
		[CustomEditor(typeof(UILocLabelTextMesh))]
		protected class UILocLabelTextMeshEditor : UILocLabelBaseEditor<UILocLabelTextMesh>
		{
			private static readonly LocLanguages[] AllLocLanguages = (System.Enum.GetValues(typeof(LocLanguages)) as LocLanguages[])!;
			
			protected override void OnInitialised()
			{
				foreach (LocLanguages language in AllLocLanguages)
				{
					if (Target._scrollSelectDict.ContainsKey(language) == false)
						Target._scrollSelectDict.Add(language, new AutoScrollSettings());
				}

				base.OnInitialised();
			}

#if !USING_UNITY_LOCALISATION
			[InspectorRegion(drawOrder: 10)]
			private void DrawTextMeshOptions()
			{
				EditorHelper.DrawEnumFieldWithUndoOption("Translated font style", ref Target._translatedFontStyle);
			}
#endif

			[InspectorRegion("Auto Scroll Options", drawOrder: 11)]
			protected void DrawAutoScrollOptions()
			{
				const string Description = "The bounds multiplier allows the text more wriggle room to scroll off-screen."
				                           + "\nWe have a couple areas where the active text box bounds does not meet the the edges of the text area (text background contrast sprite),"
				                           + "so this multiplier just tells the text to move further left by 30%, etc."
				                           + "\nThis also has the effect of making the text wait a little longer while offscreen before coming back into focus on the right side which I think feels better, but up to you";
				EditorHelper.DrawDescriptionBox(Description);
				foreach (LocLanguages language in AllLocLanguages)
				{
					using (new DrawBackgroundBoxAndIndentEditor(language.ToString()))
					{
						AutoScrollSettings autoScrollSettings = Target._scrollSelectDict[language];
						EditorHelper.DrawEnumFieldWithUndoOption("Scroll Option:", ref autoScrollSettings.ScrollType);
						if (autoScrollSettings.ScrollType != AutoScrollSelectType.DontScroll)
						{
							EditorHelper.DrawFloatFieldWithUndoOption("Scroll Speed:", ref autoScrollSettings.ScrollSpeed);
							EditorHelper.DrawFloatFieldWithUndoOption("Bounds Size Multiplier:", ref autoScrollSettings.ScrollBoundsSizeMultiplier);
						}
					}
				}
			}
		}
	} // UI LOC LABEL TEXT MESH EDITOR
#endif
}
