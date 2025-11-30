using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Newtonsoft.Json.Linq;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;

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
string tempPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp", "protoscone");

if (!Directory.Exists(tempPath))
{
	_ = Directory.CreateDirectory(tempPath);
}

if (!Directory.Exists(args[1]))
{
	_ = Directory.CreateDirectory(args[1]);
}
string[] allBglFiles = Directory.GetFiles(args[0], "*.bgl", SearchOption.AllDirectories);
List<Guid> sceneryGuids = [];
HashSet<Guid> modelGuids = [];
Console.WriteLine($"Found files:\n{string.Join(",\n", allBglFiles)}");
Dictionary<Guid, List<LibraryObject>> libraryObjects = [];
List<ModelData> modelDatas = [];
foreach (string file in allBglFiles)
{
	using FileStream fs = new(file, FileMode.Open, FileAccess.Read);
	using BinaryReader br = new(fs);

	// Read and validate BGL header
	byte[] magicNumber1 = br.ReadBytes(4);
	_ = br.BaseStream.Seek(0x10, SeekOrigin.Begin);
	byte[] magicNumber2 = br.ReadBytes(4);
	if (!magicNumber1.SequenceEqual(new byte[] { 0x01, 0x02, 0x92, 0x19 }) ||
		!magicNumber2.SequenceEqual(new byte[] { 0x03, 0x18, 0x05, 0x08 }))
	{
		Console.WriteLine("Invalid BGL header");
		return;
	}
	_ = br.BaseStream.Seek(0x14, SeekOrigin.Begin);
	uint recordCt = br.ReadUInt32();

	// Skip 0x38-byte header
	_ = br.BaseStream.Seek(0x38, SeekOrigin.Begin);

	int foundMdlData = 0;
	int foundSceneryObj = 0;

	List<int> mdlDataOffsets = [];
	List<int> sceneryObjectOffsets = [];
	for (int i = 0; i < recordCt; i++)
	{
		long recordStartPos = br.BaseStream.Position;
		uint recType = br.ReadUInt32();
		_ = br.BaseStream.Seek(recordStartPos + 0x0C, SeekOrigin.Begin);
		uint startSubsection = br.ReadUInt32();
		_ = br.BaseStream.Seek(recordStartPos + 0x10, SeekOrigin.Begin);
		uint recSize = br.ReadUInt32();
		if (recType == 0x002B) // ModelData
		{
			foundMdlData++;
			mdlDataOffsets.Add((int)startSubsection);
		}
		else if (recType == 0x0025) // SceneryObject
		{
			foundSceneryObj++;
			sceneryObjectOffsets.Add((int)startSubsection);
			Console.WriteLine($"Found SceneryObject at offset 0x{startSubsection:X}, total found: {foundSceneryObj}");
		}
	}

	// Parse SceneryObject subrecords
	List<(int offset, int size)> sceneryObjectSubrecords = [];
	foreach (int sceneryOffset in sceneryObjectOffsets)
	{
		_ = br.BaseStream.Seek(sceneryOffset + 4, SeekOrigin.Begin);
		int subrecCount = br.ReadInt32();
		int subrecOffset = (int)br.ReadUInt32();
		int size = (int)br.ReadUInt32();
		Console.WriteLine($"SceneryObject Subrecord Count: {subrecCount}, Offset: 0x{subrecOffset:X}, Size: {size}");
		sceneryObjectSubrecords.Add((subrecOffset, size));
	}

	int bytesRead = 0;
	foreach ((int subOffset, int subSize) in sceneryObjectSubrecords)
	{
		bytesRead = 0;
		while (bytesRead < subSize)
		{
			_ = br.BaseStream.Seek(subOffset + bytesRead, SeekOrigin.Begin);
			ushort id = br.ReadUInt16();
			if (id != 0x0B) // LibraryObject
			{
				uint skip = br.ReadUInt16();
				Console.WriteLine($"Unexpected subrecord type at offset 0x{subOffset + bytesRead:X}: 0x{id:X4}, skipping {skip} bytes");
				_ = br.BaseStream.Seek(subOffset + skip, SeekOrigin.Begin);
				bytesRead += (int)skip;
				continue;
			}
			ushort size = br.ReadUInt16();
			uint longitude = br.ReadUInt32(), latitude = br.ReadUInt32();
			short altitude = br.ReadInt16();
			byte[] flagsBytes = br.ReadBytes(6);
			List<Flags> flagsList = [];
			for (int j = 0; j < flagsBytes.Length; j++)
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
			Guid guidEmpty = new(br.ReadBytes(16));
			Guid guid = new(br.ReadBytes(16));
			double scale = br.ReadSingle(); // Read as float from file, store as double for precision
			_ = br.BaseStream.Seek(2, SeekOrigin.Current); // There is an unknown 2-byte field here
			LibraryObject libObj = new()
			{
				id = id,
				size = size,
				longitude = (longitude * (360.0 / 805306368.0)) - 180.0,
				latitude = 90.0 - (latitude * (180.0 / 536870912.0)),
				altitude = flags.Contains(Flags.IsAboveAGL) ? altitude + Terrain.GetElevation(90.0 - (latitude * (180.0 / 536870912.0)), (longitude * (360.0 / 805306368.0)) - 180.0) : altitude,
				flags = flags,
				pitch = Math.Round(pitch * (360.0 / 65536.0), 3),
				bank = Math.Round(bank * (360.0 / 65536.0), 3),
				heading = Math.Round(heading * (360.0 / 65536.0), 3),
				imageComplexity = imageComplexity,
				guid = guid,
				scale = Math.Round(scale + 1, 3)
			};
			if (!libraryObjects.TryGetValue(guid, out List<LibraryObject>? _))
			{
				libraryObjects[guid] = [];
			}
			sceneryGuids.Add(guid);
			libraryObjects[guid].Add(libObj);
			Console.WriteLine($"{guid}\t{guidEmpty}\t{libObj.size}\t{libObj.longitude:F6}\t{libObj.latitude:F6}\t{libObj.altitude}\t[{string.Join(",", libObj.flags)}]\t{libObj.pitch:F2}\t{libObj.bank:F2}\t{libObj.heading:F2}\t{libObj.imageComplexity}\t{libObj.scale}");
			bytesRead += size;
		}
	}

	// Parse ModelData subrecords
	List<(int offset, int size)> modelDataSubrecords = [];
	foreach (int modelDataOffset in mdlDataOffsets)
	{
		_ = br.BaseStream.Seek(modelDataOffset + 4, SeekOrigin.Begin);
		int subrecCount = br.ReadInt32();
		int subrecOffset = br.ReadInt32();
		int size = br.ReadInt32();
		modelDataSubrecords.Add((subrecOffset, size));
	}

	int objectsRead = 0;
	foreach ((int subOffset, int subSize) in modelDataSubrecords)
	{
		// Reset per-subrecord counters so all subrecords are processed
		bytesRead = 0;
		objectsRead = 0;

		while (bytesRead < subSize)
		{
			_ = br.BaseStream.Seek(subOffset + (24 * objectsRead), SeekOrigin.Begin);
			byte[] guidBytes = br.ReadBytes(16);
			Guid guid = new(guidBytes);
			uint startModelDataOffset = br.ReadUInt32();
			uint modelDataSize = br.ReadUInt32();
			_ = br.BaseStream.Seek(subOffset + startModelDataOffset, SeekOrigin.Begin);
			byte[] mdlBytes = br.ReadBytes((int)modelDataSize);
			string name = "";
			List<LodData> lods = [];
			string chunkID = Encoding.ASCII.GetString(mdlBytes, 0, Math.Min(4, mdlBytes.Length));
			if (chunkID != "RIFF")
			{
				break;
			}
			List<ModelObject> modelObjects = [];
			// Enter this model and get LOD info, GLB files, and mesh data
			for (int i = 8; i < mdlBytes.Length; i += 4)
			{
				string chunk = Encoding.ASCII.GetString(mdlBytes, i, Math.Min(4, mdlBytes.Length - i));
				if (chunk == "GXML")
				{
					int size = BitConverter.ToInt32(mdlBytes, i + 4);
					string gxmlContent = Encoding.UTF8.GetString(mdlBytes, i + 8, size);
					try
					{
						XmlDocument xmlDoc = new();
						xmlDoc.LoadXml(gxmlContent);
						name = xmlDoc.GetElementsByTagName("ModelInfo")[0]?.Attributes?["name"]?.Value.Replace(".gltf", "") ?? "Unnamed_Model";
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
					}
					catch (XmlException)
					{
						Console.WriteLine($"Failed to parse GXML for model {guid:X}");
					}
					i += size;
				}
				else if (chunk == "GLBD")
				{
					int size = BitConverter.ToInt32(mdlBytes, i + 4);
					int glbIndex = 0; // for unique filenames per GLB in this chunk

					// Scan GLBD payload and skip past each GLB block once processed
					for (int j = i + 8; j < i + 8 + size;)
					{
						// Ensure there are at least 8 bytes for type + size
						if (j + 8 > mdlBytes.Length) break;

						string sig = Encoding.ASCII.GetString(mdlBytes, j, Math.Min(4, mdlBytes.Length - j));
						if (sig == "GLB\0")
						{
							int glbSize = BitConverter.ToInt32(mdlBytes, j + 4);
							// byte[] glbBytesPre = br.ReadBytes(glbSize);
							byte[] glbBytes = mdlBytes[(j + 8)..(j + 8 + glbSize)];
							byte[] glbBytesJson = mdlBytes[(j + 8)..(j + 8 + glbSize)]; // Copy this for additional safety in processing the JSON

							// Fill the end of the JSON chunk with spaces, and replace non-printable characters with spaces.
							uint JSONLength = BitConverter.ToUInt32(glbBytesJson, 0x0C);
							for (int k = 0x14; k < 0x14 + JSONLength; k++)
							{
								if (glbBytesJson[k] < 0x20 || glbBytesJson[k] > 0x7E)
								{
									glbBytesJson[k] = 0x20;
								}
							}

							uint binLength = BitConverter.ToUInt32(glbBytes, 0x14 + (int)JSONLength);
							byte[] glbBinBytes = glbBytes[(0x14 + (int)JSONLength + 8)..(0x14 + (int)JSONLength + 8 + (int)binLength)];

							JObject json = JObject.Parse(Encoding.UTF8.GetString(glbBytesJson, 0x14, (int)JSONLength).Trim());
							JArray meshes = (JArray)json["meshes"]!;
							JArray accessors = (JArray)json["accessors"]!;
							JArray bufferViews = (JArray)json["bufferViews"]!;
							JArray images = (JArray)json["images"]!;
							JArray materials = (JArray)json["materials"]!;

							SceneBuilder scene = new();
							Dictionary<int, List<int>> meshIndexToSceneNodeIndex = [];
							for (int k = 0; k < json["nodes"]!.Count(); k++)
							{
								JObject node = (JObject)json["nodes"]![k]!;
								if (node["mesh"] != null)
								{
									int meshIndex = node["mesh"]!.Value<int>();
									if (!meshIndexToSceneNodeIndex.TryGetValue(meshIndex, out List<int>? value))
									{
										value = [];
										meshIndexToSceneNodeIndex[meshIndex] = value;
									}
									meshIndexToSceneNodeIndex[meshIndex].Add(k);
								}
							}
							foreach (JObject mesh in meshes.Cast<JObject>())
							{
								Console.WriteLine($"Processing mesh: {mesh["name"]?.Value<string>() ?? "UnnamedMesh"}");
								MeshBuilder<VertexPositionNormalTangent, VertexTexture2, VertexEmpty> meshBuilder = GlbBuilder.BuildMesh(args[0], file, mesh, accessors, bufferViews, materials, images, glbBinBytes);
								Console.WriteLine($"Built mesh with {meshBuilder.Primitives.Count} primitives.");
								foreach (var prim in meshBuilder.Primitives)
								{
									Console.WriteLine($"Primitive has {prim.Vertices.Count} vertices and {prim.GetIndices().Count} indices.");
								}
								Vector3 translationFinal = Vector3.Zero;
								Quaternion rotationFinal = Quaternion.Identity;
								Vector3 scaleFinal = Vector3.One;
								foreach (int nodeIndex in meshIndexToSceneNodeIndex[meshes.IndexOf(mesh)])
								{
									NodeBuilder node = GlbBuilder.BuildNode(nodeIndex, (JArray)json["nodes"]!);
									translationFinal += node.Translation == null ? Vector3.Zero : node.Translation.Value;
									rotationFinal *= node.Rotation == null ? Quaternion.Identity : node.Rotation.Value;
									scaleFinal *= node.Scale == null ? Vector3.One : node.Scale.Value;
								}

								float avgScale = (scaleFinal.X + scaleFinal.Y + scaleFinal.Z) / 3.0f;
								scaleFinal = new Vector3(avgScale, avgScale, avgScale);

								// Console.WriteLine($"Final Transform - Translation: {translationFinal}, Rotation: {rotationFinal}, Scale: {scaleFinal}");
								// meshBuilder is your MeshBuilder<VertexPositionNormalTangent> (or whichever type)
								Matrix4x4 transform = Matrix4x4.CreateScale(scaleFinal) *
												Matrix4x4.CreateFromQuaternion(rotationFinal) *
												Matrix4x4.CreateTranslation(translationFinal);
								scene.AddRigidMesh(meshBuilder, transform);
							}

							// Write GLB with unique filename (include index to avoid overwrites)
							string safeName = name;
							string outName = glbIndex < lods.Count ? lods[glbIndex].name : $"{safeName}_glb{glbIndex}";
							modelObjects.Add(new ModelObject
							{
								name = outName.Replace(" ", "_"),
								minSize = glbIndex < lods.Count ? lods[glbIndex].minSize : 0,
								model = scene
							});
							glbIndex++;

							// Advance j past this GLB record (type[4] + size[4] + payload[glbSize])
							j += 8 + glbSize;
						}
						else
						{
							// Not a GLB block; advance reasonably (try to skip unknown 8-byte header or 4-byte step)
							// Prefer 4-byte alignment advance to find next signature
							j += 4;
						}
					}

					// Advance i past the GLBD chunk payload
					i += size;
				}
			}
			modelDatas.Add(new ModelData
			{
				guid = guid,
				name = name,
				modelObjects = modelObjects,
			});
			modelGuids.Add(guid);
			bytesRead += (int)modelDataSize + 24;
			objectsRead++;
		}
	}
}
Console.WriteLine($"Total LibraryObjects parsed: {libraryObjects.Count}. Total ModelDatas parsed: {modelDatas.Count}.");

