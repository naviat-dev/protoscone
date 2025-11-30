using System.IO.Compression;

public class Terrain
{
	private static readonly double[,] LatitudeIndex = { { 89, 12 }, { 86, 4 }, { 83, 2 }, { 76, 1 }, { 62, 0.5 }, { 22, 0.25 }, { 0, 0.125 } };
	private static readonly HttpClientHandler handler = new() { ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true };
	public static readonly HttpClient client = new(handler);
	public static double GetElevation(double latitude, double longitude)
	{
		// Placeholder implementation
		return 0.0;
	}

	private static double BilinearInterpolate(double x, double y, double q11, double q12, double q21, double q22)
	{
		return (q11 * (1 - x) * (1 - y)) +
			   (q21 * x * (1 - y)) +
			   (q12 * (1 - x) * y) +
			   (q22 * x * y);
	}

	public static void GetBtgAsMesh(double lat, double lon, int version)
	{
		string lonHemi = lon >= 0 ? "e" : "w";
		string latHemi = lat >= 0 ? "n" : "s";
		string url = $"terramaster.flightgear.org/terrasync/ws{version}/Terrain/{lonHemi}{Math.Floor(lon / 10) * 10:000}/{latHemi}{Math.Floor(lat / 10) * 10:00}/{lonHemi}{Math.Floor(Math.Abs(lon)):000}{latHemi}{Math.Floor(Math.Abs(lat)):00}/{GetTileIndex(lat, lon)}.btg.gz";
		byte[] data = client.GetByteArrayAsync(url).Result;
		GZipStream gzip = new(new MemoryStream(data), CompressionMode.Decompress);
		BinaryReader reader = new(gzip);
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
}