using System;
using System.IO;
using UnityEngine;

namespace MuchBetterEmotes;

internal static class DrawingFiles
{
    private const long SaveLimit = 1024 * 1024;
    private const string Folder = "MuchBetterEmotes";

    public enum Status
    {
        Empty,
        Ok,
        Newer,
        Damaged
    }

    public static string LastPath => Path.Combine(Application.persistentDataPath, Folder, "drawing.json");

    public static Status Read(int slot, DrawLog target, Pen? pen) => Read(GetPath(slot), target, pen);

    public static Status Read(string path, DrawLog target, Pen? pen)
    {
        try
        {
            if (!File.Exists(path)) return Status.Empty;

            if (new FileInfo(path).Length > SaveLimit)
            {
                Warn("read", path, "The file is over 1 MiB.");
                return Status.Damaged;
            }

            string json = File.ReadAllText(path);
            if (DrawingData.IsTooDeep(json))
            {
                Warn("read", path, "The file is nested too deep.");
                return Status.Damaged;
            }

            DrawingData? data = JsonUtility.FromJson<DrawingData>(json);
            if (data == null)
            {
                Warn("read", path, "The file is empty.");
                return Status.Damaged;
            }

            switch (data.Load(target, pen))
            {
                case DrawingData.Result.Newer:
                    Warn("read", path, "The file is from a newer version, so it is left alone.");
                    return Status.Newer;
                case DrawingData.Result.Bad:
                    Warn("read", path, "The file holds an invalid stroke.");
                    return Status.Damaged;
            }

            return Status.Ok;
        }
        catch (Exception ex)
        {
            Warn("read", path, ex.Message);
            return Status.Damaged;
        }
    }

    public static bool Write(int slot, DrawLog log, Pen pen) => Write(GetPath(slot), log, pen);

    public static bool Write(string path, DrawLog log, Pen pen)
    {
        try
        {
            string json = JsonUtility.ToJson(DrawingData.From(log, pen));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            string temp = path + ".tmp";
            File.WriteAllText(temp, json);
            if (File.Exists(path)) File.Replace(temp, path, null);
            else File.Move(temp, path);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Could not write the drawing at {path}. {ex.Message}");
            return false;
        }
    }

    public static bool Delete(int slot) => Delete(GetPath(slot));

    public static bool Delete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
            return true;
        }
        catch (Exception ex)
        {
            Warn("delete", path, ex.Message);
            return false;
        }
    }

    private static string GetPath(int slot) => Path.Combine(Application.persistentDataPath, Folder, "drawings", $"slot{slot}.json");

    private static void Warn(string action, string path, string reason)
    {
        Plugin.Log.LogWarning($"Could not {action} the drawing at {path}. {reason}");
    }
}