// We are more likely to have LibraryObjects that don't have corresponding ModelData than vice versa, so iterate over ModelDatas
Dictionary<int, List<string>> finalPlacementsByTile = [];
foreach (ModelData modelData in modelDatas)
{
	if (libraryObjects.TryGetValue(modelData.guid, out List<LibraryObject>? value))
	{
		foreach (ModelObject modelObj in modelData.modelObjects)
		{
			string outGlbPath = Path.Combine(args[1], $"{modelObj.name}.glb");
			modelObj.model.ToGltf2().SaveGLB(outGlbPath, new WriteSettings { ImageWriting = ResourceWriteMode.SatelliteFile });
		}
		int[] scales = [.. libraryObjects[modelData.guid].Select(lo => (int)lo.scale).Distinct()];
		string activeName = "";
		if (modelData.modelObjects.Count == 1 && scales.Length == 0 && modelData.lightObjects.Count == 0)
		{
			activeName = $"{modelData.modelObjects[0].name}.glb";
		}
		else
		{
			XmlDocument doc = new();
			XmlElement root = doc.CreateElement("PropertyList");
			root.AppendChild(doc.CreateComment("Generated by Scone"));
			foreach (ModelObject modelObj in modelData.modelObjects)
			{
				XmlElement objElem = doc.CreateElement("model");
				objElem.AppendChild(doc.CreateElement("name"))!.InnerText = modelObj.name;
				objElem.AppendChild(doc.CreateElement("path"))!.InnerText = $"{modelObj.name}.glb";
				root.AppendChild(objElem);
			}
			Console.WriteLine(root.OuterXml);
			// activeName = $"{modelData.modelObjects[0].name}-{}"
		}
		if (!finalPlacementsByTile.ContainsKey(Terrain.GetTileIndex(value[0].latitude, value[0].longitude)))
		{
			finalPlacementsByTile[Terrain.GetTileIndex(value[0].latitude, value[0].longitude)] = [];
		}
		foreach (LibraryObject libObj in value)
		{
			string placementStr = $"OBJECT_STATIC {activeName} {libObj.longitude:F6} {libObj.latitude:F6} {libObj.altitude} {libObj.heading:F2} {libObj.pitch:F2} {libObj.bank:F2}";
			finalPlacementsByTile[Terrain.GetTileIndex(libObj.latitude, libObj.longitude)].Add(placementStr);
		}
	}
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
	public int id;
	public int size;
	public double longitude;
	public double latitude;
	public double altitude;
	public Flags[] flags;
	public double pitch;
	public double bank;
	public double heading;
	public int imageComplexity;
	public Guid guid;
	public double scale;
}

struct LodData
{
	public string name;
	public int minSize;
}

struct ModelObject
{
	public string name;
	public int minSize;
	public SceneBuilder model;
}

struct LightObject
{

}

struct ModelData
{
	public Guid guid;
	public string name;
	public List<ModelObject> modelObjects;
	public List<LightObject> lightObjects;
}