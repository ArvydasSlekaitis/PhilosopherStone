using System.Collections.Generic;

/// <summary> 
/// Simple buy (sell) and hold funcionality with optional take profit and stop loss functionality. 
/// </summary>
public class TradeLogic_1 : TradeLogic
{
    public readonly bool takeProfit = false;
    public readonly bool stopLoss = false;
    public readonly double takeProfitDistance = 0;
    public readonly double stopLossDistance = 0;
    public readonly double openOrderDistance = 1;

    //*****************************************************************************************

    public TradeLogic_1(string iVariation)
        : base(new TradeLogicInfo("TradeLogic_1", iVariation))
    {
        var info = ParseVariation(iVariation);
        
        openOrderDistance = info["OD"];
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