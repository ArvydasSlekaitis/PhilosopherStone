using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;

class AlphaVantage
{
	private const string kAlphaVantageAPIKey = "IT0Y1CLTZVUT7UY0";
	public enum Period {D1, m1, m5, m15, m30, H1};

	//**************************************************************************************

	/// <summary>
	/// Retrieves candlesticks from a server.
	/// </summary>
	public static List<Candlestick> RetrieveCandlesticks(Period iCandlestickPeriod)
	{
		HttpWebRequest request;

		switch (iCandlestickPeriod)
		{
			case Period.D1:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_DAILY&from_symbol=EUR&to_symbol=USD&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			case Period.m1:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_INTRADAY&from_symbol=EUR&to_symbol=USD&interval=1min&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			case Period.m5:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_INTRADAY&from_symbol=EUR&to_symbol=USD&interval=5min&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			case Period.m15:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_INTRADAY&from_symbol=EUR&to_symbol=USD&interval=15min&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			case Period.m30:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_INTRADAY&from_symbol=EUR&to_symbol=USD&interval=30min&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			case Period.H1:
				request = (HttpWebRequest)WebRequest.Create("https://www.alphavantage.co/query?function=FX_INTRADAY&from_symbol=EUR&to_symbol=USD&interval=60min&apikey=" + kAlphaVantageAPIKey + "&datatype=csv&outputsize=full");
				break;

			default:
				throw new ArgumentException("Unsuported candlestick period", "iCandlestickPeriod");
		}

		HttpWebResponse response = (HttpWebResponse)request.GetResponse();

		if (response.StatusCode != HttpStatusCode.OK)
			throw new Exception("Could not load candlesticks.");

		using (Stream stream = response.GetResponseStream())
			using (StreamReader reader = new StreamReader(stream))
			{
				List<Candlestick> results = Candlestick.LoadCSV(reader);
				results.Reverse();

				// Remove not full periods.
				if (Utils.UnixToDateTime(results[results.Count - 1].Timestamp) > DateTime.UtcNow)
					results.RemoveAt(results.Count - 1);

				return results;
			}
	}

	//**************************************************************************************
}

