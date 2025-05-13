//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
//             Google API
//             Author: Christopher Allport
//             Date Created: 28th April, 2022
//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
//  Description:
//
//        This script utilises the Google API Files found in the Localisation
//        Repo to interact with Google Documents (notably the Spreadsheets).
//
//=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=-=
using Google.Apis.Auth.OAuth2;
using Google.Apis.Sheets;
using Google.Apis.Sheets.v4;
using Google.Apis.Sheets.v4.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using Google.Apis.Json;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace Localisation.Localisation.Editor
{
	public static class GoogleAPI
	{
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Definitions
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		public class SpreadsheetHandler
		{
			/// <summary> Status of the Spreadsheet Fetch. </summary>
			public string Status = "";

			/// <summary> Fetch Progress so far. </summary>
			public float Progress = 0.0f;

			/// <summary> The Spreadsheet data. Not valid until 'IsCompleted' is true. </summary>
			public SpreadsheetData SpreadsheetData = new SpreadsheetData();

			/// <summary> Has Failed to Fetch the Spreadsheet information (Error is output in Status). </summary>
			public bool HasFailed { get { return Progress <= -1.0f; } }

			/// <summary> Has Fetched the Spreadsheet information. </summary>
			public bool IsCompleted { get { return Progress >= 1.0f || HasFailed; } }
		}

		public class GameDataSpreadsheetSettings
		{
			public int startX = 0;
			public int startY = 0;
			public bool sortAlphabetical = true;
			public string keyHeaderName = "Key";
			
			/// <summary>
			/// When updating the data and updating existing keys, this will make sure it finds keys with a different case.
			/// </summary>
			public bool ignoreKeyCase = true;
			
			/// <summary>
			/// Allows you to make a enum dropdown for this data. The key in the dictionary is the data's header name (field name in uploaded structure)
			/// Type must be an enum type.
			/// </summary>
			public Dictionary<string, System.Func<string[]>> enumMappings = null;

			public static GameDataSpreadsheetSettings Default = new GameDataSpreadsheetSettings();
		}

		public class SheetFetchData
        {
            public Sheet Sheet;
            public Task<ValueRange> SheetsDataGetRequest;
        }

        public struct SpreadsheetData
		{
            public Spreadsheet Spreadsheet;
            public List<SheetData> SheetsData;
        }

        public struct SheetData
		{
            public Sheet Sheet;
            public ValueRange Cells;

            public string Title { get { return Sheet.Properties.Title; } }
            public int SheetId { get { return Sheet.Properties.SheetId ?? 0; } }
		}

        [Flags]
        public enum Scope
		{
            /// <summary> See, edit, create, and delete all of your Google Drive files. </summary>
            GoogleDrive             = (1 << 0),
            /// <summary> See, edit, create, and delete only the specific Google Drive files you use with this app. </summary>
            GoogleDriveFile         = (1 << 1),
            /// <summary> See and download all your Google Drive files. </summary>
            DriveReadonly           = (1 << 2),
            /// <summary> See, edit, create, and delete all your Google Sheets spreadsheets. </summary>
            Spreadsheets            = (1 << 3),
            /// <summary> See all your Google Sheets spreadsheets. </summary>
            SpreadsheetsReadonly    = (1 << 4),
		}

		/// <summary>
		/// Contains all the data in the spreadsheet and some header information used for when updating an existing spreadsheet.
		/// </summary>
		/// <typeparam name="T"></typeparam>
		public class GameDataSpreadsheetFetchInfo<T>
		{
			public Dictionary<string, T> data;
			// Raw data includes rows which don't correspond to the input type
			public Dictionary<string, JObject> rawData;
			public Dictionary<string, int> dataRows;
			public Dictionary<string, int> header;
		}

        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        //          Consts
        //~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
        private const string ServiceApplicationName = "LocalisationService";
        private const string ServiceEmailAccount = "localisationservice@planar-courage-262504.iam.gserviceaccount.com";
        private const string PathToCredentialsJson = LocalisationOverrides.Configuration.PathToPackageLocalisationRepo + "Editor/GoogleAPICredentials.json";
        public const string NoteSpreadsheetDataSuffix = "_note";

		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		//          Methods
		//~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~
		/// <summary> Returns the Credentials for the Google Scopes. </summary>
		public static GoogleCredential GetAuthenticationCredentials(Scope _scopes)
		{
            string[] scopeIds = GetScopesIds(_scopes);

            if (System.IO.File.Exists(PathToCredentialsJson))
            {
	            using FileStream stream = new FileStream(PathToCredentialsJson, FileMode.Open, FileAccess.Read);
                GoogleCredential credential = GoogleCredential.FromStream(stream).CreateScoped(scopeIds);

                return credential;
			}
            
            throw new Exception($"file not found: [{PathToCredentialsJson}]");
		}

        /// <summary> Returns the Scope Ids </summary>
        public static string[] GetScopesIds(Scope _scopes)
		{
            List<string> scopesIds = new List<string>();
            if ((_scopes & Scope.DriveReadonly) == Scope.DriveReadonly)
            {
                scopesIds.Add(SheetsService.Scope.DriveReadonly);
            }
            else if ((_scopes & Scope.GoogleDriveFile) == Scope.GoogleDriveFile)
            {
                scopesIds.Add(SheetsService.Scope.DriveFile);
            }
            else if ((_scopes & Scope.GoogleDrive) == Scope.GoogleDrive)
            {
                scopesIds.Add(SheetsService.Scope.Drive);
            }

            if ((_scopes & Scope.SpreadsheetsReadonly) == Scope.SpreadsheetsReadonly)
            {
                scopesIds.Add(SheetsService.Scope.SpreadsheetsReadonly);
            }
            else if ((_scopes & Scope.Spreadsheets) == Scope.Spreadsheets)
            {
                scopesIds.Add(SheetsService.Scope.Spreadsheets);
            }

            return scopesIds.ToArray();
        }

        /// <summary> Returns a Spreadsheet handler. Continue to query it until the spreadsheet data is returned. </summary>
        /// <param name="_spreadsheetId"> The Google Doc (Spreadsheet) Id to grab. </param>
        public static SpreadsheetHandler FetchSpreadsheet(string _spreadsheetId)
		{
            SpreadsheetHandler spreadsheetHandler = new SpreadsheetHandler();
			_ = FetchSpreadsheetAsync(_spreadsheetId, spreadsheetHandler);
            return spreadsheetHandler;
        }

        /// <summary> Returns Spreadsheet data. </summary>
        /// <param name="_spreadsheetId"> The Google Doc (Spreadsheet) Id to grab. </param>
        public static async Task<SpreadsheetData> FetchSpreadsheetAsync(string _spreadsheetId)
        {
            SpreadsheetHandler spreadsheetHandler = new SpreadsheetHandler();
            await FetchSpreadsheetAsync(_spreadsheetId, spreadsheetHandler);
            return spreadsheetHandler.SpreadsheetData;
        }

        /// <summary> Returns Spreadsheet data. </summary>
        private static async Task FetchSpreadsheetAsync(string _spreadsheetId, SpreadsheetHandler _spreadsheetHandler)
        {
            try
            {
                const float ProgressAfterSpreadsheetFetch = 0.25f;

                GoogleCredential credentials = GetAuthenticationCredentials(Scope.Spreadsheets);
                SheetsService sheetsService = new SheetsService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credentials,
                    ApplicationName = ServiceApplicationName,
                });

                _spreadsheetHandler.Status = $"Fetching Spreadsheet [{_spreadsheetId}]";
                _spreadsheetHandler.Progress = 0.0f;

				SpreadsheetsResource.GetRequest spreadSheetGetRequest = sheetsService.Spreadsheets.Get(_spreadsheetId);
                spreadSheetGetRequest.IncludeGridData = false;

                Spreadsheet spreadsheet = await spreadSheetGetRequest.ExecuteAsync();
                _spreadsheetHandler.Progress = ProgressAfterSpreadsheetFetch;

                int sheetsCount = spreadsheet.Sheets.Count;
                _spreadsheetHandler.SpreadsheetData = new SpreadsheetData()
                {
                    Spreadsheet = spreadsheet,
                    SheetsData = new List<SheetData>(sheetsCount)
                };
                
                List<string> allSheets = new List<string>();
                for (int sheetIndex = 0; sheetIndex < sheetsCount; ++sheetIndex)
				{
                    Sheet sheet = spreadsheet.Sheets[sheetIndex];
                    allSheets.Add(sheet.Properties.Title);
				}
                
                SpreadsheetsResource.ValuesResource.BatchGetRequest sheetCellsGetRequest = sheetsService.Spreadsheets.Values.BatchGet(_spreadsheetId);
                sheetCellsGetRequest.Ranges = allSheets;
                
                _spreadsheetHandler.Status = $"Fetching Sheet Values [{string.Join("; ", allSheets)}]";
                _spreadsheetHandler.Progress = 0.25f;
                BatchGetValuesResponse result = await sheetCellsGetRequest.ExecuteAsync();

                if (result.ValueRanges.Count != allSheets.Count)
                {
	                throw new Exception($"Spreadsheet request is missing sheets!\nExpected: {string.Join(", ",allSheets)}\nGot: {string.Join(", ",result.ValueRanges.Select(v=>v.Range))}");
                }
                
                for (var index = 0; index < result.ValueRanges.Count; index++)
                {
	                var valueRange = result.ValueRanges[index];
	                Sheet sheet = spreadsheet.Sheets[index];
	                _spreadsheetHandler.SpreadsheetData.SheetsData.Add(new SheetData()
	                {
		                Sheet = sheet,
		                Cells = valueRange,
	                });
                }

                _spreadsheetHandler.Status = $"Completed";
                _spreadsheetHandler.Progress = 1.0f;
            }
            catch (System.Exception exception)
			{
                _spreadsheetHandler.Status = $"Error: {exception.Message}";
                _spreadsheetHandler.Progress = -1.0f;
                Debug.LogException(exception);
            }
        }

		/// <summary>
		/// Fetch and parse a game data spreadsheet, to be used with UpdateGameDataSpreadsheetAsync
		/// </summary>
		/// <param name="_spreadsheetId">Spreadsheet to fetch</param>
		/// <param name="_sheetID">Sheet/ tab ID to fetch. 0 by default.</param>
		/// <returns>Data for each row using the row key as the dictionary key.</returns>
		public static async Task<GameDataSpreadsheetFetchInfo<TDATA>> FetchGameDataSpreadsheetAsync<TDATA>(string _spreadsheetId, int? _sheetID, GameDataSpreadsheetSettings _settings = null) where TDATA : new()
		{
			_settings ??= GameDataSpreadsheetSettings.Default;

			GoogleCredential credentials = GetAuthenticationCredentials(Scope.Spreadsheets);
			SheetsService sheetsService = new SheetsService(new BaseClientService.Initializer()
			{
				HttpClientInitializer = credentials,
				ApplicationName = ServiceApplicationName,
			});

			SpreadsheetsResource.GetRequest spreadSheetGetRequest = sheetsService.Spreadsheets.Get(_spreadsheetId);
			spreadSheetGetRequest.IncludeGridData = false;

			Spreadsheet spreadsheet = await spreadSheetGetRequest.ExecuteAsync();

			// Find the correct sheet ID
			Sheet foundSheet = null;
			if(_sheetID == null)
			{
				if (spreadsheet.Sheets.Count != 0)
					foundSheet = spreadsheet.Sheets[0];
			}
			else
			{
				foreach(var sheet in spreadsheet.Sheets)
				{
					if(sheet.Properties.SheetId == _sheetID)
					{
						foundSheet = sheet;
						break;
					}
				}
			}
				
			if(foundSheet == null)
				throw new Exception($"Could not find sheet with ID {_sheetID}");

			SpreadsheetsResource.ValuesResource.GetRequest sheetCellsGetRequest = sheetsService.Spreadsheets.Values.Get(_spreadsheetId, foundSheet.Properties.Title);
			ValueRange rg = await sheetCellsGetRequest.ExecuteAsync();

			// No data and missing header
			if (rg.Values == null || rg.Values.Count <= _settings.startY)
				return new GameDataSpreadsheetFetchInfo<TDATA>();

			// Record last column index
			int xEnd = rg.Values[_settings.startY].Count - 1;

			var outputData = new GameDataSpreadsheetFetchInfo<TDATA>()
			{
				data = new Dictionary<string, TDATA>(),
				rawData = new Dictionary<string, JObject>(),
				dataRows = new Dictionary<string, int>(),
				header = new Dictionary<string, int>()
			};

			JObject defaultData = JObject.FromObject(new TDATA());
			
			// Read header columns
			List<string> headers = new List<string>();
			for (int i = _settings.startX; i < rg.Values[_settings.startY].Count; i++)
			{
				object columnData = rg.Values[_settings.startY][i];

				// End when a column is empty or doesn't exist.
				if (columnData == null || (string)columnData == string.Empty)
				{
					xEnd = i - 1;
					break;
				}

				if (!headers.Contains((string)columnData))
					outputData.header[(string)columnData] = i;
				
				headers.Add((string)columnData);
			}

			// Allow ID to be at a different column
			if(!outputData.header.TryGetValue(_settings.keyHeaderName, out int KeyID))
			{
				KeyID = _settings.startX;
				Debug.LogError($"Cannot find ID column header in spreadsheet: '{_settings.keyHeaderName}' defaulting to first column as ID column.");
			}

			// Read data (read past header)
			for (int i = _settings.startY + 1; i < rg.Values.Count; i++)
			{
				IList<object> row = rg.Values[i];

				// Empty row (or missing key) assume end of readable data
				if (row.Count <= _settings.startX || row[_settings.startX] == null || (string)row[_settings.startX] == string.Empty)
					break;

				JObject dataObj = new JObject();
				JObject rawDataObj = new JObject();

				// Get key, store which row this key was found for updating data
				string dataKey = (string)row[KeyID];
				outputData.dataRows[dataKey] = i;

				// Keep track of processed headers so we don't process them multiple times.
				HashSet<string> processedHeaders = new HashSet<string>();

				// Get data from each column
				for (int i1 = _settings.startX; i1 <= xEnd; i1++)
				{
					// Ignore key column
					if (row.Count <= i1 || i1 == KeyID)
						continue;

					object data = row[i1];
					string key = headers[i1 - _settings.startX];

					// Ignore duplicate header cells, use the first one found.
					if (!processedHeaders.Add(key))
						continue;

					switch (data)
					{
						case double dVal:
							rawDataObj[key] = dataObj[key] = dVal;
							break;
						case bool bVal:
							rawDataObj[key] = dataObj[key] = bVal;
							break;

						// For string, try to parse into whatever data it expects
						case string sVal:
						{
							rawDataObj[key] = sVal;
							if (defaultData.TryGetValue(key, out JToken tok))
							{
								if (tok.Type == JTokenType.String || tok.Type == JTokenType.Null)
									dataObj[key] = sVal;
								else
								{
									// Can't parse empty string value
									if (string.IsNullOrEmpty(sVal))
										continue;

									switch (tok.Type)
									{
										case JTokenType.Boolean:
											dataObj[key] = bool.Parse(sVal);
											break;
										case JTokenType.Float:
											dataObj[key] = float.Parse(sVal);
											break;
										case JTokenType.Integer:
											dataObj[key] = int.Parse(sVal);
											break;
										default:
											throw new Exception(
												$"Unsupported game data type found {key} expecting bool, string, int or float, Type: {tok.Type}");
									}
								}
							}

							break;
						}
					}
				}

				outputData.data.Add(dataKey, dataObj.ToObject<TDATA>());
				outputData.rawData.Add(dataKey, rawDataObj);
			}

			return outputData;
		}

		/// <summary>
		/// Updates values in a google spreadsheet using the input
		///
		/// _data dictionary.
		/// Each field in the data T will have a column, and the key will also have a column.
		/// This will not modify the formatting of the spreadsheet, it will only replace the values in each cell.
		/// Cell formatting will be copied from the first data row into any newly created rows.
		/// </summary>
		/// <param name="_spreadsheetId">ID of the spreadsheet to edit</param>
		/// <param name="_sheetID">ID of the sheet/ tab to edit</param>
		/// <param name="_data">Data to update.</param>
		/// <param name="_regenerate">If true, the spreadsheet will be regenerated - data will be cleared and re-input.</param>
		/// <param name="_updateExisting">Whether to update existing keys with new data, keys won't be removed.</param>
		/// <param name="_settings">Settings for how to read and update the data.</param>
		/// <param name="_ignoreExistingKeyCase">When replacing existing keys, whether to ignore the case of the existing key (it will be overwritten with the correct case)</param>
		public static async Task UpdateGameDataSpreadsheetAsync<TDATA>(string _spreadsheetId, int? _sheetID, Dictionary<string, TDATA> _data, bool _regenerate = false, bool _updateExisting = false, GameDataSpreadsheetSettings _settings = null) where TDATA : new()
		{
			try
			{
				_settings ??= GameDataSpreadsheetSettings.Default;

				// Get existing data (so we can clear it before regenerating, or use to to only add new data)
				var existingData = await FetchGameDataSpreadsheetAsync<TDATA>(_spreadsheetId, _sheetID, _settings);

				// No header data exists, force regenerate from scratch
				if (existingData?.header == null || existingData.header.Count == 0)
					_regenerate = true;

				GoogleCredential credentials = GetAuthenticationCredentials(Scope.Spreadsheets);
				SheetsService sheetsService = new SheetsService(new BaseClientService.Initializer()
				{
					HttpClientInitializer = credentials,
					ApplicationName = ServiceApplicationName,
				});

				var rowData = new List<RowData>(_data.Count + 1);

                // Add headers - only add row when creating from scratch
				JObject headerObj = JObject.FromObject(new TDATA());

				// Remove notes from header
				foreach (var keyName in headerObj.Properties().Select((p)=>p.Name).ToList())
					if (keyName.EndsWith(NoteSpreadsheetDataSuffix))
						headerObj.Remove(keyName);

				if (_regenerate)
				{
					var headerCells = new List<CellData>(headerObj.Count + 1);
					headerCells.Add(new CellData() { UserEnteredValue = new ExtendedValue() { StringValue = _settings.keyHeaderName } });
					foreach (var data in headerObj)
					{
						headerCells.Add(new CellData() { UserEnteredValue = new ExtendedValue() { StringValue = data.Key } });
					}

					rowData.Add(new RowData() { Values = headerCells });
				}

				int xEndID = _settings.startX + headerObj.Count;
				
				if(existingData.header != null)
					foreach (var head in existingData.header)
					{
						if (head.Value > xEndID)
							xEndID = head.Value;
					}

				Dictionary<string, string[]> cachedDropdownValues = new Dictionary<string, string[]>();

				List<string> keys = new List<string>(_data.Keys);

				if(_settings.sortAlphabetical)
				{
					keys.Sort();
				}

				// Try to match existing data header locations, if not found (like when regenerating) then create new header 
				List<string> headers = new List<string>();

				if (existingData?.header == null || existingData.header.Count == 0)
				{
					// No existing header, create new with key as the first column
					headers.Add(_settings.keyHeaderName);

					foreach(var headerPair in headerObj)
						headers.Add(headerPair.Key);
				}
				else
				{
					// Use existing header, store column ID of headers so we know where to write data.
					foreach(var headerPair in existingData.header)
					{
						int relativeHeaderID = headerPair.Value - _settings.startX;

						while (headers.Count < relativeHeaderID + 1)
							headers.Add(string.Empty);

						headers[relativeHeaderID] = headerPair.Key;
					}
				}

				// Find relative column ID of header
				int headerID = headers.FindIndex((str) => str == _settings.keyHeaderName);
				if (headerID == -1)
					throw new Exception($"Unable to find key header '{_settings.keyHeaderName}'");

				var Requests = new List<Request>();

				// Lowercase key names
				Dictionary<string, string> existingKeysLC =
					existingData.data != null ? existingData.data.ToDictionary((k) => k.Key.ToLower(), (v) => v.Key) : new Dictionary<string, string>();

				int GetExistingRow(string _key, out string _existingKey)
				{
					_existingKey = null;
					
					if (!_settings.ignoreKeyCase)
					{
						if (existingData.dataRows.TryGetValue(_key, out int existingRow))
							return existingRow;
						
						return -1;
					}

					if (existingKeysLC.TryGetValue(_key.ToLower(), out string existingKey))
					{
						_existingKey = existingKey;
							
						if (existingData.dataRows.TryGetValue(existingKey, out int row))
							return row;
					}

					return -1;
				}
				
				// Add data
				foreach (var key in keys)
				{
					var cellData = new List<CellData>();

					JObject dataObj = JObject.FromObject(_data[key]);

					// If we're not regenerating or updating existing keys, skip existing.
					if (!_updateExisting && !_regenerate && existingData.data.ContainsKey(key))
						continue;

					int existingRowID = GetExistingRow(key, out string existingKey);
					
					for (int i = 0; i < headers.Count; i++)
					{
						string header = headers[i];
						if (i == headerID)
						{
							cellData.Add(new CellData() { UserEnteredValue = new ExtendedValue() { StringValue = key }, });
						}
						else
						{
							// Header doesn't exist in exported data
							if (!dataObj.TryGetValue(header, out JToken val))
							{
								// This row has existing data, copy from existing data if we can
								if (_updateExisting && !_regenerate && existingRowID != -1)
								{
									if(existingData.rawData.TryGetValue(existingKey, out JObject existingObj))
										existingObj.TryGetValue(header, out val);
								}
							}
							
							// Get note if it exists
							dataObj.TryGetValue($"{header}{NoteSpreadsheetDataSuffix}", out JToken noteToken);
							string note = noteToken?.ToString();
							
							if (val == null)
							{
								// No value, add empty cell
								cellData.Add(new CellData(){Note = note});
							}
							else
							{
								switch (val.Type)
								{
									case JTokenType.String:

										if (_settings.enumMappings != null && _settings.enumMappings.TryGetValue(header,
											    out System.Func<string[]> dropdownValuesGetter))
										{
											if (!cachedDropdownValues.ContainsKey(header))
												cachedDropdownValues[header] = dropdownValuesGetter();

											string[] dropdownValues = cachedDropdownValues[header];

											// render as an enum dropdown if dropdown type is specified
											var enumValues = new List<ConditionValue>();

											foreach (var enValue in dropdownValues)
											{
												enumValues.Add(new ConditionValue()
												{
													UserEnteredValue = enValue
												});
											}

											cellData.Add(new CellData()
											{
												UserEnteredValue = new ExtendedValue() { StringValue = (string)val },
												DataValidation = new DataValidationRule()
												{
													Condition = new BooleanCondition()
													{
														Type = "ONE_OF_LIST",
														Values = enumValues
													},
													ShowCustomUi = true,
												},
												Note = note
											});
										}
										else
										{
											cellData.Add(new CellData()
											{
												UserEnteredValue = new ExtendedValue() { StringValue = (string)val },
												Note = note
											});
										}

										break;
									case JTokenType.Integer:
									case JTokenType.Float:
										cellData.Add(new CellData()
											{ UserEnteredValue = new ExtendedValue() { NumberValue = ((int)val) }, Note = note });
										break;
									case JTokenType.Boolean:
										cellData.Add(new CellData()
										{
											DataValidation = new DataValidationRule()
											{
												Condition = new BooleanCondition()
												{
													Type = "BOOLEAN"
												},
												ShowCustomUi = true

											},
											UserEnteredValue = new ExtendedValue() { BoolValue = ((bool)val) },
											Note = note
										});
										break;
									default:
										throw new Exception($"Cannot write data type to google sheet: {val.Type}");
								}
							}
						}
					}
					
					if (_updateExisting && !_regenerate && existingRowID != -1)
					{
						// Update single row on existing line
						Requests.Add(new Request()
						{
							UpdateCells = new UpdateCellsRequest()
							{
								Range = new GridRange()
								{
									SheetId = _sheetID,
									StartColumnIndex = _settings.startX,
									StartRowIndex = existingRowID,
									EndColumnIndex = _settings.startX + cellData.Count + 1,
									EndRowIndex = existingRowID + 1
								},
								// CellData fields, 'userEnteredValue' means it will only update the value of the field leaving any formatting alone.
								Fields = "userEnteredValue,dataValidation",
								Rows = new List<RowData>()
								{
									new RowData()
									{
										Values = cellData
									}
								}
							}
						});
					}
					else
					{
						// Add to end
						rowData.Add(new RowData()
						{
							Values = cellData
						});
					}
				}

				// Skip existing data if only adding
				int yDataStart = _settings.startY;
				if(!_regenerate)
					yDataStart += existingData.data.Count + 1;

				// Remove existing data if starting from scratch (in case number is different)
				if(_regenerate && existingData.data != null)
				{
					List<RowData> clearData = new List<RowData>();
					
					for (int i = 0; i < existingData.data.Count + 1; i++)
					{
						RowData rd = new RowData
						{
							Values = new List<CellData>()
						};

						for (int i1 = 0; i1 < headerObj.Count; i1++)
							rd.Values.Add(new CellData() { UserEnteredValue = new ExtendedValue() { StringValue = String.Empty } });

						clearData.Add(rd);
					}

					Requests.Add(new Request()
					{
						UpdateCells = new UpdateCellsRequest()
						{
							Range = new GridRange()
							{
								SheetId = _sheetID,
								StartColumnIndex = _settings.startX,
								StartRowIndex = _settings.startY,
								EndColumnIndex = xEndID + 1,
								EndRowIndex = _settings.startY + existingData.data.Count + 1
							},
							// CellData fields, 'userEnteredValue' means it will only update the value of the field leaving any formatting alone.
							Fields = "userEnteredValue,dataValidation,note",
							Rows = clearData
						}
					});
				}

				// Copy format of first data row (doesn't copy values)
				if(rowData.Count != 0)
				{
					Requests.Add(new Request()
					{
						CopyPaste = new CopyPasteRequest()
						{
							// Source is first data row
							Source = new GridRange()
							{
								SheetId = _sheetID,
								StartColumnIndex = _settings.startX,
								EndColumnIndex = xEndID + 1,
								StartRowIndex = _settings.startY + 1,
								EndRowIndex = _settings.startY + 2
							},

							// Destination is every newly added row
							Destination = new GridRange()
							{
								SheetId = _sheetID,
								StartColumnIndex = _settings.startX,
								EndColumnIndex = xEndID + 1,
								StartRowIndex = yDataStart,
								EndRowIndex = yDataStart + rowData.Count
							},

							// https://developers.google.com/sheets/api/reference/rest/v4/spreadsheets/request#PasteType
							// only copy/ paste formatting
							PasteType = "PASTE_FORMAT",
							PasteOrientation = "NORMAL"
						}
					});
				}

				// Add new data, including header if regenerating.
				Requests.Add(new Request()
				{
					UpdateCells = new UpdateCellsRequest()
					{
						Range = new GridRange()
						{
							SheetId = _sheetID,
							StartColumnIndex = _settings.startX,
							EndColumnIndex = xEndID + 1,
							StartRowIndex = yDataStart,
							EndRowIndex = yDataStart + rowData.Count
						},
						// CellData fields, 'userEnteredValue' means it will only update the value of the field leaving any formatting alone.
						Fields = "userEnteredValue,dataValidation,note",
						Rows = rowData
					},
				});


				// Construct update request
				BatchUpdateSpreadsheetRequest req = new BatchUpdateSpreadsheetRequest() { Requests = Requests };

				// Send request
				SpreadsheetsResource.BatchUpdateRequest spreadSheetUpdateRequest = sheetsService.Spreadsheets.BatchUpdate(req, _spreadsheetId);
				BatchUpdateSpreadsheetResponse spreadsheet = await spreadSheetUpdateRequest.ExecuteAsync();
			}
			catch (System.Exception exception)
			{
				UnityEngine.Debug.LogException(exception);
			}
		}
	}
}