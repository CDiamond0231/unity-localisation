
// Which Row Contains the Identity of the Language we are validating
var LanguageCellRowId = 1; 
var EnglishTranslationColId = 3;

var IssuesFoundSeparator = ";   ";
var ValidationOK = "//";
var ValidationBad = "\\\\";

var CHARACTER_OK = 0;
var CHARACTER_NOT_FOUND = -1;
var UNCOMMON_CHARACTER = -2;

var EMPTY_CELL_COLOUR = "#FFFFFF";
var EMPTY_CELL_FONT_COLOUR = 'black';

var TRANSLATION_OK_COLOUR = "#00FF64";
var TRANSLATION_BAD_COLOUR = "#FF6400";

var ENGLISH_CELL_COLOUR = "#434343";
var ENGLISH_CELL_FONT_COLOUR = 'white';

var DUPLICATE_ENTRY_COLOUR = "#C6CB43";
var DUPLICATE_ENTRY_FONT_COLOUR = 'black';


// Removes Specified English Words from the validation check. These words are OK to use in the language.
var AcceptableWordRemovers = 
{
	"JAPANESE":    RemoveValidEnglishTextFromJapaneseValidationCheck,
};

// Validates each character in the text per Language
var CharacterValidators = 
{
	"ARABIC":      ValidateArabicCharacter,
	"BRAZILIAN_PORTUGUESE": ValidateCommonAsciiCharacter,
	"CHINESE_SIMPLIFIED":   ValidateChineseSimplifiedCharacter,
	"CHINESE_TRADITIONAL":  ValidateChineseTraditionalCharacter,
	"FRENCH":      ValidateCommonAsciiCharacter,
	"GERMAN":      ValidateCommonAsciiCharacter,
	"ITALIAN":     ValidateCommonAsciiCharacter,
	"JAPANESE":    ValidateJapaneseCharacter,
	"KOREAN":      ValidateKoreanCharacter,
	"RUSSIAN":     ValidateRussianCharacter,
	"SPANISH":     ValidateCommonAsciiCharacter,
	"THAI":        ValidateThaiCharacter
};


function onEdit(e) 
{
	//SpreadsheetApp.getUi().alert('Do thing');
	// Set a comment on the edited cell to indicate when it was changed.
	var range = e.range;
	
	var sheetData = SpreadsheetApp.getActiveSheet();
    var startRangeCol = range.getColumn();
    var startRangeRow = range.getRow();
    var lastRangeColumn = range.getLastColumn();
    var lastRangeRow = range.getLastRow();
    var lastSheetCol = sheetData.getLastColumn();
	var lastSheetRow = sheetData.getLastRow();
	var allEnglishStrings = null;
	
	var hasUnresolvedCells = false;
	var languagesInUse = new Array((lastRangeColumn - startRangeCol) + 1);

	for (var currentCol = startRangeCol; currentCol <= lastRangeColumn; ++currentCol)
	{
		if (currentCol < EnglishTranslationColId)
		{
			// Don't do any validation on the 'Context' and 'Loc ID' columns. We don't need to check them.
			continue;
		}
	
		var languageCell = sheetData.getRange(LanguageCellRowId, currentCol);
		var languageUsed = languageCell.getValue().toUpperCase();
		var languageInUseIndex = (currentCol - startRangeCol);
		languagesInUse[languageInUseIndex] = languageUsed;
		
		for (var currentRow = startRangeRow; currentRow <= lastRangeRow; ++currentRow)
		{
			if (currentRow == LanguageCellRowId)
			{
				// Currently Editing either the Language Cell. Set Cell Colour to White/Clear Notes. Every single row below this must be re-evaluated also
				var cellAt = sheetData.getRange(currentRow, currentCol);
				cellAt.setBackground(EMPTY_CELL_COLOUR);
				cellAt.setNote("");
				
				if (languageUsed == "ENGLISH")
				{
					var englishCells = sheetData.getRange(LanguageCellRowId + 1, currentCol, lastSheetRow - 1, 1);
					if (allEnglishStrings == null)
					{
						allEnglishStrings = englishCells.getValues();
					}
					resolveEnglishCells(englishCells, allEnglishStrings, -1);
					continue;
				}
				
				// Reevaluate ALL cells associated with this Language that we just edited (This row, basically)
				var startEditRow = currentRow + 1;
				var totalRowsToEdit = (lastSheetRow - startEditRow) + 1
				var cells = sheetData.getRange(startEditRow, currentCol, totalRowsToEdit, 1);
				var values = cells.getValues();
				var results = resolveCellsLocalisationValidation(values, [languageUsed]);
				cells.setNotes(results[0]);
				cells.setBackgrounds(results[1]);

				// All rows need to be reevaluated. Break here so we don't double up on checking the same rows again
				break;
			}
			
			if (languageUsed == "ENGLISH")
			{
				// Whenever an English Cell is modified, all associated (non-English) cells must be reevaluated
				var startEditCol = currentCol + 1;
				var totalColsToEdit = (lastSheetCol - startEditCol) + 1
				var cells = sheetData.getRange(currentRow, startEditCol, 1, totalColsToEdit);
				var values = cells.getValues();
				var languagesUsed = new Array(totalColsToEdit);
				var languageCells = sheetData.getRange(LanguageCellRowId, startEditCol, 1, totalColsToEdit);
                var languagesUsed = languageCells.getValues()[0].map(function(s){ return s.toUpperCase() });
				var results = resolveCellsLocalisationValidation(values, languagesUsed);
				cells.setNotes(results[0]);
				cells.setBackgrounds(results[1]);
				
				if (allEnglishStrings == null)
				{
					allEnglishStrings = sheetData.getRange(LanguageCellRowId, currentCol, lastSheetRow, 1).getValues();
				}

				var cellAt = sheetData.getRange(currentRow, currentCol);   
				resolveEnglishCells(cellAt, allEnglishStrings, 0);
				continue;
			}
			
			// This is not a language cell nor is it associated with ENGLISH. Do the Check!
			hasUnresolvedCells = true;
		}
	}

	if (hasUnresolvedCells)
	{
		var rangeValues = range.getValues();
		var results = resolveCellsLocalisationValidation(rangeValues, languagesInUse);
		range.setNotes(results[0]);
		range.setBackgrounds(results[1]);
	}
}

