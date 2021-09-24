using System.Collections.Generic;
using System.Linq;

public class BSLogicInfo
{
    public int Id { get; set; }

    public string TypeName { get; set; }
    public string MethodName { get; set; }
    public string VariationName { get; set; }

    public string FullName { get => $"{TypeName}_{MethodName}_{VariationName}"; }
    
    public BSLogicInfo(){}
    public BSLogicInfo(string iTypeName, string iMethodName, string iVariationName)
    {
        TypeName = iTypeName;
        MethodName = iMethodName;
        VariationName = iVariationName;
    }

    //*****************************************************************************************
}