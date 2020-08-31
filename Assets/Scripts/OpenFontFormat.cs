using System;
using System.Text;
using UnityEngine;

using System.Runtime.InteropServices;

using u8 = System.Byte;
using u16 = System.UInt16;
using s16 = System.Int16;
using s32 = System.Int32;
using u32 = System.UInt32;
using s64 = System.Int64;
//FWORD			= s16
//UFWORD		= u16
//F2DOT14		= s16 (2.14 fixed point, signed integer, unsigned fraction)
//LONGDATETIME	= s64 (seconds since jan 1 1904 00:00:00)
//Tag			= uint8[4]
//Offset16		= u16
//Offset32		= u32 number of bytes
//Fixed			= u32 (16.16 fixed point)

namespace OpenFontFormat
{
	public class Tag : PropertyAttribute { };
	public class EnumFlagsAttribute : PropertyAttribute{ };

	// Tags based on 4 chars packed in one signed 32-bit integer
	public enum TableTag : u32
	{
		// Required Tables:
		FontHeader					= 1751474532,// head - Version info and some data about extremes in metrics tables.
		CharToGlyphMapping			= 1668112752,// cmap - mapping code points to glyph IDs
		HorizontalHeader			= 1751672161,// hhea - metric variations
		HorizontalMetrics			= 1752003704,// hmtx
		MaximumProfile				= 1835104368,// maxp - memory size requirements
		NamingTable					= 1851878757,// name - localization of names
		GlobalFontInformation		= 1330851634,// OS/2 - Dimensions, Ranges
		PostScript					= 1886352244,// post - compatibility with postscript printers

		// Truetype Outlines:
		ControlValueTable			= 1668707360,// cvt  - Data to be used by instructions
		FontProgram					= 1718642541,// fpgm - initialization program?
		GlyphData					= 1735162214,// glyf - Outlines (e.g.: composite bezier curves)
		IndexToLocation				= 1819239265,// loca - Offsets of glyph in the glyph array relative to the glyph header
		ControlValueProgram			= 1886545264,// prep - Control Value Program, shared for all glyphs. Run whenever point size or transformation matrix changes, and before each glyph is interpreted.
		GridFittingAndScanConversionProcedure	= 1734439792,//gasp

		// CFF Tables:
		CompactFontFormatV1			= 1128678944,// CFF 
		CompactFontFormatV2			= 1128678962,// CFF2
		VerticalOriginTable			= 1448038983,// VORG

		// SVG Tables:
		ScalableVectorGraphics		= 1398163232,// SVG 

		// Bitmap Glyph Tables:
		EmbeddedBitmapData			= 1161970772,// EBDT
		EmbeddedBitmapLocation		= 1161972803,// EBLC
		EmbeddedBitmapScaling		= 1161974595,// EBSC
		ColorBitmapData				= 1128416340,// CBDT
		ColorBitmapLocation			= 1128418371,// CBLC
		StandardBitmapGraphics		= 1935829368,// sbix

		// Optional Tables:
		DigitalSignature			= 1146308935,// DSIG
		HorizontalDeviceMetrics		= 1751412088,// hdmx
		Kerning						= 1801810542,// kern - Kerning, distance between characters
		LinearTreshold				= 1280594760,// LTSH - Something about improvements based on side bearings
		Merge						= 1296388679,// MERG - helps with AA of connected glyps, to avoid artifacts.
		MetaData					= 1835365473,// meta - Information about where the font would be used
		PCL5						= 1347175252,// PLCT - The spec mainly says it is strong discouraged for OFF fonts with TrueType outlines. Useful. Looks like a bunch of meta data about types of glyphs.
		VerticalDeviceMetrics		= 1447316824,// VDMX - How far down the next line starts
		VerticalHeaderTable			= 1986553185,// vhea - Overall vertical spacing (vertical fonts)
		VerticalMetric				= 1986884728,// vmtx - Per Glyph vertical spacing.
		ColorTable					= 1129270354,// COLR
		Palette						= 1129333068,// CPAL

