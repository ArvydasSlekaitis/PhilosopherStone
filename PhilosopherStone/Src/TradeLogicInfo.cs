
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

[Index(nameof(TypeName), nameof(VariationName), IsUnique = true)]
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
    
    //*****************************************************************************************

    public static IEnumerable<TradeLogicInfo> FindUntracked(IEnumerable<TradeLogicInfo> iInfos)
    {
        using (var db = new DataContext())
        {
            var existingInfos = db.tradeLogicInfos.AsNoTracking().ToDictionary(x => x.FullName);

            foreach (var info in iInfos)
                if(!existingInfos.ContainsKey(info.FullName))
                    yield return info;
        }
    }

    //*****************************************************************************************

    public static void AddUntrackedToDB(IEnumerable<TradeLogicInfo> iInfos)
    {
        foreach(var chunk in iInfos.Chunk(100000))
            using (var db = new DataContext())
            {        
                db.tradeLogicInfos.AddRange(chunk);
                db.SaveChanges();
            }
    }

    //*****************************************************************************************
}