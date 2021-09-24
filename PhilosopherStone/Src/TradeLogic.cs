using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;

public abstract class TradeLogic
{
    public enum TradingType { Continuous, Halting };

    public abstract List<TradeOrder> GetBuyOrders(IReadOnlyList<Candlestick> iPastHourly, double iCurrentPrice, Decision iDecision);
    public abstract List<TradeOrder> GetSellOrders(IReadOnlyList<Candlestick> iPastHourly, double iCurrentPrice, Decision iDecision);
    
    public readonly TradeLogicInfo info;
    public TradingType tradingType = TradingType.Continuous;

    //*****************************************************************************************

    public TradeLogic(TradeLogicInfo iInfo) => info = iInfo;

    //*****************************************************************************************

    // Returns average return with days to completion
    public static (double, uint) Simulate(List<TradeOrder> iBuyOrders, List<TradeOrder> iSellOrders, double iMargin, IReadOnlyList<Candlestick> iFutureHourly, Dictionary<ulong, Decision> iDecisions)
    {          
        Utils.Assert(iBuyOrders.Count + iSellOrders.Count > 0, "iBuyOrders.Count + iSellOrders.Count > 0");

        // Validate buy orders
        foreach(var order in iBuyOrders)
        {
            Utils.Assert(order.price <= iFutureHourly.First().OpenPrice, "order.Price <= iFutureHourly.First().OpenPrice");
            Utils.Assert(order.quantity >= 0 && order.quantity <= 1, "order.Quantity >= 0 && order.Quantity <= 1");
            Utils.Assert(order.openOrderTimestamp == null, "order.OpenOrderTimestamp == null");
            Utils.Assert(order.closeOrderTimestamp == null, "order.CloseOrderTimestamp == null");

            if(order.takeProfit) 
                Utils.Assert(order.takeProfitPrice > order.price, "order.TakeProfitPrice > order.Price");
             
            if(order.stopLoss)
                Utils.Assert(order.stopLossPrice < order.price, "order.StopLossPrice < order.Price");
        }

        // Validate sell orders
        foreach(var order in iSellOrders)
        {
            Utils.Assert(order.price >= iFutureHourly.First().OpenPrice, "order.Price >= iFutureHourly.First().OpenPrice");
            Utils.Assert(order.quantity >= 0 && order.quantity <= 1, "order.Quantity >= 0 && order.Quantity <= 1");
            Utils.Assert(order.openOrderTimestamp == null, "order.OpenOrderTimestamp == null");
            Utils.Assert(order.closeOrderTimestamp == null, "order.CloseOrderTimestamp == null");

            if(order.takeProfit) 
                Utils.Assert(order.takeProfitPrice < order.price, "order.TakeProfitPrice < order.Price");
             
            if(order.stopLoss)
                Utils.Assert(order.stopLossPrice > order.price, "order.StopLossPrice > order.Price");
        }

        var pred = iDecisions[iFutureHourly.First().Timestamp];
        uint cProcessed = 0;

        foreach(var c in iFutureHourly)
        {
            cProcessed++;

            // Process buy orders
            foreach(var bOrder in iBuyOrders) 
                ProcessBuyOrder(bOrder, c, cProcessed);

            // Process sell orders
            foreach(var sOrder in iSellOrders)
                ProcessSellOrder(sOrder, c, cProcessed);

            // Close orders if direction changes or margin was violated
            if(pred != iDecisions[c.Timestamp] || CalcReturn(iBuyOrders, iSellOrders, iMargin, c.ClosePrice) <= -0.50)
            {
                iSellOrders.Concat(iBuyOrders).Where(x => x.openOrderTimestamp != null && x.closeOrderTimestamp is null).ToList().ForEach(x =>
                {
                    x.closeOrderTimestamp = c.Timestamp;
                    x.closePrice = c.ClosePrice;
                });
                break;
            }

            // Stop if all orders are closed, or was not opened
            var openOrders = 0;

            foreach(var order in iBuyOrders)
                if(TradeOrder.IsOpen(order, cProcessed))
                    openOrders++;

            foreach(var order in iSellOrders)
                if(TradeOrder.IsOpen(order, cProcessed))
                    openOrders++;

            if(openOrders == 0)
                break;
        }

        var lastCandle = iFutureHourly[iFutureHourly.Count-1];

        // Close all orders that is still open
        foreach(var order in iBuyOrders.Concat(iSellOrders))
            if(order.openOrderTimestamp != null && order.closeOrderTimestamp is null)
            {
                order.closeOrderTimestamp = lastCandle.Timestamp;
                order.closePrice = lastCandle.ClosePrice; 
            }

        // Return average daily return
        return (CalcReturn(iBuyOrders, iSellOrders, iMargin, lastCandle.ClosePrice), cProcessed);
    }