		// Advanced Typographic Tables:
		Baseline					= 1111577413,// BASE
		GlyphDefinition				= 1195656518,// GDEF - List types of glyphs, and how things are connected
		GlyphPositioning			= 1196445523,// GPOS - kerning, placing symbols in the right place relative to others.
		GlyphSubstitution			= 1196643650,// GSUB
		Justification				= 1246975046,// JSTF - Glyph substitution and positioning in justified text
		MathematicalTypesetting		= 1296127048,// MATH

		// Font Variation Tables:
		AxisVariations				= 1635148146,// avar
		CVT_Variations				= 1668702578,// cvar
		FontVariations				= 1719034226,// fvar
		GlyphVariations				= 1735811442,// gvar
		HorizontalMetricsVariations	= 1213612370,// HVAR
		MetricsVariations			= 1297498450,// MVAR
		StyleAttributes				= 1398030676,// STAT
		VerticalMetricsVariations	= 1448493394,// VVAR
	}

	[Serializable]
	public struct MaximumProfile
	{
		public u32 version;
		public u16 numberOfGlyphs;
		public u16 maxPoints;
		public u16 maxContours;
		public u16 maxCompositePoints;
		public u16 maxCompositeContours;
		public u16 maxZones;
		public u16 maxTwilightPoints;
		public u16 maxStorage;
		public u16 maxFunctionDefs;
		public u16 maxInstructionDefs;
		public u16 maxStackElements;
		public u16 maxSizeOfInstructions;
		public u16 maxComponentElements;
		public u16 maxComponentDepth;
	}

	[Serializable]
	public struct OffsetTable
	{
		[Tag]
		public u32	sfntVersion;
		public u16	numTables;
		public u16	searchRange;
		public u16	entrySelector;
		public u16	rangeShift;
	}

	[Serializable]
	public struct TableRecord
	{
		[Tag]
		public u32	tableTag;
		public u32	checksum;
		public u32	offset;
		public u32	length;
	}

	[Serializable]
	public struct FontHeader
	{
		public u16					MajorVersion;
		public u16					MinorVersion;
		public u32					fontRevision;// not used on Windows, use version string (id5) in the 'name' table
		public u32					checksumAdjustment;
		public static readonly u32  SubtractChecksumFromThis = 0xB1B0AFBA;// Another magic number defined in the Open Font Spec, without explanation
		public u32					magicNumber;// Magic number to verify it is a TrueType/Open Font File
		public static readonly u32	MagicNumberExpected = 0x5F0F3CF5;
		[EnumFlags]
		public FontHeaderFlags		flags;
		public u16					unitsPerEm;
		public s64					creationTimeStamp;
		public s64					modificationTimeStamp;
		public s16					xMin;
		public s16					yMin;
		public s16					xMax;
		public s16					yMax;
		[EnumFlags]
		public MacStyleFlags		macStyle;// Must agree with OS/2 table fsSelection bits
		public u16					lowestRecPPEM;
		public FontDirection		fontDirectionHint;// depcrecated, set to 2
		public IndexToLocFormat		indexToLocFormat;
		public s16					glyphDataFormat;
	}

	public enum FontHeaderFlags : u16
	{
		BaseLineAtY							= 1 << 0,
		LeftSideBearingAtX					= 1 << 1,
		InstructionsMayDependOnPointSize	= 1 << 2,
		ForcePpemToInt						= 1 << 3,
		InstructionsMayAlterAdvanceWidth	= 1 << 4,
		LegacyBit5							= 1 << 5,	// Should not be used in OFF. May result in different behavior for vertical layout in some platforms.
		LegacyBit6							= 1 << 6,	// Should not be used in OFF. Apple specific.
		LegacyBit7							= 1 << 7,	// Should not be used in OFF. Apple specific.
		LegacyBit8							= 1 << 8,	// Should not be used in OFF. Apple specific.
		LegacyBit9							= 1 << 9,	// Should not be used in OFF. Apple specific.
		LegacyBit10							= 1 << 10,	// Should not be used in OFF. Apple specific.
		Lossless							= 1 << 11,
		Converted							= 1 << 12,
		ClearType							= 1 << 13,
		LastResort							= 1 << 14,
		Reserved							= 1 << 15	// set to 0
	}

