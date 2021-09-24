using System;
using System.Collections.Generic;
using System.Linq;

/// <summary> 
/// Same as TradeLogic_5, but all subsequent orders has a times different quantity.
/// </summary>
public class TradeLogic_8 : TradeLogic
{
    public bool takeProfit = false;
    public bool stopLoss = false;
    public readonly int lookbackTimeframe = 0;
    public readonly double takeProfitStdDev = 0.0;
    public readonly double stopLossStdDev = 0.0;
    public readonly double openFirstOrderStdDev = 0.0;
    public readonly double openOtherOrdersStdDev = 0.0;
    public int numberOfOrders;
    public readonly double subsequentOrderSize = 0.5;
    public readonly double quantityBlendStartStd = 0.0;
    public readonly double quantityBlendEndStd = 0.0;
    public readonly double quantityBlendFinalWeight = 1.0;
    
    //*****************************************************************************************

    public TradeLogic_8(string iVariation)
        : base(new TradeLogicInfo("TradeLogic_8", iVariation))
    {
        var info = ParseVariation(iVariation);
        
        numberOfOrders = (int)info["N"];
        lookbackTimeframe = (int)info["LT"];
        openFirstOrderStdDev = info["OF"];
        openOtherOrdersStdDev = info["OO"];
        takeProfitStdDev = info["TP"];
        stopLossStdDev = info["SL"];
        tradingType = info["Halting"] == 1 ? TradingType.Halting : TradingType.Continuous;
        subsequentOrderSize = info["SOS"];
        quantityBlendStartStd = info["QBSS"];
        quantityBlendEndStd = info["QBES"];
        quantityBlendFinalWeight = info["QBFW"];
        
        takeProfit = takeProfitStdDev > 0;
        stopLoss = stopLossStdDev > 0;
    }

    //*****************************************************************************************

    public override List<TradeOrder> GetBuyOrders(IReadOnlyList<Candlestick> iPastHourly, double iCurrentPrice, Decision iDecision)
    {
        var results = new List<TradeOrder>();
                
        var w = new double[numberOfOrders];
        w[0] = 1.0;
        for(int i=1; i<numberOfOrders; i++)
            w[i] = w[i-1]*subsequentOrderSize;
        w = Utils.Normalize(w);

        var historicStdDev = HistoricStdDev.Get(iPastHourly, lookbackTimeframe);
        var openFirstOrderDistance = historicStdDev * openFirstOrderStdDev;
        var openOtherOrdersDistance = historicStdDev * openOtherOrdersStdDev;
        var takeProfitDistance = historicStdDev * takeProfitStdDev;
        var stopLossDistance = historicStdDev * stopLossStdDev;

        if(quantityBlendFinalWeight != 1.0 && historicStdDev >= quantityBlendStartStd)
        {
            var multiplier = Utils.Lerp(1.0, quantityBlendFinalWeight, Math.Clamp((historicStdDev-quantityBlendStartStd) / (quantityBlendEndStd - quantityBlendStartStd), 0, 1));
            w = w.Select(x => x * multiplier).ToArray();
        }

        var openDistance = openFirstOrderDistance;

        if(iDecision == Decision.Buy)
            for(int i=0; i<numberOfOrders; i++)
            {
                var openPrice = iCurrentPrice*(1-openDistance);

                results.Add(new TradeOrder()
                {
                    price = openPrice,
                    quantity = w[i],
                    takeProfitPrice = iCurrentPrice * (1-openFirstOrderDistance) * (1+takeProfitDistance),
                    stopLossPrice = openPrice * (1-stopLossDistance),
                    takeProfit = takeProfit,
                    stopLoss = stopLoss,
                    openIfActive = i == 0 ? null : results[i-1],
                    duration = i == 0 ? 1 : 168
                });

                openDistance += openOtherOrdersDistance;
            }

        return results;
     }

    //*****************************************************************************************

    public override List<TradeOrder> GetSellOrders(IReadOnlyList<Candlestick> iPastHourly, double iCurrentPrice, Decision iDecision)
    {
        var results = new List<TradeOrder>();

        var w = new double[numberOfOrders];
        w[0] = 1.0;
        for(int i=1; i<numberOfOrders; i++)
            w[i] = w[i-1]*subsequentOrderSize;
        w = Utils.Normalize(w);

        var historicStdDev = HistoricStdDev.Get(iPastHourly, lookbackTimeframe);
        var openFirstOrderDistance = historicStdDev * openFirstOrderStdDev;
        var openOtherOrdersDistance = historicStdDev * openOtherOrdersStdDev;
        var takeProfitDistance = historicStdDev * takeProfitStdDev;
        var stopLossDistance = historicStdDev * stopLossStdDev;

        if(quantityBlendFinalWeight != 1.0 && historicStdDev >= quantityBlendStartStd)
        {
            var multiplier = Utils.Lerp(1.0, quantityBlendFinalWeight, Math.Clamp((historicStdDev-quantityBlendStartStd) / (quantityBlendEndStd - quantityBlendStartStd), 0, 1));
            w = w.Select(x => x * multiplier).ToArray();
        }

        var openDistance = openFirstOrderDistance;

        if(iDecision == Decision.Sell)
            for(int i=0; i<numberOfOrders; i++)
            {
                var openPrice = iCurrentPrice*(1+openDistance);

                results.Add(new TradeOrder()
                {
                    price = openPrice,
                    quantity = w[i],
                    takeProfitPrice = iCurrentPrice * (1+openFirstOrderDistance) * (1-takeProfitDistance),
                    stopLossPrice = openPrice * (1+stopLossDistance),
                    takeProfit = takeProfit,
                    stopLoss = stopLoss,
                    openIfActive = i == 0 ? null : results[i-1],    
                    duration = i == 0 ? 1 : 168
                });

                openDistance += openOtherOrdersDistance;
            }   
        return results;
    }

    //*****************************************************************************************

}