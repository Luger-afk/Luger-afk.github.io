// ---------------------------------------------
// Repository.cs : SQLite アクセス
// ---------------------------------------------
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.IO;

namespace DiscordSeManager
{
    public class Repository
    {
        private readonly string _dbPath;
        public Repository(string dbPath)
        {
            _dbPath = dbPath;
            SQLitePCL.Batteries_V2.Init();
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath));
            Init();
        }

        private SqliteConnection Open()
        {
            var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            return conn;
        }

        private void Init()
        {
            using (var conn = Open()){
                using (var cmd = conn.CreateCommand()){
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS SeItems (
  FileName TEXT PRIMARY KEY,
  MessageId INTEGER NOT NULL,
  FilePath TEXT NOT NULL,
  Trigger TEXT NOT NULL,
  VolumePercent INTEGER NOT NULL,
  Priority INTEGER NOT NULL,
  IsEnglish INTEGER NOT NULL,
  IsAdopted INTEGER NOT NULL,
  CreatedAt TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS Meta (
  Key TEXT PRIMARY KEY,
  Value TEXT NOT NULL
);
";
                    cmd.ExecuteNonQuery();

                } ;
            };
        }

        public void UpsertSe(SeItem item)
        {
            using (var conn = Open())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"
INSERT INTO SeItems (FileName, MessageId, FilePath, Trigger, VolumePercent, Priority, IsEnglish, IsAdopted, CreatedAt)
VALUES ($FileName, $MessageId, $FilePath, $Trigger, $VolumePercent, $Priority, $IsEnglish, $IsAdopted, $CreatedAt)
ON CONFLICT(FileName) DO NOTHING;";
                    cmd.Parameters.AddWithValue("$FileName", item.FileName);
                    cmd.Parameters.AddWithValue("$MessageId", (long)item.MessageId);
                    cmd.Parameters.AddWithValue("$FilePath", item.FilePath);
                    cmd.Parameters.AddWithValue("$Trigger", item.Trigger);
                    cmd.Parameters.AddWithValue("$VolumePercent", item.VolumePercent);
                    cmd.Parameters.AddWithValue("$Priority", item.Priority);
                    cmd.Parameters.AddWithValue("$IsEnglish", item.IsEnglish ? 1 : 0);
                    cmd.Parameters.AddWithValue("$IsAdopted", item.IsAdopted ? 1 : 0);
                    cmd.Parameters.AddWithValue("$CreatedAt", item.CreatedAt.ToString("o"));
                    cmd.ExecuteNonQuery();
                };
            };
        }

        public List<SeItem> GetAll()
        {
            var list = new List<SeItem>();
            using (var conn = Open())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT FileName, MessageId, FilePath, Trigger, VolumePercent, Priority, IsEnglish, IsAdopted, CreatedAt FROM SeItems ORDER BY CreatedAt DESC";
                    using (var r = cmd.ExecuteReader())
                    {
                        while (r.Read())
                        {
                            list.Add(new SeItem
                            {
                                FileName = r.GetString(0),
                                MessageId = (ulong)r.GetInt64(1),
                                FilePath = r.GetString(2),
                                Trigger = r.GetString(3),
                                VolumePercent = r.GetInt32(4),
                                Priority = r.GetInt32(5),
                                IsEnglish = r.GetInt32(6) == 1,
                                IsAdopted = r.GetInt32(7) == 1,
                                CreatedAt = DateTime.Parse(r.GetString(8))
                            });
                        }
                        return list;
                    } ;
                };
            };
        }

        public void Update(SeItem item)
        {
            using (var conn = Open())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"UPDATE SeItems SET Trigger=$Trigger, VolumePercent=$VolumePercent, Priority=$Priority, IsEnglish=$IsEnglish, IsAdopted=$IsAdopted WHERE FileName=$FileName";
                    cmd.Parameters.AddWithValue("$Trigger", item.Trigger);
                    cmd.Parameters.AddWithValue("$VolumePercent", item.VolumePercent);
                    cmd.Parameters.AddWithValue("$Priority", item.Priority);
                    cmd.Parameters.AddWithValue("$IsEnglish", item.IsEnglish ? 1 : 0);
                    cmd.Parameters.AddWithValue("$IsAdopted", item.IsAdopted ? 1 : 0);
                    cmd.Parameters.AddWithValue("$FileName", item.FileName);
                    cmd.ExecuteNonQuery();
                };
            };
        }

        public void DeleteAllAndFiles()
        {
            // 実ファイル削除
            var items = GetAll();
            foreach (var it in items)
            {
                try { if (File.Exists(it.FilePath)) File.Delete(it.FilePath); } catch { }
            }
            using (var conn = Open())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "DELETE FROM SeItems";
                    cmd.ExecuteNonQuery();
                };
            };
        }

        public void SetMeta(string key, string value)
        {
            using (var conn = Open())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = @"INSERT INTO Meta (Key, Value) VALUES ($k,$v) ON CONFLICT(Key) DO UPDATE SET Value=$v";
                    cmd.Parameters.AddWithValue("$k", key);
                    cmd.Parameters.AddWithValue("$v", value);
                    cmd.ExecuteNonQuery();
                };
            };
        }

        public string GetMeta(string key)
        {
            using (var conn = Open())
            {
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT Value FROM Meta WHERE Key=$k";
                    cmd.Parameters.AddWithValue("$k", key);
                    var v = cmd.ExecuteScalar();
                    return v?.ToString();
                };
            };
        }
    }
}