namespace protoscone;

public class RangeTracker
{
	public struct Range(long start, long end)
	{
		public long Start = start;
		public long End = end; // Exclusive

		public readonly long Length => End - Start;

		public override readonly string ToString()
			=> $"[{Start}, {End})";
	}

	private readonly List<Range> _ranges = new();

	/// <summary>
	/// Record an allocated region [start, end).
	/// Will merge with adjacent or overlapping intervals.
	/// </summary>
	public void Add(long start, long end)
	{
		if (start >= end) return;

		Range newRange = new(start, end);

		int i = BinarySearchInsertIndex(start);

		// Merge with previous if overlapping
		if (i > 0 && _ranges[i - 1].End >= start)
		{
			i--;
			newRange.Start = Math.Min(newRange.Start, _ranges[i].Start);
			newRange.End = Math.Max(newRange.End, _ranges[i].End);
			_ranges.RemoveAt(i);
		}

		// Merge with following ranges
		while (i < _ranges.Count && _ranges[i].Start <= newRange.End)
		{
			newRange.Start = Math.Min(newRange.Start, _ranges[i].Start);
			newRange.End = Math.Max(newRange.End, _ranges[i].End);
			_ranges.RemoveAt(i);
		}

		_ranges.Insert(i, newRange);
	}

	/// <summary>
	/// Checks if the given region overlaps any tracked range.
	/// </summary>
	public bool Overlaps(long start, long end)
	{
		if (start >= end)
			return false;

		int i = BinarySearchInsertIndex(start);

		// Check previous range
		if (i > 0 && _ranges[i - 1].End > start)
			return true;

		// Check next range
		if (i < _ranges.Count && _ranges[i].Start < end)
			return true;

		return false;
	}

	/// <summary>
	/// Returns index where a range with this start *would* be inserted.
	/// </summary>
	private int BinarySearchInsertIndex(long start)
	{
		int lo = 0, hi = _ranges.Count;

		while (lo < hi)
		{
			int mid = (lo + hi) >> 1;
			if (_ranges[mid].Start < start) lo = mid + 1;
			else hi = mid;
		}
		return lo;
	}

	public IEnumerable<Range> GetRanges() => _ranges;
}
