using System.Collections.Generic;
using System.Linq;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers;
using Microsoft.ML.AutoML;

public class ML
{
    public class ModelInput
    {
        public float[] Features;
        public bool Label; // True = buy, false = sell

        public static IEnumerable<ModelInput> FromArray(List<double[]> iFeatures, List<Decision> iLabels)
        {
            return iFeatures.Zip(iLabels, (p, l) => 
            new ModelInput
            {
                Features = p.Select(x => (float)x).ToArray(),
                Label = l == Decision.Buy
            }); 
        }

        public static IEnumerable<ModelInput> FromArray(List<double[]> iFeatures)
        {
            return iFeatures.Select(p => 
            new ModelInput
            {
                Features = p.Select(x => (float)x).ToArray()
            }); 
        }

        public static SchemaDefinition GetSchema(int iNumberOfFeatures)
        {
            var definedSchema = SchemaDefinition.Create(typeof(ModelInput));
            var vectorItemType = ((VectorDataViewType)definedSchema[0].ColumnType).ItemType;
            definedSchema[0].ColumnType = new VectorDataViewType(vectorItemType, iNumberOfFeatures);
            return definedSchema;   
        }
    }

    public class ModelOutput
    {
        public bool PredictedLabel; // True = buy, false = sell
    }

    //*****************************************************************************************

    public static ITransformer CreateModel(MLContext iContext, IEstimator<ITransformer> iPipeline, List<double[]> iFeatures, List<Decision> iLabels, int iNumberOfFeatures) 
	{
        var rawData = ModelInput.FromArray(iFeatures, iLabels);
        var schema = ModelInput.GetSchema(iNumberOfFeatures);

		var data = iContext.Data.LoadFromEnumerable(rawData, schema);

        // Pre-process data using data prep operations
       /* IEstimator<ITransformer> pipeline = iContext.Transforms.ReplaceMissingValues("Features", "Features")
                                                    .Append(iContext.Transforms.NormalizeMinMax("Features", "Features"))
                                                    .Append(iEstimator);*/

		ITransformer model = iPipeline.Fit(data);
        
        return model;
	}

    //*****************************************************************************************

    public static IEstimator<ITransformer> FindBestMLEstimator(MLContext iContext, List<double[]> iFeatures, List<Decision> iLabels, int iNumberOfFeatures, uint iMaxExperimentTimeInSeconds) 
	{
        var rawTrainData = ModelInput.FromArray(iFeatures.Take(iFeatures.Count/2).ToList(), iLabels);
        var rawTestData = ModelInput.FromArray(iFeatures.Skip(iFeatures.Count/2).ToList(), iLabels);

        var schema = ModelInput.GetSchema(iNumberOfFeatures);

		var trainData = iContext.Data.LoadFromEnumerable(rawTrainData, schema);
        var testData = iContext.Data.LoadFromEnumerable(rawTestData, schema);

        var experiment = iContext.Auto().CreateBinaryClassificationExperiment(new BinaryExperimentSettings() {MaxExperimentTimeInSeconds = iMaxExperimentTimeInSeconds} );

        ExperimentResult<BinaryClassificationMetrics> experimentResult = experiment
            .Execute(trainData, testData);

        return experimentResult.BestRun.Estimator;
/*

        // Pre-process data using data prep operations
        IEstimator<ITransformer> dataPrepEstimator = iContext.Transforms.ReplaceMissingValues("Features", "Features")
                                                    .Append(iContext.Transforms.NormalizeMinMax("Features", "Features"));
        ITransformer dataPrepTransformer = dataPrepEstimator.Fit(data);
        IDataView transformedData = dataPrepTransformer.Transform(data);

		// Train Model
		ITransformer model = iEstimator.Fit(transformedData);

        if(model is ISingleFeaturePredictionTransformer<object>)
        {
            var m = ((ISingleFeaturePredictionTransformer<object>)model).Model;
            Microsoft.ML.Trainers.LinearModelParameters ss = (Microsoft.ML.Trainers.LinearModelParameters)m;
        }
        else
        throw new System.Exception();
/*
      var f = iPipeline as EstimatorChain<ITransformer>;
*/
/*
BinaryClassificationMetrics metrics = experimentResult.BestRun.ValidationMetrics;
Console.WriteLine($"Accuracy: {metrics.Accuracy:0.##}");

        return null;*/
	}

    //*****************************************************************************************

    public static double TestModel(MLContext iContext, ITransformer iModel, List<double[]> iFeatures, List<Decision> iLabels, int iNumberOfFeatures)
    {
        var pred = Predict(iContext, iModel, iFeatures, iNumberOfFeatures);
        return (double)pred.Zip(iLabels, (first, second) => first == second ? 1 : 0).Sum() / iLabels.Count();
    }

    //*****************************************************************************************  

    public static List<Decision> Predict(MLContext iContext, ITransformer iModel, List<double[]> iFeatures, int iNumberOfFeatures)
    {
        var data = ModelInput.FromArray(iFeatures);
        var dataView = iContext.Data.LoadFromEnumerable(data, ModelInput.GetSchema(iNumberOfFeatures));
        var predictions = iModel.Transform(dataView);

        var scoreColumn = predictions.GetColumn<System.Boolean>("PredictedLabel").ToArray();

        return scoreColumn.Select(x => x ? Decision.Buy : Decision.Sell).ToList();
    }

    //*****************************************************************************************  




}