using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using LumenWorks.Framework.IO.Csv;
using System.IO;
using System.Globalization;

/// <summary> 
/// OLS regression model is built from provided dataProcesor output.
/// Returns Buy decision if resulting regression value is higher than 0, and sell signal otherwise.
/// </summary>
public class BSLogic_OLS : BSLogic
{
    ulong modelRetrainTimestamp;
    double[] weights;
    double coc;

    Func<IReadOnlyList<Candlestick>, IReadOnlyList<Candlestick>, double, int, List<(ulong, double[], double)>> dataProcessor;

    //*****************************************************************************************

    public BSLogic_OLS(BSLogicInfo iInfo, Func<IReadOnlyList<Candlestick>, IReadOnlyList<Candlestick>, double, int, List<(ulong, double[], double)>> iDataProcessor)
     : base(iInfo) => dataProcessor = iDataProcessor;
    
    //*****************************************************************************************

    public override Decision GetDecision(IReadOnlyList<Candlestick> iPastDaily, IReadOnlyList<Candlestick> iPastHourly)
    {         
        if((Utils.UnixToDateTime(iPastHourly.Last().Timestamp) - Utils.UnixToDateTime(modelRetrainTimestamp)).Days >= 30)
        {
            coc = FinanceFunctions.CalculateCostOfCapital(iPastHourly.Select(x => x.ClosePrice).ToArray(), 0.035, 1.0);
            var trainData = dataProcessor(iPastDaily, iPastHourly, coc, 1).SkipLast(24);
            weights = Fit.MultiDim(trainData.Select(x => x.Item2).ToArray(), trainData.Select(x => x.Item3).ToArray(), intercept: true);           
            modelRetrainTimestamp = iPastHourly.Last().Timestamp;
        }

        var lData = dataProcessor(iPastDaily, iPastHourly, coc, int.MaxValue);
        return weights[0] + weights.Skip(1).Zip(lData.Last().Item2, (first, secod) => first*secod).Sum() > 0 ? Decision.Buy : Decision.Sell;
    }

//*****************************************************************************************

