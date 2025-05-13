//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Loc IDs To Row IDs Interpreter
//             Author: Christopher Allport
//             Date Created: 24th November, 2021
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//      This class reads through Localisation CSV files and determines 
//      which ROW ID belongs to each LOC ID.
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;




namespace Localisation.Localisation
{
    public class LocIDsToRowIDsInterpreter
    {
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Non-Inspector Fields
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private Dictionary<int, Dictionary<int, int>> hashValueToRowIdsPerLocStringsCSV = new Dictionary<int, Dictionary<int, int>>();

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Methods
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        /// <summary> Parses the LocStrings File for LocIds->RowIds. </summary>
        /// <param name="_locTableIndex"> The table index we should store so that you can query it later. </param>
        /// <param name="_textAsset"> The Text asset to parse. </param>
        public void ParseLocStringsFile(int _locTableIndex, TextAsset _textAsset)
        {
            Dictionary<int, int> locIdsToRowIds;
            if (hashValueToRowIdsPerLocStringsCSV.TryGetValue(_locTableIndex, out locIdsToRowIds))
            {
                locIdsToRowIds.Clear();
            }
            else
            {
                locIdsToRowIds = new Dictionary<int, int>();
                hashValueToRowIdsPerLocStringsCSV.Add(_locTableIndex, locIdsToRowIds);
            }

            // Ignore first column of Loc Table, it contains the Loc ID and we don't need to worry about it
            string[] locTableRows = _textAsset.text.Split('\n');
            for (int rowNo = 1; rowNo < locTableRows.Length; ++rowNo)
            {
                string rowContents = locTableRows[rowNo];
                if (string.Equals(rowContents, " ") || string.IsNullOrEmpty(rowContents.Trim()) == false)
                {
                    string[] stringsRead = rowContents.Split('\t');
                    if (stringsRead.Length == 0)
                    {
                        continue;
                    }

                    string locIdString = stringsRead[0];
                    if (string.IsNullOrEmpty(locIdString) == false)
                    {
                        int locIdHashValue = locIdString.GetHashCode();
                        locIdsToRowIds[locIdHashValue] = rowNo;
                    }
                }
            }
        }

        /// <summary> Returns the Loc IDs to Row IDs Dictionary for the specified Loc Strings CSV file. </summary>
        /// <param name="_locTableIndex"> CSV File you are querying (This can be located inside of the LocManager class). </param>
        public Dictionary<int, int> GetLocIDsToRowIDsForCSVFile(int _locTableIndex)
        {
            Dictionary<int, int> value;
            if (hashValueToRowIdsPerLocStringsCSV.TryGetValue(_locTableIndex, out value))
            {
                return value;
            }
            return new Dictionary<int, int>();
        }

        /// <summary> Attempts to retrieve the Row ID associated with the Loc ID in the specified Loc Strings CSV. </summary>
        /// <param name="_locTableIndex"> CSV File you are querying (This can be located inside of the LocManager class). </param>
        /// <param name="_locIdHashValue"> The Loc ID you are seeking. </param>
        /// <param name="_locRowID"> Outputs the Row ID for the Loc ID. </param>
        public bool TryGetRowID(int _locTableIndex, int _locIdHashValue, out int _locRowID)
        {
            return GetLocIDsToRowIDsForCSVFile(_locTableIndex).TryGetValue(_locIdHashValue, out _locRowID);
        }

        /// <summary> Attempts to retrieve the Row ID associated with the Loc ID in any Loc Strings CSV. </summary>
        /// <param name="_locIdHashValue"> The Loc ID you are seeking. </param>
        /// <param name="_locTableIndex"> Outputs the CSV File Index this Loc ID was found in. </param>
        /// <param name="_locRowID"> Outputs the Row ID for the Loc ID. </param>
        public bool TryGetRowID(int _locIdHashValue, out int _locTableIndex, out int _locRowID)
        {
            // Checking Preferred CSV First
            if (LocIdsHelper.CSVNameToRowsDictionaryIndex.TryGetValue(LocManager.CSVPreference, out _locTableIndex))
            {
                if (TryGetRowID(_locTableIndex, _locIdHashValue, out _locRowID))
                {
                    return true;
                }
            }
            
            foreach (KeyValuePair<int, Dictionary<int, int>> pair in hashValueToRowIdsPerLocStringsCSV)
            {
                _locTableIndex = pair.Key;
                if (TryGetRowID(_locTableIndex, _locIdHashValue, out _locRowID))
                {
                    return true;
                }
            }

            _locTableIndex = 0;
            _locRowID = 0;
            return false;
        }
    }
}