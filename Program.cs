using System.Text;
using System.Xml;
using NetGltf;

// Argument validation and processing
if (args.Length != 2)
{
	Console.WriteLine("Invalid number of arguments. Usage: protoscone.exe [path/to/input] [path/to/output]");
	// return;
	args = ["C:\\Users\\sriem\\Documents\\Aviation\\all-scenery", "C:\\Users\\sriem\\Downloads"];
}

// Display the provided arguments
Console.WriteLine("Arguments provided:");
for (int i = 0; i < args.Length; i++)
{
	Console.WriteLine($"Argument {i + 1}: {args[i]}");
}

if (Directory.Exists(args[0]) == false)
{
	Console.WriteLine("Input path does not exist.");
	return;
}

// If the arguments are valid, proceed with the main logic
double[,] LatitudeIndex = { { 89, 12 }, { 86, 4 }, { 83, 2 }, { 76, 1 }, { 62, 0.5 }, { 22, 0.25 }, { 0, 0.125 } };
string tempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "protoscone");

if (!Directory.Exists(tempPath))
{
	_ = Directory.CreateDirectory(tempPath);
}

string[] allBglFiles = Directory.GetFiles(args[0], "*.bgl", SearchOption.AllDirectories);
Console.WriteLine($"Found files:\n{string.Join(",\n", allBglFiles)}");
List<LibraryObject> libraryObjects = [];
List<ModelData> modelDatas = [];
foreach (string file in allBglFiles)
{
	using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
	using BinaryReader br = new(fs);

	// Read and validate BGL header
	byte[] magicNumber1 = br.ReadBytes(4);
	br.BaseStream.Seek(0x10, SeekOrigin.Begin);
	byte[] magicNumber2 = br.ReadBytes(4);
	if (!magicNumber1.SequenceEqual(new byte[] { 0x01, 0x02, 0x92, 0x19 }) ||
		!magicNumber2.SequenceEqual(new byte[] { 0x03, 0x18, 0x05, 0x08 }))
	{
		Console.WriteLine("Invalid BGL header");
		return;
	}
	br.BaseStream.Seek(0x14, SeekOrigin.Begin);
	uint recordCt = br.ReadUInt32();

	// Skip 0x38-byte header
	br.BaseStream.Seek(0x38, SeekOrigin.Begin);

	int foundSceneryObj = 0;
	int foundMdlData = 0;

	List<int> sceneryObjectOffsets = [];
	List<int> mdlDataOffsets = [];
	for (int i = 0; i < recordCt; i++)
	{
		long recordStartPos = br.BaseStream.Position;
		uint recType = br.ReadUInt32();
		br.BaseStream.Seek(recordStartPos + 0x0C, SeekOrigin.Begin);
		uint startSubsection = br.ReadUInt32();
		br.BaseStream.Seek(recordStartPos + 0x10, SeekOrigin.Begin);
		uint recSize = br.ReadUInt32();
		if (recType == 0x0025) // SceneryObject
		{
			foundSceneryObj++;
			sceneryObjectOffsets.Add((int)startSubsection);
			Console.WriteLine($"Found SceneryObject at offset 0x{startSubsection:X}, total found: {foundSceneryObj}");
		}
		else if (recType == 0x002B) // ModelData
		{
			foundMdlData++;
			mdlDataOffsets.Add((int)startSubsection);
		}
	}

	// Parse SceneryObject subrecords
	List<(int offset, int size)> sceneryObjectSubrecords = [];
	foreach (int sceneryOffset in sceneryObjectOffsets)
	{
		br.BaseStream.Seek(sceneryOffset + 4, SeekOrigin.Begin);
		int subrecCount = br.ReadInt32();
		int subrecOffset = br.ReadInt32();
		int size = br.ReadInt32();
		Console.WriteLine($"SceneryObject Subrecord Count: {subrecCount}, Offset: 0x{subrecOffset:X}, Size: {size}");
		sceneryObjectSubrecords.Add((subrecOffset, size));
	}

	int bytesRead = 0;
	foreach ((int subOffset, int subSize) in sceneryObjectSubrecords)
	{
		while (bytesRead < subSize)
		{
			br.BaseStream.Seek(subOffset + bytesRead, SeekOrigin.Begin);
			ushort id = br.ReadUInt16(), size = br.ReadUInt16();
			if (id != 0xB) // LibraryObject
			{
				Console.WriteLine($"Unexpected subrecord type at offset 0x{subOffset + bytesRead:X}: 0x{id:X4}, skipping {size} bytes");
				br.BaseStream.Seek(subOffset + size, SeekOrigin.Begin);
				bytesRead += size;
				continue;
			}
			uint longitude = br.ReadUInt32(), latitude = br.ReadUInt32();
			short altitude = br.ReadInt16();
			byte[] flagsBytes = br.ReadBytes(6);
			List<Flags> flagsList = [];
			for (int j = 0; j < flagsBytes.Length && j < 7; j++)
			{
				if (flagsBytes[j] != 0)
				{
					flagsList.Add((Flags)j);
				}
			}
			Flags[] flags = [.. flagsList];
			ushort pitch = br.ReadUInt16();
			ushort bank = br.ReadUInt16();
			ushort heading = br.ReadUInt16();
			short imageComplexity = br.ReadInt16();
			br.BaseStream.Seek(2, SeekOrigin.Current); // There is an unknown 2-byte field here
			byte[] guidEmptyBytes = br.ReadBytes(16);
			UInt128 guidEmpty = new(BitConverter.ToUInt64(guidEmptyBytes, 8), BitConverter.ToUInt64(guidEmptyBytes, 0));
			byte[] guidBytes = br.ReadBytes(16);
			UInt128 guid = new(BitConverter.ToUInt64(guidBytes, 8), BitConverter.ToUInt64(guidBytes, 0));
			float scale = br.ReadSingle();
			LibraryObject libObj = new()
			{
				id = id,
				size = size,
				longitude = (longitude * (360.0 / 805306368.0)) - 180.0,
				latitude = 90.0 - (latitude * (180.0 / 536870912.0)),
				altitude = altitude,
				flags = flags,
				pitch = pitch * (360.0 / 65536.0),
				bank = bank * (360.0 / 65536.0),
				heading = heading * (360.0 / 65536.0),
				imageComplexity = imageComplexity,
				guidEmpty = guidEmpty,
				guid = guid,
				scale = scale
			};
			libraryObjects.Add(libObj);
			Console.WriteLine($"{libObj.guid:X4}\t{libObj.size}\t{libObj.longitude:F6}\t{libObj.latitude:F6}\t{libObj.altitude}\t[{string.Join(",", libObj.flags)}]\t{libObj.pitch:F2}\t{libObj.bank:F2}\t{libObj.heading:F2}\t{libObj.imageComplexity}\t{libObj.scale:F3}");
			bytesRead += size;
		}
	}

	// Parse ModelData subrecords
	List<(int offset, int size)> modelDataSubrecords = [];
	foreach (int modelDataOffset in mdlDataOffsets)
	{
		br.BaseStream.Seek(modelDataOffset + 4, SeekOrigin.Begin);
		int subrecCount = br.ReadInt32();
		int subrecOffset = br.ReadInt32();
		int size = br.ReadInt32();
		modelDataSubrecords.Add((subrecOffset, size));
	}

	bytesRead = 0;
	int objectsRead = 0;
	foreach ((int subOffset, int subSize) in modelDataSubrecords)
	{
		while (bytesRead < subSize)
		{
			br.BaseStream.Seek(subOffset + (24 * objectsRead), SeekOrigin.Begin);
			byte[] guidBytes = br.ReadBytes(16);
			UInt128 guid = new(BitConverter.ToUInt64(guidBytes, 8), BitConverter.ToUInt64(guidBytes, 0));
			uint startModelDataOffset = br.ReadUInt32();
			uint modelDataSize = br.ReadUInt32();
			br.BaseStream.Seek(subOffset + startModelDataOffset, SeekOrigin.Begin);
			byte[] mdlBytes = br.ReadBytes((int)modelDataSize);
			string name = $"{guid:X}";
			List<LodData> lods = [];
			string chunkID = Encoding.ASCII.GetString(mdlBytes, 0, Math.Min(4, mdlBytes.Length));
			if (chunkID != "RIFF")
			{
				Console.WriteLine($"Unexpected model data format for GUID {guid:X}: missing RIFF header");
				break;
			}
			// Enter this model and get LOD info, GLB files, and mesh data
			for (int i = 8; i < mdlBytes.Length; i += 4)
			{
				string chunk = Encoding.ASCII.GetString(mdlBytes, i, Math.Min(4, mdlBytes.Length - i));
				if (chunk == "GXML")
				{
					int size = BitConverter.ToInt32(mdlBytes, i + 4);
					string gxmlContent = Encoding.UTF8.GetString(mdlBytes, i + 8, size);
					XmlDocument xmlDoc = new();
					xmlDoc.LoadXml(gxmlContent);
					name = xmlDoc.GetElementsByTagName("ModelInfo")[0]?.Attributes?["name"]?.Value ?? $"{guid:X}";
					XmlNodeList lodNodes = xmlDoc.GetElementsByTagName("LOD");
					foreach (XmlNode lodNode in lodNodes)
					{
						string lodObjName = lodNode?.Attributes?["ModelFile"]?.Value ?? "Unnamed";
						int minSize = 0;
						try
						{
							minSize = int.Parse(lodNode?.Attributes?["minSize"]?.Value ?? "0");
						}
						catch (FormatException)
						{
							continue;
						}
						if (lodObjName != "Unnamed")
						{
							lods.Add(new LodData
							{
								name = lodObjName,
								minSize = minSize
							});
						}
					}
					i += size;
				}
				else if (chunk == "GLBD")
				{
					int size = BitConverter.ToInt32(mdlBytes, i + 4);
					for (int j = i + 8; j < i + 8 + size; j += 4)
					{
						if (Encoding.ASCII.GetString(mdlBytes, j, Math.Min(4, mdlBytes.Length - j)) == "GLB\0")
						{
							// Capture GLB file
							int glbSize = BitConverter.ToInt32(mdlBytes, j + 4);
							byte[] glbBytesPre = br.ReadBytes(glbSize);
							glbBytesPre = mdlBytes[(j + 8)..(j + 8 + glbSize)];
						}
					}
				}
			}
			modelDatas.Add(new ModelData
			{
				guid = guid,
				name = name,
				modelBytes = mdlBytes,
				lods = lods
			});
			bytesRead += (int)modelDataSize + 24;
			objectsRead++;
		}
	}
}
Console.WriteLine($"Total LibraryObjects parsed: {libraryObjects.Count}. Total ModelDatas parsed: {modelDatas.Count}.");

