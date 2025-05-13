using UnityEditor;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Localisation.GameSpecificLocalisation.Editor;
using Localisation.Localisation.SharedLocalisationEditor;
using Localisation.LocalisationOverrides;
using UnityEngine;
using UnityEditor.Callbacks;
using Debug = UnityEngine.Debug;
using LocData = Localisation.Localisation.SharedLocalisationEditor.GenerationalLocData;
using GenerateGameSpecificLocalisationIDsData = Localisation.GameSpecificLocalisation.Editor.GenerateGameSpecificLocalisationIDsData;

namespace Localisation.Localisation.Editor
{
	public static class GenerateLocalisationIDs
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		// *    Definitions
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public struct LocTableLineInfo
		{
			public string locIdString;
			public string englishText;
			public int rowId;
		}

		private class Indentation : System.IDisposable
		{
			public Indentation()
			{
				IndentLevel += 1;
			}
			public void Dispose()
			{
				IndentLevel -= 1;
			}
		}

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		// *    Consts
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public const char LineSeparator = GenerateGameSpecificLocalisationIDsData.LineSeparator;

		public const string MasterLocTableName = LocalisationOverrides.Configuration.MasterLocStringsTableFileName;
		public const string ClassName = "LocIDs";
		public const string ConversionClassName = "LocIdsHelper";
		public const string EditorClassName = "LocIDsEditorData";

		public const string EmptyValueIdentity = "Empty_Loc_String";
		public const string EmptyValueSummaryText = "Empty String Value: Will show nothing";


		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		// *    Properties
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		private static int IndentLevel { get; set; } = 0;

		private static string IndentationString { get { return new string('\t', IndentLevel); } }

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		// *    Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		[MenuItem(LocalisationOverrides.Configuration.MenuCategorisation + "Generate Loc Ids")]
		public static void CreateLocalisationIDsClassFile_MenuItem()
		{
			CreateLocalisationIDsClassFile();
			AssetDatabase.Refresh();
		}

		/// <summary> Generates a new class file with the specified version string </summary>
		/// <returns> The new generated class's filename </returns>
		public static bool CreateLocalisationIDsClassFile()
		{
			Dictionary<string, List<GenerationalLocData>> locDocToLocData;
			List<string> codeStrings = GenerateLocIDScript(out locDocToLocData);
			if (codeStrings.Count == 0)
			{
				return false;
			}

			using (StreamWriter writer = new StreamWriter(Configuration.LocalisationIDsFilePath, false))
			{
				try
				{
					foreach (string codeBatch in codeStrings)
					{
						writer.Write(codeBatch);
					}
				}
				catch (System.Exception ex)
				{
					string msg = $" threw:\n{ex.Message}";
					LocalisationOverrides.Debug.LogError(msg);
					EditorUtility.DisplayDialog("Error when trying to regenerate class", msg, "OK");
					return false;
				}
			}

			codeStrings = GenerateGameSpecificLocalisationIDsData.GenerateGameSpecificLocIDsData(locDocToLocData);
			using (StreamWriter writer = new StreamWriter(Configuration.GameSpecificLocalisationIDsFilePath, false))
			{
				try
				{
					foreach (string codeBatch in codeStrings)
					{
						writer.Write(codeBatch);
					}
				}
				catch (System.Exception ex)
				{
					string msg = $" threw:\n{ex.Message}";
					LocalisationOverrides.Debug.LogError(msg);
					EditorUtility.DisplayDialog("Error when trying to regenerate class", msg, "OK");
					return false;
				}
			}
			return true;
		}

		/// <summary> Returns a new formatted line with the Indent Tabs and EndOfLine </summary>
		private static string AddLine(string line, int newLinesCount = 1)
		{
			return $"{IndentationString}{line}{new string(LineSeparator, newLinesCount)}";
		}


