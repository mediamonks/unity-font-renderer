using System;
using System.Runtime.InteropServices;
using UnityEngine;

// Author: Laurens Mathot
// Formatting borrowed from https://gist.github.com/RC-1290/775eef351bf96c86be1a3f655d3d6f06 (which itself is based on the Windows headers)

namespace MediaMonks
{
	public class NativeFile
	{

		public struct OverlappedIo {
			UInt64 Internal;
			UInt64 InternalHigh;

			UInt32 Offset;
			UInt32 OffsetHigh;

			IntPtr  hEvent;
		};

		public struct SecurityAttributes {
			UInt32	length;
			IntPtr	securityDescriptor;
			UInt32	bInheritHandle;
		};

		 public enum AccessRights : UInt32
		{
			Read		= 0x80000000,// GENERIC_READ
			Write		= 0x40000000,// GENERIC_WRITE
			Execute		= 0x20000000,// GENERIC_EXECUTE
			All			= 0x10000000,// GENERIC_ALL
			ReadWrite	= Read | Write
		};

		public enum ShareMode : UInt32
		{
			Exclusive	= 0,
			Delete		= 0x00000004,// FILE_SHARE_DELETE
			Read		= 0x00000001,// FILE_SHARE_READ
			Write		= 0x00000002 // FILE_SHARE_WRITE
		};

		public enum CreationDisposition : UInt32
		{
			CreateNew			= 1,
			CreateAlways		= 2,
			OpenExisting		= 3,
			OpenAlways			= 4,
			TruncateExisting	= 5
		};

		public enum Attribute : UInt32
		{
			ReadOnly			= 1 << 0,// FILE_ATTRIBUTE_READONLY             
			Hidden				= 1 << 1,// FILE_ATTRIBUTE_HIDDEN               
			System				= 1 << 2,// FILE_ATTRIBUTE_SYSTEM               
			Directory			= 1 << 4,// FILE_ATTRIBUTE_DIRECTORY            
			Archive				= 1 << 5,// FILE_ATTRIBUTE_ARCHIVE              
			Device				= 1 << 6,// FILE_ATTRIBUTE_DEVICE               
			Normal				= 1 << 7,// FILE_ATTRIBUTE_NORMAL               
			Temporary			= 1 << 8,// FILE_ATTRIBUTE_TEMPORARY            
			SparseFile			= 1 << 9,// FILE_ATTRIBUTE_SPARSE_FILE          
			ReparsePoint		= 1 << 10,// FILE_ATTRIBUTE_REPARSE_POINT        
			Compressed			= 1 << 11,// FILE_ATTRIBUTE_COMPRESSED           
			Offline				= 1 << 12,// FILE_ATTRIBUTE_OFFLINE              
			NotContentIndexed	= 1 << 13,// FILE_ATTRIBUTE_NOT_CONTENT_INDEXED  
			Encrypted			= 1 << 14,// FILE_ATTRIBUTE_ENCRYPTED            
			IntegrityStream		= 1 << 15,// FILE_ATTRIBUTE_INTEGRITY_STREAM     
			Virtual				= 1 << 16,// FILE_ATTRIBUTE_VIRTUAL              
			NoScrubData			= 1 << 17,// FILE_ATTRIBUTE_NO_SCRUB_DATA        
			Ea					= 1 << 18,// FILE_ATTRIBUTE_EA                   
			Pinned				= 1 << 19,// FILE_ATTRIBUTE_PINNED               
			UnPinned			= 1 << 20,// FILE_ATTRIBUTE_UNPINNED             
			RecallOnOpen		= Ea,// FILE_ATTRIBUTE_RECALL_ON_OPEN       
			RecallOnDataAccess	= 1 << 22 // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS
		};

		public enum MoveMethod : UInt32
		{
			Begin	= 0,
			Current = 1,
			End		= 2
		};

		[StructLayout(LayoutKind.Explicit)]
		public struct HighLowOrderS64
		{
			[FieldOffset(0)]
			public Int64 Full;

