using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Data;
using MathNet.Numerics.Statistics;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.Collections.Concurrent;

public enum Decision {Sell = 0, Buy = 1};

namespace PhilosopherStone
{
    public class Program
    {
        public static volatile bool cancelled = false;

        //*****************************************************************************************

        static async Task Main(string[] args)
        {     
            CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("lt-LT", false);

            if(double.Parse("0,0005")!=0.0005 || $"{0.0005}" != "0,0005")
                return;

            Console.WriteLine("Enter server IP address or enter for localhost");
            var ip = Console.ReadLine();
            if(ip!="" && ip.Contains("."))
                DataContext.serverIp = ip;

            const bool kNewLogicsAdded = false;
            const bool kOutputPerformance = false;
            const bool kUpdatePerformance = true;
            const bool kFixBrokenTests = false;

            // dotnet publish --configuration Release

            Console.CancelKeyPress += delegate (object sender, ConsoleCancelEventArgs args) 
            {
                args.Cancel = true;
                Program.cancelled = true;
                Console.WriteLine("CANCEL command received! Cleaning up. please wait...");
            };

            if(!Directory.Exists("Data"))
                Directory.CreateDirectory("Data");

            if(!Directory.Exists("Data/BSLogicCache"))
                Directory.CreateDirectory("Data/BSLogicCache");

            var numberFormat = CultureInfo.InvariantCulture.NumberFormat;

            // Consolidate candlesticks if does not exist
            if(!File.Exists("Data/Daily.dat") || !File.Exists("Data/Hourly.dat"))
            {
                var cMinute = Candlestick.LoadHistorical1m();
                Candlestick.Save("Data/Daily.dat", Candlestick.Consolidate(cMinute, 1));
                Candlestick.Save("Data/Hourly.dat", Candlestick.Consolidate(cMinute, 24));
            }
        
            var cHourly = Candlestick.Load("Data/Hourly.dat");
            var cDaily = Candlestick.Load("Data/Daily.dat");

            var max = cDaily.Max(x => x.HighPrice - x.OpenPrice);
            var min = cDaily.Min(x => x.LowPrice - x.OpenPrice);

            HistoricStdDev.Load();

            var margins = new double[]{1.0, 0.75, 0.5, 0.25, 0.1, 0.05, 0.035};
            var mlContext = new MLContext(seed: 0);
        
            if(kNewLogicsAdded)
            {
                // Find and add untracked BSLogics to DB
                BSLogicInfo.AddUntrackedToDB(BSLogicInfo.FindUntracked(CreateBSLogicList()));
                
                // Find and add untracked TradeLogics to DB
                TradeLogicInfo.AddUntrackedToDB(TradeLogicInfo.FindUntracked(CreateTradeLogicList()));

                // Find and add untracked Tests to DB
                //TradeTest.AddUntrackedToDB(TradeTest.GetKeyList());
                //TradeTest.AddUntrackedToDB(TradeTest.GetBSLogicKeyList("BSLogic_Inline", "MedianPriceChangeVelocityIncreasing_Volatile_H"));
                //Console.WriteLine("BSLogic_Inline MedianPriceChangeVelocityIncreasing_Volatile_H tests added.");

                //TradeTest.AddUntrackedToDB(TradeTest.GetTradeLogicKeyList("TradeLogic_8", "SOS0,75"));
                //Console.WriteLine("TradeLogic_8 SOS0,75 tests added.");            
            }   

            // Fix broken tests.            
            if(kFixBrokenTests)
                using (var db = new DataContext())
                {
                    db.tradeTests.Include(x => x.results)
                        .Where(x => x.State == 1 && x.results.Count == 0).ToList()
                        .ForEach(x => x.State = 0);

                    db.SaveChanges();
                }     
            
            // Process decisions
            if(kNewLogicsAdded)
                using (var db = new DataContext())
                {
                    ConsoleSection($"Starting to process BSDecisions");

                    foreach(var info in db.bSLogicInfos.AsNoTracking())
                    {               
                        string fileName = $"Data/BSLogicCache/{info.FullName}.dat";

                        if(!File.Exists(fileName))  
                        {
                            var log = CreateBSLogic(info, mlContext);

                            var decisions = new Dictionary<ulong, Decision>();
                            for(int i=24*301; i<cHourly.Count; i++)
                            {
                                var pastDaily = new ShalowList<Candlestick>(cDaily, 0, Candlestick.FindLastHistoricIndex(cDaily, cHourly[i].Timestamp)+1);
                                var pastHourly = new ShalowList<Candlestick>(cHourly, 0, i);
                                decisions.Add(cHourly[i].Timestamp, log.GetDecision(pastDaily, pastHourly));
                            }

                            Utils.SaveToBinary(decisions.Select(x => (x.Key, (int)x.Value)).ToList(), fileName);
                            Console.WriteLine($"Proccessed {info.FullName} BS decision logic.");  
                        }
                    }
                }  

            var decisionsLibrary = new ConcurrentDictionary<int, Dictionary<ulong, Decision>>(); 
     
            // Update perforamance for new tests
            if(kNewLogicsAdded)
            {
                // Firstly try to do initial perf calc.
                using (var db = new DataContext())
                {
                    var bsList = db.bSLogicInfos.AsNoTracking().Select(x => x.Id).ToList();
                    var tlList = db.tradeLogicInfos.AsNoTracking().Select(x => x.TypeName).Distinct().ToList();
                    
                    foreach(var bsID in bsList)
                        foreach(var tlTypeName in tlList)  
                            while(true)
                            {
                                var n = UpdateTestsPerformance(bsID, tlTypeName, true);
                                if(n<=0)
                                    break;
                            }
                }
        
                // For the rest set perf to max.
                using (var db = new DataContext())
                {
                    var maxPerf = db.testResults.AsNoTracking().Max(x => x.allTimeTrading);

                    foreach(var bsID in db.bSLogicInfos.AsNoTracking().Select(x => x.Id))
                        using (var dbl = new DataContext())
                        {
                            foreach(var test in dbl.tradeTests.Where(x => x.State == 0 && x.PredictedPerformance == 0))
                                test.PredictedPerformance = maxPerf;
                            
                            dbl.SaveChanges();
                        }
                }
                Console.WriteLine("New tests were added succefully.");
                Console.ReadLine();
            }            

            // Update performance
            Task bsPerfUpdateTask = null;
            if(kUpdatePerformance)
                bsPerfUpdateTask = Task.Run(() => UpdateTestsPerformance(10000));

            // Output average performance
            if(kOutputPerformance)
                using (var db = new DataContext())
                {
                    ConsoleSection("Average BSLogic Performance: ");
                    db.testResults.Include(x => x.tradingStat).Include(x => x.test).ThenInclude(x => x.bSLogicInfo)
                        .GroupBy(x => new {TypeName = x.test.bSLogicInfo.TypeName, MethodName = x.test.bSLogicInfo.MethodName}, x => x.tradingStat.mean, (typemethod, means) => new {TypeMethod = typemethod, Average = means.Average()})
                        .OrderBy(x => x.Average)
                        .ToList()
                        .ForEach(x => Console.WriteLine($"({x.Average:F4}) {x.TypeMethod.TypeName}_{x.TypeMethod.MethodName}"));

                    ConsoleSection("Average TradeLogic Performance: ");
                    db.testResults.Include(x => x.tradingStat).Include(x => x.test).ThenInclude(x => x.tradeLogicInfo)
                        .GroupBy(x => x.test.tradeLogicInfo.TypeName, x => x.tradingStat.mean, (name, means) => new {Name = name, Average = means.Average()})
                        .OrderBy(x => x.Average)
                        .ToList()
                        .ForEach(x => Console.WriteLine($"({x.Average:F4}) {x.Name}"));
                }

            // Output current best   
            var bestResult = double.NegativeInfinity;
         
            using (var db = new DataContext())
               if(db.testResults.Any())
                    db.testResults.Include(x => x.tradingStat).Include(x => x.test).ThenInclude(x => x.bSLogicInfo).Include(x => x.test.tradeLogicInfo).Where(x => x.tradingStat.mean > 1).OrderByDescending(y => y.tradingStat.mean).Take(1).ToList().ForEach(x => 
                    {
                        bestResult = x.tradingStat.mean; 
                        Console.WriteLine($"Current best (trading mean: {x.tradingStat.mean:F2}, monthly: {(Math.Pow(x.allTimeTrading, 1.0/(cDaily.Count/30))-1.0)*100:F2}%) {x.test.bSLogicInfo.FullName}_{x.test.tradeLogicInfo.FullName}_{x.margin}");
                    }); 

            bool wasNew = false;

            // Process tests
            Parallel.ForEach(new TradeTestQueue(), new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount  }, x =>
            {
                if(double.Parse("0,0005")!=0.0005 || $"{0.0005}" != "0,0005")
                    return;

                using (var db = new DataContext())
                {
                    var test = db.tradeTests.Include(y => y.bSLogicInfo).Include(y => y.tradeLogicInfo).First(y => y.bsLogicInfoId == x.Item1 && y.tradeLogicInfoId == x.Item2);

                    if(Program.cancelled)
                    {
                        test.State = 0;
                        db.SaveChanges();
                        return;
                    }

                    try
                    {
                        var tradeLogic = CreateTradeLogic(test.tradeLogicInfo);
                        
                        if(!decisionsLibrary.ContainsKey(test.bSLogicInfo.Id))
                            decisionsLibrary.TryAdd(test.bSLogicInfo.Id, BSLogic.LoadFromCache($"Data/BSLogicCache/{test.bSLogicInfo.FullName}.dat"));                        

                        var bsDecisions = decisionsLibrary[test.bSLogicInfo.Id];

                        foreach(var margin in margins)
                        {                
                            var returns = new List<(ulong, (double, uint))>();

                            for(int i=24*301; i<cHourly.Count-1; i++)
                            {
                                var now = cHourly[i];
                                var futureHourly = new ShalowList<Candlestick>(cHourly, i, cHourly.Count-i);
                                var pastHourly = new ShalowList<Candlestick>(cHourly, 0, i);
                                
                                if(now.OpenPrice != futureHourly.First().OpenPrice)
                                    throw new Exception();

                                var buyOrders = tradeLogic.GetBuyOrders(pastHourly, now.OpenPrice, bsDecisions[now.Timestamp]);
                                var sellOrders = tradeLogic.GetSellOrders(pastHourly, now.OpenPrice, bsDecisions[now.Timestamp]);

                                returns.Add((now.Timestamp, TradeLogic.Simulate(buyOrders, sellOrders, margin, futureHourly, bsDecisions)));
                            };

                            var simCont = tradeLogic.tradingType == TradeLogic.TradingType.Continuous ? TradeLogic.SimulateContinious(returns) : TradeLogic.SimulateHalting(returns, bsDecisions);

                            test.results.Add(new TestResults()
                            {
                                margin = margin,
                                test = test,
                                meanDailyReturn = returns.Select(x => TradeLogic.CalculateAverageDailyReturn(x.Item2.Item1, 24.0 / x.Item2.Item2)).Average(),
                                meanOrderReturn = returns.Select(x => x.Item2.Item1).Average(),
                                allTimeTrading = simCont.First(),
                                tradingStat = new DescriptiveStatistics(simCont.Select(x => x).ToList())
                            }); 

                            var result = test.results.Last();

                            if(result.tradingStat.mean > bestResult)
                            {    
                                Console.WriteLine($"->New Best (trading mean: {result.tradingStat.mean:F2}, compounding monthly: {(Math.Pow(result.allTimeTrading, 1.0/(cDaily.Count/30))-1.0)*100}%) {test.bSLogicInfo.FullName}_{test.tradeLogicInfo.FullName}_{margin}");
                                bestResult = result.tradingStat.mean;
                                wasNew = true;
                            }

                            if(result.tradingStat.mean <= 1.0)
                                break;                           
                        }
                    }
                    catch(Exception)
                    {
                        using (var dbe = new DataContext())
                        {
                            dbe.tradeTests.First(y => y.bsLogicInfoId == x.Item1 && y.tradeLogicInfoId == x.Item2)
                                .State = 0;
                            dbe.SaveChanges();
                        }

                        Console.WriteLine($"Were was a error while performing test {x.Item1} {x.Item2}");

                        return;
                    }

                    test.State = 2;
                    db.SaveChanges();
                }
                                                
                Console.WriteLine($"Test Complete {x.Item1} {x.Item2}" + (wasNew ? " ( New best was found" : ""));
            });

