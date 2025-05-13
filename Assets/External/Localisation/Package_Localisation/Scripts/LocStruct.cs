//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Loc Struct
//             Author: Christopher Allport
//             Date Created: 26th March, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Passes in a Loc Data Struct for all applicable elements:
//          * Loc ID,
//          * Substrings,
//          * Text Highlight Colours
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=



namespace Localisation.Localisation
{
    public struct LocStruct 
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public LocID locId;
        public LocSubstring[] locSubstrings;
        public UnityEngine.Color[] highlightedTextColours;

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Constructors
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public LocStruct(LocID _locId)
        {
            locId = _locId;
            locSubstrings = null;
            highlightedTextColours = null;
        }

        public LocStruct(LocSubstring[] _locSubstrings)
        {
            locId = LocIDs.Empty_Loc_String;
            locSubstrings = _locSubstrings;
            highlightedTextColours = null;
        }

        public LocStruct(UnityEngine.Color[] _highlightedTextColours)
        {
            locId = LocIDs.Empty_Loc_String;
            locSubstrings = null;
            highlightedTextColours = _highlightedTextColours;
        }

        public LocStruct(LocID _locId, LocSubstring[] _locSubstrings)
        {
            locId = _locId;
            locSubstrings = _locSubstrings;
            highlightedTextColours = null;
        }

        public LocStruct(LocID _locId, UnityEngine.Color[] _highlightedTextColours)
        {
            locId = _locId;
            locSubstrings = null;
            highlightedTextColours = _highlightedTextColours;
        }

        public LocStruct(LocID _locId, LocSubstring[] _locSubstrings, UnityEngine.Color[] _highlightedTextColours)
        {
            locId = _locId;
            locSubstrings = _locSubstrings;
            highlightedTextColours = _highlightedTextColours;
        }

        public LocStruct(LocSubstring[] _locSubstrings, UnityEngine.Color[] _highlightedTextColours)
        {
            locId = LocIDs.Empty_Loc_String;
            locSubstrings = _locSubstrings;
            highlightedTextColours = _highlightedTextColours;
        }

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Operator Overloads
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        public static implicit operator LocStruct(LocID a)
        {
            return new LocStruct(a, null, null);
        }

        public static implicit operator LocStruct(int a)
        {
            return new LocStruct(a, null, null);
        }

        public static implicit operator LocStruct(string a)
        {
            return new LocStruct(a, null, null);
        }
    }
}