function resolveEnglishCells(englishCellRange, allEnglishStrings, cellIdOffset)
{
    var startRangeRow = englishCellRange.getRow();
	var lastRangeRow = englishCellRange.getLastRow();
	var englishStringsCount = allEnglishStrings.length;

	var rowsCount = (lastRangeRow - startRangeRow) + 1;
	var notes = new Array(rowsCount);
	var backgroundColours = new Array(rowsCount);
	var fontColours = new Array(rowsCount);

	var upcasedEnglishCellValues = englishCellRange.getValues().map(function(s){ return s[0].toUpperCase() });
	var upcasedEnglishStrings = allEnglishStrings.map(function(s){ return s[0].toUpperCase() });

	for (var englishRowIndex = 0; englishRowIndex < rowsCount; ++englishRowIndex)
	{
		var textLength = upcasedEnglishCellValues[englishRowIndex].length;
		if (textLength == 0)
		{
			// Nothing to do. This is an empty cell
			backgroundColours[englishRowIndex] = [EMPTY_CELL_COLOUR];
			fontColours[englishRowIndex] = [EMPTY_CELL_FONT_COLOUR];
			notes[englishRowIndex] = [""];
			continue;
		}
		
		// See if there is a duplicate entry. Keep the Entry. But mark as Duplicate Entry if another is found
		var sameEntryCell = -1;
		var thisRowAsCellID = (startRangeRow + englishRowIndex) + cellIdOffset;
		for (var rowToCheckAsCellID = 0; rowToCheckAsCellID < englishStringsCount; ++rowToCheckAsCellID)
		{
			if (rowToCheckAsCellID + 1 == thisRowAsCellID)
			{
				// Don't compare against self.
				continue;
			}
			if (upcasedEnglishStrings[rowToCheckAsCellID] == upcasedEnglishCellValues[englishRowIndex])
			{
				sameEntryCell = rowToCheckAsCellID + 1; // Plus Two because 'Cell Row' is an Index value' AND we are not including the Language Row ID (first row)
				break;
			}
		}
		
		var note = "";
		var suggestedLocId = upcasedEnglishCellValues[englishRowIndex].replace(/(\[\d+?\])/g, 'x')      // Replace [0] [1] etc.
																	  .replace(/\s/g, '_')              // Replace Spaces with '_'
										.replace(/(`|~|!|@|#|\$|%|\^|&|\*|\(|\)|\[|\]|\||\\|\||:|;|\'|\"|<|,|\.|>|\?|\/|\+|\-)/g, ''); // Replace all other Special Characters
		
		while (suggestedLocId.length > 1)
		{
			if (suggestedLocId[0].match(/[0-9]|_/g))
			{
				// First Character is a Digit or an Underscore. Keep removing first character until we have an alphabetical value.
				suggestedLocId = suggestedLocId.substr(1);
			}
			else
			{
				note += "Suggested Loc ID:\n" + suggestedLocId + "\n\n";
				break;
			}
		}
		if (sameEntryCell != -1)
		{
			backgroundColours[englishRowIndex] = [DUPLICATE_ENTRY_COLOUR];
			fontColours[englishRowIndex] = [DUPLICATE_ENTRY_FONT_COLOUR];
			note += 'Duplicate Found at Cell Row: ' + sameEntryCell;
		}
		else
		{
			backgroundColours[englishRowIndex] = [ENGLISH_CELL_COLOUR];
			fontColours[englishRowIndex] = [ENGLISH_CELL_FONT_COLOUR]; 
		}
	
		notes[englishRowIndex] = [note];
	}
	
	englishCellRange.setBackgrounds(backgroundColours);
	englishCellRange.setFontColors(fontColours);
	englishCellRange.setNotes(notes);		
}

function resolveCellsLocalisationValidation(values, languagesUsed)
{
	var rowsCount = values.length;
	var colsCount = values[0].length;
	
	var validationResolution = new Array(rowsCount);
	var colourOutput = new Array(rowsCount);
	

	for (var rowIndex = 0; rowIndex < rowsCount; ++rowIndex)
	{
		var rowValidationResolution = new Array(colsCount);
		var rowColourOutput = new Array(colsCount);
		for (var colIndex = 0; colIndex < colsCount; ++colIndex)
		{
			var value = values[rowIndex][colIndex];
			if (value == "")
			{
				// Nothing to Validate. Set Cell Colour to White/Clear Notes
				rowValidationResolution[colIndex] = "";
				rowColourOutput[colIndex] = EMPTY_CELL_COLOUR;
				continue;
			}

			var output = CheckValidLocalisation(value, languagesUsed[colIndex]);
			if (output != "")
			{
				// Issues found with the Localisation; Output issues to Note Field; and Change Cell Colour to Red
				rowValidationResolution[colIndex] = output;
				rowColourOutput[colIndex] = TRANSLATION_BAD_COLOUR;
			}  
			else
			{
				// No issues found with localisation; Clear Note field (no issues here) and Set Cell Colour to Green.
				rowValidationResolution[colIndex] = "";
				rowColourOutput[colIndex] = TRANSLATION_OK_COLOUR;  
			}
		}
		validationResolution[rowIndex] = rowValidationResolution;
		colourOutput[rowIndex] = rowColourOutput;
	}
	
	return [validationResolution, colourOutput]
}


function CheckValidLocalisation(text, languageUsed) 
{
	if ((languageUsed in CharacterValidators) == false)
	{
		var availableLanguages = Object.keys(CharacterValidators).join("\n  * ");
		return languageUsed + " does not have a defined Validator. Please ensure this cell is classified under one of the following languages:\n  * " + availableLanguages;
	}
	
	var issuesFound = "";
	var validatorFunc = CharacterValidators[languageUsed];

    // Substring Identifiers need to be removed. They will fail validation because they are not Chinese/Korean/Russian characters, etc. But they ARE valid chars
	var essentialTextOnly = text.replace(/(\[\d+?\])/g, '').replace('\n', '');
	
	if (languageUsed in AcceptableWordRemovers)
	{
		var acceptableWordRemoverFunc = AcceptableWordRemovers[languageUsed];
		essentialTextOnly = acceptableWordRemoverFunc(essentialTextOnly);
	}
  
	// Getting Unique Chars so that we don't report multiple errors for the same character if multiple exist (makes it neater)
	var uniqueChars = [];
	for (var i = 0; i < essentialTextOnly.length; i++)
	{
		var character = essentialTextOnly.charAt(i);
		if (uniqueChars.indexOf(character) == -1)
		{
			uniqueChars.push(character);
		}
	}

	for (var i = 0; i < uniqueChars.length; i++) 
	{
		var character = uniqueChars[i];
		var validationOutput = validatorFunc(character);

		// Validation Either returns a String or ValidationCode. Should only return a string if the result is a special case.
        //return validationOutput;
        if (typeof validationOutput === 'string')
        {
			if (validationOutput != "")
			{
				if (issuesFound != "")
				{
					issuesFound += IssuesFoundSeparator;
				}				
				issuesFound += validationOutput;
			}
			continue;
		}

		if (validationOutput == CHARACTER_OK)
		{
			continue;
		}

		if (issuesFound != "")
		{
			issuesFound += ";\n";
		}

		var sanitised = character.replace(" ", "space").replace("\r", "space").replace("\x0b", "space")
								 .replace("\t", "tab");

		if (validationOutput == UNCOMMON_CHARACTER)
		{
			issuesFound += sanitised + "=UncommonCharacter";
		}
		else
		{
			issuesFound += sanitised + "=InvalidCharacter";
		}
	}
  
	if (issuesFound != "")
	{
		return issuesFound;
	}
	return "";
}

function RemoveValidEnglishTextFromJapaneseValidationCheck(text)
{ 
	return text.replace(/\bok\b/gi, "")		// 'OK' is commonly used in Japanese
			   .replace(/\bhello\b/gi, "")	// 'Hello' used ocassionally
			   .replace(/\bbye\b/gi, "")	// 'Bye-Bye' commonly used
			   .replace(/\bbad\b/gi, "")	// 'Bad' commonly used in japanese Games
			   .replace(/\bgood\b/gi, "")	// 'Good' commonly used in Japanese Games
			   .replace(/\bexcellent\b/gi, "")  // 'Excellent' commonly used in Japanese games
			   .replace(/\bperfect\b/gi, "");	// 'Perfect' commonly used in Japanese games.
}

function GetUnicodeValue(character)
{
	// Making this a function in case we need to support BMP (basic multilingual plane) later on
	return character.charCodeAt(0);
}

function CheckCommonPunctuation(character, includeSpaces)
{
	if (includeSpaces)
	{
		if (character == ' ' || character == '\r' || character == '\x0b') 
		{
			return CHARACTER_OK;
		}
	}

	if (character == ',' || character == '.')
		return CHARACTER_OK;

	if (character == '!' || character == '?')
		return CHARACTER_OK;

	if (character == '+')
		return CHARACTER_OK;

	var unicode = GetUnicodeValue(character);
	if (unicode == 0xFF01) // ! FullWidth Ver
		return CHARACTER_OK;

	if (unicode == 0xFF0C) // , Fullwidth Ver
		return CHARACTER_OK;

	if (unicode == 0xFF1F)	// ? Fullwidth Ver
		return CHARACTER_OK;

	return CHARACTER_NOT_FOUND;
}

function IsArabicNumericalChar(character)
{
	if(typeof character === 'number')
		return character >= 48 && character <= 57;
	
	var charCode = GetUnicodeValue(character);
	return charCode >= 48 && charCode <= 57;
}

function ValidateCommonAsciiCharacter(character)
{  
	const StandardCharSetMinVal = 32;
	const StandardCharSetMaxVal = 126;
	const ExtendedCharSetMinVal = 128;
	const ExtendedCharSetMaxVal = 254;
	
	var asciiCode = GetUnicodeValue(character);
	if (asciiCode >= StandardCharSetMinVal && asciiCode <= StandardCharSetMaxVal)
	{
		// Using Standard Char Set
		return CHARACTER_OK;
	}
	
	if (asciiCode >= ExtendedCharSetMinVal && asciiCode <= ExtendedCharSetMaxVal)
	{
		// Using characters that are using an accent. Rarely used in English but common in European languages.
		// This function is separated so we have a clear point to debug later.
		return CHARACTER_OK;
	}
	
	return CHARACTER_NOT_FOUND;
}

function ValidateArabicCharacter(character)
{  
	var unicode = GetUnicodeValue(character);
	
	// Arabic Alphabet
	if (unicode >= 0x0600 && unicode <= 0x06FF)
		return CHARACTER_OK;

	// Arabic Supplement
    if (unicode >= 0x0750 && unicode <= 0x077F)
		return CHARACTER_OK;

	// Arabic Extended - A
    if (unicode >= 0x08A0 && unicode <= 0x08FF)
		return CHARACTER_OK;

	// Arabic Pres. Forms A
    if (unicode >= 0x08FF && unicode <= 0xFDFF)
		return CHARACTER_OK;

	// Arabic Pres. Forms B
    if (unicode >= 0xFE70 && unicode <= 0xFEFF)
		return CHARACTER_OK;

	// There are also Numerical Characters... I don't think we'll need to worry about those though.		
	if (IsArabicNumericalChar(character))
		return CHARACTER_OK;

	return CheckCommonPunctuation(character, true);
}

function ValidateChineseSimplifiedCharacter(character)
{  
    var unicode = GetUnicodeValue(character);

	if (unicode >= 0x4E00 && unicode <= 0x9FA0)
		return CHARACTER_OK;

	// CJK Symbols and Punctuation
    if (unicode >= 0x3000 && unicode <= 0x303F)
		return CHARACTER_OK;

	return CheckCommonPunctuation(character, false);
}

function ValidateChineseTraditionalCharacter(character)
{ 
	// Info here came from: https://stackoverflow.com/questions/9166130/what-are-the-upper-and-lower-bound-for-chinese-char-in-utf-8
	var unicode = GetUnicodeValue(character);
  
    // Common Characters
    if (unicode >= 0x4E00 && unicode <= 0x9FFF)
    	return CHARACTER_OK;
      
    // Rare Characters... But in use
    if (unicode >= 0x3400 && unicode <= 0x4D8f)
		return CHARACTER_OK;
		
	// CJK Symbols and Punctuation
    if (unicode >= 0x3000 && unicode <= 0x303F)
		return CHARACTER_OK;
      
    // Ideographs Supplement
    if (unicode >= 0xF900 && unicode <= 0x2FAFF)
        return CHARACTER_OK;
	
	// Chinese also uses Arabic Numerals
	if (IsArabicNumericalChar(unicode))
        return CHARACTER_OK;
  
    // const OtherAcceptedChars = [ '?', '?', '?' ]
    // for (var i = 0; i < OtherAcceptedChars.length; i++)
    // {
    //     if (character == OtherAcceptedChars[i])
    //       return CHARACTER_OK;
    // }
  
	return CheckCommonPunctuation(character, false);
}

function ValidateJapaneseCharacter(character)
{  
	var unicode = GetUnicodeValue(character);

	// Japanese Style Punctuation
	if (unicode >= 0x3000 && unicode <= 0x303f)
		return CHARACTER_OK;

	// Japanese Hiragana
	if (unicode >= 0x3040 && unicode <= 0x309C)
		return CHARACTER_OK;

	// Japanese Katakana
	if (unicode >= 0x30a0 && unicode <= 0x30ff)
		return CHARACTER_OK;

	// Romaji (Roman Alphabet (Same as English) but drawn slightly differently) and Half-Width Katakana
	if (unicode >= 0xff00 && unicode <= 0xffef)
		return CHARACTER_OK;

	// Common and Some Uncommon Kanji (Can probably remove this check if we are aiming for Children as they will have difficulty reading Kanji).
	if (unicode >= 0x4e00 && unicode <= 0x9faf)
		return CHARACTER_OK;	

	if (IsArabicNumericalChar(character))
		return CHARACTER_OK;

	return CheckCommonPunctuation(character, false);
}

function ValidateKoreanCharacter(character)
{  
	var unicode = GetUnicodeValue(character);

	// Korean Alphabet
	if (unicode >= 0x3131 && unicode <= 0xE8C9)
		return CHARACTER_OK;

	if (IsArabicNumericalChar(character))
		return CHARACTER_OK;

	return CheckCommonPunctuation(character, true);
}

function ValidateRussianCharacter(character)
{  
	var unicode = GetUnicodeValue(character);

	// Cyrillic
	if (unicode >= 0x400 && unicode <= 	0x4FF)
		return CHARACTER_OK;

	// Cyrillic Supplement
	if (unicode >= 0x500 && unicode <= 	0x52F)
		return CHARACTER_OK;

	// Cyrillic Extended - A
	if (unicode >= 0x2DE0 && unicode <= 0x2DFF)
		return CHARACTER_OK;

	// Cyrillic Extended - B
	if (unicode >= 0xA640 && unicode <= 0xA69F)
		return CHARACTER_OK;

	// Cyrillic Extended - C
	if (unicode >= 0x1C80 && unicode <= 0x1C88)
		return CHARACTER_OK;

	// Phonetic Extensions
	if (unicode >= 0x1D00 && unicode <= 0x1D7F)
		return CHARACTER_OK;

	// Combining Half-Marks
	if (unicode >= 0xFE20 && unicode <= 0xFE2F)
		return CHARACTER_OK;

	if (IsArabicNumericalChar(character))
		return CHARACTER_OK;

	return CheckCommonPunctuation(character, true);
}

function ValidateThaiCharacter(character)
{  
	var unicode = GetUnicodeValue(character);

	// Thai Script
	if (unicode >= 0xE01 && unicode <= 0xE5B)
		return CHARACTER_OK;

	if (IsArabicNumericalChar(character))
		return CHARACTER_OK;
	
	return CheckCommonPunctuation(character, false);
}