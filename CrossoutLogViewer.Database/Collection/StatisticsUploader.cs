﻿using CrossoutLogView.Common;
using CrossoutLogView.Database.Connection;
using CrossoutLogView.Database.Data;
using CrossoutLogView.Database.Events;
using CrossoutLogView.Log;
using CrossoutLogView.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CrossoutLogView.Database.Collection
{
    public class StatisticsUploader : IDisposable
    {
        private List<ILogEntry> gameLog = new List<ILogEntry>();
        private List<Game> games = new List<Game>();
        private bool yield = false;

        public static InvalidateCachedDataEventHandler InvalidateCachedData;

        public StatisticsUploader(long beginTimeStamp)
        {
            LogEntryTimeStampLowerLimit = beginTimeStamp;
        }

        public long LogEntryTimeStampLowerLimit { get; private set; }

        public void Commit()
        {
            List<ILogEntry> delta;
            using (var logCon = new LogConnection())
            {
                logCon.Open();
                delta = logCon.RequestAll(LogEntryTimeStampLowerLimit).ToList();
                logCon.Close();
            }
            if (delta.Count == 0) return;

            delta.Sort(new LogEntryTimeStampAscendingComparer());
            LogEntryTimeStampLowerLimit = delta[delta.Count - 1].TimeStamp + 1; //move begin to after latest logentry

            bool hasFinish = false;
            foreach (var l in delta)
            {
                if (l is LevelLoad ll)
                {
                    if (yield)
                    {
                        if (!hasFinish) gameLog.Add(DummyGameFinish(gameLog[gameLog.Count - 1].TimeStamp + 1));
                        FinalizeGameLog();
                    }
                    yield = !IgnoreLevel(ll);
                    hasFinish = false;
                }
                if (yield)
                {
                    gameLog.Add(l);
                    if (l is GameFinish) hasFinish = true;
                }                
            }
            if (games.Count != 0) //games were finished in the added logs
            {
                var userIDs = new List<int>();
                var weapons = new List<string>();
                using (var statCon = new StatisticsConnection())
                {
                    statCon.Open();
                    using (var trans = statCon.BeginTransaction())
                    {
                        statCon.Insert(games);
                        trans.Commit();
                    }
                    using (var trans = statCon.BeginTransaction())
                    {
                        foreach (var u in User.ParseUsers(games))
                        {
                            statCon.UpdateUser(u);
                            userIDs.Add(u.UserID);
                        }
                        trans.Commit();
                    }
                    using (var trans = statCon.BeginTransaction())
                    {
                        foreach (var w in WeaponGlobal.ParseWeapons(games))
                        {
                            statCon.UpdateWeaponGlobal(w);
                            weapons.Add(w.Name);
                        }
                        trans.Commit();
                    }
                    statCon.Close();
                }
                //send event invalidating changed data
                Logging.WriteLine<StatisticsUploader>(String.Concat("Invalidate existing data. ", games.Count, " games added. ", userIDs.Count, " user changed. ", weapons.Count, " weapons changed."));
                InvalidateCachedData?.Invoke(this, new InvalidateCachedDataEventArgs(games.Count, userIDs, weapons));
                games.Clear();
            }
        }

        public void Cleanup()
        {
            if (gameLog.Count != 0 && gameLog.Any(x => x is GameStart))
            {
                LogEntryTimeStampLowerLimit = gameLog[gameLog.Count - 1].TimeStamp + 1;
                gameLog.Add(DummyGameFinish(LogEntryTimeStampLowerLimit));
                FinalizeGameLog();
            }
            gameLog.Clear();
            yield = false;
        }

        private GameFinish DummyGameFinish(long dateTime)
        {
            return new GameFinish(dateTime, "unfinished", 0xff, "unfinished", (new TimeSpan(dateTime - gameLog[0].TimeStamp)).TotalSeconds);
        }

        private void FinalizeGameLog()
        {
            try
            {
                games.Add(Game.Parse(gameLog));
            }
            catch (PlayerNotFoundException ex)
            {
                Logging.WriteLine<StatisticsUploader>(ex);
            }
            catch (MatchingLogEntryNotFoundException ex)
            {
                Logging.WriteLine<StatisticsUploader>(ex);
            }
            catch (ArgumentNullException ex)
            {
                Logging.WriteLine<StatisticsUploader>(ex);
            }
            finally
            {
                gameLog.Clear();
            }
        }

        private static bool IgnoreLevel(LevelLoad levelLoad)
        {
            return levelLoad == null
                || String.IsNullOrEmpty(levelLoad.MapPathName)
                || String.Equals(Strings.LevelLoadNameTestDrive, levelLoad.MapPathName, StringComparison.InvariantCultureIgnoreCase)
                || String.Equals(Strings.LevelLoadNameMainMenu, levelLoad.MapPathName, StringComparison.InvariantCultureIgnoreCase);
        }

        #region IDisposable Support
        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    gameLog = null;
                    games = null;
                }
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    internal class LogEntryTimeStampAscendingComparer : IComparer<ILogEntry>
    {
        int IComparer<ILogEntry>.Compare(ILogEntry x, ILogEntry y) => x.TimeStamp.CompareTo(y.TimeStamp);
    }
}
