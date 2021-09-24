public class TradeOrder
{
    public double price;
    public double quantity;             
    public double takeProfitPrice;
    public double stopLossPrice;
    public double closePrice;
    public bool takeProfit;
    public bool stopLoss;
    public int duration;
    public TradeOrder openIfActive;
    public double? openOrderTimestamp;
    public double? closeOrderTimestamp;

    public static bool IsOpen(TradeOrder iOrder, uint iProccesed) => (iOrder.openOrderTimestamp is null && iProccesed <= iOrder.duration) || (iOrder.openOrderTimestamp != null && iOrder.closeOrderTimestamp is null);
}