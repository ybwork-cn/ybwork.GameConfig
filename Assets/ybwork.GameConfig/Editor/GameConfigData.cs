using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ybwork.GameConfig.Editor
{
    [Serializable]
    public class GameConfigKeyValueData
    {
        public string Key;
        public string Value;
    }

    public enum TableType
    {
        Map = 0,
        Array = 1,
    }

    [Serializable]
    public class GameConfigTableData
    {
        public string TableName;
        public TableType TableType;

        public MonoScript Define;

        public List<string> Array = new();
        public List<GameConfigKeyValueData> Map = new();
    }

    [Serializable]
    public class GameConfigPackageData
    {
        public string PackageName;
        public List<GameConfigTableData> Tables = new();
    }

    [Serializable]
    public class GameConfigData : ScriptableObject
    {
        public List<GameConfigPackageData> Packages = new();
        public string TargetPath;

        public static GameConfigData GetData()
        {
            GameConfigData data;
            string[] guids = AssetDatabase.FindAssets($"t:{typeof(GameConfigData).FullName}");
            if (guids.Length == 0)
            {
                data = CreateInstance<GameConfigData>();
                Directory.CreateDirectory("Assets/Settings/");
                AssetDatabase.CreateAsset(data, "Assets/Settings/" + nameof(GameConfigData) + ".asset");
            }
            else if (guids.Length == 1)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                data = AssetDatabase.LoadAssetAtPath<GameConfigData>(assetPath);
            }
            else
            {
                string message = "存在多个" + nameof(GameConfigData) + "，已自动选取第一个 at";
                foreach (var guid in guids)
                {
                    message += "\r\n\t" + AssetDatabase.GUIDToAssetPath(guid);
                }
                Debug.LogWarning(message);

                string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                data = AssetDatabase.LoadAssetAtPath<GameConfigData>(assetPath);
            }
            return data;
        }

    }
}