	public enum MacStyleFlags : u16
	{
		Bold		= 1 << 0,
		Italic		= 1 << 1,
		Underline	= 1 << 2,
		Outline		= 1 << 3,
		Shadow		= 1 << 4,
		Condensed	= 1 << 5,
		Extended	= 1 << 6,
		Reserved7	= 1 << 7,
		Reserved8	= 1 << 8,
		Reserved9	= 1 << 9,
		Reserved10	= 1 << 10,
		Reserved11	= 1 << 11,
		Reserved12	= 1 << 12,
		Reserved13	= 1 << 13,
		Reserved14	= 1 << 14,
		Reserved15	= 1 << 15
	}

	public enum FontDirection : s16
	{
		FullyMixed = 0,
		OnlyStrongLTR = 1,// Left to Right
		FullyMixedDirectionalWithNeutrals = 2,
		OnlyStronglyRTL = -1,// Right to Left
		StronglyRTLWithNeutrals = -2
	}

	public enum IndexToLocFormat : s16
	{
		ShortOffsets = 0,
		LongOffsets = 1
	}


	[Serializable]
	public struct GlyphMapHeader
	{
		public u16	version;
		public u16	numTables;
	}

	[Serializable]
	public struct EncodingRecord
	{
		public PlatformID	platformID;
		public u16			encodingID;
		public u32			offset;
	}

	public enum PlatformID : u16
	{
		Unicode		= 0,
		Macintosh	= 1,
		ISO			= 2,// deprecated according to ISO/IEC 14496-22:2019, identical to Unicode
		Windows		= 3,
		Custom		= 4
	}

	public enum UnicodeEncodingIDs : u16
	{
		Unicode_1_0					= 0,
		Unicode_1_1					= 1,
		ISO_IEC_10646				= 2,// Identical to Unicode_1_1
		Unicode_2_0_BMP				= 3,// Formats 0, 4, 6
		Unicode_2_0_Full			= 4,// Formats 0, 4, 6, 10, 12
		Unicode_Variation_sequences = 5,// Format 14
		Unicode_Full				= 6	// Formats 0, 4, 6, 10, 12, 13
	}

	public enum WindowsEncodingIDs : u16
	{
		Symbol = 0,
		UCS_2,
		ShiftJIS,
		PRC,
		Big5,
		Wansung,
		Johab,
		Reserved7,
		Reserved8,
		Reserved9,
		UCS_4
	}

	[Serializable]
	public struct GlyphHeader
	{
		public s16 numberOfContours;
		public s16 xMin;
		public s16 yMin;
		public s16 xMax;
		public s16 yMax;
	}

	public enum GlyphFlags : u8
	{
		OnCurvePoint		= 1 << 0,
		X_ShortVector		= 1 << 1,
		Y_ShortVector		= 1 << 2,
		Repeat				= 1 << 3,
		X_IsSameOrPositive	= 1 << 4,// Positive if Short Vector, Same as previous x-coordinate
		Y_IsSameOrPositive	= 1 << 5,
		Overlap				= 1 << 6,// Not required for OFF, glyphs may overlap without it. 
		ReservedBit7		= 1 << 7
	}