HashSet<string> processedModels = [];
int index = 1;
Console.WriteLine($"Processing object {index} of {libraryObjects.Count}");
foreach (LibraryObject element in libraryObjects)
{

}

int GetTileIndex(double lat, double lon)
{
	if (Math.Abs(lat) > 90 || Math.Abs(lon) > 180)
	{
		Console.WriteLine("Latitude or longitude out of range");
		return 0;
	}
	else
	{
		double lookup = Math.Abs(lat);
		double tileWidth = 0;
		for (int i = 0; i < LatitudeIndex.Length; i++)
		{
			if (lookup >= LatitudeIndex[i, 0])
			{
				tileWidth = LatitudeIndex[i, 1];
				break;
			}
		}
		int baseX = (int)Math.Floor(Math.Floor(lon / tileWidth) * tileWidth);
		int x = (int)Math.Floor((lon - baseX) / tileWidth);
		int baseY = (int)Math.Floor(lat);
		int y = (int)Math.Truncate((lat - baseY) * 8);
		return ((baseX + 180) << 14) + ((baseY + 90) << 6) + (y << 3) + x;
	}
}

static int ComponentSize(int componentType)
{
	return componentType switch
	{
		5120 => 1, // BYTE
		5121 => 1, // UNSIGNED_BYTE
		5122 => 2, // SHORT
		5123 => 2, // UNSIGNED_SHORT
		5125 => 4, // UNSIGNED_INT
		5126 => 4, // FLOAT
		_ => throw new Exception("Unknown componentType")
	};
}
static int ComponentCount(string type)
{
	return type switch
	{
		"SCALAR" => 1,
		"VEC2" => 2,
		"VEC3" => 3,
		"VEC4" => 4,
		_ => throw new Exception("Unsupported type")
	};
}

enum Flags
{
	IsAboveAGL = 0,
	NoAutogenSuppression = 1,
	NoCrash = 2,
	NoFog = 3,
	NoShadow = 4,
	NoZWrite = 5,
	NoZTest = 6,
}

struct LibraryObject
{
	public ushort id;
	public ushort size;
	public double longitude;
	public double latitude;
	public short altitude;
	public Flags[] flags;
	public double pitch;
	public double bank;
	public double heading;
	public short imageComplexity;
	public UInt128 guidEmpty;
	public UInt128 guid;
	public float scale;
}

struct LodData
{
	public string name;
	public int minSize;
}

struct ModelObject
{
	public string name;
	public List<string> textures;
	public byte[] meshData;
}

struct ModelData
{
	public UInt128 guid;
	public string name;
	public byte[] modelBytes;
	public List<LodData> lods;
	public List<string> textures;
}

partial class Program
{
	[System.Text.RegularExpressions.GeneratedRegex(@"^TEXCOORD_\d+$")]
	private static partial System.Text.RegularExpressions.Regex TexcoordRegex();

	[System.Text.RegularExpressions.GeneratedRegex(@"^COLOR_\d+$")]
	private static partial System.Text.RegularExpressions.Regex ColorRegex();
}