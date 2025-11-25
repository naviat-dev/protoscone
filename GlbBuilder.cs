using System.Numerics;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;

public class GlbBuilder
{

	public record MeshData(
		Vector3[] Positions,
		Vector3[] Normals,
		Vector2[] UVs,
		Vector4[] Tangents,
		int[] Indices
	);

	public static MeshBuilder<VertexPositionNormalTangent> BuildMesh(MeshData data)
	{
		MeshBuilder<VertexPositionNormalTangent> mesh = new("mesh");

		var prim = mesh.UsePrimitive(MaterialBuilder.CreateDefault());

		for (int i = 0; i < data.Indices.Length; i += 3)
		{
			int i0 = data.Indices[i];
			int i1 = data.Indices[i + 1];
			int i2 = data.Indices[i + 2];

			_ = prim.AddTriangle(
				new VertexPositionNormalTangent(
					data.Positions[i0], data.Normals[i0], data.Tangents[i0]),

				new VertexPositionNormalTangent(
					data.Positions[i1], data.Normals[i1], data.Tangents[i1]),

				new VertexPositionNormalTangent(
					data.Positions[i2], data.Normals[i2], data.Tangents[i2])
			);
		}

		return mesh;
	}

}