using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VRage.Game.ModAPI;
using VRageMath;

namespace WelderWall.Data.Scripts.Math0424.WelderWall.Util
{
    /// <summary>
    /// A tool for making space engineers config files easily,
    /// it only supports C# primatives
    /// Made by Math0424
    /// Version 1.0
    /// </summary>
    internal class EasyConfiguration
    {
        private bool _global;
        private Dictionary<string, object> _defaultConfig;
        private string _configName;

        public EasyConfiguration(bool saveGlobal, string name, Dictionary<string, object> defaults)
        {
            _global = saveGlobal;
            _configName = name;
            _defaultConfig = defaults;
            Load();
        }

        public EasyConfiguration(bool saveGlobal, string name, Dictionary<Enum, object> defaults)
        {
            _global = saveGlobal;
            _configName = name;
            _defaultConfig = new Dictionary<string, object>();
            foreach (var x in defaults)
                _defaultConfig.Add(x.Key.ToString(), x.Value);
            Load();
        }

        private void Save()
        {
            TextWriter writer = null;
            try
            {
                if (_global)
                {
                    if (MyAPIGateway.Utilities.FileExistsInGlobalStorage(_configName))
                        return;
                    writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(_configName);
                }
                else
                {
                    if (MyAPIGateway.Utilities.FileExistsInWorldStorage(_configName, typeof(EasyConfiguration)))
                        return;
                    writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(_configName, typeof(EasyConfiguration));
                } 

                writer.WriteLine($"# EasyConfiguration; auto-generated config file");
                writer.WriteLine($"# Surround text in \"Quotes\"");
                foreach (var config in _defaultConfig)
                {
                    if (config.Value.GetType() == typeof(string))
                        writer.WriteLine($"{config.Key} = \"{config.Value}\"");
                    else
                        writer.WriteLine($"{config.Key} = {config.Value}");
                }
            }
            finally
            {
                writer?.Flush();
                writer?.Close();
            }
        }

        private void Load()
        {
            TextReader reader = null;
            try
            {
                if (_global)
                {
                    if (!MyAPIGateway.Utilities.FileExistsInGlobalStorage(_configName))
                    {
                        Save();
                        return;
                    }
                    reader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(_configName);
                }
                else
                {
                    if (!MyAPIGateway.Utilities.FileExistsInWorldStorage(_configName, typeof(EasyConfiguration)))
                    {
                        Save();
                        return;
                    }
                    reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(_configName, typeof(EasyConfiguration));
                }

                string[] file = reader.ReadToEnd().Split('\n');
                foreach(var line in file)
                {
                    if (line.StartsWith("#") || line.Trim().Length == 0)
                        continue;

                    string[] split = line.Split('=');
                    if (split.Length < 2)
                        throw new Exception($"Cannot read config value '{line}' in file '{_configName}'");

                    string key = split[0].Trim();
                    if (_defaultConfig.ContainsKey(key))
                        _defaultConfig[key] = Cast(_defaultConfig[key].GetType(), string.Join("", split.Skip(1)));
                }

            }
            finally
            {
                reader?.Close();
            }
        }

        private object Cast(Type t, string value)
        {
            if(t == typeof(string))
            {
                if (!value.StartsWith("\"") || value.EndsWith("\""))
                    throw new Exception($"Config string requires \"Quotes\" around it '{value}'");
                return value.Substring(1, value.Length - 3);
            }
            else if (t == typeof(bool))
                return bool.Parse(value);
            else if(t == typeof(float))
                return float.Parse(value);
            else if (t == typeof(double))
                return double.Parse(value);
            else if (t == typeof(int))
                return int.Parse(value);
            else if (t == typeof(short))
                return short.Parse(value);
            else if (t == typeof(long))
                return long.Parse(value);
            else if (t == typeof(ulong))
                return ulong.Parse(value);
            else if (t == typeof(uint))
                return uint.Parse(value);
            else if (t == typeof(ushort))
                return ushort.Parse(value);
            else if (t == typeof(byte))
                return byte.Parse(value);
            else if (t == typeof(sbyte))
                return sbyte.Parse(value);
            else if (t == typeof(char))
                return char.Parse(value);
            throw new Exception($"Unsupported config serialization of '{t}'");
        }

        public int GetInt(Enum value)
        {
            return Get<int>(value.ToString());
        }

        public double GetDouble(Enum value)
        {
            return Get<double>(value.ToString());
        }

        public float GetFloat(Enum value)
        {
            return Get<float>(value.ToString());
        }

        public string GetString(Enum value)
        {
            return Get<string>(value.ToString());
        }

        public int GetInt(string value)
        {
            return Get<int>(value);
        }

        public float GetFloat(string value)
        {
            return Get<float>(value);
        }

        public double GetDouble(string value)
        {
            return Get<double>(value);
        }

        public string GetString(string value)
        {
            return Get<string>(value);
        }

        public T Get<T>(Enum value)
        {
            return Get<T>(value.ToString());
        }

        public T Get<T>(string value)
        {
            if (!_defaultConfig.ContainsKey(value))
                throw new Exception($"Config option '{value}' not found!");
            return (T)_defaultConfig[value];
        }
    }
}
