using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.Statistics;

public static class FinanceFunctions
{
	
//**************************************************************************************	

	/// <summary>
	/// Discounts future profits using given cost of capital.
	/// </summary>
	/// <param name="iPrice">Array containing prices.</param>
	/// <param name="iCOC">Cost of capital.</param>
	/// <param name="iStartTime">Starting time index.</param>
	/// <param name="iMaxProfitTime">Number of future prices to discount.</param>
	/// <returns>Discounted future profit.</returns>
	public static double DiscountFutureProfits(IReadOnlyList<double> iPrice, double iCOC, int iStartTime, int iMaxProfitTime)
	{
		if (iPrice is null)
			throw new ArgumentException("Array should not be NULL", "iPrice");

		if (iCOC < 0)
			throw new ArgumentException("Cost of capital cannot be less than 0", "iCOC");

		if (iStartTime >= iPrice.Count)
			throw new ArgumentException("Start time cannot be higher than iPrice length", "iStartTime >= iPrice.Length-1");

		double kCOCMultiplier = 1 + iCOC;
		double profit = 0;
		double culmulativeCOC = kCOCMultiplier;

		int endTime = Math.Min(iPrice.Count, iStartTime + iMaxProfitTime);

		for (int i = iStartTime; i < endTime; i++)
		{
			profit += (iPrice[i] - iPrice[i - 1]) / culmulativeCOC;
			culmulativeCOC *= kCOCMultiplier;
		}

		return profit;
	}
	
	//**************************************************************************************

	/// <summary>
	/// Calculates and returns array containing future profits.
	/// </summary>
	public static double[] CalculateFutureProfits(double[] iPrice, int iMaxProfitTime)
	{
		if (iPrice is null)
			throw new ArgumentNullException(nameof(iPrice));
				
		// Calulcate profits		
		double[] profits = new double[iPrice.Length];
		for (int i = 0; i < iPrice.Length - iMaxProfitTime; i++)
			profits[i] = (float)Math.Log(iPrice[i + iMaxProfitTime] / iPrice[i]); 

		return profits;
	}

	//**************************************************************************************

	/// <summary>
	/// Calcualtes and returns cost of capital.
	/// </summary>
	public static double CalculateCostOfCapital(double[] iPrice, double iMarginRequirement, double iMaxCapitalPerTrade)
	{
		// Calcluate close price change standard deviation -> Will be used to calculate discount rate
		var priceChangeStdDev = Statistics.StandardDeviation(iPrice.Zip(iPrice.Skip(1), (first, second) => Math.Log(second / first)));

		// Calculate cost of capital
		// If in period with 68 percent it might move stddev
		// And we have 2400 capital with 3.5 percent margin this gives us ~60000 trading capital
		// If we usualy trade only portion of this capital, say 12 percent
		// Then available trading capital is equal to  60000 * 0,12 = 7200 
		// Then this price move will correspond to 7200 * std dev
		// So final change is:
		// ((capital / margin) * capitalPerTrade * stddev) / capital -> capitalPerTrade * stddev / margin
		return iMaxCapitalPerTrade * priceChangeStdDev / iMarginRequirement;
	}

	//**************************************************************************************

	/// <summary>
	/// Calculates last cash flow which makes current ant target NPV equal.
	/// </summary>
	public static double CalculateLastCashflow(double iCurrentNPV, double iTargetNPV, double iCostOfCapital, int iLastKnownCashflowTime)
	{
		var diff = iTargetNPV - iCurrentNPV;
		var discount = Math.Pow(1 + iCostOfCapital, iLastKnownCashflowTime + 1);
		return diff * discount;
	}

	//**************************************************************************************
}