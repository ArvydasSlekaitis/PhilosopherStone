using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

public enum Decision {Sell = 0, Buy = 1};

public class Main : MonoBehaviour
{
    const int kBSLogicParam1 = 3;
    const int kBSLogicParam2 = 18;
    const string kTLLogicVariationName = "N7_LT240_OF0_OO0,5_TP0_SL0_Halting0_SOS0,15_QBSS0,001_QBES0,002_QBFW1,0";
    const double kMargin = 0.035;

    public InputField tradingCapitalGUI;
    public Text currentDecisionGUI;
    public Text lastUpdatedGUI;
    public Button updateButtonGUI;
    public Text ordersPanelTextGUI;
    public GameObject directionChangedPanelGUI;
    public GameObject cantGetDataPanelGUI;
    
    int tradingCapital = 2400;
    List<TradeOrder> orders = null;
    Decision lastDecision;
    Decision currentDecision;
    DateTime lastHourlyUpdateTime = DateTime.MinValue;
    bool isInited = false;

//***************************************************************************************************

    void Start()
    {
        CultureInfo.DefaultThreadCurrentCulture = new CultureInfo("lt-LT", false);

        if(double.Parse("0,0005")!=0.0005 || $"{0.0005}" != "0,0005")
            return;

        Application.targetFrameRate = 5;
        UpdateData();
        UpdateOrders();
        StartCoroutine(UpdateDataCo());
    }

//***************************************************************************************************

    public void OnEnterTradingCapital() 
    { 
        tradingCapital = int.Parse(tradingCapitalGUI.text);
        UpdateOrders();
    }

//***************************************************************************************************

    public void UpdateData()
    {  
        // //BSLogic_Inline_MedianPriceChangeVelocityIncreasing_H_4_13_TradeLogic_8_N7_LT240_OF0_OO0,5_TP0_SL0_Halting0_SOS0,15_QBSS0,001_QBES0,002_QBFW1,0_0,035

        var hourly = AlphaVantage.RetrieveCandlesticks(AlphaVantage.Period.H1);
        var minute = AlphaVantage.RetrieveCandlesticks(AlphaVantage.Period.m1);       
        var rescentHourly = Candlestick.Consolidate(minute, 24);
        
        // BS Logic
        var bsLogic = new BSLogic_Inline(new BSLogicInfo(), (d, h) => 
                {
                    var r = BSLogic_Inline.GetMedianPriceChangeVelocity(h, new List<int>(){kBSLogicParam1, kBSLogicParam2});
                    return r[0] >= r[1] ? Decision.Buy : Decision.Sell;
                });

        currentDecision = bsLogic.GetDecision(null, rescentHourly);
        currentDecisionGUI.text = currentDecision.ToString();
        lastUpdatedGUI.text = Utils.UnixToDateTime(minute.Last().Timestamp).ToLocalTime().ToString();
        lastHourlyUpdateTime = Utils.UnixToDateTime(hourly.Last().Timestamp).ToLocalTime();

        var TL = new TradeLogic_8(kTLLogicVariationName);

        if(currentDecision == Decision.Buy)
            orders = TL.GetBuyOrders(hourly, hourly.Last().ClosePrice, currentDecision);
        else
            orders = TL.GetSellOrders(hourly, hourly.Last().ClosePrice, currentDecision);

        if(isInited && currentDecision != lastDecision)
        {
            directionChangedPanelGUI.SetActive(true);
            directionChangedPanelGUI.GetComponentInChildren<Text>().text = $"Direction changed!{System.Environment.NewLine}{lastDecision}=>{currentDecision}";
            lastDecision = currentDecision;
        }

        if(!isInited)
            lastDecision = currentDecision;

        isInited = true;
    }

//***************************************************************************************************

    public void UpdateOrders()
    {
        var text = $"Issue {currentDecision} orders:";
        text +=  System.Environment.NewLine;
        
        for (int i = 0; i < orders.Count; i++)
        {
            TradeOrder order = orders[i];
            var quantity = Math.Floor(tradingCapital / kMargin * order.quantity / 1000);
            if(i == 0)
                text += $"{quantity}K@Current{System.Environment.NewLine}";
            else if(quantity >= 1)
                text += $"{quantity}K@{order.price:F5}{System.Environment.NewLine}";
        }

        ordersPanelTextGUI.text = text;
    }

//***************************************************************************************************

    IEnumerator UpdateDataCo()
    {
        while(true)
        {
            try
            {
                UpdateData();     
                UpdateOrders();  
            }
            catch(Exception)
            {
            }
            
            if((lastHourlyUpdateTime - DateTime.Now).TotalMinutes >= 60)
                yield return new WaitForSecondsRealtime(60);    
            else
            {
                var starRapidUpdateTime = lastHourlyUpdateTime.AddHours(1);
                yield return new WaitForSecondsRealtime((float)(starRapidUpdateTime - DateTime.Now).TotalSeconds);  
            }

            if((lastHourlyUpdateTime - DateTime.Now).TotalMinutes >= 70)
            {
                cantGetDataPanelGUI.SetActive(true);
            }
        }
    }

//***************************************************************************************************

}
