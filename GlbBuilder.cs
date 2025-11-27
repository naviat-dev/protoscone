using System.Numerics;
using Newtonsoft.Json.Linq;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;

public class GlbBuilder
{

	public class PrimData
	{
		public Vector3[] Positions { get; set; } = [];
		public Vector3[] Normals { get; set; } = [];
		public List<Vector2[]> TexCoords { get; set; } = [];
		public Vector4[] Tangents { get; set; } = [];
		public int[] Indices { get; set; } = [];
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

	public static NodeBuilder BuildNode(int nodeIndex, JArray nodesJson)
	{
		JObject nodeJson = (JObject)nodesJson[nodeIndex];
		NodeBuilder node = new(nodeJson["name"]?.Value<string>() ?? $"Node_{nodeIndex}");
		// Apply transformations if present
		if (nodeJson["translation"] != null)
		{
			JArray translation = (JArray)nodeJson["translation"]!;
			node.WithLocalTranslation(new Vector3(translation[0].Value<float>(), translation[1].Value<float>(), translation[2].Value<float>()));
		}
		if (nodeJson["rotation"] != null)
		{
			JArray rotation = (JArray)nodeJson["rotation"]!;
			node.WithLocalRotation(new Quaternion(rotation[0].Value<float>(), rotation[1].Value<float>(), rotation[2].Value<float>(), rotation[3].Value<float>()));
		}
		if (nodeJson["scale"] != null)
		{
			JArray scale = (JArray)nodeJson["scale"]!;
			node.WithLocalScale(new Vector3(scale[0].Value<float>(), scale[1].Value<float>(), scale[2].Value<float>()));
		}

		return node;
	}

	public static MeshBuilder<VertexPositionNormalTangent, VertexTexture2, VertexEmpty> BuildMesh(JObject meshJson, JArray accessorsJson, JArray bufferViewsJson, byte[] glbBinBytes)
	{

		MeshBuilder<VertexPositionNormalTangent, VertexTexture2, VertexEmpty> mesh = new(meshJson["name"]?.Value<string>() ?? "UnnamedMesh");
		foreach (JObject primJson in ((JArray)meshJson["primitives"]!).Cast<JObject>())
		{
			PrimitiveBuilder<MaterialBuilder, VertexPositionNormalTangent, VertexTexture2, VertexEmpty> prim = mesh.UsePrimitive(MaterialBuilder.CreateDefault());
			JObject attributes = (JObject)primJson["attributes"]!;
			PrimData data = new();

			// Load indices
			int idxAccIndex = (int)primJson["indices"]!;
			if (accessorsJson.Count > idxAccIndex)
			{
				data.Indices = LoadIndexData((JObject)accessorsJson[idxAccIndex], bufferViewsJson, glbBinBytes);
			}

			// Load positions
			int posAccIndex = (int)attributes["POSITION"]!;
			if (accessorsJson.Count > posAccIndex)
			{
				data.Positions = LoadPositionAccessorData((JObject)accessorsJson[posAccIndex], bufferViewsJson, glbBinBytes);
			}

			// Load normals
			int normAccIndex = (int)attributes["NORMAL"]!;
			if (accessorsJson.Count > normAccIndex)
			{
				data.Normals = LoadNormalAccessorData((JObject)accessorsJson[normAccIndex], bufferViewsJson, glbBinBytes);
			}

			// Load tangents
			if (attributes["TANGENT"] != null)
			{
				int tangAccIndex = (int)attributes["TANGENT"]!;
				if (accessorsJson.Count > tangAccIndex)
				{
					data.Tangents = LoadTangentAccessorData((JObject)accessorsJson[tangAccIndex], bufferViewsJson, glbBinBytes);
				}
			}

			// Load all texture coordinate sets
			for (int texCoordIndex = 0; texCoordIndex < 2; texCoordIndex++)
			{
				string texCoordKey = $"TEXCOORD_{texCoordIndex}";
				if (attributes[texCoordKey] != null)
				{
					int uvAccIndex = (int)attributes[texCoordKey]!;
					if (accessorsJson.Count > uvAccIndex)
					{
						data.TexCoords.Add(LoadTexCoordAccessorData((JObject)accessorsJson[uvAccIndex], bufferViewsJson, glbBinBytes));
					}
				}
				else
				{
					break; // Stop when we hit the first missing TEXCOORD_N
				}
			}

			for (int i = 0; i < data.Indices.Length; i += 3)
			{
				int idx1 = data.Indices[i];
				int idx2 = data.Indices[i + 1];
				int idx3 = data.Indices[i + 2];

				int baseVertex = primJson["extras"]?["ASOBO_primitive"]?["BaseVertexIndex"]?.Value<int>() ?? 0;

				VertexPositionNormalTangent geo1 = new(data.Positions[idx1], -data.Normals[idx1], data.Tangents.Length > 0 ? -data.Tangents[idx1] : Vector4.Zero);
				VertexPositionNormalTangent geo2 = new(data.Positions[idx2], -data.Normals[idx2], data.Tangents.Length > 0 ? -data.Tangents[idx2] : Vector4.Zero);
				VertexPositionNormalTangent geo3 = new(data.Positions[idx3], -data.Normals[idx3], data.Tangents.Length > 0 ? -data.Tangents[idx3] : Vector4.Zero);

				MaterialBuilder mat = MaterialBuilder.CreateDefault();

				Vector2 uv0_1 = data.TexCoords.Count > 0 ? data.TexCoords[0][idx1] : Vector2.Zero;
				Vector2 uv0_2 = data.TexCoords.Count > 0 ? data.TexCoords[0][idx2] : Vector2.Zero;
				Vector2 uv0_3 = data.TexCoords.Count > 0 ? data.TexCoords[0][idx3] : Vector2.Zero;

				Vector2 uv1_1 = data.TexCoords.Count > 1 ? data.TexCoords[1][idx1] : Vector2.Zero;
				Vector2 uv1_2 = data.TexCoords.Count > 1 ? data.TexCoords[1][idx2] : Vector2.Zero;
				Vector2 uv1_3 = data.TexCoords.Count > 1 ? data.TexCoords[1][idx3] : Vector2.Zero;

				VertexTexture2 mat1 = new(uv0_1, uv1_1);
				VertexTexture2 mat2 = new(uv0_2, uv1_2);
				VertexTexture2 mat3 = new(uv0_3, uv1_3);

				VertexBuilder<VertexPositionNormalTangent, VertexTexture2, VertexEmpty> v1 = new(geo1, mat1);
				VertexBuilder<VertexPositionNormalTangent, VertexTexture2, VertexEmpty> v2 = new(geo2, mat2);
				VertexBuilder<VertexPositionNormalTangent, VertexTexture2, VertexEmpty> v3 = new(geo3, mat3);

				prim.AddTriangle(v1, v2, v3);
			}
		}
		return mesh;
	}