	public enum CompositeGlyphFlags : u16
	{
		Args1And2AreWords		= 1 << 0,// otherwise bytes
		ArgsAreXyValues			= 1 << 1,// font design units, otherwise point in original glyph to connect to
		RoundXyToGrid			= 1 << 2,// if they are XY values
		WeHaveAScale			= 1 << 3,// otherwise scale is 1.0
		Reserved4				= 1 << 4,// 0
		MoreComponents			= 1 << 5,// at least one more glyph
		SeparateScale			= 1 << 6,// separate scale for x and y
		HasMatrix2x2			= 1 << 7,// scaling and 90 degree rotations of glyph components
		HasInstructions			= 1 << 8,
		UseFirstGlyphMetrics	= 1 << 9,// aw, lsb and rsb of the original glyph. Both for hinted and unhinted characters. Undefined for rotated composite components.
		OverlapCompound			= 1 << 10,
		ScaledComponentOffset	= 1 << 11,// X and Y offset are in Component glyph coordinate system.
		UnscaledComponentOffset	= 1 << 12,// X and Y offset are in Current glyph coordinate system.
		Reserved13				= 1 << 13,// 0
		Reserved14				= 1 << 14,// 0
		Reserved15				= 1 << 15 // 0
	}

	public enum WindowsLanguageIDs : u16
	{
		Afrikaans_South_Africa										= 0x0436,
		Albanian_Albania											= 0x041C,
		Alsatian_France												= 0x0484,
		Amharic_Ethiopia											= 0x045E,
		Arabic_Algeria												= 0x1401,
		Arabic_Bahrain												= 0x3C01,
		Arabic_Egypt												= 0x0C01,
		Arabic_Iraq													= 0x0801,
		Arabic_Jordan												= 0x2C01,
		Arabic_Kuwait												= 0x3401,
		Arabic_Lebanon												= 0x3001,
		Arabic_Libya												= 0x1001,
		Arabic_Morocco												= 0x1801,
		Arabic_Oman													= 0x2001,
		Arabic_Qatar												= 0x4001,
		Arabic_Saudi_Arabia											= 0x0401,
		Arabic_Syria												= 0x2801,
		Arabic_Tunisia												= 0x1C01,
		Arabic_UAE													= 0x3801,
		Arabic_Yemen												= 0x2401,
		Armenian_Armenia											= 0x042B,
		Assamese_India												= 0x044D,
		Azeri_Cyrillic_Azerbaijan									= 0x082C,
		Azeri_Latin_Azerbaijan										= 0x042C,
		Bashkir_Russia												= 0x046D,
		Basque_Basque												= 0x042D,
		Belarusian_Belarus											= 0x0423,
		Bengali_Bangladesh											= 0x0845,
		Bengali_India												= 0x0445,
		Bosnian_Cyrillic_Bosnia_and_Herzegovina						= 0x201A,
		Bosnian_Latin_Bosnia_and_Herzegovina						= 0x141A,
		Breton_France												= 0x047E,
		Bulgarian_Bulgaria											= 0x0402,
		Catalan_Catalan												= 0x0403,
		Chinese_Hong_Kong_SAR										= 0x0C04,
		Chinese_Macao_SAR											= 0x1404,
		Frisian_Netherlands											= 0x0462,
		Galician_Galician											= 0x0456,
		Georgian_Georgia											= 0x0437,
		German_Austria												= 0x0C07,
		German_Germany												= 0x0407,
		German_Liechtenstein										= 0x1407,
		German_Luxembourg											= 0x1007,
		German_Switzerland											= 0x0807,
		Greek_Greece												= 0x0408,
		Greenlandic_Greenland										= 0x046F,
		Gujarati_India												= 0x0447,
		Hausa_Latin_Nigeria											= 0x0468,
		Hebrew_Israel												= 0x040D,
		Hindi_India													= 0x0439,
		Hungarian_Hungary											= 0x040E,
		Icelandic_Iceland											= 0x040F,
		Igbo_Nigeria												= 0x0470,
		Indonesian_Indonesia										= 0x0421,
		Inuktitut_Canada											= 0x045D,
		Inuktitut_Latin_Canada										= 0x085D,
		Irish_Ireland												= 0x083C,
		isiXhosa_South_Africa										= 0x0434,
		isiZulu_South_Africa										= 0x0435,
		Italian_Italy												= 0x0410,
		Italian_Switzerland											= 0x0810,
		Japanese_Japan												= 0x0411,
		Kannada_India												= 0x044B,
		Kazakh_Kazakhstan											= 0x043F,
		Khmer_Cambodia												= 0x0453,
		K_iche_Guatemala											= 0x0486,
		Kinyarwanda_Rwanda											= 0x0487,
		Kiswahili_Kenya												= 0x0441,
		Konkani_India												= 0x0457,
		Korean_Korea												= 0x0412,
		Kyrgyz_Kyrgyzstan											= 0x0440,
		Lao_Lao_PDR													= 0x0454,
		Latvian_Latvia												= 0x0426,
		Lithuanian_Lithuania										= 0x0427,
		Lower_Sorbian_Germany										= 0x082E,
		Luxembourgish_Luxembourg									= 0x046E,
		Macedonian_FYROM_Former_Yugoslav_Republic_of_Macedonia		= 0x042F,
		Malay_Brunei_Darussalam										= 0x083E,
		Malay_Malaysia												= 0x043E,
		Malayalam_India												= 0x044C,
		Maltese_Malta												= 0x043A,
		Maori_New_Zealand											= 0x0481,
		Mapudungun_Chile											= 0x047A,
		Marathi_India												= 0x044E,
		Mohawk_Mohawk												= 0x047C,
		Mongolian_Cyrillic_Mongolia									= 0x0450,
		Mongolian_Traditional_Peoples_Republic_of_China				= 0x0850,
		Nepali_Nepal												= 0x0461,
		Norwegian_Bokmal_Norway										= 0x0414,
		Norwegian_Nynorsk_Norway									= 0x0814,
		Occitan_France												= 0x0482,
		Odia_formerly_Oriya_India									= 0x0448,
		Pashto_Afghanistan											= 0x0463,
		Polish_Poland												= 0x0415,
		Portuguese_Brazil											= 0x0416,
		Portuguese_Portugal											= 0x0816,
		Punjabi_India												= 0x0446,
		Quechua_Bolivia												= 0x046B,
		Quechua_Ecuador												= 0x086B,
		Quechua_Peru												= 0x0C6B,
		Romanian_Romania											= 0x0418,
		Romansh_Switzerland											= 0x0417,
		Russian_Russia												= 0x0419,
		Sami_Inari_Finland											= 0x243B,
		Sami_Lule_Norway											= 0x103B,
		Sami_Lule_Sweden											= 0x143B,
		Sami_Northern_Finland										= 0x0C3B,
		Sami_Northern_Norway										= 0x043B,
		Sami_Northern_Sweden										= 0x083B,
		Sami_Skolt_Finland											= 0x203B,
		Sami_Southern_Norway										= 0x183B,
		Sami_Southern_Sweden										= 0x1C3B,
		Sanskrit_India												= 0x044F,
		Serbian_Cyrillic_Bosnia_and_Herzegovina						= 0x1C1A,
		Serbian_Cyrillic_Serbia										= 0x0C1A,
		Serbian_Latin_Bosnia_and_Herzegovina						= 0x181A,
		Serbian_Latin_Serbia										= 0x081A,
		Sesotho_sa_Leboa_South_Africa								= 0x046C,
		Setswana_South_Africa										= 0x0432,
		Sinhala_Sri_Lanka											= 0x045B,
		Slovak_Slovakia												= 0x041B,
		Slovenian_Slovenia											= 0x0424,
		Spanish_Argentina											= 0x2C0A,
		Spanish_Bolivia												= 0x400A,
		Spanish_Chile												= 0x340A,
		Spanish_Colombia											= 0x240A,
		Spanish_Costa_Rica											= 0x140A,
		Spanish_Dominican_Republic									= 0x1C0A,
		Spanish_Ecuador												= 0x300A,
		Spanish_El_Salvador											= 0x440A,
		Spanish_Guatemala											= 0x100A,
		Spanish_Honduras											= 0x480A,
		Spanish_Mexico												= 0x080A,
		Spanish_Nicaragua											= 0x4C0A,
		Spanish_Panama												= 0x180A,
		Spanish_Paraguay											= 0x3C0A,
		Spanish_Peru												= 0x280A,
		Spanish_Puerto_Rico											= 0x500A,
		Spanish_Modern_sort_Spain									= 0x0C0A,
		Spanish_Traditional_sort_Spain								= 0x040A,
		Spanish_United_States										= 0x540A,
		Spanish_Uruguay												= 0x380A,
		Spanish_Venezuela											= 0x200A,
		Sweden_Finland												= 0x081D,
		Swedish_Sweden												= 0x041D,
		Syriac_Syria												= 0x045A,
		Tajik_Cyrillic_Tajikistan									= 0x0428,
		Tamazight_Latin_Algeria										= 0x085F,
		Tamil_India													= 0x0449,
		Tatar_Russia												= 0x0444,
		Telugu_India												= 0x044A,
		Thai_Thailand												= 0x041E,
		Tibetan_PRC													= 0x0451,
		Turkish_Turkey												= 0x041F,
		Turkmen_Turkmenistan										= 0x0442,
		Uighur_PRC													= 0x0480,
		Ukrainian_Ukraine											= 0x0422,
		Upper_Sorbian_Germany										= 0x042E,
		Urdu_Islamic_Republic_of_Pakistan							= 0x0420,
		Uzbek_Cyrillic_Uzbekistan									= 0x0843,
		Uzbek_Latin_Uzbekistan										= 0x0443,
		Vietnamese_Vietnam											= 0x042A,
		Welsh_United_Kingdom										= 0x0452,
		Wolof_Senegal												= 0x0488,
		Yakut_Russia												= 0x0485,
		Yi_PRC														= 0x0478,
		Yoruba_Nigeria												= 0x046A
	}

