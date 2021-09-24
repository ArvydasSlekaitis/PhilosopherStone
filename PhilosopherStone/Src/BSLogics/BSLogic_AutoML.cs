using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;
using Microsoft.ML;

/// <summary> 
/// AutoML model is built from provided dataProcesror output.
/// </summary>
public class BSLogic_AutoML : BSLogic
{
    readonly MLContext mlContext;
    readonly Func<IReadOnlyList<Candlestick>, IReadOnlyList<Candlestick>, double, int, List<(ulong, double[], Decision)>> dataProcessor;
    readonly int featuresCount;

    ulong autoMLTimestamp;
    ulong modelRetrainTimestamp;
    IEstimator<ITransformer> estimator;
    ITransformer model;  
    double coc;
    PredictionEngine<ML.ModelInput, ML.ModelOutput> predictionEngine;
    
//*****************************************************************************************

    public BSLogic_AutoML(BSLogicInfo iInfo, MLContext iMLContext, Func<IReadOnlyList<Candlestick>, IReadOnlyList<Candlestick>, double, int, List<(ulong, double[], Decision)>> iDataProcessor, int iFeaturesCount) 
        : base(iInfo)
    {
        mlContext = iMLContext;
        dataProcessor = iDataProcessor;
        featuresCount = iFeaturesCount;
    }
    
//*****************************************************************************************

    public override Decision GetDecision(IReadOnlyList<Candlestick> iPastDaily, IReadOnlyList<Candlestick> iPastHourly)
    {
        bool rebuild = (Utils.UnixToDateTime(iPastHourly.Last().Timestamp) - Utils.UnixToDateTime(autoMLTimestamp)).Days >= 365;
        bool retrain = rebuild || (Utils.UnixToDateTime(iPastHourly.Last().Timestamp) - Utils.UnixToDateTime(modelRetrainTimestamp)).Days >= 30;

        // Every month retrain the model
        if(retrain)
        {   
            coc = FinanceFunctions.CalculateCostOfCapital(iPastHourly.Select(x => x.ClosePrice).ToArray(), 0.035, 1.0);
            var trainData = dataProcessor(iPastDaily, iPastHourly, coc, 1).SkipLast(24);

            // Every year find the best ML model
            if(rebuild)
            {
                estimator = ML.FindBestMLEstimator(mlContext, trainData.Select(x => x.Item2).ToList(), trainData.Select(x => x.Item3).ToList(), featuresCount, 30);
                autoMLTimestamp = iPastHourly.Last().Timestamp;
            }
                
            model = ML.CreateModel(mlContext, estimator, trainData.Select(x => x.Item2).ToList(), trainData.Select(x => x.Item3).ToList(), featuresCount);
            predictionEngine = mlContext.Model.CreatePredictionEngine<ML.ModelInput, ML.ModelOutput>(model, true, ML.ModelInput.GetSchema(featuresCount));
            modelRetrainTimestamp = iPastHourly.Last().Timestamp;
        }

        var lData = dataProcessor(iPastDaily, iPastHourly, coc, int.MaxValue);

        var pred =  predictionEngine.Predict(new ML.ModelInput() { Features = lData.Last().Item2.Select(x => (float)x).ToArray() });  
        return pred.PredictedLabel ? Decision.Buy : Decision.Sell;
    }

//*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetTechnicalAnalysisModelData(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency)
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
        //MACD = PhilosopherStone.Program.TransformTaLibArray(MACD, MACDBegInd, MACDNbelements);
        //MACDSign = PhilosopherStone.Program.TransformTaLibArray(MACDSign, MACDBegInd, MACDNbelements);
        MACDHist = PhilosopherStone.Program.TransformTaLibArray(MACDHist, MACDBegInd, MACDNbelements);