    //*****************************************************************************************

    public static double CalcReturn(List<TradeOrder> iBuyOrders, List<TradeOrder> iSellOrders, double iMargin, double iCurrentPrice)
    {
        var returns = new List<double>(iBuyOrders.Count + iSellOrders.Count);

        for(var i =0; i<iBuyOrders.Count; i++)
        {
            var order = iBuyOrders[i];

            if(order.openOrderTimestamp is null)
                continue;

            var sellPrice = order.closeOrderTimestamp is null ? iCurrentPrice : order.closePrice;
            returns.Add(Math.Log(sellPrice / order.price - 0.00028)*order.quantity/iMargin);
        }

        for(var i =0; i<iSellOrders.Count; i++)
        {
            var order = iSellOrders[i];

            if(order.openOrderTimestamp is null)
                continue;

            var sellPrice = order.closeOrderTimestamp is null ? iCurrentPrice : order.closePrice;
            returns.Add(Math.Log(order.price / sellPrice -0.00028)*order.quantity/iMargin);
        }   
        
        switch(returns.Count)
        {
            case 0: return 0;
            case 1: return returns[0];
            case 2: return (returns[0] + returns[1])/2.0;
            default: return ArrayStatistics.Mean(returns.ToArray());
        }    
    }

    //*****************************************************************************************

    public static double CalculateAverageDailyReturn(double iReturn, double iDaysToCompletion)
    {
        if(iReturn <= -1)
            return -1;
        else
            return Math.Pow(1+iReturn, iDaysToCompletion) -1.0;
    }

    //*****************************************************************************************

    public static void ProcessBuyOrder(TradeOrder iOrder, Candlestick iLast, uint iTime)
    {
        // Enter buy orders orders
        if(iOrder.openOrderTimestamp is null && iTime <= iOrder.duration && (iOrder.openIfActive is null || iOrder.openIfActive!=null && iOrder.openIfActive.openOrderTimestamp != null && iOrder.openIfActive.closeOrderTimestamp is null))
            if(iLast.LowPrice <= iOrder.price)
            {
                iOrder.openOrderTimestamp = iLast.Timestamp;
                return;
            }

        // Stop loss from buy orders
        if(iOrder.openOrderTimestamp != null && iOrder.closeOrderTimestamp is null && iOrder.stopLoss)    
            if(iLast.LowPrice <= iOrder.stopLossPrice)
            {
                iOrder.closeOrderTimestamp = iLast.Timestamp;
                iOrder.closePrice = (iLast.LowPrice + iOrder.stopLossPrice)/2.0; 
                return;
            }

        // Take profit from buy orders
        if(iOrder.openOrderTimestamp != null && iOrder.closeOrderTimestamp is null && iOrder.takeProfit)    
            if(iLast.HighPrice >= iOrder.takeProfitPrice)
            {
                iOrder.closeOrderTimestamp = iLast.Timestamp;
                iOrder.closePrice = iOrder.takeProfitPrice; 
                return;
            }
    }