	namespace EncodingFormats
	{
		[Serializable]
		public struct Format0
		{
			public u16	format;
			public u16	length;
			public u16	language;
			public u8[]	glyphIdArray;
		}

		[Serializable]
		public struct Format4
		{
			public u16		format;
			public u16		length;
			public u16		language;
			public u16		segCountX2;
			public u16		searchRange;
			public u16		entrySelector;
			public u16		rangeShift;
			public u16[]	endCodes;
			public u16		reservedPad;
			public u16[]	startCodes;
			public s16[]	idDeltas;
			public u16[]	idRangeOffsets;
		}

	}

	public class Convert
	{
		public static int FourCharToInt(string id)
		{
			int result = id[0] << 24;
			result += id[1] << 16;
			result += id[2] << 8;
			result += id[3] << 0;
			return result;
		}

		public static string UIntToFourChar(u32 id)
		{
			StringBuilder textType = new StringBuilder(4);

			textType.Append((char)(id >> 24 & 0xFF));
			textType.Append((char)(id >> 16 & 0xFF));
			textType.Append((char)(id >> 8 & 0xFF));
			textType.Append((char)(id >> 0 & 0xFF));
			return textType.ToString();
		}

		public static string IntToFourCharWithInt(int id)
		{
			StringBuilder textType = new StringBuilder(4);

			textType.Append((char)(id >> 24 & 0xFF));
			textType.Append((char)(id >> 16 & 0xFF));
			textType.Append((char)(id >> 8 & 0xFF));
			textType.Append((char)(id >> 0 & 0xFF));
			textType.Append("(");
			textType.Append(id);
			textType.Append(")");
			return textType.ToString();
		}
	}

}