        // Bollinger Bands
        var upperBBand = new double[closePrice.Count];
        var middleBBand = new double[closePrice.Count];
        var lowerBBand = new double[closePrice.Count];
        TALib.Core.Bbands(closePrice.ToArray(), 0, closePrice.Count-1, upperBBand, middleBBand, lowerBBand, out var BBandBegInd, out var BBandNbelement, TALib.Core.MAType.Sma, 20);
        upperBBand = PhilosopherStone.Program.TransformTaLibArray(upperBBand, BBandBegInd, BBandNbelement);
        //middleBBand = PhilosopherStone.Program.TransformTaLibArray(middleBBand, BBandBegInd, BBandNbelement);
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
        //stocSlowD = PhilosopherStone.Program.TransformTaLibArray(stocSlowD, stocBegIdx, stockNbElement);

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
        
        var data = new List<(ulong, double[], Decision)>();
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
                FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        }

        return data;
    } 

//*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetTechnicalAnalysisModelData_H(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency)
    {
        var closePrice = iHourly.Select(x=> x.ClosePrice).ToList();
        var highPrice = iHourly.Select(x=> x.HighPrice).ToList();
        var lowPrice = iHourly.Select(x=> x.LowPrice).ToList();

        // RSI
        var rsi = new double[closePrice.Count];
        TALib.Core.Rsi(closePrice.ToArray(), 0, closePrice.Count-1, rsi, out int rsiBegIndex, out int rsiNbElement);
        rsi = PhilosopherStone.Program.TransformTaLibArray(rsi, rsiBegIndex, rsiNbElement);

        // MACD 
        var MACD = new double[closePrice.Count];
        var MACDSign = new double[closePrice.Count];
        var MACDHist = new double[closePrice.Count];
        TALib.Core.Macd(closePrice.ToArray(), 0, closePrice.Count-1, MACD, MACDSign, MACDHist, out var MACDBegInd, out var MACDNbelements);
        //MACD = PhilosopherStone.Program.TransformTaLibArray(MACD, MACDBegInd, MACDNbelements);
        //MACDSign = PhilosopherStone.Program.TransformTaLibArray(MACDSign, MACDBegInd, MACDNbelements);
        MACDHist = PhilosopherStone.Program.TransformTaLibArray(MACDHist, MACDBegInd, MACDNbelements);

        // Bollinger Bands
        var upperBBand = new double[closePrice.Count];
        var middleBBand = new double[closePrice.Count];
        var lowerBBand = new double[closePrice.Count];
        TALib.Core.Bbands(closePrice.ToArray(), 0, closePrice.Count-1, upperBBand, middleBBand, lowerBBand, out var BBandBegInd, out var BBandNbelement, TALib.Core.MAType.Sma, 20);
        upperBBand = PhilosopherStone.Program.TransformTaLibArray(upperBBand, BBandBegInd, BBandNbelement);
        //middleBBand = PhilosopherStone.Program.TransformTaLibArray(middleBBand, BBandBegInd, BBandNbelement);
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
        //stocSlowD = PhilosopherStone.Program.TransformTaLibArray(stocSlowD, stocBegIdx, stockNbElement);

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
        
        var data = new List<(ulong, double[], Decision)>();
        var takeSampleAfter = 0;

        for(int i=beginIdx.Max(); i<iHourly.Count; i++)
        {
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
                    rsi[i] - 50,
                    MACDHist[i],
                    lastClose < lowerBBand[i] ? -1 : lastClose > upperBBand[i] ? 1 : 0,
                    lastClose < sma[i] ? -1 : 1,
                    stocSlowK[i] - 50,
                    adx[i],
                    lastClose < ema[i] ? -1 : 1                        
                },
                FinanceFunctions.DiscountFutureProfits(closePrice, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        }

        return data;
    } 