    public static List<(ulong, double[], double)> GetTechnicalAnalysisModelData(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency)
    {
        var closePrice = iDaily.Select(x=> x.ClosePrice).ToList();
        var highPrice = iDaily.Select(x=> x.HighPrice).ToList();
        var lowPrice = iDaily.Select(x=> x.LowPrice).ToList();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToArray();

        // RSI
        var rsi = new double[closePrice.Count];
        TALib.Core.Rsi(closePrice.ToArray(), 0, closePrice.Count-1, rsi, out int rsiBegIndex, out int rsiNbElement);
        rsi = PhilosopherStone.Program.TransformTaLibArray(rsi, rsiBegIndex, rsiNbElement);

        // MACD 
        var MACD = new double[closePrice.Count];
        var MACDSign = new double[closePrice.Count];
        var MACDHist = new double[closePrice.Count];
        TALib.Core.Macd(closePrice.ToArray(), 0, closePrice.Count-1, MACD, MACDSign, MACDHist, out var MACDBegInd, out var MACDNbelements);
        MACD = PhilosopherStone.Program.TransformTaLibArray(MACD, MACDBegInd, MACDNbelements);
        MACDSign = PhilosopherStone.Program.TransformTaLibArray(MACDSign, MACDBegInd, MACDNbelements);
        MACDHist = PhilosopherStone.Program.TransformTaLibArray(MACDHist, MACDBegInd, MACDNbelements);

        // Bollinger Bands
        var upperBBand = new double[closePrice.Count];
        var middleBBand = new double[closePrice.Count];
        var lowerBBand = new double[closePrice.Count];
        TALib.Core.Bbands(closePrice.ToArray(), 0, closePrice.Count-1, upperBBand, middleBBand, lowerBBand, out var BBandBegInd, out var BBandNbelement, TALib.Core.MAType.Sma, 20);
        upperBBand = PhilosopherStone.Program.TransformTaLibArray(upperBBand, BBandBegInd, BBandNbelement);
        middleBBand = PhilosopherStone.Program.TransformTaLibArray(middleBBand, BBandBegInd, BBandNbelement);
        lowerBBand = PhilosopherStone.Program.TransformTaLibArray(lowerBBand, BBandBegInd, BBandNbelement);

        //SMA
        var sma = new double[closePrice.Count];
        TALib.Core.Sma(closePrice.ToArray(), 0, closePrice.Count-1, sma, out var SMABegInd, out var SMANbElement, 200 );
        sma = PhilosopherStone.Program.TransformTaLibArray(sma, SMABegInd, SMANbElement);

        // Stochastic %K
        var stocSlowK = new double[closePrice.Count];
        var stocSlowD = new double[closePrice.Count];
        TALib.Core.Stoch(highPrice.ToArray(), lowPrice.ToArray(), closePrice.ToArray(), 0, closePrice.Count-1, stocSlowK, stocSlowD, out var stocBegIdx, out var stockNbElement, TALib.Core.MAType.Sma, TALib.Core.MAType.Sma, 14);
        stocSlowK = PhilosopherStone.Program.TransformTaLibArray(stocSlowK, stocBegIdx, stockNbElement);
        stocSlowD = PhilosopherStone.Program.TransformTaLibArray(stocSlowD, stocBegIdx, stockNbElement);

        // Average Directional Index
        var adx = new double[closePrice.Count];
        TALib.Core.Adx(highPrice.ToArray(), lowPrice.ToArray(), closePrice.ToArray(), 0, closePrice.Count-1, adx, out var adxBegIdx, out var adxNbElement, 14);
        adx = PhilosopherStone.Program.TransformTaLibArray(adx, adxBegIdx, adxNbElement);

        // Exponential Moving Average
        var ema = new double[closePrice.Count];
        TALib.Core.Ema(closePrice.ToArray(), 0, closePrice.Count-1, ema, out var emaBegIdx, out var emaNbElement, 50);
        ema = PhilosopherStone.Program.TransformTaLibArray(ema, emaBegIdx, emaNbElement);

        // Transform to normal;
        var beginIdx = new int[] {rsiBegIndex, MACDBegInd, BBandBegInd, SMABegInd, stocBegIdx, adxBegIdx, emaBegIdx};
        var nbElements = new int[] {rsiNbElement, MACDNbelements, BBandNbelement, SMANbElement, stockNbElement, adxNbElement, emaNbElement};
        
        var data = new List<(ulong, double[], double)>();
        var takeSampleAfter = 0;

        for(int i=0; i<iHourly.Count; i++)
        {
            var pastDaily = new ShalowList<Candlestick>(iDaily, 0, Candlestick.FindLastHistoricIndex(iDaily, iHourly[i].Timestamp)+1);
                
            var id = pastDaily.Count-1;

            if(id < beginIdx.Max())
                continue;

            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var lastClose = iHourly[i-1].ClosePrice;

            data.Add((
                iHourly[i].Timestamp, 
                new double[]
                {
                    rsi[id] - 50,
                    MACDHist[id],
                    lastClose < lowerBBand[id] ? -1 : lastClose > upperBBand[id] ? 1 : 0,
                    lastClose < sma[id] ? -1 : 1,
                    stocSlowK[id] - 50,
                    adx[id],
                    lastClose < ema[id] ? -1 : 1                        
                },
                FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24)));
        }

        return data;
    } 