		/// <summary> Regenerates (and replaces) the code for LocIDs.cs with new const data </summary>
		/// <returns> Code to write to file </returns>
		private static List<string> GenerateLocIDScript(out Dictionary<string, List<GenerationalLocData>> _locDocToLocData)
		{
			// Returning as List because there's a chance we could end up hitting the string size limit. So this will prevent that.
			LinkedList<string> locIdsBatchHeaderValues;
			if (GenerateConstLocValuesBatch(out _locDocToLocData, out locIdsBatchHeaderValues) == false)
			{
				// Bad Values
				return new List<string>();
			}

			// Assign the Output Strings Capacity to 10 times the Loc Ids we have as there are currently 13 additional Const values that setup const 
			//  Editor Data items that I use for Editor purposes (dropdown menus etc.). I'm giving it 15 times the capacity in case I add more stuff and forget to increase.
			//  It won't cause issues if I go beyond capacity. Just slightly inefficient.
			int totalLocIdsCount = locIdsBatchHeaderValues.Count;
			int outputStringsCapacity = totalLocIdsCount * 15;
			List<string> outputStrings = new List<string>(outputStringsCapacity);
			outputStrings.Add(AddLine("// This file is autogenerated in the Unity Editor"));
			outputStrings.Add(AddLine("using System.Collections.Generic;"));
			outputStrings.Add(AddLine("using UnityEngine;", 2));
			outputStrings.Add(AddLine("#pragma warning disable 618", 2));

			outputStrings.Add(AddLine($"public static partial class {ClassName}"));
			outputStrings.Add(AddLine("{"));
			{
				outputStrings.AddRange(locIdsBatchHeaderValues);
			}
			outputStrings.Add(AddLine("}", 3));

			outputStrings.Add(AddLine("namespace Localisation"));
			outputStrings.Add(AddLine("{"));
			{
				using (new Indentation())
				{
					outputStrings.AddRange(GenerateHashValueDictionaryBatch(_locDocToLocData));

					outputStrings.Add(AddLine("", 2));
					outputStrings.AddRange(GenerateCustomEditorDataBatch(_locDocToLocData));
				}
			}
			outputStrings.Add(AddLine("}"));


			if (outputStrings.Count > outputStringsCapacity)
			{
				Debug.LogWarning($"Warning: {nameof(GenerateLocalisationIDs)} created a script file output with {outputStrings.Count} lines. "
							 + $"The capacity was designated as {outputStringsCapacity} lines or less. This is not an error. But is not optimal. Please increase the Capacity to fix.");
			}
			else
			{
				Debug.Log($"Warning: {nameof(GenerateLocalisationIDs)} created a script file output with {outputStrings.Count} lines. "
						+ $"The capacity was designated as {outputStringsCapacity} lines or less.");
			}
			return outputStrings;
		}

		/// <summary> Scans through the Localisation Table File for LocIDs and RowIds to match and checks for errors.</summary>
		/// <param name="_pathToLocStringsTable"> The local path (from Resources) to the Loc Table. </param>
		/// <param name="_locTableLinesData"> Outputs the Loc Ids and Row Index found per valid line in the Loc Table. </param>
		/// <returns> Returns true if no issues are found. False if errors are found. </returns>
		public static bool GetLocalisationIDStrings(string _pathToLocStringsTable, out List<LocTableLineInfo> _locTableLinesData)
		{
			TextAsset textAsset = UnityEditor.AssetDatabase.LoadAssetAtPath<TextAsset>(_pathToLocStringsTable);
			if (textAsset == null)
				throw new UnityException($"Could not open Loc Table file at Resources/{_pathToLocStringsTable}");

			string locTableContents = textAsset.text.Replace("\r\n", "\n");

			string[] locLines = locTableContents.Split('\n');

			_locTableLinesData = new List<LocTableLineInfo>();

			bool noIssuesFound = true;

			// Ignore first row
			for (int rowNo = 1; rowNo < locLines.Length; ++rowNo)
			{
				string line = locLines[rowNo];
				if (line == " " || string.IsNullOrEmpty(line.Trim()) == false)
				{
					// Ignore first column of Loc Table, it contains ID, and we don't need it in langStrings
					string[] stringsRead = line.Split('\t');
					string id = stringsRead[0];
					string englishText = stringsRead[1];

					// Store ID if there's an entry in the column
					if (string.IsNullOrEmpty(id) == false)
					{
						bool alreadyExists = false;
						for (int locIndex = 0; locIndex < _locTableLinesData.Count; ++locIndex)
						{
							if (string.Equals(_locTableLinesData[locIndex].locIdString, id))
							{
								int thisRowId = rowNo + 1;
								int existingAtRowId = _locTableLinesData[locIndex].rowId + 1;
								string errorMsg = $"Localisation ID '{id}' (Line: {thisRowId}) already exists at Line: {existingAtRowId}.";
								Debug.LogError(errorMsg);
								EditorUtility.DisplayDialog("Duplicate Loc ID!", errorMsg, "D'oh");
								noIssuesFound = false;
								alreadyExists = true;
							}
						}

						if (alreadyExists == false)
						{
							_locTableLinesData.Add(new LocTableLineInfo()
							{
								locIdString = id,
								englishText = englishText,
								rowId = rowNo,
							});
						}
					}
				}
			}

			return noIssuesFound;
		}

