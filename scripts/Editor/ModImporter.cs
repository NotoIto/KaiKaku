using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;
using System.IO;

namespace NotoIto.KaiKaku
{
    public class ModImporter : AssetPostprocessor
    {
        void OnPreprocessModel(GameObject obj)
        {
        }

        void OnPostprocessModel(GameObject obj)
        {
            ModelImporter modelImporter = (ModelImporter)assetImporter;
            if (obj.transform.Find("Armature") != null && modelImporter.animationType != ModelImporterAnimationType.Human)
                ConvertToHumanoid();
        }

        void ConvertToHumanoid()
        {

        }
    }
}