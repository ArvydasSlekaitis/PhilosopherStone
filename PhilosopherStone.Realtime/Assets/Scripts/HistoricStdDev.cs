using System.Collections.Generic;
using MathNet.Numerics.Statistics;

public class HistoricStdDev
{
    public int Lookback {get; set; }
    public ulong Timestamp {get; set; }
    public double StdDev {get; set; }

    //*****************************************************************************************

    public static double CaclHistoricsStdDev(IReadOnlyList<Candlestick> iPastHourly, int iLookbackTimeframe)
    {
        var data = new ShalowList<Candlestick>(iPastHourly, iPastHourly.Count-iLookbackTimeframe-1, iLookbackTimeframe+1);
        return ArrayStatistics.StandardDeviation(Candlestick.CalculateClosePriceChanges(data));
    }

    //*****************************************************************************************

    public static double Get(IReadOnlyList<Candlestick> iPastHourly, int iLookbackTimeframe)
    {
        return CaclHistoricsStdDev(iPastHourly, iLookbackTimeframe);
    }

    //*****************************************************************************************
}