			[FieldOffset(0)]
			public Int32 Low;

			[FieldOffset(4)]
			public Int32 High;

			public HighLowOrderS64(Int64 fullValue)
			{
				// Assign high and low so the compiler doesn't complain:
				Low = 0;
				High = 0;
				// actually assign everything all at once:
				Full = fullValue;
			}
		}

		public const UInt32 InvalidSetFilePointer = 4294967295;// unsigned 32-bit -1
		public const Int64 InvalidHandle = 4294967295;

		[DllImport("Kernel32.dll", EntryPoint ="ReadFile")]
		public static extern bool Read(
			IntPtr fileHandle,
			IntPtr buffer,
			UInt32 numberOfBytesToRead,
			IntPtr numberofBytesRead,
			IntPtr overlapped
			);

		[DllImport("Kernel32.dll", EntryPoint = "CreateFileW")]
		public static extern IntPtr Create([MarshalAs(UnmanagedType.LPWStr)]string fileName, AccessRights desiredAccess, ShareMode shareMode, IntPtr securityAttributes, CreationDisposition creationDisposition, Attribute flagsAndAttributes, IntPtr hTemplateFile);

		[DllImport("Kernel32.dll", EntryPoint = "SetFilePointer")]
		public static extern UInt32 SetFilePointer( IntPtr file, Int32 distanceToMove, IntPtr distanceToMoveHigh, MoveMethod moveMethod );

		[DllImport("Kernel32.dll", EntryPoint = "CloseHandle")]
		public static extern bool CloseHandle( IntPtr handle );

		[DllImport("Kernel32.dll", EntryPoint = "GetFileSizeEx")]
		private static extern bool GetFileSize( IntPtr handle, IntPtr fileSize );

		public static bool ReadWithOffset(IntPtr fileHandle, Int64 fileOffset, byte[] buffer)
		{
			object bytesRead = 0;
			bool succeeded = ReadWithOffset(fileHandle, fileOffset, buffer, ref bytesRead);
			return succeeded;
		}

		public static bool ReadWithOffset(IntPtr fileHandle, Int64 fileOffset, byte[] buffer, ref object bytesRead)
		{
			GCHandle bufferHandle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
			bool succeeded = ReadWithOffset(fileHandle, fileOffset, bufferHandle.AddrOfPinnedObject(), (uint)buffer.Length, ref bytesRead);
			bufferHandle.Free();
			return succeeded;
		}

		public static bool ReadWithOffset(IntPtr fileHandle, Int64 fileOffset, IntPtr buffer, uint byteReadCount, ref object bytesRead)
		{
			var fileIndex = new HighLowOrderS64(fileOffset);
			GCHandle highOrderHandle = GCHandle.Alloc(fileIndex.High, GCHandleType.Pinned);
			var distanceMovedLow = SetFilePointer(fileHandle, fileIndex.Low, highOrderHandle.AddrOfPinnedObject(), MoveMethod.Begin);
			
			if (distanceMovedLow == InvalidSetFilePointer)
			{
				Debug.LogError("Something went wrong while setting the file pointer.");
				return false;
			}
			bytesRead = 0;
			GCHandle bytesReadHandle = GCHandle.Alloc(bytesRead, GCHandleType.Pinned);

			bool succeeded = Read(fileHandle, buffer, byteReadCount, bytesReadHandle.AddrOfPinnedObject(), IntPtr.Zero);
			highOrderHandle.Free();
			bytesReadHandle.Free();
			return succeeded;
		}

		public static bool GetSize(IntPtr fileHandle, ref Int64 sizeInBytes)
		{
			object boxedSize = sizeInBytes;
			GCHandle sizeInBytesHandle = GCHandle.Alloc(boxedSize, GCHandleType.Pinned);
			bool succeeded = GetFileSize(fileHandle, sizeInBytesHandle.AddrOfPinnedObject());

			sizeInBytesHandle.Free();
			sizeInBytes = (Int64)boxedSize;
			return succeeded;
		}
	}
}