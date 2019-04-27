using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

//Save load helper is a helper class intended to abstract away the save, load, copying data processes for splash files 
public static class JsonSerialisationHelper
{
    private static JsonSerializerSettings _settings;
    private static JsonSerializerSettings Settings
    {
        get
        {
            if (_settings == null)
            {
                GetSettings();
            }
            return _settings;
        }
    }

    private static void GetSettings()
    {
        _settings = new JsonSerializerSettings();
        _settings.TypeNameHandling = TypeNameHandling.Auto;
        _settings.Formatting = Formatting.Indented;
    }
    
    public static void Save<T>(string path, T obj)
    {
        // Save module count
        string jsonPayload = ConvertToJson(obj);
        System.IO.File.WriteAllText(path, jsonPayload);        
    }

    public static string ConvertToJson<T>(T obj)
    {
       return JsonConvert.SerializeObject(obj, typeof(T), Settings);
    }

    public static object LoadFromFile<T>(string path)
    {       
        string payload = System.IO.File.ReadAllText(path);
        return LoadFromString<T>(payload);
    }

    public static object LoadFromString<T>(string payload)
    {
        return JsonConvert.DeserializeObject(payload, typeof(T), Settings);
    }
}

