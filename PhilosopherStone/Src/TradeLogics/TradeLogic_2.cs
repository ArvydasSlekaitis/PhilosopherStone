using System;
using System.Collections.Generic;
using System.Linq;

/// <summary> 
/// Same as TradeLogic_1, but takes profit and stop losses based on prievous periods standard deviation.
/// </summary>
public class TradeLogic_2 : TradeLogic
{
    public readonly bool takeProfit = false;
    public readonly bool stopLoss = false;
    public readonly int lookbackTimeframe = 0;
    public readonly double takeProfitStdDev = 0.0;
    public readonly double stopLossStdDev = 0.0;
    public readonly double openOrderStdDev = 0.0;

    //*****************************************************************************************

    public TradeLogic_2(string iVariation)
        : base(new TradeLogicInfo("TradeLogic_2", iVariation))
    {
        var info = ParseVariation(iVariation);
        
        lookbackTimeframe = (int)info["LT"];
        openOrderStdDev = info["OOS"];
        takeProfitStdDev = info["TPS"];
        stopLossStdDev = info["SLS"];
        tradingType = info["Halting"] == 1 ? TradingType.Halting : TradingType.Continuous;
        
        takeProfit = takeProfitStdDev > 0;
        stopLoss = stopLossStdDev > 0;
    }

    //*****************************************************************************************

    public override List<TradeOrder> GetBuyOrders(IReadOnlyList<Candlestick> iPastHourly, double iCurrentPrice, Decision iDecision)
    {
        var results = new List<TradeOrder>();

        var historicStdDev = HistoricStdDev.Get(iPastHourly, lookbackTimeframe);
        var openOrderDistance = historicStdDev * openOrderStdDev;
        var takeProfitDistance = historicStdDev * takeProfitStdDev;
        var stopLossDistance = historicStdDev * stopLossStdDev;

        var openPrice = iCurrentPrice*(1-openOrderDistance);

        if(iDecision == Decision.Buy)
             results.Add(new TradeOrder()
            {
                price = openPrice,
                quantity = 1,
                takeProfitPrice = openPrice * (1+takeProfitDistance),
                stopLossPrice = openPrice * (1-stopLossDistance),
                takeProfit = takeProfit,
                stopLoss = stopLoss,
                duration = 1
            });
             
        return results;
     }

    //*****************************************************************************************

    public override List<TradeOrder> GetSellOrders(IReadOnlyList<Candlestick> iPastHourly, double iCurrentPrice, Decision iDecision)
    {
        var results = new List<TradeOrder>();

        var historicStdDev = HistoricStdDev.Get(iPastHourly, lookbackTimeframe);
        var openOrderDistance = historicStdDev * openOrderStdDev;
        var takeProfitDistance = historicStdDev * takeProfitStdDev;
        var stopLossDistance = historicStdDev * stopLossStdDev;

        var openPrice = iCurrentPrice*(1+openOrderDistance);

        if(iDecision == Decision.Sell)
             results.Add(new TradeOrder()
            {
                price = openPrice,
                quantity = 1,
                takeProfitPrice = openPrice * (1-takeProfitDistance),
                stopLossPrice = openPrice * (1+stopLossDistance),
                takeProfit = takeProfit,
                stopLoss = stopLoss,
                duration = 1
            });
             
        return results;
    }

    //*****************************************************************************************

}