		/// <summary> Generates the LocIds Constants Section.</summary>
		/// <param name="_locDocToLocData"> Outputs the Generated LocData per LocId (identity, hash value, row id, etc.)</param>
		/// <param name="_returnOutputCode"> Outputs the code to write to the LocIds.cs file. </param>
		private static bool GenerateConstLocValuesBatch(out Dictionary<string, List<GenerationalLocData>> _locDocToLocData, out LinkedList<string> _returnOutputCode)
		{
			char[] subCategorySplitter = new char[] { '_' };
			_locDocToLocData = new Dictionary<string, List<GenerationalLocData>>();
			_returnOutputCode = new LinkedList<string>();
			
			LocalisationImportHandler importHandler = (AssetDatabase.LoadAssetAtPath(Configuration.LocalisationImportConfigFilePath, typeof(LocalisationImportHandler)) as LocalisationImportHandler)!;
			using (new Indentation())
			{
				_returnOutputCode.AddLast(AddLine("//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
				_returnOutputCode.AddLast(AddLine("//                   Loc Docs Info                       "));
				_returnOutputCode.AddLast(AddLine("//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
				_returnOutputCode.AddLast(AddLine($"public static class LocDocsInfo"));
				_returnOutputCode.AddLast(AddLine("{"));
				using (new Indentation())
				{
					_returnOutputCode.AddLast(AddLine($"public const int TotalLocDocs = {importHandler.DocumentFetchInfos.Length};", 2));
					_returnOutputCode.AddLast(AddLine("/// <summary> Used as identifiers for Resource Names or Addressable keys. </summary>"));
					_returnOutputCode.AddLast(AddLine("public static readonly string[] LocDocs = new string[TotalLocDocs]"));
					_returnOutputCode.AddLast(AddLine("{"));
					using (new Indentation())
					{
						for (int locTableIndex = 0; locTableIndex < importHandler.DocumentFetchInfos.Length; ++locTableIndex)
						{
							string locTableFilename = System.IO.Path.GetFileNameWithoutExtension(importHandler.DocumentFetchInfos[locTableIndex].csvFilePath);
							_returnOutputCode.AddLast(AddLine($"\"{locTableFilename}\","));
						}
					}
					_returnOutputCode.AddLast(AddLine("};", 2));

					_returnOutputCode.AddLast(AddLine("#if UNITY_EDITOR"));
					_returnOutputCode.AddLast(AddLine("/// <summary> Used during build to move Loc String CSV files over to the Resources folder, if not using Addressables. </summary>"));
					_returnOutputCode.AddLast(AddLine("public static readonly string[] LocDocsPaths = new string[TotalLocDocs]"));
					_returnOutputCode.AddLast(AddLine("{"));
					using (new Indentation())
					{
						for (int locTableIndex = 0; locTableIndex < importHandler.DocumentFetchInfos.Length; ++locTableIndex)
						{
							_returnOutputCode.AddLast(AddLine($"\"{importHandler.DocumentFetchInfos[locTableIndex].csvFilePath}\","));
						}
					}
					_returnOutputCode.AddLast(AddLine("};"));
					_returnOutputCode.AddLast(AddLine("#endif"));
				}
				_returnOutputCode.AddLast(AddLine("}", 2));
			}

			for (int locTableIndex = 0; locTableIndex < importHandler.DocumentFetchInfos.Length; ++locTableIndex)
			{
				string fullPathToLocDoc = importHandler.DocumentFetchInfos[locTableIndex].csvFilePath;
				string locTableFilename = System.IO.Path.GetFileNameWithoutExtension(fullPathToLocDoc);
				bool isMasterDoc = string.Equals(locTableFilename, LocalisationOverrides.Configuration.MasterLocStringsTableFileName);

				List<LocTableLineInfo> locTableLinesData;
				if (GetLocalisationIDStrings(fullPathToLocDoc, out locTableLinesData) == false)
				{
					// Errors found while importing
					_returnOutputCode = null;
					return false;
				}

				string locTableName;
				int locLinesCount = locTableLinesData.Count;
				List<GenerationalLocData> locDataForTable = new List<GenerationalLocData>();
				if (isMasterDoc)
				{
					locTableName = MasterLocTableName;
					_locDocToLocData.Add(MasterLocTableName, locDataForTable);
				}
				else
				{
					// Game Specific Loc Table name
					locTableName = locTableFilename.Replace("/", "_");
					_locDocToLocData.Add(locTableName, locDataForTable);
				}

				for (int i = 0; i < locLinesCount; ++i)
				{
					string identity = locTableLinesData[i].locIdString;
					string englishText = locTableLinesData[i].englishText;
					int locHashValue = (LocID)identity;

					// Separating into Sub Categories also because large games will have enough Loc IDs that the dropdown box will start showing stuff offscreen.
					string identitySeparatedAsCategories = $"{identity[0].ToString().ToUpper()}/";
					string[] subCategories = identity.Split(subCategorySplitter);

					if (subCategories.Length == 1)
					{
						identitySeparatedAsCategories += identity;
					}
					else
					{
						identitySeparatedAsCategories += $"{subCategories[0]}/{subCategories[0]} {subCategories[1]}";
						for (int subCatIndex = 2; subCatIndex < subCategories.Length; ++subCatIndex)
						{
							identitySeparatedAsCategories += $" {subCategories[subCatIndex]}";
						}
					}

					locDataForTable.Add(new GenerationalLocData()
					{
						identity = identity,
						identityWithCategory = identitySeparatedAsCategories,
						hashValue = locHashValue,
						englishText = englishText,
						rowId = i + 1,
					});
				}

				using (new Indentation())
				{
					if (isMasterDoc)
					{
						_returnOutputCode.AddLast(AddLine("//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
						_returnOutputCode.AddLast(AddLine("//                   Master Loc Ids                       "));
						_returnOutputCode.AddLast(AddLine("//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
						_returnOutputCode.AddLast(AddLine($"/// <summary> {EmptyValueSummaryText} </summary>"));
						_returnOutputCode.AddLast(AddLine($"public const int {EmptyValueIdentity} = 0;"));

						// Sorting by alphabetical order
						locDataForTable.Sort((a, b) => string.CompareOrdinal(a.identity, b.identity));
						foreach (GenerationalLocData locData in locDataForTable)
						{
							_returnOutputCode.AddLast(AddLine($"/// <summary> {locData.englishText.Replace("\n", " ")} </summary>"));
							_returnOutputCode.AddLast(AddLine($"public const int {locData.identity} = {locData.hashValue};"));
						}
					}
					else
					{
						_returnOutputCode.AddLast(AddLine(string.Empty, 2));
						_returnOutputCode.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
						_returnOutputCode.AddLast(AddLine($"//           {locTableName} Loc Ids                       "));
						_returnOutputCode.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
						_returnOutputCode.AddLast(AddLine($"public static class {locTableName}_LocIds"));
						_returnOutputCode.AddLast(AddLine("{"));
						using (new Indentation())
						{
							_returnOutputCode.AddLast(AddLine($"/// <summary> {EmptyValueSummaryText} </summary>"));
							_returnOutputCode.AddLast(AddLine($"public const int {EmptyValueIdentity} = 0;"));

							// Sorting by alphabetical order
							locDataForTable.Sort((a, b) => string.CompareOrdinal(a.identity, b.identity));
							foreach (GenerationalLocData locData in locDataForTable)
							{
								_returnOutputCode.AddLast(AddLine($"/// <summary> {locData.englishText.Replace("\n", " ")} </summary>"));
								_returnOutputCode.AddLast(AddLine($"public const int {locData.identity} = {locData.hashValue};"));
							}
						}
						_returnOutputCode.AddLast(AddLine("}"));
					}

					// Adding in Empty Value last as we have already added it as a Const Value in the script. It's still needed for the remaining sections though
					locDataForTable.Add(new GenerationalLocData()
					{
						identity = EmptyValueIdentity,
						identityWithCategory = EmptyValueIdentity.Replace('_', ' '),
						hashValue = 0,
						englishText = EmptyValueSummaryText,
						rowId = 0,
					});
				}
			}
			return true;
		}

		private static LinkedList<string> GenerateHashValueDictionaryBatch(Dictionary<string, List<GenerationalLocData>> _locDocToLocData)
		{
			LinkedList<string> returnVal = new LinkedList<string>();
			returnVal.AddLast(AddLine($"public static class {ConversionClassName}"));
			returnVal.AddLast(AddLine("{"));
			using (new Indentation())
			{
				returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
				returnVal.AddLast(AddLine($"//                    Definitions                         "));
				returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
				foreach (KeyValuePair<string, List<GenerationalLocData>> locTableAndDataPair in _locDocToLocData)
				{
					string locTableName = locTableAndDataPair.Key;
					returnVal.AddLast(AddLine($"public const int {locTableName}_TotalLocIdsCount = {locTableAndDataPair.Value.Count};"));
				}
				returnVal.AddLast(AddLine(string.Empty, 3));

				foreach (KeyValuePair<string, List<GenerationalLocData>> locTableAndDataPair in _locDocToLocData)
				{
					string locTableName = locTableAndDataPair.Key;
					
					List<CombinedLocDocsInfo> combinedLocDocsInfo = new List<CombinedLocDocsInfo>(locTableAndDataPair.Value.Count);
					foreach (GenerationalLocData locData in locTableAndDataPair.Value)
					{
						if (combinedLocDocsInfo.Any(x => x.locData.hashValue == locData.hashValue) == false)
						{
							combinedLocDocsInfo.Add(new CombinedLocDocsInfo()
							{
								locTableName = locTableName,
								locData = locData,
							});
						}
					}
					
					List<CombinedLocDocsInfo> hashIdOrderedLocDataList = GetHashValueOrderedList(combinedLocDocsInfo);
					List<CombinedLocDocsInfo> locTableLineOrderedLocDataList = GetLocTableLineOrderedList(combinedLocDocsInfo);

					string rowIdsEnumIdentity = $"{locTableName}_RowIds";
					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"//              {locTableName} Row Ids                    "));
					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"[System.Obsolete(\"Don't use this class. RowIDs can change at any time. Instead, use Localisation.LocID because the value will always be consistent.\")]"));
					returnVal.AddLast(AddLine($"public enum {rowIdsEnumIdentity}"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						foreach (CombinedLocDocsInfo combinedLocData in locTableLineOrderedLocDataList)
						{
							returnVal.AddLast(AddLine($"{combinedLocData.locData.RowValueIdentity},"));
						}
					}
					returnVal.AddLast(AddLine($"}}", 3));


					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"//              {locTableName} Conversion                 "));
					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"private static Dictionary<int, int> {locTableName}_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						string classNameIdentity = ClassName;
						if (string.Equals(locTableName, MasterLocTableName) == false)
						{
							classNameIdentity += $".{locTableName}_LocIds";
						}

						returnVal.AddLast(AddLine($"Dictionary<int, int> output = new Dictionary<int, int>({ConversionClassName}.{locTableName}_TotalLocIdsCount);"));
						foreach (CombinedLocDocsInfo combinedLocData in hashIdOrderedLocDataList)
						{
							returnVal.AddLast(AddLine($"output.Add( {classNameIdentity}.{combinedLocData.locData.HashValueIdentity}, (int){rowIdsEnumIdentity}.{combinedLocData.locData.RowValueIdentity} );"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));
					returnVal.AddLast(AddLine($"public static readonly Dictionary<int, int> {locTableName}_HashValueToRowId = {locTableName}_Get();", 3));
				}


				returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
				returnVal.AddLast(AddLine($"//                  Hash To Rows Reference                "));
				returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
				returnVal.AddLast(AddLine("public static readonly Dictionary<string, int> CSVNameToRowsDictionaryIndex = new Dictionary<string, int>"));
				returnVal.AddLast(AddLine("{"));
				using (new Indentation())
				{
					int index = 0;
					foreach (KeyValuePair<string, List<GenerationalLocData>> locTableAndDataPair in _locDocToLocData)
					{
						string locTableName = locTableAndDataPair.Key;
						returnVal.AddLast(AddLine($"{{ \"{locTableName}\", {index++} }},"));
					}
				}
				returnVal.AddLast(AddLine("};", 3));
				
				returnVal.AddLast(AddLine("public static readonly Dictionary<int, int>[] HashToRowsDictionaryReferences = new Dictionary<int, int>[]"));
				returnVal.AddLast(AddLine("{"));
				using (new Indentation())
				{
					foreach (KeyValuePair<string, List<GenerationalLocData>> locTableAndDataPair in _locDocToLocData)
					{
						string locTableName = locTableAndDataPair.Key;
						returnVal.AddLast(AddLine($"{locTableName}_HashValueToRowId,"));
					}
				}
				returnVal.AddLast(AddLine("};", 3));
			}
			returnVal.AddLast(AddLine($"}}")); // <= public static class {ConversionClassName}
			return returnVal;
		}

		private class CombinedLocDocsInfo
		{
			public GenerationalLocData locData;
			public string locTableName;
		}

		private static LinkedList<string> GenerateCustomEditorDataBatch(Dictionary<string, List<GenerationalLocData>> _locDocToLocData)
		{
			string GetClassName(CombinedLocDocsInfo combinedLocData)
			{
				string classNameIdentity = ClassName;
				if (string.Equals(combinedLocData.locTableName, MasterLocTableName) == false)
					classNameIdentity += $".{combinedLocData.locTableName}_LocIds";
				return classNameIdentity;
			}

			int totalLocIdsCount = _locDocToLocData.Values.Sum(v => v.Count);
			List<CombinedLocDocsInfo> combinedLocDocsInfo = new List<CombinedLocDocsInfo>(totalLocIdsCount);
			foreach (KeyValuePair<string, List<GenerationalLocData>> pair in _locDocToLocData)
			{
				foreach (GenerationalLocData locData in pair.Value)
				{
					if (combinedLocDocsInfo.Any(x => x.locData.hashValue == locData.hashValue) == false)
					{
						combinedLocDocsInfo.Add(new CombinedLocDocsInfo()
						{
							locTableName = pair.Key,
							locData = locData,
						});
					}
				}
			}

			LinkedList<string> returnVal = new LinkedList<string>();
			returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
			returnVal.AddLast(AddLine($"//                 Custom Editor Data                     "));
			returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
			returnVal.AddLast(AddLine($"#if UNITY_EDITOR"));
			{
				returnVal.AddLast(AddLine($"public static class {EditorClassName}"));
				returnVal.AddLast(AddLine($"{{"));
				using (new Indentation())
				{
					string combinedLocTableName = "Combined";
					List<CombinedLocDocsInfo> locTableLineOrderedLocDataList = GetLocTableLineOrderedList(combinedLocDocsInfo);
					List<CombinedLocDocsInfo> alphabeticallyOrderedLocDataList = GetAlphabeticallyOrderedList(combinedLocDocsInfo);
					List<CombinedLocDocsInfo> hashIdOrderedLocDataList = GetHashValueOrderedList(combinedLocDocsInfo);

					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"//         {combinedLocTableName} Editor Data Builders"));
					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"private static GUIContent[] {combinedLocTableName}_EditorLocTableLineOrderedLocStringIDs_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						// We are using i++ in these fields instead of direct numbers because using direct number references causes Git to show
						// a massive changelog whenever a new Loc ID is added that appears before other older Loc IDs as the `output[<num>]` will
						// then change for every line after the new Loc ID. i++ resolves that issue.
						int count = locTableLineOrderedLocDataList.Count;
						returnVal.AddLast(AddLine($"int i = 0;"));
						returnVal.AddLast(AddLine($"GUIContent[] output = new GUIContent[{count}];"));
						for (int index = 0; index < count; ++index)
						{
							// Adding the First Letter then a slash because this allows the dropdown system to separate by categories.
							GenerationalLocData locData = locTableLineOrderedLocDataList[index].locData;
							returnVal.AddLast(AddLine($"output[i++] = new GUIContent(\"{locData.identity}\");"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));
					returnVal.AddLast(AddLine($"public static readonly GUIContent[] {combinedLocTableName}_EditorLocTableLineOrderedLocStringIDs = {combinedLocTableName}_EditorLocTableLineOrderedLocStringIDs_Get();", 3));



					returnVal.AddLast(AddLine($"private static GUIContent[] {combinedLocTableName}_EditorAlphabeticallyOrderedLocStringIDs_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						int count = alphabeticallyOrderedLocDataList.Count;
						returnVal.AddLast(AddLine($"int i = 0;"));
						returnVal.AddLast(AddLine($"GUIContent[] output = new GUIContent[{count}];"));
						for (int index = 0; index < count; ++index)
						{
							// Adding the First Letter then a slash because this allows the dropdown system to separate by categories.
							GenerationalLocData locData = alphabeticallyOrderedLocDataList[index].locData;
							returnVal.AddLast(AddLine($"output[i++] = new GUIContent(\"{locData.identity}\");"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));
					returnVal.AddLast(AddLine($"public static readonly GUIContent[] {combinedLocTableName}_EditorAlphabeticallyOrderedLocStringIDs = {combinedLocTableName}_EditorAlphabeticallyOrderedLocStringIDs_Get();", 3));


					returnVal.AddLast(AddLine($"private static GUIContent[] {combinedLocTableName}_EditorCategorisedAlphabeticallyOrderedLocStringIDs_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						int count = alphabeticallyOrderedLocDataList.Count;
						returnVal.AddLast(AddLine($"int i = 0;"));
						returnVal.AddLast(AddLine($"GUIContent[] output = new GUIContent[{count}];"));
						for (int index = 0; index < count; ++index)
						{
							// Adding the First Letter then a slash because this allows the dropdown system to separate by categories.
							GenerationalLocData locData = alphabeticallyOrderedLocDataList[index].locData;
							returnVal.AddLast(AddLine($"output[i++] = new GUIContent(\"{locData.identityWithCategory}\");"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));
					returnVal.AddLast(AddLine($"public static readonly GUIContent[] {combinedLocTableName}_EditorCategorisedAlphabeticallyOrderedLocStringIDs = {combinedLocTableName}_EditorCategorisedAlphabeticallyOrderedLocStringIDs_Get();", 2));


					returnVal.AddLast(AddLine($"private static Dictionary<string, int> {combinedLocTableName}_EditorLocStringIdToHashId_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						returnVal.AddLast(AddLine($"Dictionary<string, int> output = new Dictionary<string, int>({alphabeticallyOrderedLocDataList.Count});"));
						foreach (CombinedLocDocsInfo combinedLocData in alphabeticallyOrderedLocDataList)
						{
							string classNameIdentity = GetClassName(combinedLocData);
							returnVal.AddLast(AddLine($"output.Add( \"{combinedLocData.locData.identity}\", (int){classNameIdentity}.{combinedLocData.locData.HashValueIdentity} );"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));
					returnVal.AddLast(AddLine($"public static readonly Dictionary<string, int> {combinedLocTableName}_EditorLocStringIdToHashId = {combinedLocTableName}_EditorLocStringIdToHashId_Get();", 3));


					returnVal.AddLast(AddLine($"private static Dictionary<string, int> {combinedLocTableName}_EditorCategorisedLocStringIdToHashId_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						returnVal.AddLast(AddLine($"Dictionary<string, int> output = new Dictionary<string, int>({alphabeticallyOrderedLocDataList.Count});"));
						foreach (CombinedLocDocsInfo combinedLocData in alphabeticallyOrderedLocDataList)
						{
							string classNameIdentity = GetClassName(combinedLocData);
							returnVal.AddLast(AddLine($"output.Add( \"{combinedLocData.locData.identityWithCategory}\", (int){classNameIdentity}.{combinedLocData.locData.HashValueIdentity} );"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));
					returnVal.AddLast(AddLine($"public static readonly Dictionary<string, int> {combinedLocTableName}_EditorCategorisedLocStringIdToHashId = {combinedLocTableName}_EditorCategorisedLocStringIdToHashId_Get();", 3));

					returnVal.AddLast(AddLine($"private enum {combinedLocTableName}_EditorHashIdToInspectorPopupIndexOrder"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						foreach (CombinedLocDocsInfo combinedLocData in alphabeticallyOrderedLocDataList)
						{
							returnVal.AddLast(AddLine($"{combinedLocData.locData.identity},"));
						}
					}
					returnVal.AddLast(AddLine($"}}", 3));

					returnVal.AddLast(AddLine($"private static Dictionary<int, int> {combinedLocTableName}_EditorHashIdToInspectorPopupIndex_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						returnVal.AddLast(AddLine($"Dictionary<int, int> output = new Dictionary<int, int>({hashIdOrderedLocDataList.Count});"));
						foreach (CombinedLocDocsInfo combinedLocData in hashIdOrderedLocDataList)
						{
							string classNameIdentity = GetClassName(combinedLocData);
							string rowIdsEnumIdentity = $"{combinedLocTableName}_EditorHashIdToInspectorPopupIndexOrder.{combinedLocData.locData.identity}";
							returnVal.AddLast(AddLine($"output.Add( (int){classNameIdentity}.{combinedLocData.locData.HashValueIdentity}, (int){rowIdsEnumIdentity} );"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));


					returnVal.AddLast(AddLine($"private static int[] {combinedLocTableName}_EditorInspectorPopupIndexToHashId_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						returnVal.AddLast(AddLine($"int[] output = new int[{alphabeticallyOrderedLocDataList.Count}];"));
						for (int orderId = 0; orderId < alphabeticallyOrderedLocDataList.Count; ++orderId)
						{
							string classNameIdentity = GetClassName(alphabeticallyOrderedLocDataList[orderId]);
							GenerationalLocData locData = alphabeticallyOrderedLocDataList[orderId].locData;
							returnVal.AddLast(AddLine($"output[{orderId}] = (int){classNameIdentity}.{locData.HashValueIdentity};"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));


					returnVal.AddLast(AddLine($"private static Dictionary<int, string> {combinedLocTableName}_EditorHashIdToLocStringDropdownCategoryId_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						returnVal.AddLast(AddLine($"Dictionary<int, string> output = new Dictionary<int, string>({hashIdOrderedLocDataList.Count});"));
						foreach (CombinedLocDocsInfo combinedLocData in hashIdOrderedLocDataList)
						{
							string classNameIdentity = GetClassName(combinedLocData);
							returnVal.AddLast(AddLine($"output.Add( (int){classNameIdentity}.{combinedLocData.locData.HashValueIdentity}, \"{combinedLocData.locData.identityWithCategory}\" );"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));


					returnVal.AddLast(AddLine($"private static Dictionary<int, string> {combinedLocTableName}_EditorHashIdToLocStringId_Get()"));
					returnVal.AddLast(AddLine($"{{"));
					using (new Indentation())
					{
						returnVal.AddLast(AddLine($"Dictionary<int, string> output = new Dictionary<int, string>({hashIdOrderedLocDataList.Count});"));
						foreach (CombinedLocDocsInfo combinedLocData in hashIdOrderedLocDataList)
						{
							string classNameIdentity = GetClassName(combinedLocData);
							returnVal.AddLast(AddLine($"output.Add( (int){classNameIdentity}.{combinedLocData.locData.HashValueIdentity}, \"{combinedLocData.locData.identity}\" );"));
						}
						returnVal.AddLast(AddLine($"return output;"));
					}
					returnVal.AddLast(AddLine($"}}", 3));



					returnVal.AddLast(AddLine($"", 10));
					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"//            Editor Data Readonly Getters                "));
					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"public static readonly int[] {combinedLocTableName}_EditorInspectorPopupIndexToHashId = {combinedLocTableName}_EditorInspectorPopupIndexToHashId_Get();"));
					returnVal.AddLast(AddLine($"public static readonly Dictionary<int, int> {combinedLocTableName}_EditorHashIdToInspectorPopupIndex = {combinedLocTableName}_EditorHashIdToInspectorPopupIndex_Get();"));
					returnVal.AddLast(AddLine($"public static readonly Dictionary<int, string> {combinedLocTableName}_EditorHashIdToLocStringDropdownCategoryId = {combinedLocTableName}_EditorHashIdToLocStringDropdownCategoryId_Get();"));
					returnVal.AddLast(AddLine($"public static readonly Dictionary<int, string> {combinedLocTableName}_EditorHashIdToLocStringId = {combinedLocTableName}_EditorHashIdToLocStringId_Get();"));
					returnVal.AddLast(AddLine($"", 3));

					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"//             HashIds To StringsIds Reference            "));
					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine("public static readonly Dictionary<int, string>[] EditorHashToStringIdsDictionaryReferences = new Dictionary<int, string>[]"));
					returnVal.AddLast(AddLine("{"));
					using (new Indentation())
					{
						returnVal.AddLast(AddLine($"{combinedLocTableName}_EditorHashIdToLocStringId,"));
					}
					returnVal.AddLast(AddLine("};", 3));


					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine($"//             StringsIds To HashIds Reference            "));
					returnVal.AddLast(AddLine($"//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~"));
					returnVal.AddLast(AddLine("public static readonly Dictionary<string, int>[] EditorStringIdsToHashIdsDictionaryReferences = new Dictionary<string, int>[]"));
					returnVal.AddLast(AddLine("{"));
					using (new Indentation())
					{
						returnVal.AddLast(AddLine($"{combinedLocTableName}_EditorLocStringIdToHashId,"));
					}
					returnVal.AddLast(AddLine("};"));
				}
				returnVal.AddLast(AddLine($"}}")); // public static class {EditorClassName}
			}
			returnVal.AddLast(AddLine($"#endif")); // #if UNITY_EDITOR

			return returnVal;
		}


		private static List<CombinedLocDocsInfo> GetAlphabeticallyOrderedList(List<CombinedLocDocsInfo> _original)
		{
			List<CombinedLocDocsInfo> alphabeticallyOrderedLocDataList = new List<CombinedLocDocsInfo>(_original);
			alphabeticallyOrderedLocDataList.Sort((a, b) =>
			{
				// The 'No Loc ID' must come first
				if (string.Equals(a.locData.identity, EmptyValueIdentity))
					return -1;
				if (string.Equals(b.locData.identity, EmptyValueIdentity))
					return 1;

				return String.CompareOrdinal(a.locData.identity, b.locData.identity);
			});

			return alphabeticallyOrderedLocDataList;
		}

		private static List<CombinedLocDocsInfo> GetLocTableLineOrderedList(List<CombinedLocDocsInfo> _original)
		{
			List<CombinedLocDocsInfo> locTableLineOrderedLocDataList = new List<CombinedLocDocsInfo>(_original);
			locTableLineOrderedLocDataList.Sort((a, b) =>
			{
				if (a.locData.rowId < b.locData.rowId)
					return -1;
				if (a.locData.rowId == b.locData.rowId)
					return 0;
				return 1;
			});

			return locTableLineOrderedLocDataList;
		}

		private static List<CombinedLocDocsInfo> GetHashValueOrderedList(List<CombinedLocDocsInfo> _original)
		{
			List<CombinedLocDocsInfo> hashIdOrderedLocDataList = new List<CombinedLocDocsInfo>(_original);
			hashIdOrderedLocDataList.Sort((a, b) =>
			{
				if (a.locData.hashValue < b.locData.hashValue)
					return -1;
				if (a.locData.hashValue == b.locData.hashValue)
					return 0;
				return 1;
			});

			return hashIdOrderedLocDataList;
		}
	}
}