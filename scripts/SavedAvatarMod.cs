using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System;

namespace NotoIto.KaiKaku
{
    public static class SavedAvatarMod
    {
        private static string avatarModPath = "Assets/NotoIto/KaiKaku/saved/avatar_";
        
        public static void Set(string avatarName, Dictionary<int, ModInfo> modList)
        {
            SetSavedObject(avatarModPath + avatarName, modList);
        }

        public static Dictionary<int, ModInfo> Get(string avatarName)
        {
            return GetSavedObject(avatarModPath + avatarName);
        }

        private static Dictionary<int, ModInfo> GetSavedObject(string path)
        {
            if (!File.Exists(path))
                return null;
            Dictionary<int, ModInfo> obj;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                BinaryFormatter f = new BinaryFormatter();
                obj = (Dictionary<int, ModInfo>)f.Deserialize(fs);
            }
            return obj;
        }

        private static void SetSavedObject(string path, Dictionary<int, ModInfo> savedObject)
        {
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            {
                BinaryFormatter bf = new BinaryFormatter();
                bf.Serialize(fs, savedObject);
            }
        }
    }
}