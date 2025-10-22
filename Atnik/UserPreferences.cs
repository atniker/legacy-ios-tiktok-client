using MonoTouch.Foundation;
using System;

public class UserPreferences
{
    public static void SetString(string key, string value)
    {
        NSUserDefaults.StandardUserDefaults.SetString(value, key);
        NSUserDefaults.StandardUserDefaults.Synchronize();
    }

    public static string GetString(string key, string defaultValue = null)
    {
        string value = NSUserDefaults.StandardUserDefaults.StringForKey(key);
        if (value == null)
        {
            return defaultValue;
        }
        return value;
    }

    public static void SetBool(string key, bool value)
    {
        NSUserDefaults.StandardUserDefaults.SetBool(value, key);
        NSUserDefaults.StandardUserDefaults.Synchronize();
    }

    public static bool GetBool(string key, bool defaultValue = false)
    {
        if (NSUserDefaults.StandardUserDefaults.ValueForKey(new NSString(key)) != null)
        {
            bool value = NSUserDefaults.StandardUserDefaults.BoolForKey(key);
            return value;
        }
        return defaultValue;
    }

    public static void Remove(string key)
    {
        NSUserDefaults.StandardUserDefaults.RemoveObject(key);
        NSUserDefaults.StandardUserDefaults.Synchronize();
    }

    public static bool HasKey(string key)
    {
        return NSUserDefaults.StandardUserDefaults.ValueForKey(new NSString(key)) != null;
    }
}