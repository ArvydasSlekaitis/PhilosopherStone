using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;

public class DescriptiveStatistics
{
    public int DescriptiveStatisticsId { get; set; }
    public TestResults testResults { get; set; }
    public int testResultsId {get; set; }

    public double mean  { get; set; }
    public double median  { get; set; }
    public double stdDev  { get; set; }
    public double lowerQuartile { get; set; }
    public double upperQuartile { get; set; }
    public double min { get; set; }
    public double max { get; set; }

   //*****************************************************************************************

    public DescriptiveStatistics(){}

   //*****************************************************************************************

    public DescriptiveStatistics(List<double> iNumbers)
    {
        mean = iNumbers.Average();
        median = iNumbers.Median();
        stdDev = iNumbers.StandardDeviation();
        lowerQuartile = iNumbers.LowerQuartile();
        upperQuartile = iNumbers.UpperQuartile();
        min = iNumbers.Min();
        max = iNumbers.Max();
    }

    //*****************************************************************************************
}