/* using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using g3;

public class Terrain
{
	private static readonly double[,] LatitudeIndex = { { 89, 12 }, { 86, 4 }, { 83, 2 }, { 76, 1 }, { 62, 0.5 }, { 22, 0.25 }, { 0, 0.125 } };
	private static readonly HttpClientHandler handler = new() { ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true };
	private static readonly HttpClient client = new(handler);
	private static Dictionary<int, List<DMesh3>> meshCache = [];
	public static double GetElevation(double latitude, double longitude)
	{
		try
		{
			int index = GetTileIndex(latitude, longitude);
			if (!meshCache.ContainsKey(index))
			{
				meshCache[index] = GetBtgAsMesh(latitude, longitude, 3);
			}
		}
		catch (HttpRequestException e)
		{
			Console.WriteLine($"Error fetching BTG data: {e.Message}");
		}
		return 0.0;
	}

	private static double BilinearInterpolate(double x, double y, double q11, double q12, double q21, double q22)
	{
		return (q11 * (1 - x) * (1 - y)) +
			   (q21 * x * (1 - y)) +
			   (q12 * (1 - x) * y) +
			   (q22 * x * y);
	}

	public static List<DMesh3> GetBtgAsMesh(double lat, double lon, int version)
	{
		List<DMesh3> meshes = [];
		string lonHemi = lon >= 0 ? "e" : "w";
		string latHemi = lat >= 0 ? "n" : "s";
		string topLevelUrl = $"https://terramaster.flightgear.org/terrasync/ws{version}/Terrain/{lonHemi}{Math.Abs(Math.Floor(lon / 10)) * 10:000}{latHemi}{Math.Abs(Math.Floor(lat / 10)) * 10:00}/{lonHemi}{Math.Abs(Math.Floor(lon)):000}{latHemi}{Math.Abs(Math.Floor(lat)):00}";
		string urlStg = $"{topLevelUrl}/{GetTileIndex(lat, lon)}.stg";
		Console.WriteLine($"Fetching STG data from {urlStg}");
		string[] btgFiles = [.. Encoding.ASCII.GetString(client.GetByteArrayAsync(urlStg).Result).Split('\n').Select(line => new Regex(@"^OBJECT (.+\.btg)$").Match(line)).Where(m => m.Success).Select(m => m.Groups[1].Value)];
		Console.WriteLine($"Found {btgFiles.Length} BTG files in STG");
		foreach (string btgFile in btgFiles)
		{
			List<Vector3> vertices = [];
			List<Index> triangles = [];
			Vector3 center = new(0, 0, 0);
			float radius = 0;
			byte[] data = client.GetByteArrayAsync($"{topLevelUrl}/{btgFile}.gz").Result;
			GZipStream gzip = new(new MemoryStream(data), CompressionMode.Decompress);
			BinaryReader reader = new(gzip);
			ushort tileVersion = reader.ReadUInt16(); // version
			if (string.Concat(new string(reader.ReadChars(2)).Reverse()) != "SG") // magic
			{
				Console.WriteLine("Not a valid BTG file");
				return [];
			}
			_ = reader.ReadUInt32(); // creation date
			ushort objectCount = reader.ReadUInt16(); // number of objects
			for (int i = 0; i < objectCount; i++)
			{
				byte type = reader.ReadByte();
				ushort objPropsCount = reader.ReadUInt16();
				ushort objElementsCount = reader.ReadUInt16();
				BtgObjProp[] objProps = new BtgObjProp[objPropsCount];
				BtgObjElem[] objElems = new BtgObjElem[objElementsCount];
				Console.WriteLine($"Object {i}: Type {type}, {objPropsCount} properties, {objElementsCount} elements");
				for (int j = 0; j < objPropsCount; j++)
				{
					objProps[j].type = reader.ReadChar();
					objProps[j].byteCount = reader.ReadUInt32();
					objProps[j].data = reader.ReadBytes((int)objProps[j].byteCount);
				}
				for (int j = 0; j < objElementsCount; j++)
				{
					objElems[j].byteCount = reader.ReadUInt32();
					objElems[j].data = reader.ReadBytes((int)objElems[j].byteCount);
				}
				if (objElems.Length == 0)
					continue;
				if (type == 0)
				{
					Console.WriteLine($"Processing BTG Sphere, length {objElems.Length}");
					center = new((float)BitConverter.ToDouble(objElems[^1].data, 0), (float)BitConverter.ToDouble(objElems[^1].data, 8), (float)BitConverter.ToDouble(objElems[^1].data, 16));
					radius = BitConverter.ToSingle(objElems[^1].data, 24);
				}
				else if (type == 1)
				{

				}
				else if (type == 2)
				{

				}
				else if (type == 10)
				{

				}
				else if (type == 11)
				{

				}
				else if (type == 12)
				{

				}
				else
				{
					// We don't care about any other object types
					continue;
				}
			}
			Console.WriteLine($"Center: {center}, Radius: {radius}");
			Console.ReadLine();
		}
		return meshes;
	}

	public static int GetTileIndex(double lat, double lon)
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

	public static (double lat, double lon) GetLatLon(int tileIndex)
	{
		// Extract x, y, baseY, baseX from the tile index (reverse of GetTileIndex bit packing)
		// GetTileIndex packs as: ((baseX + 180) << 14) + ((baseY + 90) << 6) + (y << 3) + x
		int x = tileIndex & 0b111; // last 3 bits
		int y = (tileIndex >> 3) & 0b111; // next 3 bits (not 6!)
		int baseY = ((tileIndex >> 6) & 0b11111111) - 90; // next 8 bits, then subtract 90
		int baseX = (tileIndex >> 14) - 180; // remaining bits, then subtract 180

		// Determine the tileWidth for this latitude band
		double lookup = Math.Abs(baseY);
		double tileWidth = 0;
		for (int i = 0; i < LatitudeIndex.Length; i++)
		{
			if (lookup >= LatitudeIndex[i, 0])
			{
				tileWidth = LatitudeIndex[i, 1];
				break;
			}
		}

		// Reconstruct the coordinates (reverse of GetTileIndex coordinate calculation)
		double lat = baseY + y / 8.0;
		double lon = baseX + x * tileWidth;

		return (lat, lon);
	}

	struct BtgObjProp
	{
		public char type;
		public uint byteCount;
		public byte[] data;
	}
	struct BtgObjElem
	{
		public uint byteCount;
		public byte[] data;
	}
} */