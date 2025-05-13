//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Generational Loc Data
//             Author: Christopher Allport
//             Date Created: 1st June, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      Localisation Loc Data when Generating LocIDs.cs
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using System.Collections;
using System.Collections.Generic;
using UnityEngine;




namespace Localisation.Localisation.SharedLocalisationEditor
{
    public struct GenerationalLocData
    {
        public const string DefinedHashValueSuffix = "";
        public const string DefinedRowIdValueSuffix = "";

        /// <summary> Loc Id (e.g. Yellow) </summary>
        public string identity;
        /// <summary> Loc Id with category slash (e.g. Y/Yellow) </summary>
        public string identityWithCategory;
        /// <summary> The actual hash value </summary>
        public int hashValue;
        /// <summary> Which Line is this Loc String located on </summary>
        public int rowId;
		/// <summary> English Body Text </summary>
		public string englishText;

		/// <summary> The Const Value Identifier for this Loc Id (e.g. YELLOW_ID) </summary>
		public string HashValueIdentity
        {
            get { return identity + DefinedHashValueSuffix; }
        }
        /// <summary> The Const Value Identifier for the Row Id of this Loc String (e.g. YELLOW_CELL_ROW) </summary>
        public string RowValueIdentity
        {
            get { return identity + DefinedRowIdValueSuffix; }
        }
    }
}