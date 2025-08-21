// ---------------------------------------------
// AppConfig.cs : INIの読み書き（簡易）
// ---------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiscordSeManager
{
    public class AppConfig
    {
        private readonly string _path;
        private readonly Dictionary<string, Dictionary<string, string>> _data = new Dictionary<string, Dictionary<string, string>>();

        public AppConfig(string path)
        {
            _path = path;
            if (File.Exists(_path)) Load();
        }

        public string Get(string section, string key, string defaultValue = null)
        {
            if (_data.TryGetValue(section, out var sec) && sec.TryGetValue(key, out var val))
                return val;
            return defaultValue;
        }

        public void Set(string section, string key, string value)
        {
            if (!_data.TryGetValue(section, out var sec))
            {
                sec = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                _data[section] = sec;
            }
            sec[key] = value;
        }

        public Dictionary<string, Dictionary<string, string>> Get_data()
        {
            return _data;
        }

        public void Save()
        {
            using (var sw = new StreamWriter(_path, false)) {
                foreach (var record in _data)
                {
                    sw.WriteLine($"[{record.Key}]");
                    foreach (var r in record.Value)
                    {
                        sw.WriteLine($"{r.Key}={r.Value}");
                    }
                    sw.WriteLine();
                }
            } ;
        }

        private void Load()
        {
            string currentSection = "Global";
            _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var raw in File.ReadAllLines(_path))
            {
                var line = raw.Trim();
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith(";")) continue;
                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    currentSection = line.Substring(1, line.Length - 2).Trim();
                    if (!_data.ContainsKey(currentSection))
                        _data[currentSection] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
                else
                {
                    var idx = line.IndexOf('=');
                    if (idx > 0)
                    {
                        var key = line.Substring(0, idx).Trim();
                        var val = line.Substring(idx+1).Trim();
                        _data[currentSection][key] = val;
                    }
                }
            }
        }
    }
}