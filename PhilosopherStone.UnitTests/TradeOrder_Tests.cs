using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace PhilosopherStone.UnitTests
{
    [TestClass]
    public class TradeOrder_Tests
    {
        //*****************************************************************************************

        [TestMethod]
        public void IsOpen_NotOpenedWithinDuration_True()
        {
            var order = new TradeOrder()
            {
                duration = 2,
                openOrderTimestamp = null
            };
            
            var res = TradeOrder.IsOpen(order, 1);
            Assert.AreEqual(res, true);
        }

        //*****************************************************************************************

        [TestMethod]
        public void IsOpen_NotOpenedPassedDuration_False()
        {
            var order = new TradeOrder()
            {
                duration = 2,
                openOrderTimestamp = null
            };
            
            var res = TradeOrder.IsOpen(order, 3);
            Assert.AreEqual(res, false);
        }

        //*****************************************************************************************

        [TestMethod]
        public void IsOpen_OpenedButNotClosed_False()
        {
            var order = new TradeOrder()
            {
                openOrderTimestamp = 0,
                closeOrderTimestamp = null
            };
            
            var res = TradeOrder.IsOpen(order, 0);
            Assert.AreEqual(res, true);
        }

        //*****************************************************************************************       

    }
}