﻿using System;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Dapper;
using HoaxBuzzer.Web.Helper;
using HoaxBuzzer.Web.Repositories;
using Npgsql;
using NUnit.Framework;

namespace HoaxBuzzer.Test
{

    public static class RandomExtension
    {
        public static bool NextBool(this Random r) => r.NextDouble() >= 0.5;
    }
    
    [TestFixture]
    public class Tests
    {
        [Test]
        public void Mqtt1()
        {
            var rnd = new Random();
            var client1 = MqttClientFactory.CreateConnectedClient();
            var client2 = MqttClientFactory.CreateConnectedClient();
            var lck = new object();
            string mqttMsg = null;

            var topic = "opentrigger/tests/D59979C42762488F8B570A1F16BE3AB6/" + DateTime.Now.Ticks;
            var msg = "asdf-" + DateTime.Now.Ticks;

            client1.Subscribe(topic);
            client1.MqttMsgPublishReceived += (sender, args) =>
            {

                var inMsg = Encoding.UTF8.GetString(args.Message);
                var dbgMsg = $"msg '{inMsg}' from '{args.Topic}'";
                Debug.WriteLine(dbgMsg);
                lock (lck)
                {
                    mqttMsg = inMsg;
                }
            };

            client2.Publish(topic, Encoding.UTF8.GetBytes(msg));

            var sw = new Stopwatch();
            sw.Start();

            while (true)
            {
                lock (lck)
                {
                    if(mqttMsg == msg) break;
                }
                System.Threading.Thread.Sleep(500);
                if(sw.ElapsedMilliseconds > 5000) throw new Exception("Timeout");
            }
            client1.Disconnect();
            client2.Disconnect();
            Assert.AreEqual(msg, mqttMsg);
        }
        [Test]
        public void AppSettings1()
        {
            var exCnt = 0;
            try
            {
                AppSettings.Get("does-not-exist-234");
            }
            catch (Exception)
            {
                exCnt++;
            }
            var guid = Guid.NewGuid().ToString();
            var fallback = AppSettings.Get("does-not-exist-456", guid);
            Assert.AreEqual(guid,fallback);
            Assert.AreEqual(1,exCnt);
        }
        
        [Test]
        public void PostgresDa1()
        {
            var connstr = AppSettings.Get("DbConnectionString");
            using (var db = new NpgsqlConnection(connstr))
            {
                var r = new Random();
                
                var articles = db.Query<Entities.Article>("select * from article").ToList();
                var images = articles.Select(a => db.GetImage(a.fkScreenshot)).ToList();

                var statistics = articles.Select(a =>
                {
                    var voteId = db.setVote(a, r.NextBool());
                    return db.getStatisticsForVote(voteId);
                }).ToList();

                for (var i = 0; i < articles.Count; i++)
                {
                    var a = db.GetNextArticle();
                    var voteId = db.setVote(a.id, r.NextBool());
                    var stat = db.getStatisticsForVote(voteId);
                    statistics.Add(stat);
                }
                
                db.Close();
            }
        }
    }
}