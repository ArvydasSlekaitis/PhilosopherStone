using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

[Index(nameof(TypeName), nameof(MethodName), nameof(VariationName), IsUnique = true)]
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

    public static IEnumerable<BSLogicInfo> FindUntracked(IEnumerable<BSLogicInfo> iInfos)
    {
        using (var db = new DataContext())
        {
            var existingInfos = db.bSLogicInfos.AsNoTracking().ToDictionary(x => x.FullName);

            foreach (var info in iInfos)
                if(!existingInfos.ContainsKey(info.FullName))
                    yield return info;
        }
    }

    //*****************************************************************************************

    public static void AddUntrackedToDB(IEnumerable<BSLogicInfo> iInfos)
    {
        foreach(var chunk in iInfos.Chunk(100000))
            using (var db = new DataContext())
            {        
                db.bSLogicInfos.AddRange(chunk);
                db.SaveChanges();
            }
    }

    //*****************************************************************************************
}