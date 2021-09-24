using System.Collections.Generic;

/// <summary> 
/// Same as TradeLogic_3, but takes profit and stop losses based on prievous periods standard deviation.
/// </summary>
public class TradeLogic_4 : TradeLogic
{
    public bool takeProfit = false;
    public bool stopLoss = false;
    public readonly int lookbackTimeframe = 0;
    public readonly double takeProfitStdDev = 0.0;
    public readonly double stopLossStdDev = 0.0;
    public readonly double openFirstOrderStdDev = 0.0;
    public readonly double openOtherOrdersStdDev = 0.0;
    public int numberOfOrders;

    //*****************************************************************************************

    public TradeLogic_4(string iVariation)
        : base(new TradeLogicInfo("TradeLogic_4", iVariation))
    {
        var info = ParseVariation(iVariation);
        
        numberOfOrders = (int)info["N"];
        lookbackTimeframe = (int)info["LT"];
        openFirstOrderStdDev = info["OF"];
        openOtherOrdersStdDev = info["OO"];
        takeProfitStdDev = info["TP"];
        stopLossStdDev = info["SL"];
        tradingType = info["Halting"] == 1 ? TradingType.Halting : TradingType.Continuous;
        
        takeProfit = takeProfitStdDev > 0;
        stopLoss = stopLossStdDev > 0;
    }

    //*****************************************************************************************

    public override List<TradeOrder> GetBuyOrders(IReadOnlyList<Candlestick> iPastHourly, double iCurrentPrice, Decision iDecision)
    {
        var results = new List<TradeOrder>();
                
        var w = new double[numberOfOrders];
        for(int i=0; i<numberOfOrders; i++)
            w[i] = (i+1)*2;
        w = Utils.Normalize(w);

        var historicStdDev = HistoricStdDev.Get(iPastHourly, lookbackTimeframe);
        var openFirstOrderDistance = historicStdDev * openFirstOrderStdDev;
        var openOtherOrdersDistance = historicStdDev * openOtherOrdersStdDev;
        var takeProfitDistance = historicStdDev * takeProfitStdDev;
        var stopLossDistance = historicStdDev * stopLossStdDev;

        var openDistance = openFirstOrderDistance;

        if(iDecision == Decision.Buy)
            for(int i=0; i<numberOfOrders; i++)
            {
                var openPrice = iCurrentPrice*(1-openDistance);

                results.Add(new TradeOrder()
                {
                    price = openPrice,
                    quantity = w[i],
                    takeProfitPrice = openPrice * (1+takeProfitDistance),
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
        for(int i=0; i<numberOfOrders; i++)
            w[i] = (i+1)*2;
        w = Utils.Normalize(w);

        var historicStdDev = HistoricStdDev.Get(iPastHourly, lookbackTimeframe);
        var openFirstOrderDistance = historicStdDev * openFirstOrderStdDev;
        var openOtherOrdersDistance = historicStdDev * openOtherOrdersStdDev;
        var takeProfitDistance = historicStdDev * takeProfitStdDev;
        var stopLossDistance = historicStdDev * stopLossStdDev;

        var openDistance = openFirstOrderDistance;

        if(iDecision == Decision.Sell)
            for(int i=0; i<numberOfOrders; i++)
            {
                var openPrice = iCurrentPrice*(1+openDistance);

                results.Add(new TradeOrder()
                {
                    price = openPrice,
                    quantity = w[i],
                    takeProfitPrice = openPrice * (1-takeProfitDistance),
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