using UnityEngine;
using UnityEngine.Profiling;
using System;
using System.IO;
using System.Collections.Generic;

using u8 = System.Byte;
using s8 = System.SByte;
using u16 = System.UInt16;
using s16 = System.Int16;
using u32 = System.UInt32;
using s32 = System.Int32;
using u64 = System.UInt64;
using s64 = System.Int64;

namespace MediaMonks
{
	[ExecuteInEditMode]
    public class OpenFontFileRead : MonoBehaviour
    {
		public string FileLocation;
		public List<OpenFontFormat.TableRecord> TableDirectory;
		public OpenFontFormat.GlyphMapHeader CharToGlyphMapping;
		public List<OpenFontFormat.EncodingRecord> EncodingRecords;
		public List<OpenFontFormat.EncodingFormats.Format4> SubTables;
		public OpenFontFormat.FontHeader FontHeader;
		public OpenFontFormat.GlyphHeader GlyphHeader;

		public bool VerifyEachTableChecksumOnEnable = true;
		public bool VerifyFullFileChecksumOnEnable = true;

		public OpenFontFormat.MaximumProfile Maximums;

		private Int64 _maxFileSize = 100000000;
		private OpenFontFormat.EncodingFormats.Format4 _selectedEncoding;
		private s64 _selectedEncodingIDRangeOffsetLocation = 0;// in bytes

		private u8[] _fontFileBuffer;
		private int _indexToLocationIndex = -1;
		private int _glyphDataIndex = -1;
		private int _maximumProfileIndex = -1;

