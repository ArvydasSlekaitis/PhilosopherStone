using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using Microsoft.EntityFrameworkCore;

[Index(nameof(testbsLogicInfoId), nameof(testTradeLogicInfoId), nameof(margin), IsUnique = true), Index(nameof(testTradeLogicInfoId))]
public class TestResults
{
    public int Id { get; set; }

    public TradeTest test { get; set; }
    public int testTradeLogicInfoId {get; set; }
    public int testbsLogicInfoId { get; set; }

    public double margin { get; set; }

    public DescriptiveStatistics tradingStat { get; set; }

    public double allTimeTrading { get; set; }
    public double meanDailyReturn { get; set; }
    public double meanOrderReturn { get; set; }

    //*****************************************************************************************
    
    public static List<double> CreateRegressionModel(int iBSLogicId, string iTLTypeName)
    {
        using (var db = new DataContext())
        {
            var parameters = new Dictionary<int, List<double>>();

            foreach(var info in db.tradeLogicInfos.AsNoTracking().Where(x => x.TypeName == iTLTypeName))
            {
                if(!parameters.ContainsKey(info.Id))
                    parameters.Add(info.Id, new List<double>());
                
                parameters[info.Id] = TradeLogic.ParseVariationNumerical(info.VariationName);
            }

            var maxParam = parameters.Max(x => x.Value.Count);
            foreach(var p in parameters)
                while(p.Value.Count < maxParam)
                    p.Value.Add(0);

            var data = new Dictionary<int, (double, List<double>)>();

            foreach(var test in db.testResults.AsNoTracking()
                .Where(x => x.testbsLogicInfoId == iBSLogicId && x.test.tradeLogicInfo.TypeName == iTLTypeName)
                .Select(x => new {TradeLogicId = x.testTradeLogicInfoId, Mean = x.tradingStat.mean}))
                {
                    if(data.TryGetValue(test.TradeLogicId, out var d))
                    {
                        if(test.Mean > d.Item1)
                            data[test.TradeLogicId] = (test.Mean, d.Item2);
                    }
                    else
                        data.Add(test.TradeLogicId, (test.Mean, parameters[test.TradeLogicId]));
                }

            if(data.Count < 25)
                return null;

            var x = data.Select(x => x.Value.Item2).ToList();
            var y = data.Select(x => x.Value.Item1).ToList();
            
            var enabledParameters = new bool[maxParam];
            for(int i=0; i<maxParam; i++)
                enabledParameters[i]  = x.Skip(1).Count(p => p[i]==x[0][i]) == x.Count-1 ? false : true;

            if(enabledParameters.Count(x => x == true) != x[0].Count)
                for(int i=0; i<x.Count; i++)
                {
                    var newEntry = new List<double>();
                    for(int k=0; k<x[i].Count; k++)
                        if(enabledParameters[k])
                            newEntry.Add(x[i][k]);

                    x[i] = newEntry;
                }

            double[] weights = Fit.MultiDim(x.Select(p => p.ToArray()).ToArray(), y.ToArray(), intercept: true);

            var transformedWeights = new List<double>();
            transformedWeights.Add(weights[0]);
            var c = 1;

            for(int i=0; i<maxParam; i++)
                if(!enabledParameters[i])
                    transformedWeights.Add(0);
                else
                {
                    transformedWeights.Add(weights[c]);
                    c++;
                }

            return transformedWeights;
        }
    }

    //*****************************************************************************************
}
