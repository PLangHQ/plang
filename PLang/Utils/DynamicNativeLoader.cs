using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace PLang.Utils
{
	class DynamicNativeLoader
	{
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
		private static extern IntPtr LoadLibrary(string lpFileName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool FreeLibrary(IntPtr hModule);

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_DOS_HEADER
		{
			public ushort e_magic;       // Magic number
			public ushort e_cblp;
			public ushort e_cp;
			public ushort e_crlc;
			public ushort e_cparhdr;
			public ushort e_minalloc;
			public ushort e_maxalloc;
			public ushort e_ss;
			public ushort e_sp;
			public ushort e_csum;
			public ushort e_ip;
			public ushort e_cs;
			public ushort e_lfarlc;
			public ushort e_ovno;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
			public ushort[] e_res1;
			public ushort e_oemid;
			public ushort e_oeminfo;
			[MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
			public ushort[] e_res2;
			public int e_lfanew;         // File address of new exe header
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_FILE_HEADER
		{
			public ushort Machine;
			public ushort NumberOfSections;
			public uint TimeDateStamp;
			public uint PointerToSymbolTable;
			public uint NumberOfSymbols;
			public ushort SizeOfOptionalHeader;
			public ushort Characteristics;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_OPTIONAL_HEADER
		{
			public ushort Magic;
			public byte MajorLinkerVersion;
			public byte MinorLinkerVersion;
			public uint SizeOfCode;
			public uint SizeOfInitializedData;
			public uint SizeOfUninitializedData;
			public uint AddressOfEntryPoint;
			public uint BaseOfCode;
			public uint BaseOfData;
			public uint ImageBase;
			public uint SectionAlignment;
			public uint FileAlignment;
			public ushort MajorOperatingSystemVersion;
			public ushort MinorOperatingSystemVersion;
			public ushort MajorImageVersion;
			public ushort MinorImageVersion;
			public ushort MajorSubsystemVersion;
			public ushort MinorSubsystemVersion;
			public uint Win32VersionValue;
			public uint SizeOfImage;
			public uint SizeOfHeaders;
			public uint CheckSum;
			public ushort Subsystem;
			public ushort DllCharacteristics;
			public uint SizeOfStackReserve;
			public uint SizeOfStackCommit;
			public uint SizeOfHeapReserve;
			public uint SizeOfHeapCommit;
			public uint LoaderFlags;
			public uint NumberOfRvaAndSizes;
			public IMAGE_DATA_DIRECTORY ExportTable;
			// Add more fields if necessary
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_DATA_DIRECTORY
		{
			public uint VirtualAddress;
			public uint Size;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct IMAGE_EXPORT_DIRECTORY
		{
			public uint Characteristics;
			public uint TimeDateStamp;
			public ushort MajorVersion;
			public ushort MinorVersion;
			public uint Name;
			public uint Base;
			public uint NumberOfFunctions;
			public uint NumberOfNames;
			public uint AddressOfFunctions;     // RVA from base of image
			public uint AddressOfNames;         // RVA of name pointers
			public uint AddressOfNameOrdinals;  // RVA of ordinal numbers
		}

		public static void ReadExportedFunctions(string dllPath)
		{
			using (FileStream fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read))
			using (BinaryReader reader = new BinaryReader(fs))
			{
				IMAGE_DOS_HEADER dosHeader = FromBinaryReader<IMAGE_DOS_HEADER>(reader);
				if (dosHeader.e_magic != 0x5A4D) // MZ magic number
					throw new InvalidDataException("Invalid DOS header in DLL.");

				reader.BaseStream.Seek(dosHeader.e_lfanew, SeekOrigin.Begin); // Move to the PE header
				int peHeader = reader.ReadInt32();
				if (peHeader != 0x4550) // PE\0\0 magic number
					throw new InvalidDataException("Invalid PE header.");

				IMAGE_FILE_HEADER fileHeader = FromBinaryReader<IMAGE_FILE_HEADER>(reader);
				IMAGE_OPTIONAL_HEADER optionalHeader = FromBinaryReader<IMAGE_OPTIONAL_HEADER>(reader);

				// Seek to the export directory
				reader.BaseStream.Seek(optionalHeader.ExportTable.VirtualAddress, SeekOrigin.Begin);
				IMAGE_EXPORT_DIRECTORY exportDirectory = FromBinaryReader<IMAGE_EXPORT_DIRECTORY>(reader);

				// Get function names
				reader.BaseStream.Seek(exportDirectory.AddressOfNames, SeekOrigin.Begin);
				for (int i = 0; i < exportDirectory.NumberOfNames; i++)
				{
					uint nameRVA = reader.ReadUInt32();
					long namePosition = optionalHeader.ImageBase + nameRVA;
					reader.BaseStream.Seek(namePosition, SeekOrigin.Begin);

					string functionName = ReadNullTerminatedString(reader);
					Console.WriteLine($"Exported function: {functionName}");

					// Move back to continue reading names
					reader.BaseStream.Seek(optionalHeader.ExportTable.VirtualAddress + i * 4, SeekOrigin.Begin);
				}
			}
		}

		private static T FromBinaryReader<T>(BinaryReader reader) where T : struct
		{
			int size = Marshal.SizeOf(typeof(T));
			byte[] bytes = reader.ReadBytes(size);
			GCHandle handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
			T theStruct = (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
			handle.Free();
			return theStruct;
		}

		private static string ReadNullTerminatedString(BinaryReader reader)
		{
			StringBuilder result = new StringBuilder();
			char c;
			while ((c = reader.ReadChar()) != '\0')
			{
				result.Append(c);
			}
			return result.ToString();
		}

	}
}