	private static Vector3[] LoadNormalAccessorData(JObject accessorJson, JArray bufferViewsJson, byte[] binBytes)
	{
		int count = accessorJson["count"]!.Value<int>();

		JObject bufferView = (JObject)bufferViewsJson[accessorJson["bufferView"]!.Value<int>()];

		int accessorByteOffset = accessorJson["byteOffset"]?.Value<int>() ?? 0;
		int bufferViewByteOffset = bufferView["byteOffset"]?.Value<int>() ?? 0;

		int componentType = accessorJson["componentType"]!.Value<int>(); // should be 5120 (BYTE)
		string? type = accessorJson["type"]!.Value<string>();            // should be "VEC4"

		int componentSize = ComponentSize(componentType);  // for byte: 1
		int numComponents = ComponentCount(type!);         // for VEC4: 4

		// Convert from VEC4 BYTE to VEC3 FLOAT
		Vector3[] normals = new Vector3[count];

		int stride = bufferView["byteStride"]?.Value<int>() ?? (componentSize * numComponents); // if byteStride is absent, assume packed data

		for (int i = 0; i < count; i++)
		{
			int offset = bufferViewByteOffset + accessorByteOffset + (i * stride);

			normals[i] = new Vector3((sbyte)binBytes[offset + (0 * componentSize)],
									 (sbyte)binBytes[offset + (1 * componentSize)],
									 (sbyte)binBytes[offset + (2 * componentSize)]);
		}

		return normals;
	}

	private static Vector3[] LoadPositionAccessorData(JObject accessorJson, JArray bufferViewsJson, byte[] binBytes)
	{
		int count = accessorJson["count"]!.Value<int>();

		JObject bufferView = (JObject)bufferViewsJson[accessorJson["bufferView"]!.Value<int>()];

		int accessorByteOffset = accessorJson["byteOffset"]?.Value<int>() ?? 0;
		int bufferViewByteOffset = bufferView["byteOffset"]?.Value<int>() ?? 0;

		int componentType = accessorJson["componentType"]!.Value<int>(); // should be 5126 (FLOAT)
		string? type = accessorJson["type"]!.Value<string>();            // should be "VEC3"

		int componentSize = ComponentSize(componentType);  // for float: 4
		int numComponents = ComponentCount(type!);         // for VEC3: 3

		Vector3[] positions = new Vector3[count];

		int stride = bufferView["byteStride"]?.Value<int>() ?? (componentSize * numComponents);

		for (int i = 0; i < count; i++)
		{
			int offset = bufferViewByteOffset + accessorByteOffset + (i * stride);

			positions[i] = new Vector3(BitConverter.ToSingle(binBytes, offset + (0 * componentSize)),
								   BitConverter.ToSingle(binBytes, offset + (1 * componentSize)),
								   BitConverter.ToSingle(binBytes, offset + (2 * componentSize)));
		}

		return positions;
	}