//*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeModelData_H(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency, int iPeriods)
    {
        var data = new List<(ulong, double[], Decision)>();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToList();

        var takeSampleAfter = 0;

        for(int i=iPeriods+1; i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var d = new List<double>(iPeriods);

            for(int ii=i-iPeriods; ii<i; ii++)
                d.Add(Math.Log(iHourly[ii].ClosePrice / iHourly[ii-1].ClosePrice));
            
            data.Add((iHourly[i].Timestamp, d.ToArray(), FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        } 

        return data;
    }   

    //*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeModelData_D(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency, int iPeriods)
    {
        var data = new List<(ulong, double[], Decision)>();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToList();

        var takeSampleAfter = 0;

        for(int i=24*(1+iPeriods); i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var d = new List<double>(iPeriods);

            var pastDaily = new ShalowList<Candlestick>(iDaily, 0, Candlestick.FindLastHistoricIndex(iDaily, iHourly[i].Timestamp)+1);

            for(int ii=pastDaily.Count-iPeriods; ii<pastDaily.Count; ii++)
                d.Add(Math.Log(pastDaily[ii].ClosePrice / pastDaily[ii-1].ClosePrice));
            
            data.Add((iHourly[i].Timestamp, d.ToArray(), FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        } 

        return data;
    }      

//*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeModelData_24H(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency, int iPeriods)
    {
        var data = new List<(ulong, double[], Decision)>();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToList();

        var takeSampleAfter = 0;

        for(int i=24*(iPeriods+1); i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var d = new List<double>(iPeriods);

            for(int ii=i-((iPeriods-1)*24)-1; ii<i; ii+=24)
                d.Add(Math.Log(iHourly[ii].ClosePrice / iHourly[ii-23].ClosePrice));
            
            data.Add((iHourly[i].Timestamp, d.ToArray(), FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        } 

        return data;
    }

    //*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeModelData_H_Ref(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency, int iPeriods)
    {
        var data = new List<(ulong, double[], Decision)>();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToList();

        var takeSampleAfter = 0;

        for(int i=iPeriods+1; i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var d = new List<double>(iPeriods);

            for(int ii=i-iPeriods; ii<i; ii++)
                d.Add(Math.Log(iHourly[i-1].ClosePrice / iHourly[ii-1].ClosePrice));
            
            data.Add((iHourly[i].Timestamp, d.ToArray(), FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        } 

        return data;
    }

    //*****************************************************************************************

    static List<(ulong, double[], Decision)> GetPriceChangeWithAboveBelowMovingAverageModelData_D(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency, int iPriceChangePeriods, int iMovingAveragePeriods)
    {
        var data = new List<(ulong, double[], Decision)>();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToList();

        var takeSampleAfter = 0;

        for(int i=24*(1+Math.Max(iPriceChangePeriods, iMovingAveragePeriods)); i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var d = new List<double>(iPriceChangePeriods+1);

            var pastDaily = new ShalowList<Candlestick>(iDaily, 0, Candlestick.FindLastHistoricIndex(iDaily, iHourly[i].Timestamp)+1);

            for(int ii=pastDaily.Count-iPriceChangePeriods; ii<pastDaily.Count; ii++)
                d.Add(Math.Log(pastDaily[ii].ClosePrice / pastDaily[ii-1].ClosePrice));
                              
            d.Add(iHourly[i-1].ClosePrice >= pastDaily.TakeLast(iMovingAveragePeriods+1).Select(x => x.ClosePrice).MovingAverage(iMovingAveragePeriods).Last() ? 1.0 : -1.0);

            data.Add((iHourly[i].Timestamp, d.ToArray(), FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        } 

        return data;
    }

    public static List<(ulong, double[], Decision)> GetPriceChangeWithAboveBelowMovingAverageModelData_D_4_200(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency) 
        => GetPriceChangeWithAboveBelowMovingAverageModelData_D(iDaily, iHourly, iCOC, iSamplingFrequency, 4, 200); 

    //*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeVelocityModelData_H_2_6_12(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency) 
        => GetPriceChangeVelocityModelData_H(iDaily, iHourly, iCOC, iSamplingFrequency, new List<int>{2, 6, 12});

    public static List<(ulong, double[], Decision)> GetPriceChangeVelocityModelData_H_2_6_12_24(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency) 
        => GetPriceChangeVelocityModelData_H(iDaily, iHourly, iCOC, iSamplingFrequency, new List<int>{2, 6, 12, 24});
    
    //*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeVelocityModelData_D_2_7_14(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency) 
        => GetPriceChangeVelocityModelData_D(iDaily, iHourly, iCOC, iSamplingFrequency, new List<int>{2, 7, 14});

    public static List<(ulong, double[], Decision)> GetPriceChangeVelocityModelData_D_2_7_14_28(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency) 
        => GetPriceChangeVelocityModelData_D(iDaily, iHourly, iCOC, iSamplingFrequency, new List<int>{2, 7, 14, 28});

    //*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeAccelerationModelData_H(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency, int iPeriods)
    {
        var data = new List<(ulong, double[], Decision)>();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToList();

        var takeSampleAfter = 0;

        for(int i=iPeriods+1; i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var d = new List<double>(iPeriods);

            for(int ii=i-iPeriods-1; ii<i-1; ii++)
            {
                var change = Math.Log(iHourly[i-1].ClosePrice / iHourly[ii].ClosePrice);
                var time = (double)(i-1-ii);
                d.Add(change/Math.Pow(time, 2.0));
            }
            data.Add((iHourly[i].Timestamp, d.ToArray(), FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        } 

        return data;
    }

    //*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeAccelerationModelData_D(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency, int iPeriods)
    {
        var data = new List<(ulong, double[], Decision)>();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToList();

        var takeSampleAfter = 0;

        for(int i=24*(1+iPeriods); i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var d = new List<double>(iPeriods);

            var pastDaily = new ShalowList<Candlestick>(iDaily, 0, Candlestick.FindLastHistoricIndex(iDaily, iHourly[i].Timestamp)+1);

            for(int ii=pastDaily.Count-iPeriods-1; ii<pastDaily.Count-1; ii++)
            {
                var change = Math.Log(pastDaily[pastDaily.Count-1].ClosePrice / pastDaily[ii].ClosePrice);
                var time = (double)(pastDaily.Count-1-ii);
                d.Add(change/Math.Pow(time, 2));
            }

            data.Add((iHourly[i].Timestamp, d.ToArray(), FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        } 

        return data;
    }

    //*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeVelocityModelData_H(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency, List<int> iPeriods)
    {
        var data = new List<(ulong, double[], Decision)>();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToList();

        var takeSampleAfter = 0;

        for(int i=iPeriods.Max()+1; i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var d = new List<double>(iPeriods.Count);
            foreach (var p in iPeriods)
            {
                var change = Math.Log(iHourly[i-1].ClosePrice / iHourly[i-1-p].ClosePrice);
                var time = (double)(p);
                d.Add(change/time);
            }

            data.Add((iHourly[i].Timestamp, d.ToArray(), FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        } 

        return data;
    }

    //*****************************************************************************************

    public static List<(ulong, double[], Decision)> GetPriceChangeVelocityModelData_D(IReadOnlyList<Candlestick> iDaily, IReadOnlyList<Candlestick> iHourly, double iCOC, int iSamplingFrequency, List<int> iPeriods)
    {
        var data = new List<(ulong, double[], Decision)>();
        var hourlyClose = iHourly.Select(x => x.ClosePrice).ToList();

        var takeSampleAfter = 0;

        for(int i=24*(1+iPeriods.Max()); i<iHourly.Count; i++)
        {
            takeSampleAfter--;
            if(takeSampleAfter > 0 && i+1 < iHourly.Count)
                continue;
            else
                takeSampleAfter = iSamplingFrequency;

            var d = new List<double>(iPeriods.Count);

            var pastDaily = new ShalowList<Candlestick>(iDaily, 0, Candlestick.FindLastHistoricIndex(iDaily, iHourly[i].Timestamp)+1);

            foreach (var p in iPeriods)
            {
                var change = Math.Log(pastDaily[pastDaily.Count-1].ClosePrice / pastDaily[pastDaily.Count-1-p].ClosePrice);
                var time = (double)(p);
                d.Add(change/time);
            }

            data.Add((iHourly[i].Timestamp, d.ToArray(), FinanceFunctions.DiscountFutureProfits(hourlyClose, iCOC, i, 24) >= 0 ? Decision.Buy : Decision.Sell));
        } 

        return data;
    }
    
    //*****************************************************************************************
    

    
}