using UnityEngine;
using System.Collections;

namespace Localisation.Localisation
{
    public class LocalisedURLOpener : UILocLabelBase
    {
        #region Inspector variables

        public string fallbackUrlToOpen;

        #endregion    // Inspector variables

        string urlToOpen;

        protected override int CharacterCount => urlToOpen.Length;

		public override Color TextColour
        {
            get { return Color.white; }
            set { }
        }

        /// <summary> Returns whether or not there is culled text on this component. </summary>
		public override CulledTextStates CulledTextState
        {
            get { return CulledTextStates.HasNoCulledText; }
        }

        /// <summary> Opens the URL </summary>
        public void OpenURL()
        {
            Application.OpenURL(urlToOpen);
        }

        protected override bool IsTextInitialised { get { return true; } }

        /// <summary> Called from Awake() </summary>
        protected override void Init()
        {
        }

        protected override void SetTextContent(string text, bool shouldRenderRightToLeft)
        {
            urlToOpen = text;

            if (string.IsNullOrEmpty(urlToOpen))
            {
                urlToOpen = fallbackUrlToOpen;
            }
        }

        protected override void ForceTextExpansion()
        {
        }

        /// <summary> Applies Font Override for this Loc Component </summary>
		protected override void ApplyFontOverride()
		{
		}
    }
}
