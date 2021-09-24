using System;
using System.Collections.Generic;
using System.Linq;

/// <summary> 
/// Double order.
/// </summary>
public class TradeLogic_3 : TradeLogic
{
    public bool takeProfit = false;
    public bool stopLoss = false;
    public double takeProfitDistance = 0;
    public double stopLossDistance = 0;
    public double openFirstOrderDistance = 1;
    public double openOtherOrdersDistance = 1;
    public int numberOfOrders;

    //*****************************************************************************************

    public TradeLogic_3(string iVariation)
        : base(new TradeLogicInfo("TradeLogic_3", iVariation))
    {
        var info = ParseVariation(iVariation);
        
        openFirstOrderDistance = info["OFD"];
        openOtherOrdersDistance = info["OOD"];
        numberOfOrders = (int)info["N"];
        takeProfitDistance = info["TP"];
        stopLossDistance = info["SL"];
        tradingType = info["Halting"] == 1 ? TradingType.Halting : TradingType.Continuous;

        takeProfit = takeProfitDistance > 0;
        stopLoss = stopLossDistance > 0;
    }

    //*****************************************************************************************

    public override List<TradeOrder> GetBuyOrders(IReadOnlyList<Candlestick> iPastHourly, double iCurrentPrice, Decision iDecision)
    {
        var results = new List<TradeOrder>();
                
        var w = new double[numberOfOrders];
        for(int i=0; i<numberOfOrders; i++)
            w[i] = (i+1)*2;
        w = Utils.Normalize(w);

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