//*****************************************************************************************

    static List<(ulong, double)> USACPI = null;
    static List<(ulong, double)> EUCPI = null;
    static List<(ulong, double)> USAInterest = null;  
    static List<(ulong, double)> EUInterest = null;  
    static List<(ulong, double)> USAGDP = null;  
    static List<(ulong, double)> EUGDP = null;  
    static List<(ulong, double)> BigMacSeminanuoly = null;

    public static List<(ulong, double[], double)> GetFundamentalAnalysisModelData(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency)
    {
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToArray();

        lock(hourlyClose) 
        {
            USACPI ??= LoadMonthly("RawFundamentals/CPI USA Monthly AUCSL.csv", 30+12);
            EUCPI ??= LoadMonthly("RawFundamentals/CPI EU Monthly CP0000EU272020M086NEST.csv", 30+16);
            USAInterest ??= LoadDaily("RawFundamentals/Effective Federal Funds Rate (DFF).csv", 24+12);
            EUInterest ??= LoadDaily("RawFundamentals/ECB Deposit Facility Rate for Euro Area (ECBDFR).csv", 24+12);
            USAGDP ??= LoadQuarterly("RawFundamentals/USA quartrely GDP.csv", 90+30);
            EUGDP ??= LoadQuarterly("RawFundamentals/GDP EU Quarterly without UK CPMNACSCAB1GQEU272020.csv", 90+19);
            BigMacSeminanuoly ??= LoadDaily("RawFundamentals/PPP.csv", 24*12);
        }

        var usaCPIChange = USACPI.Skip(1).Zip(USACPI, (curr, prev) => new { Date = curr.Item1, Change = curr.Item2 / prev.Item2 -1.0f});      
        var euCPIChange = EUCPI.Skip(1).Zip(EUCPI, (curr, prev) => new { Date = curr.Item1, Change = curr.Item2 / prev.Item2 -1.0f});

        var usaGDPChange = USAGDP.Skip(1).Zip(USAGDP, (curr, prev) => new { Date = curr.Item1, Change = curr.Item2 / prev.Item2 -1.0f});
        var euGDPChange = EUGDP.Skip(1).Zip(EUGDP, (curr, prev) => new { Date = curr.Item1, Change = curr.Item2 / prev.Item2 -1.0f});
       
        var data = new List<(ulong, double[], double)>();
        var takeSampleAfter = 0;

        for(int i=1; i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            data.Add((
                iHourly[i].Timestamp, 
                new double[]
                {
                    USAInterest.Last(x => x.Item1 <= iHourly[i].Timestamp).Item2 - EUInterest.Last(x => x.Item1 <= iHourly[i].Timestamp).Item2,
                    USAGDP.Last(x => x.Item1 <= iHourly[i].Timestamp).Item2,
                    EUGDP.Last(x => x.Item1 <= iHourly[i].Timestamp).Item2,
                    USACPI.Last(x => x.Item1 <= iHourly[i].Timestamp).Item2,
                    EUCPI.Last(x => x.Item1 <= iHourly[i].Timestamp).Item2,
                    usaGDPChange.Last(x => x.Date <= iHourly[i].Timestamp).Change - euGDPChange.Last(x => x.Date <= iHourly[i].Timestamp).Change,
                    usaCPIChange.Last(x => x.Date <= iHourly[i].Timestamp).Change - euCPIChange.Last(x => x.Date <= iHourly[i].Timestamp).Change
                },
                FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24)));
        }           

        return data;
    }

      //*****************************************************************************************

        public static List<(ulong, double)> LoadQuarterly(string iSource, int iDaysOffset)
        {
            var results = new List<(ulong, double)>();
            using var csv = new CachedCsvReader(new StreamReader(iSource), true);
            
            while (csv.ReadNextRecord())
                results.Add(new (Utils.DateTimeToUnix(DateTime.Parse(csv[0]).AddDays(iDaysOffset)), double.Parse(csv[1], CultureInfo.InvariantCulture.NumberFormat)));
            
            return results;
        } 

    //*****************************************************************************************

        public static List<(ulong, double)> LoadMonthly(string iSource, int iDaysOffset)
        {
            var results = new List<(ulong, double)>();
            using var csv = new CachedCsvReader(new StreamReader(iSource), true);
            
            while (csv.ReadNextRecord())
                results.Add(new (Utils.DateTimeToUnix(DateTime.Parse(csv[0]).AddDays(iDaysOffset)), double.Parse(csv[1], CultureInfo.InvariantCulture.NumberFormat)));
        
            return results;
        } 

    //*****************************************************************************************

        public static List<(ulong, double)> LoadDaily(string iSource, int iHoursOffset)
        {
            var results = new List<(ulong, double)>();
            using var csv = new CachedCsvReader(new StreamReader(iSource), true);
            
            while (csv.ReadNextRecord())
                results.Add(new (Utils.DateTimeToUnix(DateTime.Parse(csv[0]).AddHours(iHoursOffset)), double.Parse(csv[1], CultureInfo.InvariantCulture.NumberFormat)));
    
            return results;
        } 

        //*****************************************************************************************
}