	private static Vector4[] LoadTangentAccessorData(JObject accessorJson, JArray bufferViewsJson, byte[] binBytes)
	{
		int count = accessorJson["count"]!.Value<int>();

		JObject bufferView = (JObject)bufferViewsJson[accessorJson["bufferView"]!.Value<int>()];

		int accessorByteOffset = accessorJson["byteOffset"]?.Value<int>() ?? 0;
		int bufferViewByteOffset = bufferView["byteOffset"]?.Value<int>() ?? 0;

		int componentType = accessorJson["componentType"]!.Value<int>(); // should be 5120 (BYTE)
		string? type = accessorJson["type"]!.Value<string>();            // should be "VEC4"

		int componentSize = ComponentSize(componentType);  // for byte: 1
		int numComponents = ComponentCount(type!);         // for VEC4: 4

		// Convert from VEC4 BYTE to VEC4 FLOAT
		Vector4[] tangents = new Vector4[count];

		int stride = bufferView["byteStride"]?.Value<int>() ?? (componentSize * numComponents);

		for (int i = 0; i < count; i++)
		{
			int offset = bufferViewByteOffset + accessorByteOffset + (i * stride);

			tangents[i] = new Vector4((sbyte)binBytes[offset + (0 * componentSize)],
								  (sbyte)binBytes[offset + (1 * componentSize)],
								  (sbyte)binBytes[offset + (2 * componentSize)],
								  (sbyte)binBytes[offset + (3 * componentSize)]);
		}

		return tangents;
	}

	private static int[] LoadIndexData(JObject accessorJson, JArray bufferViewsJson, byte[] binBytes)
	{
		int count = accessorJson["count"]!.Value<int>();

		JObject bufferView = (JObject)bufferViewsJson[accessorJson["bufferView"]!.Value<int>()];

		int accessorByteOffset = accessorJson["byteOffset"]?.Value<int>() ?? 0;
		int bufferViewByteOffset = bufferView["byteOffset"]?.Value<int>() ?? 0;

		int componentType = accessorJson["componentType"]!.Value<int>(); // 5123 (UNSIGNED_SHORT) or 5125 (UNSIGNED_INT)

		int componentSize = ComponentSize(componentType);  // 2 for UNSIGNED_SHORT, 4 for UNSIGNED_INT

		int[] indices = new int[count];

		int stride = componentSize;

		for (int i = 0; i < count; i++)
		{
			int offset = bufferViewByteOffset + accessorByteOffset + (i * stride);

			if (componentType == 5123) // UNSIGNED_SHORT
			{
				indices[i] = BitConverter.ToUInt16(binBytes, offset);
			}
			else if (componentType == 5125) // UNSIGNED_INT
			{
				// Cast down to int safely
				indices[i] = (int)BitConverter.ToUInt32(binBytes, offset);
			}
			else
			{
				throw new Exception($"Unsupported index componentType: {componentType}");
			}
		}

		return indices;
	}

	private static Vector2[] LoadTexCoordAccessorData(JObject accessorJson, JArray bufferViewsJson, byte[] binBytes)
	{
		int count = accessorJson["count"]!.Value<int>();

		JObject bufferView = (JObject)bufferViewsJson[accessorJson["bufferView"]!.Value<int>()];

		int accessorByteOffset = accessorJson["byteOffset"]?.Value<int>() ?? 0;
		int bufferViewByteOffset = bufferView["byteOffset"]?.Value<int>() ?? 0;

		int componentType = accessorJson["componentType"]!.Value<int>();
		string? type = accessorJson["type"]!.Value<string>();

		int componentSize = ComponentSize(componentType);
		int numComponents = ComponentCount(type!);

		Vector2[] texCoords = new Vector2[count];

		int stride = bufferView["byteStride"]?.Value<int>() ?? (componentSize * numComponents);

		for (int i = 0; i < count; i++)
		{
			int offset = bufferViewByteOffset + accessorByteOffset + (i * stride);

			// Read as half-precision float (16-bit float) - component type 5122 (SHORT) is used to indicate float16
			Half u = BitConverter.ToHalf(binBytes, offset + (0 * componentSize));
			Half v = BitConverter.ToHalf(binBytes, offset + (1 * componentSize));

			texCoords[i] = new Vector2((float)u, (float)v);
		}

		return texCoords;
	}
}