		private void OnEnable()
		{
			TableDirectory = new List<OpenFontFormat.TableRecord>(); 

			string fullPath = RelativeToFullPath(FileLocation);
			if (!File.Exists(fullPath))
			{
				Debug.Log("No file found at " + fullPath, this);
				return;
			}

			#region Read entire file
			Profiler.BeginSample("Read Entire File.");
			Int64 offsetInBuffer = 0;
			{
				IntPtr fileHandle = NativeFile.Create(fullPath, NativeFile.AccessRights.Read, NativeFile.ShareMode.Read, IntPtr.Zero, NativeFile.CreationDisposition.OpenExisting, 0, IntPtr.Zero);
				if ((Int64)fileHandle == NativeFile.InvalidHandle)
				{
					Debug.LogError("Something went wrong while trying to open the file at:'" + fullPath + "'. Aborting Meta Data read.", this);
					return;
				}

				Int64 fileSize = 0;
				bool foundSize = NativeFile.GetSize(fileHandle, ref fileSize);
				if (!foundSize)
				{
					Debug.LogError("Failed to determine file size.", this);
					return;
				}
				if (fileSize > _maxFileSize)
				{
					Debug.LogError("Arbitrary file limit reached. Are you sure you're loading a Font file? This font reader loads the entire file at once, which might require rethinking if font files get massive. If you like living wild, you can just increase the maxFileSize.");
					return;
				}


				_fontFileBuffer = new byte[fileSize];
				Int64 fileOffset = 0;
				bool readSucceeded = NativeFile.ReadWithOffset(fileHandle, fileOffset, _fontFileBuffer);
				if (!readSucceeded)
				{
					Debug.LogError("Failed to read font file.");
					return;
				}

				bool succeeded = NativeFile.CloseHandle(fileHandle);
				if (!succeeded)
				{
					Debug.LogError("Something went wrong while trying to close the file handle. ", this);
				}
				fileHandle = IntPtr.Zero;
			}
			Profiler.EndSample();
			#endregion

			#region Offset Table
			var OffsetTable = new OpenFontFormat.OffsetTable();
			{
				OffsetTable.sfntVersion = BigEndianReadAndAdvance.U32(_fontFileBuffer, ref offsetInBuffer);
				if (OffsetTable.sfntVersion != OpenFontFormat.Convert.FourCharToInt("OTTO") &&
					OffsetTable.sfntVersion != 0x10000)
				{
					Debug.LogWarning("The provided file isn't a supported font file.", this);
					return;
				}
				OffsetTable.numTables		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
				OffsetTable.searchRange		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
				OffsetTable.entrySelector	= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
				OffsetTable.rangeShift		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
			}
			#endregion

			#region Table Directory
			int charToGlyphMappingIndex = -1;
			int fontHeaderIndex = -1;
			{
				for (int tableIndex = 0; tableIndex < OffsetTable.numTables; ++tableIndex)
				{
					Int64 spaceRemainingInBuffer = _fontFileBuffer.LongLength - offsetInBuffer;
					if (spaceRemainingInBuffer < 16)
					{
						Debug.LogError("Not enough space in the file to continue reading", this);
					}
					OpenFontFormat.TableRecord record = new OpenFontFormat.TableRecord();
					record.tableTag	= BigEndianReadAndAdvance.U32(_fontFileBuffer, ref offsetInBuffer);
					record.checksum	= BigEndianReadAndAdvance.U32(_fontFileBuffer, ref offsetInBuffer);
					record.offset	= BigEndianReadAndAdvance.U32(_fontFileBuffer, ref offsetInBuffer);
					record.length	= BigEndianReadAndAdvance.U32(_fontFileBuffer, ref offsetInBuffer);

					TableDirectory.Add(record);
					switch(record.tableTag)
					{
						case (s32)OpenFontFormat.TableTag.CharToGlyphMapping:
							charToGlyphMappingIndex = tableIndex;
							break;
						case (s32)OpenFontFormat.TableTag.GlyphData:
							_glyphDataIndex = tableIndex;
							break;
						case (s32)OpenFontFormat.TableTag.FontHeader:
							fontHeaderIndex = tableIndex;
							break;
						case (s32)OpenFontFormat.TableTag.IndexToLocation:
							_indexToLocationIndex = tableIndex;
							break;
						case (s32)OpenFontFormat.TableTag.MaximumProfile:
							_maximumProfileIndex = tableIndex;
							break;
					}
				}
			}
			#endregion

			#region Check for missing required records:
			if (charToGlyphMappingIndex < 0){ Debug.LogError("Char to Glyph Mapping record is missing.", this); return;  }
			if (fontHeaderIndex < 0) { Debug.LogError("Font Header record is missing", this); return;  }
			#endregion

			#region parse font header
			var fontHeaderRecord = TableDirectory[fontHeaderIndex];
			{
				FontHeader = new OpenFontFormat.FontHeader();

				s64 arrayOffset = fontHeaderRecord.offset;
				FontHeader.MajorVersion = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);
				FontHeader.MinorVersion = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);
				FontHeader.fontRevision = BigEndianReadAndAdvance.U32(_fontFileBuffer, ref arrayOffset);
				FontHeader.checksumAdjustment = BigEndianReadAndAdvance.U32(_fontFileBuffer, ref arrayOffset);
				FontHeader.magicNumber = BigEndianReadAndAdvance.U32(_fontFileBuffer, ref arrayOffset);
				FontHeader.flags = (OpenFontFormat.FontHeaderFlags)BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);
				FontHeader.unitsPerEm = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);
				FontHeader.creationTimeStamp = (s64)BigEndianReadAndAdvance.U64(_fontFileBuffer, ref arrayOffset);
				FontHeader.modificationTimeStamp = (s64)BigEndianReadAndAdvance.U64(_fontFileBuffer, ref arrayOffset);
				FontHeader.xMin = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				FontHeader.yMin = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				FontHeader.xMax = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				FontHeader.yMax = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				FontHeader.macStyle = (OpenFontFormat.MacStyleFlags)BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);
				FontHeader.lowestRecPPEM = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);
				FontHeader.fontDirectionHint = (OpenFontFormat.FontDirection)BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				FontHeader.indexToLocFormat = (OpenFontFormat.IndexToLocFormat)BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				FontHeader.glyphDataFormat = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);

				if (FontHeader.magicNumber != OpenFontFormat.FontHeader.MagicNumberExpected)
				{
					Debug.LogError("Expected magic number in the font header with a value of " + OpenFontFormat.FontHeader.MagicNumberExpected + ", but instead it was: " + FontHeader.magicNumber, this);
				}
			}
			#endregion

			#region Verify all table checksums:
			Profiler.BeginSample("Verify all table checksums");
			if (VerifyEachTableChecksumOnEnable)
			{
				for (s32 tableIndex = 0; tableIndex < TableDirectory.Count; ++tableIndex)
				{
					var tableRecord = TableDirectory[tableIndex];
					switch (tableRecord.tableTag)
					{
						case (u32)OpenFontFormat.TableTag.FontHeader:
							{
								u32 calculatedChecksum = CalculateChecksum(_fontFileBuffer, fontHeaderRecord.length, fontHeaderRecord.offset) - FontHeader.checksumAdjustment;
								if (fontHeaderRecord.checksum != calculatedChecksum)
								{
									Debug.LogError("Checksum mismatch for font header. Expected " + fontHeaderRecord.checksum + ", got " + calculatedChecksum);
									return;
								}
								break;
							}
						default:
							{
								u32 calculatedChecksum = CalculateChecksum(_fontFileBuffer, tableRecord.length, tableRecord.offset);
								if (tableRecord.checksum != calculatedChecksum)
								{
									Debug.LogError("Checksum mismatch for " + OpenFontFormat.Convert.UIntToFourChar(tableRecord.tableTag) + " table. Expected " + tableRecord.checksum + ", got " + calculatedChecksum);
									return;
								}
								break;
							}
					}
				}
			}
			Profiler.EndSample();
			#endregion

			#region Verify Checksum for entire file
			Profiler.BeginSample("Verify checksum entire file.");
			if (VerifyFullFileChecksumOnEnable)
			{
				u32 calculatedChecksum = CalculateChecksum(_fontFileBuffer, (u32)_fontFileBuffer.Length, (s64)0) - FontHeader.checksumAdjustment;
				if (OpenFontFormat.FontHeader.SubtractChecksumFromThis - calculatedChecksum != FontHeader.checksumAdjustment)
				{
					Debug.Log("File does not match checksum", this);
				}
			}
			Profiler.EndSample();
			#endregion

			#region Character to Glyph Index Mapping Table
			{
				var cmapHeaderRecord = TableDirectory[charToGlyphMappingIndex];
				offsetInBuffer = cmapHeaderRecord.offset;

				CharToGlyphMapping.version	= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
				CharToGlyphMapping.numTables	= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);

				EncodingRecords = new List<OpenFontFormat.EncodingRecord>();
				for (int tableIndex = 0; tableIndex < CharToGlyphMapping.numTables; ++tableIndex)
				{
					OpenFontFormat.EncodingRecord record = new OpenFontFormat.EncodingRecord();
					record.platformID	= (OpenFontFormat.PlatformID) BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					record.encodingID	= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					record.offset		= BigEndianReadAndAdvance.U32(_fontFileBuffer, ref offsetInBuffer);
					EncodingRecords.Add(record);
				}

				SubTables = new List<OpenFontFormat.EncodingFormats.Format4>();
				for (int subTableIndex = 0; subTableIndex < EncodingRecords.Count; ++ subTableIndex)
				{
					var encodingRecord = EncodingRecords[subTableIndex];
					var subTable = new OpenFontFormat.EncodingFormats.Format4();
					

					Int64 subTableOffset = cmapHeaderRecord.offset + encodingRecord.offset;
					subTable.format = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					if (subTable.format != 4)
					{
						SubTables.Add(subTable);
						continue;
					}
					subTable.length			= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					subTable.language		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					subTable.segCountX2		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					subTable.searchRange	= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					subTable.entrySelector	= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					subTable.rangeShift		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);

					int segmentCount = subTable.segCountX2 / 2;
					subTable.endCodes		= new u16[segmentCount];
					subTable.startCodes		= new u16[segmentCount];
					subTable.idDeltas		= new s16[segmentCount];
					subTable.idRangeOffsets	= new u16[segmentCount];

					for (int segmentIndex = 0; segmentIndex < segmentCount; ++segmentIndex)
					{
						subTable.endCodes[segmentIndex] = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					}
					subTable.reservedPad = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					for (int segmentIndex = 0; segmentIndex < segmentCount; ++segmentIndex)
					{
						subTable.startCodes[segmentIndex] = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					}
					for (int segmentIndex = 0; segmentIndex < segmentCount; ++segmentIndex)
					{
						subTable.idDeltas[segmentIndex] = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref subTableOffset);
					}
					s64 IDRangeOffsetLocation = subTableOffset;
					for (int segmentIndex = 0; segmentIndex < segmentCount; ++segmentIndex)
					{
						subTable.idRangeOffsets[segmentIndex] = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref subTableOffset);
					}

					if (encodingRecord.platformID == OpenFontFormat.PlatformID.Unicode)
					{
						_selectedEncoding = subTable;
						_selectedEncodingIDRangeOffsetLocation = IDRangeOffsetLocation;
					}

					SubTables.Add(subTable);
				}

			}
			#endregion

			#region Maximum Profile table
			{
				var maxpRecord = TableDirectory[_maximumProfileIndex];
				offsetInBuffer = maxpRecord.offset;

				Maximums.version			= BigEndianReadAndAdvance.U32(_fontFileBuffer, ref offsetInBuffer);
				Maximums.numberOfGlyphs		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);

				if ( Maximums.version == 0x00010000)
				{
					Maximums.maxPoints				= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);			
					Maximums.maxContours			= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxCompositePoints		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxCompositeContours	= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxZones				= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxTwilightPoints		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxStorage				= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxFunctionDefs		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxInstructionDefs		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxStackElements		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxSizeOfInstructions	= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxComponentElements	= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
					Maximums.maxComponentDepth		= BigEndianReadAndAdvance.U16(_fontFileBuffer, ref offsetInBuffer);
				}
			}
			#endregion
		}


		public void GetControlPoints(string unicodeCharacter, List<Glyph> result)
		{
			#region Convert Character to glyph ID
			Profiler.BeginSample("Convert Character to Glyph ID");
			int codePoint = char.ConvertToUtf32(unicodeCharacter, 0);
			int glyphID = 0;// Missing Glyph
			if (codePoint > u16.MaxValue)
			{
				Debug.LogWarning("Character is not supported in Open Font format 4.");
			}
			else
			{
				for (int segmentIndex = 0; segmentIndex < _selectedEncoding.endCodes.Length; ++segmentIndex)
				{
					if (_selectedEncoding.endCodes[segmentIndex] >= codePoint)
					{
						if (_selectedEncoding.startCodes[segmentIndex] <= codePoint)
						{
							u16 idRangeOffset = _selectedEncoding.idRangeOffsets[segmentIndex];
							if (idRangeOffset != 0)
							{
								// Indexes are in an array right after the array of offsets, we'll need to convert the address to be indexed with bytes:
								s64 relativeCodePoint = (codePoint - _selectedEncoding.startCodes[segmentIndex]) * sizeof(u16);
								s64 addressOfcurrentSegment = _selectedEncodingIDRangeOffsetLocation + segmentIndex * sizeof(u16);
								s64 glyphIdArrayIndex = addressOfcurrentSegment + relativeCodePoint + idRangeOffset;

								glyphID = BigEndianRead.U16(_fontFileBuffer, glyphIdArrayIndex);
								if (glyphID != 0)
								{
									glyphID = (glyphID + _selectedEncoding.idDeltas[segmentIndex]) % 65536;// Do modulo manually, rather than relying on 16-bit integer wrapping.
									break;
								}
								else
								{
									break;// Missing glyph
								}
							}
							else
							{
								glyphID = (_selectedEncoding.idDeltas[segmentIndex] + codePoint) % 65536;
								break;
							}
						}
						else
						{
							break;// Missing Glyph
						}
					}
				}
			}
			Profiler.EndSample();
			#endregion

			GetControlPoints(glyphID, result);
		}

		public void GetControlPoints(int glyphID, List<Glyph> result)
		{ 
			if (glyphID >= Maximums.numberOfGlyphs)
			{
				return;
			}
			#region Parse location indexing table
			s64 offsetIntoGlyphArray = 0;
			if (_indexToLocationIndex >= 0)
			{
				var locaTableRecord = TableDirectory[_indexToLocationIndex];
				s64 arrayOffset = locaTableRecord.offset;

				if (FontHeader.indexToLocFormat == OpenFontFormat.IndexToLocFormat.ShortOffsets)
				{
					s64 locationIndex = arrayOffset + glyphID * sizeof(u16);
					offsetIntoGlyphArray = BigEndianRead.U16(_fontFileBuffer, locationIndex) * sizeof(u16);
					if (offsetIntoGlyphArray == BigEndianRead.U16(_fontFileBuffer, locationIndex + sizeof(u16)))
					{
						return;// No contours.
					}
				}
				else if (FontHeader.indexToLocFormat == OpenFontFormat.IndexToLocFormat.LongOffsets)
				{
					s64 locationIndex = arrayOffset + glyphID * sizeof(u32);
					offsetIntoGlyphArray = BigEndianRead.U32(_fontFileBuffer, locationIndex);
					if (offsetIntoGlyphArray == BigEndianRead.U32(_fontFileBuffer, locationIndex + sizeof(u32)))
					{
						return;// No Contours.
					}
				}

			}
			#endregion

			#region Parse Glyph
			Profiler.BeginSample("Parse Glyph");
			if (_glyphDataIndex >= 0)
			{
				var glyfHeaderRecord = TableDirectory[_glyphDataIndex];
				GlyphHeader = new OpenFontFormat.GlyphHeader();

				s64 arrayOffsetOfGlyphHeader = glyfHeaderRecord.offset;
				s64 arrayOffset = arrayOffsetOfGlyphHeader + offsetIntoGlyphArray;

				GlyphHeader.numberOfContours = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				GlyphHeader.xMin = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				GlyphHeader.yMin = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				GlyphHeader.xMax = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
				GlyphHeader.yMax = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);

				if (GlyphHeader.numberOfContours == 0)
				{
					Debug.LogError("Encountered a glyph with 0 contours, which is undefined at this stage.");
				}

				if (GlyphHeader.numberOfContours < 0)
				{
					u16 flags = 0;
					do
					{
						// Composite glyph
						flags = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);
						u16 glyphIndex = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);

						s32 xOffset = 0;
						s32 yOffset = 0;

						s32 firstGlyphMatchPointNumber = 0;
						s32 thisGlyphMatchPointNumber = 0;

						float scale = 1.0f;
						float yScale = 1.0f;
						float scale01 = 1.0f;
						float scale10 = 1.0f;

						bool argsAreXYOffsets = (flags & (u16)OpenFontFormat.CompositeGlyphFlags.ArgsAreXyValues) > 0;
						bool argsAreWords = (flags & (u16)OpenFontFormat.CompositeGlyphFlags.Args1And2AreWords) > 0;

						if (argsAreXYOffsets)
						{
							if (argsAreWords)
							{
								xOffset += BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
								yOffset += BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
							}
							else
							{
								xOffset += BigEndianReadAndAdvance.U8(_fontFileBuffer, ref arrayOffset);
								yOffset += BigEndianReadAndAdvance.U8(_fontFileBuffer, ref arrayOffset);
							}

						}
						if ((flags & (u16)OpenFontFormat.CompositeGlyphFlags.WeHaveAScale) > 0)
						{
							scale = BigEndianReadAndAdvance.F2Dot14(_fontFileBuffer, ref arrayOffset);
						}
						else if ((flags & (u16)OpenFontFormat.CompositeGlyphFlags.SeparateScale) > 0)
						{
							scale = BigEndianReadAndAdvance.F2Dot14(_fontFileBuffer, ref arrayOffset);
							yScale = BigEndianReadAndAdvance.F2Dot14(_fontFileBuffer, ref arrayOffset);
						}
						else if ((flags & (u16)OpenFontFormat.CompositeGlyphFlags.HasMatrix2x2) > 0)
						{
							scale = BigEndianReadAndAdvance.F2Dot14(_fontFileBuffer, ref arrayOffset);
							scale01 = BigEndianReadAndAdvance.F2Dot14(_fontFileBuffer, ref arrayOffset);
							scale10 = BigEndianReadAndAdvance.F2Dot14(_fontFileBuffer, ref arrayOffset);
							yScale = BigEndianReadAndAdvance.F2Dot14(_fontFileBuffer, ref arrayOffset);
						}
						GetControlPoints(glyphIndex, result);
					} while ((flags & (u16)OpenFontFormat.CompositeGlyphFlags.MoreComponents) > 0);

					u16 instructionCount = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);
					u8[] instructions = new u8[instructionCount];

					for (int instructionIndex = 0; instructionIndex < instructionCount; ++ instructionIndex)
					{
						instructions[instructionIndex] = BigEndianReadAndAdvance.U8(_fontFileBuffer, ref arrayOffset);
					}
				}
				
				else if (GlyphHeader.numberOfContours > 0)
				{
					Glyph simpleGlyph = new Glyph();
					result.Add(simpleGlyph);
					simpleGlyph.ControlPoints = new List<Vector2>();
					simpleGlyph.OnCurve = new List<bool>();

					simpleGlyph.EndIndices = new List<int>(GlyphHeader.numberOfContours);

					for (int contourIndex = 0; contourIndex < GlyphHeader.numberOfContours; ++contourIndex)
					{
						simpleGlyph.EndIndices.Add(BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset));
					}
					
					u16 instructionCount = BigEndianReadAndAdvance.U16(_fontFileBuffer, ref arrayOffset);
					arrayOffset += instructionCount;// skip instructions for now, instructions are one byte.

					// Unroll the flags array:
					int totalPointCount = simpleGlyph.EndIndices[simpleGlyph.EndIndices.Count - 1] + 1;
					List<u8> unrolledFlags = new List<u8>();
					while (unrolledFlags.Count < totalPointCount)
					{
						u8 flag = _fontFileBuffer[arrayOffset++];
						u16 flagCount = 1;
						if ((flag & (u8)OpenFontFormat.GlyphFlags.Repeat) > 0)
						{
							flagCount += _fontFileBuffer[arrayOffset++];
						}
						for (int identicalFlagIndex = 0; identicalFlagIndex < flagCount; ++identicalFlagIndex)
						{
							unrolledFlags.Add(flag);
						}
					}

					#region Parse X Coordinates
					var xCoordinates = new List<s16>();
					s16 lastX = 0;
					for (int flagIndex = 0; flagIndex < unrolledFlags.Count; ++flagIndex)
					{
						var flag = unrolledFlags[flagIndex];
						s16 xCoord = 0;

						bool isOnCurve      = (flag & (u8)OpenFontFormat.GlyphFlags.OnCurvePoint) > 0;
						bool isShort        = (flag & (u8)OpenFontFormat.GlyphFlags.X_ShortVector) > 0;
						bool sameOrPositive = (flag & (u8)OpenFontFormat.GlyphFlags.X_IsSameOrPositive) > 0;
						bool isRepeated     = !isShort && sameOrPositive;
						bool isPositive     = isShort && sameOrPositive;

						simpleGlyph.OnCurve.Add(isOnCurve);

						if (isOnCurve)
						{
							if (isRepeated)
							{
								xCoord = lastX;
							}
							else if (isShort)
							{
								xCoord = _fontFileBuffer[arrayOffset++];
								if (!isPositive) { xCoord = (short)-xCoord; }
								xCoord += lastX;
							}
							else
							{
								xCoord = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
								xCoord += lastX;
							}
							xCoordinates.Add(xCoord);
							lastX = xCoord;
						}
						else
						{
							if (isRepeated)
							{
								xCoord = lastX;
							}
							else if (isShort)
							{
								xCoord = _fontFileBuffer[arrayOffset++];
								if (!isPositive) { xCoord = (short)-xCoord; }
								xCoord += lastX;
							}
							else
							{
								xCoord = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
								xCoord += lastX;
							}
							xCoordinates.Add(xCoord);
							lastX = xCoord;
						}
					}
					#endregion
					#region Parse Y Coordinates
					var yCoordinates = new List<s16>();
					s16 lastY = 0;
					for (int flagIndex = 0; flagIndex < unrolledFlags.Count; ++flagIndex)
					{
						var flag = unrolledFlags[flagIndex];
						s16 yCoord = 0;

						bool isOnCurve      = (flag & (u8)OpenFontFormat.GlyphFlags.OnCurvePoint) > 0;
						bool isShort        = (flag & (u8)OpenFontFormat.GlyphFlags.Y_ShortVector) > 0;
						bool sameOrPositive = (flag & (u8)OpenFontFormat.GlyphFlags.Y_IsSameOrPositive) > 0;
						bool isRepeated     = !isShort && sameOrPositive;
						bool isPositive     = isShort && sameOrPositive;

						if (isOnCurve)
						{
							if (isRepeated)
							{
								yCoord = lastY;
							}
							else if (isShort)
							{
								yCoord = _fontFileBuffer[arrayOffset++];
								if (!isPositive) { yCoord = (short)-yCoord; }
								yCoord += lastY;
							}
							else
							{
								yCoord = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
								yCoord += lastY;
							}
							yCoordinates.Add(yCoord);
							lastY = yCoord;
						}
						else
						{
							if (isRepeated)
							{
								yCoord = lastY;
							}
							else if (isShort)
							{
								yCoord = _fontFileBuffer[arrayOffset++];
								if (!isPositive) { yCoord = (short)-yCoord; }
								yCoord += lastY;
							}
							else
							{
								yCoord = BigEndianReadAndAdvance.S16(_fontFileBuffer, ref arrayOffset);
								yCoord += lastY;
							}
							yCoordinates.Add(yCoord);
							lastY = yCoord;
						}
					}
					#endregion
					
					for (int coordinateIndex = 0; coordinateIndex < xCoordinates.Count; ++ coordinateIndex)
					{
						var x = xCoordinates[coordinateIndex] / 256f;
						var y = yCoordinates[coordinateIndex] / 256f;
						simpleGlyph.ControlPoints.Add(new Vector2(x, y));
					}

				}
			}
			else
			{
				//Debug.LogError("No glyf table found", this);
			}
			Profiler.EndSample();
			#endregion

			return;
		}

		public static string RelativeToFullPath(string location)
		{
			return Path.GetFullPath(Path.Combine(Application.dataPath, "../", location));
		}

		public static u32 CalculateChecksum(byte[] fileBuffer, u32 length, s64 offsetInBuffer)
		{
			u32 padding = length % 4 > 0u ? 4u : 0u;
			u32 paddedLength = ((length / 4) * 4) + padding;
			u32 checksum = 0;

			for (int tableIndex = 0; tableIndex < paddedLength; tableIndex += 4)
			{
				checksum += BigEndianRead.U32(fileBuffer, offsetInBuffer + tableIndex);
			}

			return checksum;
		}
		
	}

	public class BigEndianRead
	{

		public static int S32(byte[] buffer, Int64 index)
		{
			int result = buffer[index] << 24;
			result += buffer[index + 1] << 16;
			result += buffer[index + 2] << 8;
			result += buffer[index + 3] << 0;
			return result;
		}

		public static uint U32(byte[] buffer, Int64 index)
		{
			uint result = (uint)(buffer[index] << 24);
			result += (uint)(buffer[index + 1] << 16);
			result += (uint)(buffer[index + 2] << 8);
			result += (uint)(buffer[index + 3] << 0);

			return result;
		}

		public static s16 S16(byte[] buffer, Int64 index)
		{
			s16 mostSignificant = 8;
			s16 result = (s16)(buffer[index] << mostSignificant);
			result += (s16) buffer[index + 1];
			return result;
		}

		public static u16 U16(byte[] buffer, Int64 index)
		{
			short mostSignificant = 8;
			u16 result = (u16)(buffer[index] << mostSignificant);
			result += (u16) buffer[index + 1];
			
			return result;
		}
		public static u64 U64(byte[] buffer, Int64 index)
		{
			u64 result = (u64)(buffer[index]) << 56;
			result += ((u64)buffer[index + 1]) << 48;
			result += ((u64)buffer[index + 2]) << 40;
			result += ((u64)buffer[index + 3]) << 32;

			result += ((u64)buffer[index + 4]) << 24;
			result += ((u64)buffer[index + 5]) << 16;
			result += ((u64)buffer[index + 6]) << 8;
			result += ((u64)buffer[index + 7]) << 0;		
			return result;
		}
	}

	public class BigEndianReadAndAdvance
	{
		public static int S32(byte[] buffer, ref Int64 index)
		{
			int result = BigEndianRead.S32(buffer, index);
			index += 4;
			return result;
		}
		public static uint U32(byte[] buffer, ref Int64 index)
		{
			uint result = BigEndianRead.U32(buffer, index);
			index += 4;
			return result;
		}
		public static s16 S16(byte[] buffer, ref Int64 index)
		{
			s16 result = BigEndianRead.S16(buffer, index);
			index += 2;
			return result;
		}
		public static u16 U16(byte[] buffer, ref Int64 index)
		{
			u16 result = BigEndianRead.U16(buffer, index);
			index += 2;
			return result;
		}
		public static u8 U8(byte[] buffer, ref Int64 index)
		{
			u8 result = buffer[index];
			index += 1;
			return result;
		}
		public static s8 S8(byte[] buffer, ref Int64 index)
		{
			s8 result = (s8)buffer[index];
			index += 1;
			return result;
		}
		public static u64 U64(byte[] buffer, ref Int64 index)
		{
			u64 result = BigEndianRead.U64(buffer, index);
			index += 8;
			return result;
		}
		public static float F2Dot14(byte[] buffer, ref Int64 index)
		{
			s16 fixedPoint = BigEndianRead.S16(buffer, index);
			index += 2;

			float result = 0;
			int twoBitTwosComplement = fixedPoint >> 14;

			if ((twoBitTwosComplement & 2) > 0)
			{
				int everythingFilledExceptTwoBits  = ~3;
				twoBitTwosComplement = twoBitTwosComplement | everythingFilledExceptTwoBits;
			}

			result += twoBitTwosComplement;
			float temp = ((float)(fixedPoint & 0x3FFF)) / 0x4000; ;
			result += temp;
			return result;
		}
	}

	[Serializable]
	public class Glyph
	{
		public List<Vector2>	ControlPoints;
		public List<int>		EndIndices;
		public List<bool>       OnCurve;
	}

}