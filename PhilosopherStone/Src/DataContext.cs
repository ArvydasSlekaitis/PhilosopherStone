using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class DataContext : DbContext
{
    public static string serverIp = "127.0.0.1";
    public static string kConnectionString { get => @"Server=" + serverIp + ";Database=PS;Uid=remote;Pwd=compilation9;"; }
    
    public DbSet<TradeLogicInfo> tradeLogicInfos { get; set; }
    public DbSet<BSLogicInfo> bSLogicInfos { get; set; }
    public DbSet<TradeTest> tradeTests { get; set; }
    public DbSet<TestResults> testResults { get; set; }
    public DbSet<DescriptiveStatistics> tradingStat { get; set; }
    public DbSet<HistoricStdDev> historicStdDevs {get; set; }
    
//*****************************************************************************************

    protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseMySql(kConnectionString, ServerVersion.AutoDetect(kConnectionString),mysqlOptions => {
            mysqlOptions.CommandTimeout(4000); 
        });//.EnableSensitiveDataLogging()//.LogTo(Console.WriteLine, LogLevel.Information);
            //=> options.UseSqlite(@"Data Source=./Data.db");

//*****************************************************************************************

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TradeTest>()
            .HasKey(c => new { c.bsLogicInfoId, c.tradeLogicInfoId });

        modelBuilder.Entity<TestResults>()
            .HasOne(p => p.test)
            .WithMany(b => b.results)
            .HasForeignKey(s => new { s.testbsLogicInfoId, s.testTradeLogicInfoId });

        modelBuilder.Entity<DescriptiveStatistics>()
            .HasOne(p => p.testResults)
            .WithOne(q => q.tradingStat);    

        modelBuilder.Entity<HistoricStdDev>()
            .HasKey(c => new { c.Lookback, c.Timestamp });             
    }

//*****************************************************************************************

}