            if(bsPerfUpdateTask != null)
                await bsPerfUpdateTask; 
    }

    //*****************************************************************************************

    public static double[] TransformTaLibArray(double[] iArray, int iBegInd, int iNbelements)
    {
        var results = new double[iArray.Length];

        for (int i = 0; i < iNbelements; i++)
            results[iBegInd+i] = iArray[i];

        return results;
    }

    //*****************************************************************************************
        
        public static void ConsoleSection(string iName)
        {
            Console.WriteLine("");
            Console.WriteLine("------------------------------------------------------------");    
            Console.WriteLine(iName);
            Console.WriteLine("------------------------------------------------------------");
        }

//***************************************************************************************** 

    static double SignificanceWeightedAverage(double iAverage, int iCount, int iSignificanceLevel, double iDefault)
        =>  Utils.Lerp(iDefault, iAverage, Math.Min((double)iCount / iSignificanceLevel, 1.0));

//***************************************************************************************** 

    private static IEnumerable<BSLogicInfo> CreateBSLogicList()
    {
        // {BSLogic_Inline}
            // BuyIfLastChangePositive
            yield return new BSLogicInfo("BSLogic_Inline", "BuyIfLastChangePositive", "Hour");
            yield return new BSLogicInfo("BSLogic_Inline", "BuyIfLastChangePositive", "Day");

            // PureRandom
            yield return new BSLogicInfo("BSLogic_Inline", "PureRandom", "PureRanom");

            // MostProbableDecision_H 
            foreach (var x in new int[]{3, 6, 12, 24})
                yield return new BSLogicInfo("BSLogic_Inline", "MostProbableDecision_H", $"{x}");

            // MostProbableDecision_D     
            foreach (var x in new int[]{3, 7, 14, 21, 30, 60, 90})
                yield return new BSLogicInfo("BSLogic_Inline", "MostProbableDecision_D", $"{x}");

            // BuyIfAboveMovingAverage_H
            foreach (var x in new int[]{6, 12, 24, 48})
                yield return new BSLogicInfo("BSLogic_Inline", "BuyIfAboveMovingAverage_H", $"{x}");
             
            // BuyIfAboveMovingAverage_D
            foreach (var x in new int[]{7, 14, 21, 30, 60, 90, 180, 200})
                yield return new BSLogicInfo("BSLogic_Inline", "BuyIfAboveMovingAverage_D", $"{x}");           
     
            // BuyIfAboveEMA_H
            foreach (var x in new int[]{6, 12, 24, 48})
                yield return new BSLogicInfo("BSLogic_Inline", "BuyIfAboveEMA_H", $"{x}"); 

            // BuyIfAboveEMA_D
            foreach (var x in new int[]{7, 14, 21, 30, 60, 90, 180, 200})
                yield return new BSLogicInfo("BSLogic_Inline", "BuyIfAboveEMA_D", $"{x}"); 

            // BuyIfRSI_H
            yield return new BSLogicInfo("BSLogic_Inline", "BuyIfRSI", "H");
 
            // BuyIfRSI_D
            yield return new BSLogicInfo("BSLogic_Inline", "BuyIfRSI", "D");    

            // BuyIfMovingAverageAboveOther_H
            foreach (var x in new List<(int, int)>{(50, 200), (25, 100)})
                yield return new BSLogicInfo("BSLogic_Inline", "BuyIfMovingAverageAboveOther_H", $"{x.Item1}_{x.Item2}");

            // BuyIfMovingAverageAboveOther_D
            foreach (var x in new List<(int, int)>{(50, 200), (25, 100)})
                yield return new BSLogicInfo("BSLogic_Inline", "BuyIfMovingAverageAboveOther_D", $"{x.Item1}_{x.Item2}");

            // PriceChangeVelocityIncreasing_H
            foreach (var x in Enumerable.Range(2, 3))
                yield return new BSLogicInfo("BSLogic_Inline", "PriceChangeVelocityIncreasing_H", $"{x}"); 

            foreach (var x in new List<(int, int)>{(3, 6), (4, 8), (6, 12), (7, 14), (8, 16), (9, 18), (12, 24), (3, 12), (6, 24), (6, 48), (12, 48), (50, 200)})
                yield return new BSLogicInfo("BSLogic_Inline", "PriceChangeVelocityIncreasing_H", $"{x.Item1}_{x.Item2}");

            // PriceChangeVelocityIncreasing_D
            foreach (var x in Enumerable.Range(2, 3))
                yield return new BSLogicInfo("BSLogic_Inline", "PriceChangeVelocityIncreasing_D", $"{x}");

            foreach (var x in new List<(int, int)>{(6, 12), (50, 200)})
                yield return new BSLogicInfo("BSLogic_Inline", "PriceChangeVelocityIncreasing_D", $"{x.Item1}_{x.Item2}");

            // PriceChangeVelocityPositive_H
            foreach (var x in Enumerable.Range(2, 4))
                yield return new BSLogicInfo("BSLogic_Inline", "PriceChangeVelocityPositive_H", $"{x}");

            // PriceChangeVelocityPositive_D
            foreach (var x in Enumerable.Range(2, 4))  
                yield return new BSLogicInfo("BSLogic_Inline", "PriceChangeVelocityPositive_D", $"{x}");
                
            // MedianPriceChangeVelocityIncreasing_H
            for(int i=2; i<=23; i++)
                for(int k=i+1; k<=24; k++)
                    yield return new BSLogicInfo("BSLogic_Inline", "MedianPriceChangeVelocityIncreasing_H", $"{i}_{k}");

            // MedianPriceChangeVelocityIncreasing_Volatile_H
            yield return new BSLogicInfo("BSLogic_Inline", "MedianPriceChangeVelocityIncreasing_Volatile_H", $"6_12_4_12_0,001");

            foreach(var x in new List<(int, int)>{(6, 48), (12, 48), (50, 200)})
                yield return new BSLogicInfo("BSLogic_Inline", "MedianPriceChangeVelocityIncreasing_H", $"{x.Item1}_{x.Item2}");

            // OnePeriodPriceChangeVelocityHigher_H
            foreach (var x in new List<int>{2, 3, 4, 5, 8, 12, 24})
                yield return new BSLogicInfo("BSLogic_Inline", "OnePeriodPriceChangeVelocityHigher_H", $"{x}");

        // {BSLogic_AutoML}
            // TA_D
            yield return new BSLogicInfo("BSLogic_AutoML", "TA_D", "RSI_MACD_BollingerBands_SMA_Stochastic_ADI_EMA");

            // TA_H
            yield return new BSLogicInfo("BSLogic_AutoML", "TA_H", "RSI_MACD_BollingerBands_SMA_Stochastic_ADI_EMA");

            // PriceChange_H
            foreach (var x in Enumerable.Range(2, 4))
                yield return new BSLogicInfo("BSLogic_AutoML", "PriceChange_H", $"{x}"); 

            // PriceChange_D
            foreach (var x in Enumerable.Range(2, 4))
                yield return new BSLogicInfo("BSLogic_AutoML", "PriceChange_D", $"{x}");

            // PriceChange_24H
            foreach (var x in Enumerable.Range(2, 4))
                yield return new BSLogicInfo("BSLogic_AutoML", "PriceChange_24H", $"{x}");

            // PriceChange_H_Ref
            foreach (var x in Enumerable.Range(2, 4))
                yield return new BSLogicInfo("BSLogic_AutoML", "PriceChange_H_Ref", $"{x}");
  
            // PriceChangeWithAboveBelowMovingAverage_D
            yield return new BSLogicInfo("BSLogic_AutoML", "PriceChangeWithAboveBelowMovingAverage_D", "4_200");

            // PriceChangeVelocity_H
            foreach (var x in Enumerable.Range(2, 4))
                yield return new BSLogicInfo("BSLogic_AutoML", "PriceChangeVelocity_H", $"{x}");

            yield return new BSLogicInfo("BSLogic_AutoML", "PriceChangeVelocity_H", "2_6_12");
            yield return new BSLogicInfo("BSLogic_AutoML", "PriceChangeVelocity_H", "2_6_12_24");

            // PriceChangeVelocity_D
            foreach (var x in Enumerable.Range(2, 4))
               yield return new BSLogicInfo("BSLogic_AutoML", "PriceChangeVelocity_D", $"{x}");
   
            yield return new BSLogicInfo("BSLogic_AutoML", "PriceChangeVelocity_D", "2_7_14");
            yield return new BSLogicInfo("BSLogic_AutoML", "PriceChangeVelocity_D", "2_7_14_28");

            // PriceChangeAcceleration_H
            foreach (var x in Enumerable.Range(2, 4))
               yield return new BSLogicInfo("BSLogic_AutoML", "PriceChangeAcceleration_H", $"{x}");

            // PriceChangeAcceleration_D
            foreach (var x in Enumerable.Range(2, 4))
               yield return new BSLogicInfo("BSLogic_AutoML", "PriceChangeAcceleration_D", $"{x}");
                   
        // {BSLogic_OLS}
            // FA
            yield return new BSLogicInfo("BSLogic_OLS", "FA", "InterestDifference_USAGDP_EUGDP_USACPI_EUCPI_BVPChangeDifference_CPIChangeDifference");
            
            // TA_D
            yield return new BSLogicInfo("BSLogic_OLS", "TA_D", "RSI_MACD_BollingerBands_SMA_Stochastic_ADI_EMA");
         
         yield break;
    }

//*****************************************************************************************

