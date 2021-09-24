public class TradeLogicInfo
{
    public int Id { get; set; }
    public string TypeName { get; set; }
    public string VariationName { get; set; }

    public string FullName { get => $"{TypeName}_{VariationName}"; }

    public TradeLogicInfo(){}
    public TradeLogicInfo(string iTypeName, string iVariationName)
    {
        TypeName = iTypeName;
        VariationName = iVariationName;
    }

}