using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Returns decision as returned by provided data processor.
/// </summary>
public class BSLogic_Inline : BSLogic
{
    Func<IReadOnlyList<Candlestick>, IReadOnlyList<Candlestick>, Decision> dataProcessor;

    //*****************************************************************************************

    public BSLogic_Inline(BSLogicInfo iInfo, Func<IReadOnlyList<Candlestick>, IReadOnlyList<Candlestick>, Decision> iDataProcessor)
            : base(iInfo) => dataProcessor = iDataProcessor;

    //*****************************************************************************************

    public override Decision GetDecision(IReadOnlyList<Candlestick> iPastDaily, IReadOnlyList<Candlestick> iPastHourly)
        => dataProcessor(iPastDaily, iPastHourly);

    //*****************************************************************************************

    public static double CountPositiveRatio(IReadOnlyList<Candlestick> iData, int iPeriods)
    {
        var positive = 0;
        for(int i=0; i<iPeriods; i++)
            if(iData[iData.Count-i-1].ClosePrice >= iData[iData.Count-i-2].ClosePrice)
                positive++;
        return (double)positive / iPeriods;
    }

    //*****************************************************************************************

    public static List<double> GetPriceChangeVelocity(IReadOnlyList<Candlestick> iCandlesticks, List<int> iOfDaysBefore)
    {
        return iOfDaysBefore.Select(x => 
        {
            var change = Math.Log(iCandlesticks[iCandlesticks.Count-1].ClosePrice / iCandlesticks[iCandlesticks.Count-1-x].ClosePrice);
            var time = (double)(x);
            return change/time;
        }
        ).ToList();
    }

    //*****************************************************************************************

    public static List<double> GetMedianPriceChangeVelocity(IReadOnlyList<Candlestick> iCandlesticks, List<int> iOfDaysBefore)
    {
        return iOfDaysBefore.Select(x => 
        {
            var change = Math.Log(iCandlesticks[iCandlesticks.Count-1].MedianPrice / iCandlesticks[iCandlesticks.Count-1-x].MedianPrice);
            var time = (double)(x);
            return change/time;
        }
        ).ToList();
    }

    //*****************************************************************************************
}