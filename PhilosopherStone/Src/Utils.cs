using System;
using System.Collections.Generic;
using System.Data;
using System.Text;
using System.Linq;
using System.IO;

class Utils
{
//**************************************************************************************

	/// <summary>
	/// Converts given unix time to DateTime.
	/// </summary>
	public static DateTime UnixToDateTime(double iUnixTime)
	{
		DateTime dtDateTime = new System.DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
		dtDateTime = dtDateTime.AddMilliseconds(iUnixTime);
		return dtDateTime;
	}

	//**************************************************************************************	

	/// <summary>
	/// Converts given date time to unix time.
	/// </summary>
	public static ulong DateTimeToUnix(DateTime iDateTime) 	=> (ulong)iDateTime.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

	//**************************************************************************************

	/// <summary>
	/// Saves given data table to a csv.
	/// </summary>
	public static void SaveDataTableToCSV(DataTable iDataTable, string iFilename)
	{
		StringBuilder sb = new StringBuilder();

		IEnumerable<string> columnNames = iDataTable.Columns.Cast<DataColumn>().
										  Select(column => column.ColumnName);
		sb.AppendLine(string.Join(";", columnNames));

		foreach (DataRow row in iDataTable.Rows)
		{
			IEnumerable<string> fields = row.ItemArray.Select(field => field.ToString());
			sb.AppendLine(string.Join(";", fields));
		}

		File.WriteAllText(iFilename, sb.ToString());
	}

	//**************************************************************************************

	public static void SaveToBinary<T1, T2>(List<(T1, T2)> iData, string iFileName)
	{
		using (var writer = new BinaryWriter(File.Open(iFileName, FileMode.Create)))
		{
			writer.Write(iData.Count());

			foreach(var d in iData)
			{
				if(typeof(T1) == typeof(ulong))
					writer.Write(Convert.ToUInt64(d.Item1));
				else
					throw new Exception("Unknown format");
				
				if(typeof(T2) == typeof(double))
					writer.Write(Convert.ToDouble(d.Item2));
				else if(typeof(T2) == typeof(int))
					writer.Write(Convert.ToInt32(d.Item2));
				else
					throw new Exception("Unknown format");
			}
		}
	}

	//**************************************************************************************

	public static void LoadFromBinary(out List<(ulong, double)> oData, string iFileName)
	{		
		using (var reader = new BinaryReader(File.Open(iFileName, FileMode.Open)))
		{
			var count = reader.ReadInt32();
			oData = new List<(ulong, double)>(count);

			for(int i=0; i<count; i++)
				oData.Add((reader.ReadUInt64(), reader.ReadDouble()));
		}
	}

	//**************************************************************************************

	public static void LoadFromBinary(out List<(ulong, int)> oData, string iFileName)
	{		
		using (var reader = new BinaryReader(File.OpenRead(iFileName)))
		{
			var count = reader.ReadInt32();
			oData = new List<(ulong, int)>(count);

			for(int i=0; i<count; i++)
				oData.Add((reader.ReadUInt64(), reader.ReadInt32()));
		}
	}

	//**************************************************************************************

	public static void SaveArrayAsCSV<T>(T[][] arrayToSave, string fileName)
	{
		using (StreamWriter file = new StreamWriter(fileName))
			foreach (T[] item in arrayToSave)
				file.WriteLine(string.Join(";", item).Replace(',', '.').Replace(';',','));
	}

	//**************************************************************************************

	public static void Assert(bool iCondition, string iMessage)
	{
		if(!iCondition) 
			throw new Exception(iMessage);	
	}

	//**************************************************************************************

	/// <summary>
	/// Returns normalized array.
	/// </summary>
	public static double[] Normalize(double[] iValues)
	{
		if (iValues is null)
			throw new ArgumentNullException("iValues");

		if (iValues.Length < 1)
			throw new ArgumentException("Cannot normalize empty array");

		var results = new double[iValues.Length];
		var sum = iValues.Sum();

		for (int i = 0; i < iValues.Length; i++)
			results[i] = iValues[i] / sum;

		return results;
	}

	//**************************************************************************************

	public static double Lerp(double iVal1, double iVal2, double iInterpolation) =>
		iVal1*(1-iInterpolation) + iVal2*(iInterpolation);
	
	//**************************************************************************************
}