using System;
using System.Collections.Generic;
using System.IO;

public abstract class BSLogic
{
    public abstract Decision GetDecision(IReadOnlyList<Candlestick> iPastDaily, IReadOnlyList<Candlestick> iPastHourly);

    public readonly BSLogicInfo info;

    //*****************************************************************************************

    public BSLogic(BSLogicInfo iInfo) => info = iInfo;

    //*****************************************************************************************

    public static Dictionary<ulong, Decision> LoadFromCache(string iFilename)
    {
        if(!File.Exists(iFilename))
            throw new ArgumentException($"File {iFilename} could not be found.");

        var results = new Dictionary<ulong, Decision>();

        Utils.LoadFromBinary(out List<(ulong, int)> dat, iFilename);
        dat.ForEach(x => results.Add(x.Item1, (Decision)x.Item2));

        return results;
    }

    //*****************************************************************************************

    public static List<double> ParseVariation(string iVariation)
    {
        var results = new List<double>();

        foreach(string s in iVariation.Split('_'))
            results.Add(double.Parse(s));

        return results;
    }

    //*****************************************************************************************

}

