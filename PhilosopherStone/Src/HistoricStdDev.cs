using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using Microsoft.EntityFrameworkCore;

public class HistoricStdDev
{
    public int Lookback {get; set; }
    public ulong Timestamp {get; set; }
    public double StdDev {get; set; }

    static ConcurrentDictionary<int, ConcurrentDictionary<ulong, double>> historicStdDev = new ConcurrentDictionary<int, ConcurrentDictionary<ulong, double>>(Environment.ProcessorCount*2, 64);

    //*****************************************************************************************

    public static void Load()
    {
        using (var db = new DataContext())
            foreach(var stdDev in db.historicStdDevs.AsNoTracking())
            {
                if(!historicStdDev.ContainsKey(stdDev.Lookback))
                    historicStdDev.TryAdd(stdDev.Lookback, new ConcurrentDictionary<ulong, double>());

                historicStdDev[stdDev.Lookback].TryAdd(stdDev.Timestamp, stdDev.StdDev);
            }
    }

    //*****************************************************************************************

    public static double CaclHistoricsStdDev(IReadOnlyList<Candlestick> iPastHourly, int iLookbackTimeframe)
    {
        var data = new ShalowList<Candlestick>(iPastHourly, iPastHourly.Count-iLookbackTimeframe-1, iLookbackTimeframe+1);
        return ArrayStatistics.StandardDeviation(Candlestick.CalculateClosePriceChanges(data));
    }

     //*****************************************************************************************

    public static double Get(IReadOnlyList<Candlestick> iPastHourly, int iLookbackTimeframe)
    {
        if(!historicStdDev.ContainsKey(iLookbackTimeframe))
            historicStdDev.TryAdd(iLookbackTimeframe, new ConcurrentDictionary<ulong, double>(Environment.ProcessorCount*2, 200000));

        if(historicStdDev[iLookbackTimeframe].TryGetValue(iPastHourly[iPastHourly.Count-1].Timestamp, out var v))
            return v;
        else
        {
            var std = CaclHistoricsStdDev(iPastHourly, iLookbackTimeframe);
            historicStdDev[iLookbackTimeframe].TryAdd(iPastHourly[iPastHourly.Count-1].Timestamp, std);

            lock(historicStdDev)
                using (var db = new DataContext())
                    if(!db.historicStdDevs.Any(x => x.Lookback == iLookbackTimeframe && x.Timestamp == iPastHourly[iPastHourly.Count-1].Timestamp))
                    {
                        db.historicStdDevs.Add(new HistoricStdDev(){Lookback = iLookbackTimeframe, Timestamp = iPastHourly[iPastHourly.Count-1].Timestamp, StdDev = std});
                        try
                        {
                            db.SaveChanges();
                        }
                        catch(Exception) {}
                    }

            return std;
        }
    }

    //*****************************************************************************************
}