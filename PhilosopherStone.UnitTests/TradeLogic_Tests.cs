using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PhilosopherStone.UnitTests
{
    [TestClass]
    public class TradeLogic_Tests
    {

    //*****************************************************************************************

        [TestMethod]
        public void CalcReturn_NoOrders_Zero()
        {
            var res = TradeLogic.CalcReturn(new List<TradeOrder>(), new List<TradeOrder>(), 0.0, 1);
            Assert.AreEqual(res, 0.0);
        }

    //*****************************************************************************************

        [TestMethod]
        public void CalcReturn_CompletedBuyOrder_Profit()
        {
            var buyOrders = new List<TradeOrder>()
            {
                new TradeOrder()
                {
                    price = 1.1,
                    quantity = 0.75,           
                    closePrice = 1.15,
                    openOrderTimestamp = 0,
                    closeOrderTimestamp = 0
                }
            };

            var res = TradeLogic.CalcReturn(buyOrders, new List<TradeOrder>(), 0.5, 1.2);
            Assert.AreEqual(res, 0.066275850918098, 0.0000000001);
        }

        //*****************************************************************************************

        [TestMethod]
        public void CalcReturn_CompletedBuyOrder_Loss()
        {
            var buyOrders = new List<TradeOrder>()
            {
                new TradeOrder()
                {
                    price = 1.15,
                    quantity = 0.75,           
                    closePrice = 1.1,
                    openOrderTimestamp = 0,
                    closeOrderTimestamp = 0
                }
            };

            var res = TradeLogic.CalcReturn(buyOrders, new List<TradeOrder>(), 0.5, 1.2);
            Assert.AreEqual(res, -0.067116799044828, 0.0000000001);
        }

        //*****************************************************************************************

        [TestMethod]
        public void CalcReturn_CompletedSellOrder_Profit()
        {
            var sellOrders = new List<TradeOrder>()
            {
                new TradeOrder()
                {
                    price = 1.15,
                    quantity = 0.75,           
                    closePrice = 1.1,
                    openOrderTimestamp = 0,
                    closeOrderTimestamp = 0
                }
            };

            var res = TradeLogic.CalcReturn(new List<TradeOrder>(), sellOrders, 0.5, 1.2);
            Assert.AreEqual(res, 0.066275850918098, 0.0000000001);
        }

        //*****************************************************************************************

        [TestMethod]
        public void CalcReturn_CompletedSellOrder_Loss()
        {
            var sellOrders = new List<TradeOrder>()
            {
                new TradeOrder()
                {
                    price = 1.1,
                    quantity = 0.75,           
                    closePrice = 1.15,
                    openOrderTimestamp = 0,
                    closeOrderTimestamp = 0
                }
            };

            var res = TradeLogic.CalcReturn(new List<TradeOrder>(), sellOrders, 0.5, 1.2);
            Assert.AreEqual(res, -0.067116799044828, 0.0000000001);
        }

        //*****************************************************************************************

        [TestMethod]
        public void CalcReturn_CompletedBuySellOrder_Average()
        {
            var sellOrders = new List<TradeOrder>()
            {
                new TradeOrder()
                {
                    price = 1.1,
                    quantity = 0.75,           
                    closePrice = 1.15,
                    openOrderTimestamp = 0,
                    closeOrderTimestamp = 0
                }
            };

            var buyOrders = new List<TradeOrder>()
            {
                new TradeOrder()
                {
                    price = 1.1,
                    quantity = 0.75,           
                    closePrice = 1.15,
                    openOrderTimestamp = 0,
                    closeOrderTimestamp = 0
                }
            };

            var res = TradeLogic.CalcReturn(buyOrders, sellOrders, 0.5, 1.2);
            Assert.AreEqual(res, (-0.067116799044828+0.066275850918098)/2.0, 0.0000000001);
        }

        //*****************************************************************************************

        [TestMethod]
        public void CalcReturn_NoOpenedBuyOrder_Zero()
        {
            var buyOrders = new List<TradeOrder>()
            {
                new TradeOrder()
                {
                    price = 1.1,
                    quantity = 0.75,           
                    closePrice = 1.15,
                    openOrderTimestamp = null,
                    closeOrderTimestamp = null
                }
            };

            var res = TradeLogic.CalcReturn(buyOrders, new List<TradeOrder>(), 0.5, 1.2);
            Assert.AreEqual(res, 0.0, 0.0000000001);
        }

        //*****************************************************************************************

        [TestMethod]
        public void CalcReturn_NoOpenedSellOrder_Zero()
        {
            var sellOrders = new List<TradeOrder>()
            {
                new TradeOrder()
                {
                    price = 1.1,
                    quantity = 0.75,           
                    closePrice = 1.15,
                    openOrderTimestamp = null,
                    closeOrderTimestamp = null
                }
            };

            var res = TradeLogic.CalcReturn(sellOrders, new List<TradeOrder>(), 0.5, 1.2);
            Assert.AreEqual(res, 0.0, 0.0000000001);
        }

        //*****************************************************************************************

        [TestMethod]
        public void ProccessBuyOrder_NotOpenedWithinDurationNoPredesor_Open()
        {
            var order = new TradeOrder()
            {
                price = 1.1,
                quantity = 0.75,           
                closePrice = 1.15,
                duration = 1,
                openOrderTimestamp = null,
                closeOrderTimestamp = null
            };

            var lastCandlestick = new Candlestick()
            {
                OpenPrice = 1.1,
                ClosePrice = 1.2,
                HighPrice = 1.2,
                LowPrice = 1.1,
                MedianPrice = 1.5,
                Timestamp = 1
            };

            TradeLogic.ProcessBuyOrder(order, lastCandlestick, 0);

            Assert.AreEqual(order.openOrderTimestamp, 1);
            Assert.AreEqual(order.price, 1.1);
        }

        //*****************************************************************************************

        [TestMethod]
        public void ProccessBuyOrder_NotOpenedExpiredDurationNoPredesor_NoOpen()
        {
            var order = new TradeOrder()
            {
                price = 1.1,
                quantity = 0.75,           
                closePrice = 1.15,
                duration = 1,
                openOrderTimestamp = null,
                closeOrderTimestamp = null
            };

            var lastCandlestick = new Candlestick()
            {
                OpenPrice = 1.1,
                ClosePrice = 1.2,
                HighPrice = 1.2,
                LowPrice = 1.1,
                MedianPrice = 1.5,
                Timestamp = 1
            };

            TradeLogic.ProcessBuyOrder(order, lastCandlestick, 2);

            Assert.AreEqual(order.openOrderTimestamp, null);
        }

        //*****************************************************************************************

        [TestMethod]
        public void ProccessBuyOrder_NotOpenedWithinDurationWithOpenedPredesor_Open()
        {
            var pred = new TradeOrder()
            {
                price = 1.1,
                quantity = 0.75,           
                closePrice = 1.15,
                duration = 1,
                openOrderTimestamp = 1,
                closeOrderTimestamp = null
            };

            var order = new TradeOrder()
            {
                price = 1.1,
                quantity = 0.75,           
                closePrice = 1.15,
                duration = 1,
                openOrderTimestamp = null,
                closeOrderTimestamp = null,
                openIfActive = pred
            };

            var lastCandlestick = new Candlestick()
            {
                OpenPrice = 1.1,
                ClosePrice = 1.2,
                HighPrice = 1.2,
                LowPrice = 1.1,
                MedianPrice = 1.5,
                Timestamp = 1
            };

            TradeLogic.ProcessBuyOrder(order, lastCandlestick, 1);

            Assert.AreEqual(order.openOrderTimestamp, 1);
            Assert.AreEqual(order.price, 1.1);
        }

        //*****************************************************************************************

        [TestMethod]
        public void ProccessBuyOrder_NotOpenedWithinDurationWithNotOpenedPredesor_NoOpen()
        {
            var pred = new TradeOrder()
            {
                price = 1.1,
                quantity = 0.75,           
                closePrice = 1.15,
                duration = 1,
                openOrderTimestamp = null,
                closeOrderTimestamp = null
            };

            var order = new TradeOrder()
            {
                price = 1.1,
                quantity = 0.75,           
                closePrice = 1.15,
                duration = 1,
                openOrderTimestamp = null,
                closeOrderTimestamp = null,
                openIfActive = pred
            };

            var lastCandlestick = new Candlestick()
            {
                OpenPrice = 1.1,
                ClosePrice = 1.2,
                HighPrice = 1.2,
                LowPrice = 1.1,
                MedianPrice = 1.5,
                Timestamp = 1
            };

            TradeLogic.ProcessBuyOrder(order, lastCandlestick, 1);

            Assert.AreEqual(order.openOrderTimestamp, null);
        }

        //*****************************************************************************************

        [TestMethod]
        public void ProccessBuyOrder_OpenedNotClosedTakeProfit_TakeProfit()
        {
            var order = new TradeOrder()
            {
                price = 1.1,
                quantity = 0.75,           
                closePrice = 1.15,
                duration = 1,
                takeProfit = true,
                takeProfitPrice = 1.2,
                openOrderTimestamp = 0,
                closeOrderTimestamp = null
            };

            var lastCandlestick = new Candlestick()
            {
                OpenPrice = 1.1,
                ClosePrice = 1.2,
                HighPrice = 1.2,
                LowPrice = 1.1,
                MedianPrice = 1.5,
                Timestamp = 1
            };

            TradeLogic.ProcessBuyOrder(order, lastCandlestick, 0);

            Assert.AreEqual(order.closeOrderTimestamp, 1);
            Assert.AreEqual(order.closePrice, 1.2);
        }

        //*****************************************************************************************

        [TestMethod]
        public void ProccessBuyOrder_OpenedNotClosedDontTakeProfit_DontTakeProfit()
        {
            var order = new TradeOrder()
            {
                price = 1.1,
                quantity = 0.75,           
                closePrice = 1.15,
                duration = 1,
                takeProfit = true,
                takeProfitPrice = 1.2,
                openOrderTimestamp = 0,
                closeOrderTimestamp = null
            };

            var lastCandlestick = new Candlestick()
            {
                OpenPrice = 1.1,
                ClosePrice = 1.15,
                HighPrice = 1.15,
                LowPrice = 1.1,
                MedianPrice = 1.01,
                Timestamp = 1
            };

            TradeLogic.ProcessBuyOrder(order, lastCandlestick, 0);

            Assert.AreEqual(order.closeOrderTimestamp, null);
        }

        //*****************************************************************************************

        [TestMethod]
        public void SimulateHalting_Halt()
        {
            var returns = new List<(ulong, (double, uint))>()
            {
                (1, (0.1, 1)),
                (2, (0.1, 1)),
                (3, (0.1, 1)),
                (4, (0.1, 2)),
                (5, (0.1, 1))
            };
            
            var decisions = new Dictionary<ulong, Decision>()
            {
                {1, Decision.Buy},
                {2, Decision.Buy},
                {3, Decision.Buy},
                {4, Decision.Buy},
                {5, Decision.Sell}
            };

            var r = TradeLogic.SimulateHalting(returns, decisions);

            Assert.AreEqual(r.Count, 5);
            Assert.AreEqual(r[0], 1.1*1.1);
            Assert.AreEqual(r[1], 1.1*1.1);
            Assert.AreEqual(r[2], 1.1*1.1);
            Assert.AreEqual(r[3], 1.1);
            Assert.AreEqual(r[4], 1.1);
        }

        //*****************************************************************************************

        [TestMethod]
        public void SimulateHalting_Continious()
        {
            var returns = new List<(ulong, (double, uint))>()
            {
                (1, (0.1, 1)),
                (2, (0.1, 1)),
                (3, (0.1, 1)),
                (4, (0.1, 2)),
                (5, (0.1, 1))
            };

            var r = TradeLogic.SimulateContinious(returns);

            Assert.AreEqual(r.Count, 5);
            Assert.AreEqual(r[0], 1.1*1.1*1.1*1.1);
            Assert.AreEqual(r[1], 1.1*1.1*1.1);
            Assert.AreEqual(r[2], 1.1*1.1);
            Assert.AreEqual(r[3], 1.1);
            Assert.AreEqual(r[4], 1.1);
        }

        //*****************************************************************************************
    }
}