    private static IEnumerable<TradeLogicInfo> CreateTradeLogicList()
    {
        var distances = new double[]{0, 0.0005, 0.001, 0.002, 0.0025, 0.005, 0.010};

        // TradeLogic_1
        foreach(var ooDistance in new double[]{0, 0.0005, 0.001})
            foreach(var tpDistance in distances)
                foreach(var slDistance in distances)
                    foreach(var halting in new int[]{0, 1})
                        yield return new TradeLogicInfo("TradeLogic_1", $"OD{ooDistance}_TP{tpDistance}_SL{slDistance}_Halting{halting}");  
                    
        // TradeLogic_2
        foreach(var timeframe in new int[]{48, 168, 720, 2160, 4320})
            foreach(var ooStdDev in new double[]{0.0, 0.5, 1.0, 2.0, 3.0})
                foreach(var tpStdDev in new double[]{0.0, 0.5, 1.0, 2.0, 3.0})
                    foreach(var slStdDev in new double[]{0.0, 0.5, 1.0, 2.0, 3.0})
                        foreach(var halting in new int[]{0, 1})
                            if(ooStdDev + tpStdDev + slStdDev != 0.0)    
                                yield return new TradeLogicInfo("TradeLogic_2", $"LT{timeframe}_OOS{ooStdDev}_TPS{tpStdDev}_SLS{slStdDev}_Halting{halting}");

        // TradeLogic_3
        foreach(var ofDistance in new double[]{0, 0.0005, 0.001})
            foreach(var ooDistance in distances)
                foreach(var numberOfOrders in new int[]{2, 3, 4, 5})
                    foreach(var tpDistance in distances.Skip(1))
                        foreach(var slDistance in distances.Skip(1))
                            foreach(var halting in new int[]{0, 1})
                                yield return new TradeLogicInfo("TradeLogic_3", $"OFD{ofDistance}_OOD{ooDistance}_N{numberOfOrders}_TP{tpDistance}_SL{slDistance}_Halting{halting}");

        // TradeLogic_4
        foreach(var numberOfOrders in new int[]{2, 3, 4, 5})
            foreach(var timeframe in new int[]{48, 168, 360, 720, 1440, 2160, 4320})
                foreach(var ofStdDev in new double[]{0, 0.25})
                    foreach(var ooStdDev in new double[]{0.0, 0.5, 1.0, 2.0, 3.0})
                        foreach(var tpStdDev in new double[]{0.0, 0.5, 1.0, 2.0, 3.0})
                            foreach(var slStdDev in new double[]{0.0, 0.5, 1.0, 2.0, 3.0})
                                foreach(var halting in new int[]{0, 1})
                                    if(ooStdDev + tpStdDev + slStdDev != 0.0)
                                        yield return new TradeLogicInfo("TradeLogic_4", $"N{numberOfOrders}_LT{timeframe}_OF{ofStdDev}_OO{ooStdDev}_TP{tpStdDev}_SL{slStdDev}_Halting{halting}");

        // TradeLogic_5
        foreach(var numberOfOrders in new int[]{2, 3, 4, 5})
            foreach(var timeframe in new int[]{48, 168, 360, 720, 1440, 2160, 4320})
                foreach(var ofStdDev in new double[]{0, 0.25})
                    foreach(var ooStdDev in new double[]{0.0, 0.5, 1.0, 1.5, 2.0, 3.0})
                        foreach(var tpStdDev in new double[]{0.0, 0.5, 1.0, 2.0, 3.0})
                            foreach(var slStdDev in new double[]{0.0, 0.5, 1.0, 2.0, 3.0})
                                foreach(var halting in new int[]{0, 1})
                                    if(ooStdDev + tpStdDev + slStdDev != 0.0)
                                        yield return new TradeLogicInfo("TradeLogic_5",  $"N{numberOfOrders}_LT{timeframe}_OF{ofStdDev}_OO{ooStdDev}_TP{tpStdDev}_SL{slStdDev}_Halting{halting}");

        // TradeLogic_6
        foreach(var ofDistance in new double[]{0, 0.0005, 0.001})
            foreach(var ooDistance in distances)
                foreach(var numberOfOrders in new int[]{2, 3, 4, 5})
                    foreach(var tpDistance in distances.Skip(1))
                        foreach(var slDistance in distances.Skip(1))
                            foreach(var halting in new int[]{0, 1})
                                yield return new TradeLogicInfo("TradeLogic_6", $"OFD{ofDistance}_OOD{ooDistance}_N{numberOfOrders}_TP{tpDistance}_SL{slDistance}_Halting{halting}");
                        
        // TradeLogic_7
        foreach(var numberOfOrders in new int[]{3, 4, 5})
            foreach(var timeframe in new int[]{360, 720, 1440})
                foreach(var ofStdDev in new double[]{0, 0.25})
                    foreach(var ooStdDev in new double[]{0.5, 1.0, 1.5})
                        foreach(var tpStdDev in new double[]{0.0, 0.5})
                            foreach(var slStdDev in new double[]{0.0, 0.5})
                                foreach(var halting in new int[]{0, 1})
                                    if(ooStdDev + tpStdDev + slStdDev != 0.0)
                                        yield return new TradeLogicInfo("TradeLogic_7",  $"N{numberOfOrders}_LT{timeframe}_OF{ofStdDev}_OO{ooStdDev}_TP{tpStdDev}_SL{slStdDev}_Halting{halting}");

        // TradeLogic_8
        foreach(var numberOfOrders in new int[]{3, 4, 5, 6, 7})
            foreach(var timeframe in new int[]{168, 240, 360, 504, 720, 1440})
                foreach(var ofStdDev in new double[]{0, 0.25})
                    foreach(var ooStdDev in new double[]{0.25, 0.5, 1.0, 1.5})
                        foreach(var tpStdDev in new double[]{0.0, 0.5})
                            foreach(var slStdDev in new double[]{0.0, 0.5})
                                foreach(var halting in new int[]{0, 1})
                                    foreach(var subsequentOrderSize in new double[]{0.75, 0.5, 0.30, 0.25, 0.20, 0.15, 0.10, 0.05, 0.025, 0.0} )
                                        foreach(var quantityBlendStartStd in new double[]{0.001, 0.002})
                                            foreach(var quantityBlendEndStd in new double[]{0.002, 0.003})
                                                foreach(var quantityBlendFinalWeight in new double[]{1.0, 0.75})
                                                    if(quantityBlendEndStd > quantityBlendStartStd)
                                                        yield return new TradeLogicInfo("TradeLogic_8",  $"N{numberOfOrders}_LT{timeframe}_OF{ofStdDev}_OO{ooStdDev}_TP{tpStdDev}_SL{slStdDev}_Halting{halting}_SOS{subsequentOrderSize}_QBSS{quantityBlendStartStd}_QBES{quantityBlendEndStd}_QBFW{quantityBlendFinalWeight}");
        
        yield break;    
    }

