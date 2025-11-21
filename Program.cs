using System.Text;
using System.Xml;
using SharpGLTF.Schema2;
using Newtonsoft.Json.Linq;

// Argument validation and processing
if (args.Length != 2)
{
	Console.WriteLine("Invalid number of arguments. Usage: protoscone.exe [path/to/input] [path/to/output]");
	return;
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
string[] allSubfiles = Directory.GetFiles(args[0], "*", SearchOption.AllDirectories);
string placementFile = "", objectFile = "";
Dictionary<string, string> guidToName = [];
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
					name = xmlDoc.GetElementsByTagName("ModelInfo")[0]?.Attributes["name"]?.Value ?? $"{guid:X}";
					XmlNodeList lodNodes = xmlDoc.GetElementsByTagName("LOD");
					foreach (XmlNode lodNode in lodNodes)
					{
						// Console.WriteLine($"Found LOD Node: {lodNode.OuterXml}");
						string lodObjName = lodNode.Attributes["ModelFile"]?.Value ?? "Unnamed";
						int minSize = 0;
						try
						{
							minSize = int.Parse(lodNode.Attributes["minSize"]?.Value ?? "0");
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
								Console.WriteLine($"Removing extension: {ext}");
								ext?.Parent?.Remove();
							}
							foreach (JToken ext in json.SelectTokens("$..extensionsUsed").ToList())
							{
								Console.WriteLine($"Removing extensionsUsed: {ext}");
								ext?.Parent?.Remove();
							}
							foreach (JToken ext in json.SelectTokens("$..extensionsRequired").ToList())
							{
								Console.WriteLine($"Removing extensionsRequired: {ext}");
								ext?.Parent?.Remove();
							}
							foreach (JToken extras in json.SelectTokens("$..extras").ToList())
							{
								Console.WriteLine($"Removing extras: {extras}");
								extras?.Parent?.Remove();
							}

							// Remove non-standard optimizations
							List<byte> newBin = [];
							JArray newBufferViews = [];

							// We'll collect converted attribute arrays
							List<float> positions = null;
							List<float> normals = null;
							List<float> tangents = null;
							Dictionary<string, List<float>> texcoords = []; // TEXCOORD_0, TEXCOORD_1, etc.
							Dictionary<string, List<float>> colors = [];    // COLOR_0, COLOR_1, etc.
							byte[] indicesBytes = null;
							int indicesComponentType = -1;
							int indexCount = 0;

							JArray? accessors = (JArray?)json["accessors"];
							JArray? bufferViews = (JArray?)json["bufferViews"];
							JArray? buffers = (JArray?)json["buffers"];

							// We'll detect indices accessor separately and copy indices as-is
							int vertexCount = 0;
							if (accessors != null)
							{
								// If POSITION exists, use it for vertexCount
								for (int a = 0; a < accessors.Count; a++)
								{
									JObject acc = (JObject)accessors[a]!;
									string accName = (string?)acc["name"] ?? "";
									if (accName.Contains("_POSITION") || accName.EndsWith("_POSITION"))
									{
										vertexCount = (int)acc["count"]!;
										break;
									}
								}
								if (vertexCount == 0)
								{
									// fallback: take max count among accessors except indices
									int max = 0;
									foreach (JObject acc in accessors.Cast<JObject>())
									{
										string type = (string?)acc["type"] ?? "SCALAR";
										if (type == "SCALAR" && acc["name"]?.ToString()?.Contains("_indices") == true) continue;
										int c = (int)acc["count"]!;
										if (c > max) max = c;
									}
									vertexCount = max;
								}
							}

							// Convert: iterate accessors, but we will perform conversions grouped by semantic names
							if (accessors != null && bufferViews != null)
							{
								// For readability: find mapping from attribute semantic to accessor index by scanning meshes
								// This mimics how a loader finds NORMAL/TANGENT attributes.
								Dictionary<string, int> semanticToAccessor = [];
								if (json["meshes"] is JArray meshes)
								{
									foreach (JObject mesh in meshes.Cast<JObject>())
									{
										JArray primitives = (JArray?)mesh["primitives"];
										if (primitives == null) continue;
										foreach (JObject prim in primitives.Cast<JObject>())
										{
											JObject attrs = (JObject?)prim["attributes"];
											if (attrs == null) continue;
											foreach (KeyValuePair<string, JToken> kv in attrs)
											{
												string semantic = kv.Key;
												int accIndex = kv.Value.Value<int>();
												if (!semanticToAccessor.ContainsKey(semantic))
													semanticToAccessor[semantic] = accIndex;
											}
											// indices
											if (prim["indices"] != null)
											{
												indexCount = (int)accessors[(int)prim["indices"]]["count"]!;
												indicesComponentType = (int)accessors[(int)prim["indices"]]["componentType"]!;
												// copy indices bytes: find its bufferView and copy bytes exactly
												JObject idxAcc = (JObject)accessors[(int)prim["indices"]];
												int idxBV = idxAcc["bufferView"] != null ? (int)idxAcc["bufferView"]! : -1;
												if (idxBV >= 0)
												{
													JObject bv = (JObject)bufferViews[idxBV];
													int bvOffset = bv["byteOffset"] != null ? (int)bv["byteOffset"]! : 0;
													int bvLen = (int)bv["byteLength"]!;
													indicesBytes = new byte[bvLen];
													Array.Copy(glbBinBytesPre, bvOffset, indicesBytes, 0, bvLen);
												}
											}
										}
									}
								}

								byte[] bin = glbBinBytesPre;

								// If an accessor points into an interleaved bufferView, decode accordingly.
								// We'll populate lists for each attribute if present.
								if (semanticToAccessor.TryGetValue("POSITION", out int posAccIdx))
								{
									// Read positions as-is (should be VEC3 FLOAT)
									JObject acc = (JObject)accessors[posAccIdx];
									int bvIndex = acc["bufferView"] != null ? (int)acc["bufferView"]! : -1;
									if (bvIndex >= 0)
									{
										JObject bv = (JObject)bufferViews[bvIndex];
										int bvOffset = bv["byteOffset"] != null ? (int)bv["byteOffset"]! : 0;
										int bvStride = bv["byteStride"] != null ? (int)bv["byteStride"]! : 0;
										int count = (int)acc["count"]!;
										positions = new List<float>(count * 3);
										int elementSize = ComponentSize((int)acc["componentType"]!) * ComponentCount((string)acc["type"]!);
										int stride = bvStride != 0 ? bvStride : elementSize;
										int accOffset = acc["byteOffset"] != null ? (int)acc["byteOffset"]! : 0;
										for (int k = 0; k < count; k++)
										{
											int baseOff = bvOffset + accOffset + k * stride;
											float x = ReadFloat(bin, baseOff + 0);
											float y = ReadFloat(bin, baseOff + 4);
											float z = ReadFloat(bin, baseOff + 8);
											positions.Add(x); positions.Add(y); positions.Add(z);
										}
									}
								}

								// TANGENT
								if (semanticToAccessor.TryGetValue("TANGENT", out int tanAccIdx))
								{
									JObject acc = (JObject)accessors[tanAccIdx];
									int bvIndex = acc["bufferView"] != null ? (int)acc["bufferView"]! : -1;
									if (bvIndex >= 0)
									{
										JObject bv = (JObject)bufferViews[bvIndex];
										int bvOffset = bv["byteOffset"] != null ? (int)bv["byteOffset"]! : 0;
										int bvStride = bv["byteStride"] != null ? (int)bv["byteStride"]! : 0;
										int count = (int)acc["count"]!;
										tangents = new List<float>(count * 4);
										int elementSize = ComponentSize((int)acc["componentType"]!) * ComponentCount((string)acc["type"]!);
										int stride = bvStride != 0 ? bvStride : elementSize;
										int accOffset = acc["byteOffset"] != null ? (int)acc["byteOffset"]! : 0;
										for (int k = 0; k < count; k++)
										{
											int baseOff = bvOffset + accOffset + k * stride;
											// four signed bytes
											float tx = ReadSByte(bin, baseOff + 0) / 127.0f;
											float ty = ReadSByte(bin, baseOff + 1) / 127.0f;
											float tz = ReadSByte(bin, baseOff + 2) / 127.0f;
											float tw = ReadSByte(bin, baseOff + 3) / 127.0f;
											// normalize xyz components
											float len = (float)Math.Sqrt(tx * tx + ty * ty + tz * tz);
											if (len > 1e-6f) { tx /= len; ty /= len; tz /= len; } else { tx = 0; ty = 0; tz = 1; }
											tangents.Add(tx); tangents.Add(ty); tangents.Add(tz); tangents.Add(tw);
										}
									}
								}

								// NORMAL
								if (semanticToAccessor.TryGetValue("NORMAL", out int nAccIdx))
								{
									JObject acc = (JObject)accessors[nAccIdx];
									int bvIndex = acc["bufferView"] != null ? (int)acc["bufferView"]! : -1;
									if (bvIndex >= 0)
									{
										JObject bv = (JObject)bufferViews[bvIndex];
										int bvOffset = bv["byteOffset"] != null ? (int)bv["byteOffset"]! : 0;
										int bvStride = bv["byteStride"] != null ? (int)bv["byteStride"]! : 0;
										int count = (int)acc["count"]!;
										normals = new List<float>(count * 3);
										int elementSize = ComponentSize((int)acc["componentType"]!) * ComponentCount((string)acc["type"]!);
										int stride = bvStride != 0 ? bvStride : elementSize;
										int accOffset = acc["byteOffset"] != null ? (int)acc["byteOffset"]! : 0;
										for (int k = 0; k < count; k++)
										{
											int baseOff = bvOffset + accOffset + k * stride;
											float nx = ReadSByte(bin, baseOff + 0) / 127.0f;
											float ny = ReadSByte(bin, baseOff + 1) / 127.0f;
											float nz = ReadSByte(bin, baseOff + 2) / 127.0f;
											// normalize
											float len = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
											if (len > 1e-6f) { nx /= len; ny /= len; nz /= len; } else { nx = 0; ny = 0; nz = 1; }
											normals.Add(nx); normals.Add(ny); normals.Add(nz);
										}
									}
								}

								// Process all TEXCOORD_n attributes
								foreach (KeyValuePair<string, int> kvp in semanticToAccessor.Where(kv => kv.Key.StartsWith("TEXCOORD_")))
								{
									string semantic = kvp.Key;
									int uvAccIdx = kvp.Value;
									JObject acc = (JObject)accessors[uvAccIdx];
									int componentType = (int)acc["componentType"]!;
									string type = (string)acc["type"]!;
									Console.WriteLine($"TEXCOORD {semantic}: componentType={componentType}, type={type}");

									int bvIndex = acc["bufferView"] != null ? (int)acc["bufferView"]! : -1;
									if (bvIndex >= 0)
									{
										JObject bv = (JObject)bufferViews[bvIndex];
										int bvOffset = bv["byteOffset"] != null ? (int)bv["byteOffset"]! : 0;
										int bvStride = bv["byteStride"] != null ? (int)bv["byteStride"]! : 0;
										int count = (int)acc["count"]!;
										Console.WriteLine($"  bufferView stride={bvStride}, offset={bvOffset}, count={count}");

										List<float> uvList = new(count * 2);
										int elementSize = ComponentSize((int)acc["componentType"]!) * ComponentCount((string)acc["type"]!);
										int stride = bvStride != 0 ? bvStride : elementSize;
										int accOffset = acc["byteOffset"] != null ? (int)acc["byteOffset"]! : 0;

										// Print first few UV values for debugging
										for (int k = 0; k < Math.Min(5, count); k++)
										{
											int baseOff = bvOffset + accOffset + k * stride;
											ushort su = ReadUInt16(bin, baseOff + 0);
											ushort sv = ReadUInt16(bin, baseOff + 2);
											// Read as half-precision float (float16)
											float u = (float)BitConverter.UInt16BitsToHalf(su);
											float v = (float)BitConverter.UInt16BitsToHalf(sv);
											Console.WriteLine($"  UV[{k}]: raw=({su:X4}, {sv:X4}), as half float=({u:F4}, {v:F4})");
										}

										for (int k = 0; k < count; k++)
										{
											int baseOff = bvOffset + accOffset + k * stride;
											ushort su = ReadUInt16(bin, baseOff + 0);
											ushort sv = ReadUInt16(bin, baseOff + 2);
											// Read as half-precision float (float16)
											float u = (float)BitConverter.UInt16BitsToHalf(su);
											float v = (float)BitConverter.UInt16BitsToHalf(sv);
											uvList.Add(u);
											uvList.Add(v);
										}
										texcoords[semantic] = uvList;
									}
								}

								// Process all COLOR_n attributes
								foreach (KeyValuePair<string, int> kvp in semanticToAccessor.Where(kv => kv.Key.StartsWith("COLOR_")))
								{
									string semantic = kvp.Key;
									int colAccIdx = kvp.Value;
									JObject acc = (JObject)accessors[colAccIdx];
									int bvIndex = acc["bufferView"] != null ? (int)acc["bufferView"]! : -1;
									if (bvIndex >= 0)
									{
										JObject bv = (JObject)bufferViews[bvIndex];
										int bvOffset = bv["byteOffset"] != null ? (int)bv["byteOffset"]! : 0;
										int bvStride = bv["byteStride"] != null ? (int)bv["byteStride"]! : 0;
										int count = (int)acc["count"]!;
										List<float> colorList = new(count * 4);
										int elementSize = ComponentSize((int)acc["componentType"]!) * ComponentCount((string)acc["type"]!);
										int stride = bvStride != 0 ? bvStride : elementSize;
										int accOffset = acc["byteOffset"] != null ? (int)acc["byteOffset"]! : 0;
										for (int k = 0; k < count; k++)
										{
											int baseOff = bvOffset + accOffset + k * stride;
											ushort r = ReadUInt16(bin, baseOff + 0);
											ushort g = ReadUInt16(bin, baseOff + 2);
											ushort b = ReadUInt16(bin, baseOff + 4);
											ushort a = ReadUInt16(bin, baseOff + 6);
											colorList.Add(r / 65535f);
											colorList.Add(g / 65535f);
											colorList.Add(b / 65535f);
											colorList.Add(a / 65535f);
										}
										colors[semantic] = colorList;
									}
								}
							}

							// --- Build new tightly-packed bufferViews and update accessors ---
							// We'll append in order: positions, normals, tangents, tex0, tex1, colors, indices
							Dictionary<string, int> bvIndexMapping = []; // semantic -> new bufferView index

							void AddFloatArrayAsBufferView(List<float> arr, int components, int target, string semantic)
							{
								if (arr == null) return;
								int byteOffset = newBin.Count;
								for (int i = 0; i < arr.Count; i++)
								{
									newBin.AddRange(BitConverter.GetBytes(arr[i]));
								}
								int byteLength = arr.Count * 4;
								JObject bv = new()
								{
									["buffer"] = 0,
									["byteOffset"] = byteOffset,
									["byteLength"] = byteLength,
									["target"] = target
								};
								int newIndex = newBufferViews.Count;
								newBufferViews.Add(bv);
								bvIndexMapping[semantic] = newIndex;
							}

							// positions (ARRAY_BUFFER 34962)
							AddFloatArrayAsBufferView(positions, 3, 34962, "POSITION");
							AddFloatArrayAsBufferView(normals, 3, 34962, "NORMAL");
							AddFloatArrayAsBufferView(tangents, 4, 34962, "TANGENT");

							// Add all texcoords
							foreach (KeyValuePair<string, List<float>> kvp in texcoords)
							{
								AddFloatArrayAsBufferView(kvp.Value, 2, 34962, kvp.Key);
							}

							// Add all colors
							foreach (KeyValuePair<string, List<float>> kvp in colors)
							{
								AddFloatArrayAsBufferView(kvp.Value, 4, 34962, kvp.Key);
							}

							// indices
							int indicesBVIndex = -1;
							if (indicesBytes != null)
							{
								int idxByteOffset = newBin.Count;
								newBin.AddRange(indicesBytes);
								JObject bv = new()
								{
									["buffer"] = 0,
									["byteOffset"] = idxByteOffset,
									["byteLength"] = indicesBytes.Length,
									["target"] = 34963 // ELEMENT_ARRAY_BUFFER
								};
								indicesBVIndex = newBufferViews.Count;
								newBufferViews.Add(bv);
							}

							// Update accessors to point to new bufferViews
							if (accessors != null)
							{
								for (int a = 0; a < accessors.Count; a++)
								{
									JObject acc = (JObject)accessors[a]!;
									string accName = (string?)acc["name"] ?? "";
									// map by name suffix (common pattern in these files)
									if (accName.Contains("_POSITION") && bvIndexMapping.TryGetValue("POSITION", out int posValue))
									{
										acc["bufferView"] = posValue;
										acc["componentType"] = 5126;
										acc["type"] = "VEC3";
										acc.Remove("byteOffset");
									}
									else if (accName.Contains("_NORMAL") && bvIndexMapping.TryGetValue("NORMAL", out int normalValue))
									{
										acc["bufferView"] = normalValue;
										acc["componentType"] = 5126;
										acc["type"] = "VEC3";
										acc.Remove("byteOffset");
									}
									else if (accName.Contains("_TANGENT") && bvIndexMapping.TryGetValue("TANGENT", out int tangentValue))
									{
										acc["bufferView"] = tangentValue;
										acc["componentType"] = 5126;
										acc["type"] = "VEC4";
										acc.Remove("byteOffset");
									}
									// Handle all TEXCOORD_n
									else if (accName.Contains("_TEXCOORD_"))
									{
										// Extract semantic like "TEXCOORD_0", "TEXCOORD_1", etc.
										string[] parts = accName.Split('_');
										for (int partIdx = 0; partIdx < parts.Length - 1; partIdx++)
										{
											if (parts[partIdx] == "TEXCOORD")
											{
												string semantic = $"TEXCOORD_{parts[partIdx + 1]}";
												if (bvIndexMapping.TryGetValue(semantic, out int texcoordValue))
												{
													acc["bufferView"] = texcoordValue;
													acc["componentType"] = 5126;
													acc["type"] = "VEC2";
													acc.Remove("byteOffset");
													break;
												}
											}
										}
									}
									// Handle all COLOR_n
									else if (accName.Contains("_COLOR_"))
									{
										// Extract semantic like "COLOR_0", "COLOR_1", etc.
										string[] parts = accName.Split('_');
										for (int partIdx = 0; partIdx < parts.Length - 1; partIdx++)
										{
											if (parts[partIdx] == "COLOR")
											{
												string semantic = $"COLOR_{parts[partIdx + 1]}";
												if (bvIndexMapping.TryGetValue(semantic, out int colorValue))
												{
													acc["bufferView"] = colorValue;
													acc["componentType"] = 5126;
													acc["type"] = "VEC4";
													acc.Remove("byteOffset");
													break;
												}
											}
										}
									}
									else
									{
										// Possibly indices accessor — leave as-is but point to new index BV if we have one
										if (acc["type"]?.ToString() == "SCALAR" && indicesBVIndex >= 0)
										{
											acc["bufferView"] = indicesBVIndex;
											// keep componentType as original (likely 5123)
											acc.Remove("byteOffset");
										}
									}
								}
							}

							// Replace JSON bufferViews and buffers
							json["bufferViews"] = newBufferViews;
							json["buffers"] = new JArray(new JObject(new JProperty("byteLength", newBin.Count)));

							// Build JSON text and pad with spaces (0x20)
							string outJson = json.ToString(Newtonsoft.Json.Formatting.None);
							byte[] outJsonBytes = Encoding.UTF8.GetBytes(outJson);
							int jsonPad = (4 - (outJsonBytes.Length % 4)) % 4;
							byte[] outJsonPadded = new byte[outJsonBytes.Length + jsonPad];
							Array.Copy(outJsonBytes, outJsonPadded, outJsonBytes.Length);
							for (int p = 0; p < jsonPad; p++) outJsonPadded[outJsonBytes.Length + p] = 0x20;

							// Build BIN padded (0x00)
							byte[] outBin = [.. newBin];
							int binPad = (4 - (outBin.Length % 4)) % 4;
							byte[] outBinPadded = new byte[outBin.Length + binPad];
							Array.Copy(outBin, outBinPadded, outBin.Length);
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

static sbyte ReadSByte(byte[] binArr, int off) => unchecked((sbyte)binArr[off]);
static short ReadInt16(byte[] binArr, int off) => (short)(binArr[off] | (binArr[off + 1] << 8));
static ushort ReadUInt16(byte[] binArr, int off) => (ushort)(binArr[off] | (binArr[off + 1] << 8));
static float ReadFloat(byte[] binArr, int off)
{
	return BitConverter.ToSingle(binArr, off); // BitConverter uses system endianness; glb is little-endian on most machines.
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
}