    //*****************************************************************************************

    public static void ProcessSellOrder(TradeOrder iOrder, Candlestick iLast, uint iTime)
    {
        // Enter sell orders 
        if(iOrder.openOrderTimestamp is null && iTime <= iOrder.duration && (iOrder.openIfActive is null || iOrder.openIfActive!=null && iOrder.openIfActive.openOrderTimestamp != null && iOrder.openIfActive.closeOrderTimestamp is null))
            if(iLast.HighPrice >= iOrder.price)
            {
                iOrder.openOrderTimestamp = iLast.Timestamp;
                return;
            }

        // Stop loss from sell orders
        if(iOrder.openOrderTimestamp != null && iOrder.closeOrderTimestamp is null && iOrder.stopLoss)    
            if(iLast.HighPrice >= iOrder.stopLossPrice)
            {
                iOrder.closeOrderTimestamp = iLast.Timestamp;
                iOrder.closePrice = (iOrder.stopLossPrice + iLast.HighPrice) / 2.0; 
                return;
            }

        // Take profit from sell orders
        if(iOrder.openOrderTimestamp != null && iOrder.closeOrderTimestamp is null && iOrder.takeProfit)    
            if(iLast.LowPrice <= iOrder.takeProfitPrice)
            {
                iOrder.closeOrderTimestamp = iLast.Timestamp;
                iOrder.closePrice = iOrder.takeProfitPrice; 
                return;
            }
    }

    //*****************************************************************************************

    public static List<double> SimulateContinious(List<(ulong, (double, uint))> iReturns)
    {
        var results = new double[iReturns.Count];

        for(int i=iReturns.Count-1; i>=0; i--)
        {
            var capital = 1.0;
            for(int k = i; k<iReturns.Count;)
            {
                if(k == i)
                {
                    capital *= 1+iReturns[k].Item2.Item1;
                    k+=(int)iReturns[k].Item2.Item2;
                }
                else
                {
                    capital *= results[k];
                    break;
                }
                if(capital <= 0)
                    break;
            }

            results[i]=capital;            
        }

        return results.ToList();
    }

    //*****************************************************************************************

    public static List<double> SimulateHalting(List<(ulong, (double, uint))> iReturns, Dictionary<ulong, Decision> iDecisions)
    {
        var decisions = new List<Decision>(iReturns.Count);
        iReturns.ForEach(x => decisions.Add(iDecisions[x.Item1]));

        var results = new double[iReturns.Count];

        for(int i=iReturns.Count-1; i>=0; i--)
        {
            var capital = 1.0;
            var decision = decisions[i];

            for(int k = i; k<iReturns.Count;)
            {
                if(k == i)
                {
                    capital *= 1+iReturns[k].Item2.Item1;
                    decision = decisions[k];                    
                    k+=(int)iReturns[k].Item2.Item2;                    
                }
                else if(decision != decisions[k])
                {
                    capital *= results[k];
                    break;
                }
                else
                    k++;
                
                if(capital <= 0)
                    break;
            }

            results[i]=capital;   
        }

        return results.ToList();
    }

    //*****************************************************************************************

    public static Dictionary<string, double> ParseVariation(string iVariation)
    {
        var results = new Dictionary<string, double>();

        foreach(string s in iVariation.Split('_'))
        {
            string name = new string(s.TakeWhile(x => char.IsLetter(x)).ToArray());
            double value = double.Parse(new string(s.SkipWhile(x => char.IsLetter(x)).ToArray()));
            results.Add(name, value);
        }

        return results;
    }

    //*****************************************************************************************

    public static List<double> ParseVariationNumerical(string iVariation)
    {
        var results = new List<double>();

        foreach(string s in iVariation.Split('_'))
        {
            double value = double.Parse(new string(s.SkipWhile(x => char.IsLetter(x)).ToArray()));
            results.Add(value);
        }

        return results;
    }

    //*****************************************************************************************

}