    //***************************************************************************************** 

    public static TradeLogic CreateTradeLogic(TradeLogicInfo iInfo) =>    
        iInfo.TypeName switch
        {
            "TradeLogic_1" => new TradeLogic_1(iInfo.VariationName),
            "TradeLogic_2" => new TradeLogic_2(iInfo.VariationName),
            "TradeLogic_3" => new TradeLogic_3(iInfo.VariationName),
            "TradeLogic_4" => new TradeLogic_4(iInfo.VariationName),
            "TradeLogic_5" => new TradeLogic_5(iInfo.VariationName),
            "TradeLogic_6" => new TradeLogic_6(iInfo.VariationName),
            "TradeLogic_7" => new TradeLogic_7(iInfo.VariationName),
            "TradeLogic_8" => new TradeLogic_8(iInfo.VariationName),
           _ => throw new ArgumentOutOfRangeException(nameof(iInfo.TypeName), $"Undefined TradeLogic type name: {iInfo.TypeName}"),
        };    

    //***************************************************************************************** 

    public static BSLogic CreateBSLogic(BSLogicInfo iInfo, MLContext iMLContext)
    {  
        Random rnd = new Random(0);

        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfLastChangePositive" && iInfo.VariationName == "Hour")
            return new BSLogic_Inline(iInfo, (d, h) => h[h.Count-1].ClosePrice >= h[h.Count-2].ClosePrice ? Decision.Buy : Decision.Sell);

        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "TA_D" && iInfo.VariationName == "RSI_MACD_BollingerBands_SMA_Stochastic_ADI_EMA")
            return new BSLogic_AutoML(iInfo, iMLContext, BSLogic_AutoML.GetTechnicalAnalysisModelData, 7);

        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "TA_H" && iInfo.VariationName == "RSI_MACD_BollingerBands_SMA_Stochastic_ADI_EMA")            
            return new BSLogic_AutoML(iInfo, iMLContext, BSLogic_AutoML.GetTechnicalAnalysisModelData_H, 7);
            
        if(iInfo.TypeName == "BSLogic_OLS" && iInfo.MethodName == "TA_D" && iInfo.VariationName == "RSI_MACD_BollingerBands_SMA_Stochastic_ADI_EMA")
            return new BSLogic_OLS(iInfo, BSLogic_OLS.GetTechnicalAnalysisModelData);

        if(iInfo.TypeName == "BSLogic_OLS" && iInfo.MethodName == "FA" && iInfo.VariationName == "InterestDifference_USAGDP_EUGDP_USACPI_EUCPI_BVPChangeDifference_CPIChangeDifference")
            return new BSLogic_OLS(iInfo, BSLogic_OLS.GetFundamentalAnalysisModelData);

        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfLastChangePositive" && iInfo.VariationName == "Day")
            return new BSLogic_Inline(iInfo, (d, h) => d[d.Count-1].ClosePrice >= d[d.Count-2].ClosePrice ? Decision.Buy : Decision.Sell);

        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "PureRandom" && iInfo.VariationName == "PureRanom")
            return new BSLogic_Inline(iInfo, (d, h) => rnd.NextDouble() > 0.5 ? Decision.Buy : Decision.Sell);

        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "PriceChange_H")
        {
            var p = int.Parse(iInfo.VariationName);
            return new BSLogic_AutoML(iInfo, iMLContext, (d, h, coc, sampling) => BSLogic_AutoML.GetPriceChangeModelData_H(d, h, coc, sampling, p), p);
        }

        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "PriceChange_D")
        {
             var p = int.Parse(iInfo.VariationName);
             return new BSLogic_AutoML(iInfo, iMLContext, (d, h, coc, sampling) => BSLogic_AutoML.GetPriceChangeModelData_D(d, h, coc, sampling, p), p);
        }

        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "PriceChange_24H")
        {
            var p = int.Parse(iInfo.VariationName);
            return new BSLogic_AutoML(iInfo, iMLContext, (d, h, coc, sampling) => BSLogic_AutoML.GetPriceChangeModelData_24H(d, h, coc, sampling, p), p);
        }

        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "PriceChange_H_Ref")
        {
            var p = int.Parse(iInfo.VariationName);
            return new BSLogic_AutoML(iInfo, iMLContext, (d, h, coc, sampling) => BSLogic_AutoML.GetPriceChangeModelData_H_Ref(d, h, coc, sampling, p), p);
        }

        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "MostProbableDecision_H")   
        {
            var p = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => rnd.NextDouble() <= BSLogic_Inline.CountPositiveRatio(h, p) ? Decision.Buy : Decision.Sell);
        } 

        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "MostProbableDecision_H")   
        {
            var p = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => rnd.NextDouble() <= BSLogic_Inline.CountPositiveRatio(h, p) ? Decision.Buy : Decision.Sell);
        } 

        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "MostProbableDecision_D")   
        {
            var p = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => rnd.NextDouble() <= BSLogic_Inline.CountPositiveRatio(d, p) ? Decision.Buy : Decision.Sell);
        }

        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfAboveMovingAverage_H")   
        {
            var period = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => h[h.Count-1].ClosePrice >= (new ShalowList<Candlestick>(h, h.Count-period-1, period+1)).Select(x => x.ClosePrice).MovingAverage(period).Last() ? Decision.Buy : Decision.Sell);
        }
   
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfAboveMovingAverage_D")   
        {
            var period = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => h[h.Count-1].ClosePrice >= (new ShalowList<Candlestick>(d, d.Count-period-1, period+1)).Select(x => x.ClosePrice).MovingAverage(period).Last() ? Decision.Buy : Decision.Sell);
        }
        
        // BuyIfAboveEMA_H
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfAboveEMA_H")   
        {
            var period = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => 
            {
                var ema = new double[period];
                TALib.Core.Ema((new ShalowList<Candlestick>(h, h.Count-period-1, period+1)).Select(x => x.ClosePrice).ToArray(), 0, period-1, ema, out var emaBegIdx, out var emaNbElement, 6);
                ema = PhilosopherStone.Program.TransformTaLibArray(ema, emaBegIdx, emaNbElement);
                return h[h.Count-1].ClosePrice >= ema.Last() ? Decision.Buy : Decision.Sell;
            });
        }

        // BuyIfAboveEMA_D
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfAboveEMA_D")   
        {
            var period = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => 
            {
                var ema = new double[period];
                TALib.Core.Ema((new ShalowList<Candlestick>(d, d.Count-period-1, period+1)).Select(x => x.ClosePrice).ToArray(), 0, period-1, ema, out var emaBegIdx, out var emaNbElement, 6);
                ema = PhilosopherStone.Program.TransformTaLibArray(ema, emaBegIdx, emaNbElement);
                return h[h.Count-1].ClosePrice >= ema.Last() ? Decision.Buy : Decision.Sell;
            });
        }

        // BuyIfRSI_H
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfRSI" && iInfo.VariationName == "H")  
            return new BSLogic_Inline(iInfo, (d, h) => 
            {
                var rsi = new double[15];
                TALib.Core.Rsi((new ShalowList<Candlestick>(h, h.Count-15-1, 15+1)).Select(x => x.ClosePrice).ToArray(), 0, 14, rsi, out var rsiBegIdx, out var rsiNbElement);
                rsi = PhilosopherStone.Program.TransformTaLibArray(rsi, rsiBegIdx, rsiNbElement);
                return rsi.Last() >= 50 ? Decision.Buy : Decision.Sell;
            });

        // BuyIfRSI_D
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfRSI" && iInfo.VariationName == "D")  
            return new BSLogic_Inline(iInfo, (d, h) => 
        {
            var rsi = new double[15];
            TALib.Core.Rsi((new ShalowList<Candlestick>(d, d.Count-15-1, 15+1)).Select(x => x.ClosePrice).ToArray(), 0, 14, rsi, out var rsiBegIdx, out var rsiNbElement);
            rsi = PhilosopherStone.Program.TransformTaLibArray(rsi, rsiBegIdx, rsiNbElement);
            return rsi.Last() >= 50 ? Decision.Buy : Decision.Sell;
        });

        // BuyIfMovingAverageAboveOther_H
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfMovingAverageAboveOther_H")
        {
            var p = BSLogic_Inline.ParseVariation(iInfo.VariationName).Select(x => (int)x).ToList();
            return new BSLogic_Inline(iInfo, (d, h) => (new ShalowList<Candlestick>(h, h.Count-p[0]-1, p[0]+1)).Select(x => x.ClosePrice).MovingAverage(p[0]).Last() >= (new ShalowList<Candlestick>(h, h.Count-p[1]-1, p[1]+1)).Select(x => x.ClosePrice).MovingAverage(p[1]).Last() ? Decision.Buy : Decision.Sell);
        }

       // BuyIfMovingAverageAboveOther_D
       if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "BuyIfMovingAverageAboveOther_D")
       {
            var p = BSLogic_Inline.ParseVariation(iInfo.VariationName).Select(x => (int)x).ToList();
            return new BSLogic_Inline(iInfo, (d, h) => (new ShalowList<Candlestick>(d, d.Count-p[0]-1, p[0]+1)).Select(x => x.ClosePrice).MovingAverage(p[0]).Last() >= (new ShalowList<Candlestick>(d, d.Count-p[1]-1, p[1]+1)).Select(x => x.ClosePrice).MovingAverage(p[1]).Last() ? Decision.Buy : Decision.Sell);
       }

        // PriceChangeWithAboveBelowMovingAverage_D
        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "PriceChangeWithAboveBelowMovingAverage_D" && iInfo.VariationName == "4_200")
            return new BSLogic_AutoML(iInfo, iMLContext, BSLogic_AutoML.GetPriceChangeWithAboveBelowMovingAverageModelData_D_4_200, 5);

        // PriceChangeVelocity_H
        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "PriceChangeVelocity_H")
        if(iInfo.VariationName.Contains('_'))
            return iInfo.VariationName switch 
            {
                "2_6_12" => new BSLogic_AutoML(iInfo, iMLContext, BSLogic_AutoML.GetPriceChangeVelocityModelData_H_2_6_12, 3),
                "2_6_12_24" => new BSLogic_AutoML(iInfo, iMLContext, BSLogic_AutoML.GetPriceChangeVelocityModelData_H_2_6_12_24, 4),
                _ => throw new ArgumentOutOfRangeException(nameof(iInfo.VariationName), $"Undefined BSLogic variation: {iInfo.TypeName}, {iInfo.MethodName}, {iInfo.VariationName}")
            };
        else
        {
            var p = int.Parse(iInfo.VariationName);
            return new BSLogic_AutoML(iInfo, iMLContext, (d, h, coc, sampling) => BSLogic_AutoML.GetPriceChangeVelocityModelData_H(d, h, coc, sampling, Enumerable.Range(1, p).Reverse().ToList()), p);
        }

        // PriceChangeVelocity_D
        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "PriceChangeVelocity_D")
            if(iInfo.VariationName.Contains('_'))
                return iInfo.VariationName switch 
                {
                    "2_7_14" => new BSLogic_AutoML(iInfo, iMLContext, BSLogic_AutoML.GetPriceChangeVelocityModelData_D_2_7_14, 3),
                    "2_7_14_28" => new BSLogic_AutoML(iInfo, iMLContext, BSLogic_AutoML.GetPriceChangeVelocityModelData_D_2_7_14_28, 4),
                    _ => throw new ArgumentOutOfRangeException(nameof(iInfo.VariationName), $"Undefined BSLogic variation: {iInfo.TypeName}, {iInfo.MethodName}, {iInfo.VariationName}")
                };  
            else
            {
                var p = int.Parse(iInfo.VariationName);
                return new BSLogic_AutoML(iInfo, iMLContext, (d, h, coc, sampling) => BSLogic_AutoML.GetPriceChangeVelocityModelData_D(d, h, coc, sampling, Enumerable.Range(1, p).Reverse().ToList()), p);
            }

        // PriceChangeAcceleration_H
        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "PriceChangeAcceleration_H")
        {
            var p = int.Parse(iInfo.VariationName);
            return new BSLogic_AutoML(iInfo, iMLContext, (d, h, coc, sampling) => BSLogic_AutoML.GetPriceChangeAccelerationModelData_H(d, h, coc, sampling, p), p);
        }

        // PriceChangeAcceleration_D
        if(iInfo.TypeName == "BSLogic_AutoML" && iInfo.MethodName == "PriceChangeAcceleration_D")
        {
            var p = int.Parse(iInfo.VariationName);
            return new BSLogic_AutoML(iInfo, iMLContext, (d, h, coc, sampling) => BSLogic_AutoML.GetPriceChangeAccelerationModelData_D(d, h, coc, sampling, p), p);
        }

        // PriceChangeVelocityIncreasing_H
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "PriceChangeVelocityIncreasing_H")
        {
            if(iInfo.VariationName.Contains('_'))
            {
                var p = BSLogic_Inline.ParseVariation(iInfo.VariationName).Select(x => (int)x).ToList();
                return new BSLogic_Inline(iInfo, (d, h) => 
                {
                    var r = BSLogic_Inline.GetPriceChangeVelocity(h, new List<int>(){p[0], p[1]});
                    return r[0] >= r[1] ? Decision.Buy : Decision.Sell;
                });
            }
            else
            {
                var period = int.Parse(iInfo.VariationName);
                return new BSLogic_Inline(iInfo, (d, h) => 
                {
                    var r = BSLogic_Inline.GetPriceChangeVelocity(h, new List<int>(){1, period});
                    return r[0] >= r[1] ? Decision.Buy : Decision.Sell;
                });
            }
        }

        // PriceChangeVelocityIncreasing_D
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "PriceChangeVelocityIncreasing_D")
        {
            if(iInfo.VariationName.Contains('_'))
            {
                var p = BSLogic_Inline.ParseVariation(iInfo.VariationName).Select(x => (int)x).ToList();
                return new BSLogic_Inline(iInfo, (d, h) => 
                {
                    var r = BSLogic_Inline.GetPriceChangeVelocity(d, new List<int>(){p[0], p[1]});
                    return r[0] >= r[1] ? Decision.Buy : Decision.Sell;
                });
            }
            else
            {
                var period = int.Parse(iInfo.VariationName);
                return new BSLogic_Inline(iInfo, (d, h) => 
                {
                    var r = BSLogic_Inline.GetPriceChangeVelocity(d, new List<int>(){1, period});
                    return r[0] >= r[1] ? Decision.Buy : Decision.Sell;
                });
            }
        }

        // PriceChangeVelocityPositive_H
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "PriceChangeVelocityPositive_H")
        {
            var period = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => 
                BSLogic_Inline.GetPriceChangeVelocity(h, Enumerable.Range(1, period).ToList()).Average() >= 0 ? Decision.Buy : Decision.Sell);
        }

        // PriceChangeVelocityPositive_D
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "PriceChangeVelocityPositive_D")
        {
            var period = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => 
                BSLogic_Inline.GetPriceChangeVelocity(d, Enumerable.Range(1, period).ToList()).Average() >= 0 ? Decision.Buy : Decision.Sell);
        }

        // MedianPriceChangeVelocityIncreasing_H
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "MedianPriceChangeVelocityIncreasing_H")
        {
            var p = BSLogic_Inline.ParseVariation(iInfo.VariationName).Select(x => (int)x).ToList();
            return new BSLogic_Inline(iInfo, (d, h) => 
                {
                    var r = BSLogic_Inline.GetMedianPriceChangeVelocity(h, new List<int>(){p[0], p[1]});
                    return r[0] >= r[1] ? Decision.Buy : Decision.Sell;
                });
        }

        // MedianPriceChangeVelocityIncreasing_Volatile_H
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "MedianPriceChangeVelocityIncreasing_Volatile_H")
        {
            var p = BSLogic_Inline.ParseVariation(iInfo.VariationName).Select(x => (double)x).ToList();
            return new BSLogic_Inline(iInfo, (d, h) => 
                {
                    var std = HistoricStdDev.Get(h, 240);
                    List<int> selectedParams = null;

                    if(std <= p[4])
                        selectedParams = new List<int>(){(int)p[0], (int)p[1]};
                    else
                        selectedParams = new List<int>(){(int)p[2], (int)p[3]};

                    var r = BSLogic_Inline.GetMedianPriceChangeVelocity(h, new List<int>(){selectedParams[0], selectedParams[1]});
                    return r[0] >= r[1] ? Decision.Buy : Decision.Sell;
                });
        }
       
        // OnePeriodPriceChangeVelocityHigher_H
        if(iInfo.TypeName == "BSLogic_Inline" && iInfo.MethodName == "OnePeriodPriceChangeVelocityHigher_H")
        {
            var period = int.Parse(iInfo.VariationName);
            return new BSLogic_Inline(iInfo, (d, h) => 
        {
            var r0 = BSLogic_Inline.GetPriceChangeVelocity(h, new List<int>(){period});
            var r1 = BSLogic_Inline.GetPriceChangeVelocity(new ShalowList<Candlestick>(h, 0, h.Count-1), new List<int>(){period});
            return r0[0] >= r1[0] ? Decision.Buy : Decision.Sell;
        });
        }

        throw new ArgumentOutOfRangeException(nameof(iInfo.VariationName), $"Undefined BSLogic variation: {iInfo.TypeName}, {iInfo.MethodName}, {iInfo.VariationName}");
    }

    //***************************************************************************************** 

    static async Task<bool> UpdateTestsPerformance(int iInterval = 60000)
    {
        Console.WriteLine("Starting to update tests performance");
        
        while(true)
            try
            {
                using (var db = new DataContext())
                {     
                    if(Program.cancelled)
                        return false;              

                    var historicFiltered = new List<TestResults>(25);

                    foreach(var r in db.testResults.AsNoTracking().Include(x => x.tradingStat).Include(x => x.test).ThenInclude(x => x.tradeLogicInfo)
                                    .OrderByDescending(x => x.Id).Take(25))
                                    {
                                        var found = false;
                                        for(int i=0; i<historicFiltered.Count; i++)
                                            if(historicFiltered[i].testbsLogicInfoId == r.testbsLogicInfoId && historicFiltered[i].testTradeLogicInfoId == r.testTradeLogicInfoId)
                                            {
                                                if(r.tradingStat.mean > historicFiltered[i].tradingStat.mean)
                                                    historicFiltered[i] = r;
                                                found = true;
                                            }
                                        if(!found)
                                            historicFiltered.Add(r);
                                    }    
                
                    var historic = historicFiltered.GroupBy(x => new {x.testbsLogicInfoId, x.test.tradeLogicInfo.TypeName}, x => new { Predicted = x.test.PredictedPerformance, Real = x.tradingStat.mean }, (type, perf) => new {BSId = type.testbsLogicInfoId, TLTypeName = type.TypeName, TotalVariance = perf.Sum(x => Math.Pow(x.Predicted - x.Real, 2.0))})
                                    .OrderByDescending(x => x.TotalVariance)
                                    .First();

                    if(UpdateTestsPerformance(historic.BSId, historic.TLTypeName) == 0)
                        await Task.Delay(5000);
                    else
                        await Task.Delay(iInterval);
                } }
            catch(Exception)
            {
                Console.WriteLine("Were was an error while updating tests performance");
                await Task.Delay(5000);
            }   
    }

    //***************************************************************************************** 

    static int UpdateTestsPerformance(int iBSId, string iTradeLogicName, bool iOnlyFirstTimers = false)
    {
        var count = 0;

        using (var db = new DataContext())
        {
            IQueryable<TradeTest> list = db.tradeTests.Include(x => x.tradeLogicInfo)
                                    .Where(x => x.State == 0 && x.bsLogicInfoId == iBSId && x.tradeLogicInfo.TypeName == iTradeLogicName);

            if(iOnlyFirstTimers)
                list = list.Where(x => x.PredictedPerformance == 0);

            list = list.OrderBy(x => x.LastUpdated)
                        .Take(1000);

            var weights = TestResults.CreateRegressionModel(iBSId, iTradeLogicName); 
            
            if(weights is null || !list.Any())
                return 0;              

            foreach(var test in list)
            {                        
                var p = TradeLogic.ParseVariationNumerical(test.tradeLogicInfo.VariationName);
                test.PredictedPerformance = weights[0] + weights.Skip(1).Zip(p, (first, second) => first * second).Sum();
                test.LastUpdated = DateTime.Now;
                count++;
            }

            try
            {
                db.SaveChanges();
                Console.WriteLine($"Performance updated for {iBSId} {iTradeLogicName} (X{count})");
            }       
            catch(Exception exp) 
            {
                Console.WriteLine("Error while updating performance: " + exp.ToString());
            }   
        }

        return count;
    }

    //***************************************************************************************** 

    }

    
}
