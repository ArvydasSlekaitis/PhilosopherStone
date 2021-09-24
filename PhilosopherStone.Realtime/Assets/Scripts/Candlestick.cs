using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using MathNet.Numerics.Statistics;
using System.Linq;

public class Candlestick : IComparer<Candlestick>, IComparable<Candlestick>
{
	public ulong Timestamp { get; set; }
	public double OpenPrice { get; set; }
	public double ClosePrice { get; set; }
	public double HighPrice { get; set; }
	public double LowPrice { get; set; }
	public double MedianPrice { get; set; }

	//**************************************************************************************

	public int Compare(Candlestick x, Candlestick y) => x.Timestamp.CompareTo(y.Timestamp);

	public int CompareTo(Candlestick other) => Timestamp.CompareTo(other.Timestamp);

	//**************************************************************************************

	/// <summary>
	/// Load candlesticks from a CSV file.
	/// </summary>
	public static List<Candlestick> LoadCSV(StreamReader iReader)
	{
		var results = new List<Candlestick>();

		while (!iReader.EndOfStream)
		{
			string line = iReader.ReadLine();
			if (line == "timestamp,open,high,low,close")
				continue;

			string[] contents;

			if (line.Contains(","))
				contents = line.Split(',');
			else
				contents = line.Split(';');

			DateTime date;

			if(contents[0].Length == 10)
			{
				date = DateTime.Parse(contents[0]);
				date = new DateTime(date.Year, date.Month, date.Day, 23, 59, 59);
			}
			else if (contents[0].Contains(":") || contents[0].Contains("-"))
				date = DateTime.Parse(contents[0]);
			else
				date = DateTime.ParseExact(contents[0], "yyyyMMdd HHmmss", CultureInfo.InvariantCulture);

			Candlestick entry;
			try
			{
				entry = new Candlestick()
				{
					Timestamp = Utils.DateTimeToUnix(date),
					OpenPrice = float.Parse(contents[1], CultureInfo.InvariantCulture),
					ClosePrice = float.Parse(contents[4], CultureInfo.InvariantCulture),
					HighPrice = float.Parse(contents[2], CultureInfo.InvariantCulture),
					LowPrice = float.Parse(contents[3], CultureInfo.InvariantCulture)		
				};
				entry.MedianPrice = Statistics.Median(new double[] { entry.OpenPrice, entry.ClosePrice, entry.LowPrice, entry.HighPrice}); 

				ConsistencyCheck(entry);
			}
			catch
			{
				Console.WriteLine("Invalid data detected while reading candlesticks from csv:" + line);
				entry = null;
			}

			if (entry != null)
				results.Add(entry);
		}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Loads and returns raw candlestick data from csv files.
	/// </summary>
	public static List<Candlestick> LoadHistorical1m(int iStartYear=2000, int iEndYear=2020)
	{
		var candlesticks = new List<Candlestick>();

		for (int i = iStartYear; i <= iEndYear; i++)
		{
			var filename = "./Raw/" + i.ToString() + ".csv";
			if (File.Exists(filename))
				using (StreamReader reader = new StreamReader(filename))
					candlesticks.AddRange(Candlestick.LoadCSV(reader));
		}

		return candlesticks;
	}

	//**************************************************************************************

	/// <summary>
	/// Save candlesticks to binary file.
	/// </summary>
	public static void Save(string iFilename, List<Candlestick> iCandlestickData)
	{
		using (BinaryWriter writer = new BinaryWriter(File.OpenWrite(iFilename)))
		{
			writer.Write(iCandlestickData.Count);

			for (int i = 0; i < iCandlestickData.Count; i++)
			{
				writer.Write(iCandlestickData[i].Timestamp);
				writer.Write(iCandlestickData[i].OpenPrice);
				writer.Write(iCandlestickData[i].ClosePrice);
				writer.Write(iCandlestickData[i].HighPrice);
				writer.Write(iCandlestickData[i].LowPrice);
				writer.Write(iCandlestickData[i].MedianPrice);
			}
		}
	}

	//**************************************************************************************
	
	/// <summary>
	/// Load candlesticks from binary file.
	/// </summary>
	public static List<Candlestick> Load(string iFilename)
	{
		List<Candlestick> candlesticks;

		using (BinaryReader reader = new BinaryReader(File.OpenRead(iFilename)))
		{
			int count = reader.ReadInt32();
			candlesticks = new List<Candlestick>(count);

			for (int i = 0; i < count; i++)
				candlesticks.Add(new Candlestick()
				{
					Timestamp = reader.ReadUInt64(),
					OpenPrice = reader.ReadDouble(),
					ClosePrice = reader.ReadDouble(),
					HighPrice = reader.ReadDouble(),
					LowPrice = reader.ReadDouble(),
					MedianPrice = reader.ReadDouble()
				});
		}

		return candlesticks;
	}

//**************************************************************************************

	public static void ConsistencyCheck(Candlestick iCandlestick) 
	{
		if (iCandlestick.OpenPrice < 0 || iCandlestick.ClosePrice < 0 || iCandlestick.LowPrice < 0 || iCandlestick.HighPrice < 0)
			throw new ArgumentException("Negative data", "openPrice < 0 || closePrice < 0 || lowPrice < 0 || highPrice < 0");
	}

//**************************************************************************************

	/// <summary>
	/// Consolidate candlesticks from higher period to lower period.
	/// </summary>
	public static List<Candlestick> Consolidate(List<Candlestick> iCandlesticks, int iPeriodsPerDay)
	{
		if (iCandlesticks is null)
			throw new NullReferenceException("iSourceCandlestick");

		if (iCandlesticks.Count <= 0)
			throw new ArgumentException("Array should not be empty", "iSourceCandlestick");

		if (iPeriodsPerDay <= 0)
			throw new ArgumentException("Value should be greater than zero", "iPeriodsPerDay");

		var results = new List<Candlestick>();

		DateTime initialTime = Utils.UnixToDateTime(iCandlesticks[0].Timestamp);
		initialTime = new DateTime(initialTime.Year, initialTime.Month, initialTime.Day, 0, 0, 0);

		ulong periodDuration = 86400000 / (ulong)iPeriodsPerDay;
		ulong startTime = Utils.DateTimeToUnix(initialTime);
		ulong endTime = startTime + periodDuration;

		var tCandlesticks = new List<Candlestick>();

		for (int i = 0; i < iCandlesticks.Count; i++)
		{
			if (iCandlesticks[i].Timestamp >= endTime)
			{
				if (tCandlesticks.Count > 0)
				{
					results.Add(new Candlestick()
					{
						Timestamp = tCandlesticks.Last().Timestamp,
						OpenPrice = tCandlesticks.First().OpenPrice,
						ClosePrice = tCandlesticks.Last().ClosePrice,
						HighPrice = tCandlesticks.Max(x=> x.HighPrice),
						LowPrice = tCandlesticks.Min(x => x.LowPrice),
						MedianPrice = Statistics.Median(tCandlesticks.Select(x => x.MedianPrice))
					});

					tCandlesticks.Clear();
				}
				startTime += periodDuration;
				endTime += periodDuration;
				i--;
				continue;
			}
			else
				tCandlesticks.Add(iCandlesticks[i]);
		}

		return results;
	}

	//**************************************************************************************

	/// <summary>
	/// Given a presorted candlestick list (based on StartTime) returns candlestick that starts with a given time.
	/// </summary>
	public static int FindLastHistoricIndex(IReadOnlyList<Candlestick> iCandlesticks, ulong iTime)
	{
		if (iCandlesticks is null)
			throw new ArgumentNullException(nameof(iCandlesticks));

		int lowerBound = 0;
		int upperBound = iCandlesticks.Count-1;

		while (lowerBound <= upperBound)
		{
			int midPoint = (int)Math.Floor((double)(lowerBound + upperBound) / 2);

			if (iCandlesticks[midPoint].Timestamp < iTime)
				lowerBound = midPoint;
			else if (iCandlesticks[midPoint].Timestamp > iTime)
				upperBound = midPoint;
			else
			{
				upperBound = midPoint;
				lowerBound = midPoint-1;
			}

			if (upperBound - lowerBound <= 1)
				return iCandlesticks[upperBound].Timestamp < iTime ? upperBound : lowerBound;
		}

		throw new ArgumentException("Could not find candlestick index.");
	}
	
	//**************************************************************************************

	/// <summary>
	/// Creates and returns a new candlestick list that is the copy of provided list with range [iStartIndex; iEndIndex].
	/// </summary>
	public static List<Candlestick> CreateCopy(List<Candlestick> iCandlesticks, int iStartIndex, int iEndIndex)
	{
		if (iCandlesticks is null)
			throw new ArgumentNullException(nameof(iCandlesticks));

		Candlestick[] results = new Candlestick[iEndIndex - iStartIndex];
		iCandlesticks.CopyTo(iStartIndex, results, 0, results.Length);
		return new List<Candlestick>(results);
	}
	
	//**************************************************************************************

	public static double[] CalculateClosePriceChanges(IReadOnlyList<Candlestick> iCandlesticks)
    {
        var results = new double[iCandlesticks.Count-1];
        for(int i=1; i<iCandlesticks.Count; i++)
            results[i-1] = Math.Log(iCandlesticks[i].ClosePrice / iCandlesticks[i-1].ClosePrice);
        return results;
    }

	//**************************************************************************************
}