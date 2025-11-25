using System.Text;
using System.Xml;
using Newtonsoft.Json.Linq;
using protoscone;

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

							// Fill the end of the JSON chunk with spaces, and replace non-printable characters with spaces.
							uint JSONLength = BitConverter.ToUInt32(glbBytesPre, 0x0C);
							for (int k = 0x14; k < 0x14 + JSONLength; k++)
							{
								if (glbBytesPre[k] < 0x20 || glbBytesPre[k] > 0x7E)
								{
									glbBytesPre[k] = 0x20;
								}
							}

							uint binLength = BitConverter.ToUInt32(glbBytesPre, 0x14 + (int)JSONLength);
							byte[] glbBinBytesPre = glbBytesPre[(0x14 + (int)JSONLength + 8)..(0x14 + (int)JSONLength + 8 + (int)binLength)];

							JObject json = JObject.Parse(Encoding.UTF8.GetString(glbBytesPre, 0x14, (int)JSONLength).Trim());
							// Remove extensions and extras
							foreach (JToken ext in json.SelectTokens("$..extensions").ToList())
							{
								ext?.Parent?.Remove();
							}
							foreach (JToken ext in json.SelectTokens("$..extensionsUsed").ToList())
							{
								ext?.Parent?.Remove();
							}
							foreach (JToken ext in json.SelectTokens("$..extensionsRequired").ToList())
							{
								ext?.Parent?.Remove();
							}
							foreach (JToken extras in json.SelectTokens("$..extras").ToList())
							{
								extras?.Parent?.Remove();
							}

							// 1. Load and sort bufferViews *BUT* keep track of old→new index mapping
							List<JObject> originalBufferViews = [.. ((JArray?)json["bufferViews"] ?? []).Cast<JObject>()];

							// Sort by byteOffset (missing offsets sorted as 0)
							List<(JObject bv, int originalIndex)> sorted = [.. originalBufferViews
								.Select((bv, i) => (bv, originalIndex: i))
								.OrderBy(x => (long?)x.bv["byteOffset"] ?? 0)];

							// Map new → old indices
							Dictionary<int, int> bufferViewIndexMap = sorted
								.Select((x, newIndex) => new { x.originalIndex, newIndex })
								.ToDictionary(x => x.newIndex, x => x.originalIndex);

							// Replace JSON array with *sorted* buffered views
							JArray sortedBufferViews = [];
							foreach (var (bv, originalIndex) in sorted)
								sortedBufferViews.Add(bv);
							json["bufferViews"] = sortedBufferViews;

							// 2. Pre-group accessors by ORIGINAL bufferView index
							JArray accessors = (JArray?)json["accessors"] ?? [];

							Dictionary<int, List<JObject>> accessorsByOriginalView = accessors
								.Cast<JObject>()
								.Where(a => a["bufferView"] != null)
								.GroupBy(a => (int)a["bufferView"]!)
								.ToDictionary(g => g.Key,
											  g => g.OrderBy(a => (long?)a["byteOffset"] ?? 0).ToList());

							// 3. Create reverse mapping: old -> new index
							Dictionary<int, int> oldToNewIndexMap = bufferViewIndexMap.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
							
							// Update accessors to refer to NEW bufferView indices
							foreach (JObject accessor in accessors.Cast<JObject>())
							{
								if (accessor["bufferView"] != null)
								{
									int oldIndex = (int)accessor["bufferView"]!;
									accessor["bufferView"] = oldToNewIndexMap[oldIndex];
								}
							}

							List<byte> newBinData = [];
							// 4. Now operate sequentially over *sorted* bufferViews
							for (int k = 0; k < sortedBufferViews.Count; k++)
							{
								List<byte> tempBvBin = [];
								JToken bufferView = sortedBufferViews[k];
								int originalIndex = bufferViewIndexMap[k];
								JObject originalBufferView = originalBufferViews[originalIndex];
								long bvByteOffset = (long)(bufferView["byteOffset"] ?? 0);
								int bvByteLength = (int)(bufferView["byteLength"] ?? 0);
								int bvByteStride = (int)(bufferView["byteStride"] ?? 0);

								// Find all accessors that reference this bufferView
								if (!accessorsByOriginalView.TryGetValue(originalIndex, out List<JObject>? accessorsForView))
								{
									accessorsForView = [];
								}

								if (accessorsForView.Count > 1)
								{
									Console.WriteLine($"BufferView at index {k} is shared by {accessorsForView.Count} accessors.");
								}
								else
								{
									Console.WriteLine($"BufferView at index {k} is used by {accessorsForView.Count} accessor.");
								}
								for (int l = 0; l < accessorsForView.Count; l++)
								{
									List<byte> tempAccBin = [];
									JObject accessor = accessorsForView[l];
									int originalAccIndex = ((JArray?)json["accessors"])?.IndexOf(accessor) ?? -1;
									if (originalAccIndex == -1)
									{
										Console.WriteLine("Could not find accessor index.");
										continue;
									}
									JObject? originalAccessor = (JObject?)json["accessors"]?[originalAccIndex];
									int accByteOffset = (int)(accessor["byteOffset"] ?? 0);
									int accCount = (int)(accessor["count"] ?? 0);
									string accType = (string?)accessor["type"] ?? "SCALAR";
									int accComponentType = (int)(accessor["componentType"] ?? 5126); // Default to FLOAT
									int componentSize = ComponentSize(accComponentType);
									int componentCount = ComponentCount(accType);
									int totalAccByteLength = accCount * componentSize * componentCount;

									// If the accessor is a COLOR_n, NORMAL, TANGENT or TEXCOORD_n, resize it appropriately
									string accessorRole = "";
									// Access every attribute of every primitive of every mesh
									JArray? meshes = (JArray?)json["meshes"];
									if (meshes != null)
									{
										foreach (JToken mesh in meshes)
										{
											JArray? primitives = (JArray?)mesh["primitives"];
											if (primitives != null)
											{
												foreach (JToken primitive in primitives)
												{
													JObject? attributes = (JObject?)primitive["attributes"];
													if (attributes != null)
													{
														foreach (JProperty attribute in attributes.Properties())
														{
															string attrName = attribute.Name;
															int attrAccessorIndex = (int?)attribute.Value ?? -1;
															if (attrAccessorIndex == originalAccIndex)
															{
																accessorRole = attrName;
																break;
															}
														}
													}
													if (!string.IsNullOrEmpty(accessorRole))
													{
														break;
													}
												}
											}
											if (!string.IsNullOrEmpty(accessorRole))
											{
												break;
											}
										}
									}
									if (accessorRole != "POSITION")
									{
										if (originalAccessor?["min"] != null)
										{
											originalAccessor?.Remove("min");
										}
										if (originalAccessor?["max"] != null)
										{
											originalAccessor?.Remove("max");
										}
									}
									byte[] currentAccBytes = glbBinBytesPre[(int)(bvByteOffset + accByteOffset)..(int)(bvByteOffset + accByteOffset + totalAccByteLength)];
									if (accessorRole == "")
									{
										Console.WriteLine("Accessor role is empty, skipping processing.");
										continue;
									}
									else if (TexcoordRegex().IsMatch(accessorRole) || ColorRegex().IsMatch(accessorRole))
									{
										// These just need to be normalized, so no binary changes needed
										originalAccessor["normalized"] = true;
										originalAccessor["componentType"] = 5123; // UNSIGNED_SHORT
										if (tempBvBin.Count != 0)
										{
											originalAccessor["byteOffset"] = tempBvBin.Count;
										}
										tempAccBin.AddRange(currentAccBytes);
									}
										else if (accessorRole == "NORMAL")
										{
											originalAccessor["componentType"] = 5126; // FLOAT
											originalAccessor["type"] = "VEC3";
											float[] normals = new float[accCount * 3];
											for (int m = 0; m < accCount; m++)
											{
												int baseIndex = m * 4;
												int actualIndex = m * 3;
												// Unpack signed bytes and normalize to unit vector
												float x = (sbyte)currentAccBytes[baseIndex + 0] / 127.0f;
												float y = (sbyte)currentAccBytes[baseIndex + 1] / 127.0f;
												float z = (sbyte)currentAccBytes[baseIndex + 2] / 127.0f;
												float length = MathF.Sqrt(x * x + y * y + z * z);
												if (length > 0.0f)
												{
													normals[actualIndex + 0] = x / length;
													normals[actualIndex + 1] = y / length;
													normals[actualIndex + 2] = z / length;
												}
												else
												{
													normals[actualIndex + 0] = 0.0f;
													normals[actualIndex + 1] = 0.0f;
													normals[actualIndex + 2] = 1.0f;
												}
											}
											// convert normal
										if (tempBvBin.Count != 0)
										{
											originalAccessor["byteOffset"] = tempBvBin.Count;
										}
										foreach (float f in normals)
										{
											byte[] bytes = BitConverter.GetBytes(f);
											tempAccBin.AddRange(bytes);
										}
									}
										else if (accessorRole == "TANGENT")
										{
											originalAccessor["componentType"] = 5126; // FLOAT
											float[] tangents = new float[accCount * 4];
											for (int m = 0; m < accCount; m++)
											{
												int baseIndex = m * 4;
												// Unpack signed bytes, normalize xyz to unit vector, set w to ±1.0
												float x = (sbyte)currentAccBytes[baseIndex + 0] / 127.0f;
												float y = (sbyte)currentAccBytes[baseIndex + 1] / 127.0f;
												float z = (sbyte)currentAccBytes[baseIndex + 2] / 127.0f;
												float w = (sbyte)currentAccBytes[baseIndex + 3] < 0 ? -1.0f : 1.0f;
												float length = MathF.Sqrt(x * x + y * y + z * z);
												if (length > 0.0f)
												{
													tangents[baseIndex + 0] = x / length;
													tangents[baseIndex + 1] = y / length;
													tangents[baseIndex + 2] = z / length;
												}
												else
												{
													tangents[baseIndex + 0] = 1.0f;
													tangents[baseIndex + 1] = 0.0f;
													tangents[baseIndex + 2] = 0.0f;
												}
												tangents[baseIndex + 3] = w;
											}
											// convert tangent
										if (tempBvBin.Count != 0)
										{
											originalAccessor["byteOffset"] = tempBvBin.Count;
										}
										foreach (float f in tangents)
										{
											byte[] bytes = BitConverter.GetBytes(f);
											tempAccBin.AddRange(bytes);
										}
									}
									else if (accessorRole == "POSITION")
									{
										float[] min = [float.MaxValue, float.MaxValue, float.MaxValue];
										float[] max = [float.MinValue, float.MinValue, float.MinValue];
										for (int m = 0; m < accCount; m++)
										{
											int baseIndex = m * 12;
											// POSITION data is already in float format, just read it
											float x = BitConverter.ToSingle(currentAccBytes, baseIndex + 0);
											float y = BitConverter.ToSingle(currentAccBytes, baseIndex + 4);
											float z = BitConverter.ToSingle(currentAccBytes, baseIndex + 8);
											if (x < min[0]) min[0] = x;
											if (y < min[1]) min[1] = y;
											if (z < min[2]) min[2] = z;
											if (x > max[0]) max[0] = x;
											if (y > max[1]) max[1] = y;
											if (z > max[2]) max[2] = z;
										}
										originalAccessor["min"] = new JArray(min);
										originalAccessor["max"] = new JArray(max);
										// convert position
										if (tempBvBin.Count != 0)
										{
											originalAccessor["byteOffset"] = tempBvBin.Count;
										}
										tempAccBin.AddRange(currentAccBytes);
									}
									else
									{
										// just copy raw bytes
										if (tempBvBin.Count != 0)
										{
											originalAccessor["byteOffset"] = tempBvBin.Count;
										}
										tempAccBin.AddRange(currentAccBytes);
									}
									tempBvBin.AddRange(tempAccBin);
								}
								originalBufferView["byteLength"] = tempBvBin.Count;
								if (newBinData.Count != 0)
								{
									originalBufferView["byteOffset"] = newBinData.Count;
								}
								newBinData.AddRange(tempBvBin);
							}

							// Build BIN padded (0x00)
							byte[] outBin = [.. newBinData];
							int binPad = (4 - (outBin.Length % 4)) % 4;
							byte[] outBinPadded = new byte[outBin.Length + binPad];
							Array.Copy(outBin, outBinPadded, outBin.Length);
							json["buffers"][0]["byteLength"] = outBinPadded.Length;

							// Build JSON text and pad with spaces (0x20)
							string outJson = json.ToString(Newtonsoft.Json.Formatting.None);
							byte[] outJsonBytes = Encoding.UTF8.GetBytes(outJson);
							int jsonPad = (4 - (outJsonBytes.Length % 4)) % 4;
							byte[] outJsonPadded = new byte[outJsonBytes.Length + jsonPad];
							Array.Copy(outJsonBytes, outJsonPadded, outJsonBytes.Length);
							for (int p = 0; p < jsonPad; p++) outJsonPadded[outJsonBytes.Length + p] = 0x20;
							// padding bytes are zero by default

							// final GLB assemble
							int totalLength = 12 + 8 + outJsonPadded.Length + 8 + outBinPadded.Length;
							List<byte> outGlb = [];
							outGlb.AddRange(Encoding.ASCII.GetBytes("glTF")); // magic
							outGlb.AddRange(BitConverter.GetBytes(2)); // version
							outGlb.AddRange(BitConverter.GetBytes(totalLength));
							// JSON chunk
							outGlb.AddRange(BitConverter.GetBytes(outJsonPadded.Length));
							outGlb.AddRange(Encoding.ASCII.GetBytes("JSON"));
							outGlb.AddRange(outJsonPadded);
							// BIN chunk
							outGlb.AddRange(BitConverter.GetBytes(outBinPadded.Length));
							outGlb.AddRange(Encoding.ASCII.GetBytes("BIN\0"));
							outGlb.AddRange(outBinPadded);
							File.WriteAllBytes(Path.Combine(args[0], $"{name}.glb"), [.. outGlb]);
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