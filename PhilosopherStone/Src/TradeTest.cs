using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;

[Index(nameof(State), nameof(PredictedPerformance)), Index(nameof(State), nameof(LastUpdated))]
public class TradeTest
{   
    public int bsLogicInfoId {get; set; }
    public BSLogicInfo bSLogicInfo { get; set; }

    public int tradeLogicInfoId { get; set; }
    public TradeLogicInfo tradeLogicInfo { get; set; }
   
    public List<TestResults> results { get; } = new List<TestResults>();

    // 0 - not started; 1 - started; 2 - completed;
    public ushort State { get; set; }

    public double PredictedPerformance { get; set; }
    
    [DatabaseGenerated(DatabaseGeneratedOption.Computed)]
    public DateTime LastUpdated { get; set; }

    //*****************************************************************************************

    public static IEnumerable<(int, int)> GetKeyList()
    {
        using (var db = new DataContext())
        {
            var bsInfos = db.bSLogicInfos.Select(x => x.Id).ToList();
            var tlInfos = db.tradeLogicInfos.Select(x => x.Id).ToList();
       
            foreach(var bs in bsInfos)
                foreach(var tl in tlInfos)
                    yield return (bs, tl);
        }
    }

    //*****************************************************************************************

    public static IEnumerable<(int, int)> GetBSLogicKeyList(string iTypeName, string iMethodName)
    {
        using (var db = new DataContext())
        {
            var bsInfos = db.bSLogicInfos.Where(x => x.TypeName == iTypeName && x.MethodName == iMethodName).Select(x => x.Id).ToList();
            var tlInfos = db.tradeLogicInfos.Select(x => x.Id).ToList();
       
            foreach(var bs in bsInfos)
                foreach(var tl in tlInfos)
                    yield return (bs, tl);
        }
    }

    //*****************************************************************************************

    public static IEnumerable<(int, int)> GetTradeLogicKeyList(string iTypeName)
    {
        using (var db = new DataContext())
        {
            var bsInfos = db.bSLogicInfos.Select(x => x.Id).ToList();
            var tlInfos = db.tradeLogicInfos.Where(x => x.TypeName == iTypeName).Select(x => x.Id).ToList();
       
            foreach(var bs in bsInfos)
                foreach(var tl in tlInfos)
                    yield return (bs, tl);
        }
    }

    //*****************************************************************************************

    public static IEnumerable<(int, int)> GetTradeLogicKeyList(string iTypeName, string iContainsInVariation)
    {
        using (var db = new DataContext())
        {
            var bsInfos = db.bSLogicInfos.Select(x => x.Id).ToList();
            var tlInfos = db.tradeLogicInfos.Where(x => x.TypeName == iTypeName && x.VariationName.Contains(iContainsInVariation)).Select(x => x.Id).ToList();
       
            foreach(var bs in bsInfos)
                foreach(var tl in tlInfos)
                    yield return (bs, tl);
        }
    }

    //*****************************************************************************************
    
    public static void AddUntrackedToDB(IEnumerable<(int, int)> iKeys)
    {
         foreach(var chunk in iKeys.Chunk(100000))
         {
            string query = "INSERT IGNORE INTO tradetests(bsLogicInfoId,tradeLogicInfoId) VALUES ";
            query+=string.Join(',', chunk.Select(x => $"({x.Item1}, {x.Item2})"));

            try
            {
                using (MySqlConnection mConnection = new MySqlConnection(DataContext.kConnectionString))
                    {
                        mConnection.Open();
                        using (MySqlCommand myCmd = new MySqlCommand(query.ToString(), mConnection))
                        {
                            myCmd.CommandType = CommandType.Text;
                            myCmd.CommandTimeout = 120;
                            myCmd.ExecuteNonQuery();
                        }
                    }
            }
            catch(Exception exp)
            {
                Console.WriteLine("Were was an error while inserting new tests: " + exp.ToString());
            }
         }    
    }

    //*****************************************************************************************

    public static IEnumerable<(int, int, double)> RecalculatePerformance(Dictionary<int, double> iBSPerfromance, Dictionary<int, double> iTLPerfromance)
    {
        using (var db = new DataContext())
            foreach(var test in db.tradeTests.Where(x => x.State == 0).Select(x => new {bsID = x.bsLogicInfoId, tlID = x.tradeLogicInfoId}))
                yield return (test.bsID, test.tlID, (iBSPerfromance[test.bsID] + iTLPerfromance[test.tlID])/2.0);
    }

    //*****************************************************************************************

    public static (int, int) GetNextTest()
    {
        string query = "call get_next_test_in_queue";
        
        try
        {
            using (MySqlConnection mConnection = new MySqlConnection(DataContext.kConnectionString))
            {
                mConnection.Open();
                using (MySqlCommand myCmd = new MySqlCommand(query.ToString(), mConnection))
                {
                    myCmd.CommandTimeout = 40000;
                    myCmd.CommandType = CommandType.Text;
                    var r = myCmd.ExecuteReader();
                    r.Read();
                    return (r.GetInt32(0), r.GetInt32(1));
                }
            }
        }
        catch(Exception)
        {
            Console.WriteLine("There was an error while trying to get next test");
            Thread.Sleep(1000);
            return GetNextTest();
        }
    }

    //*****************************************************************************************
    
  
}