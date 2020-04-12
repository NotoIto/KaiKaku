using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;
using System.Linq;

namespace NotoIto.KaiKaku
{
    public class ModManager : EditorWindow
    {
        private static List<ModInfo> modList = new List<ModInfo>();
        private static GameObject avatarObject;
        private static Transform avatarTransform;
        private static string avatarName;
        private static bool avatarSelected = false;
        private static Vector2 scrollPos = Vector2.zero;
        private static Dictionary<string, Dictionary<int, ModInfo>> allModInfoList = new Dictionary<string, Dictionary<int, ModInfo>>();
        private static bool englishMode = false;

        [MenuItem("Tools/改変革命")]
        private static void Open()
        {
            englishMode = false;
            EditorWindow.GetWindow<ModManager>("改変革命");
        }

        [MenuItem("Tools/KaiKaku")]
        private static void OpenEn()
        {
            englishMode = true;
            EditorWindow.GetWindow<ModManager>("KaiKaku");
        }

        private void Update()
        {
            UpdateActiveObject();
        }

        private void OnGUI()
        {
            if (!avatarSelected)
                return;
            if (!IsInstalled())
            {
                if (GUILayout.Button(englishMode ? "Install" : "インストール"))
                    Init();
            }
            else
            {
                MainGUI();
            }
            EditorGUILayout.EndScrollView();
        }

        private void UpdateActiveObject()
        {
            if (Selection.activeGameObject == null)
            {
                avatarSelected = false;
                return;
            }
            avatarTransform = Selection.activeGameObject.transform.root;
            avatarObject = avatarTransform.gameObject;
            if (IsHumanoid(avatarObject))
            {
                avatarName = avatarObject.GetComponent<Animator>().avatar.name;
                if (!allModInfoList.ContainsKey(avatarName))
                {
                    if (SavedAvatarMod.Get(avatarName) != null)
                        allModInfoList.Add(avatarName, SavedAvatarMod.Get(avatarName));
                    else
                        allModInfoList.Add(avatarName, new Dictionary<int, ModInfo>());
                }
                avatarSelected = true;
            }
            else
            {
                avatarSelected = false;
            }
            Repaint();
        }

        private void MainGUI()
        {
            var defaultColor = GUI.backgroundColor;
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
            EditorGUILayout.LabelField("MODS", new GUIStyle() { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            EditorGUILayout.BeginVertical(GUI.skin.box);
            foreach (Transform child in GetModsObject().transform)
            {
                var modObject = child.gameObject;
                var modInfoList = allModInfoList[avatarName];

                if (!modInfoList.ContainsKey(modObject.GetInstanceID()))
                {
                    var mi = new ModInfo();
                    if (IsHumanoid(modObject))
                        mi.isHumanoid = true;
                    modInfoList.Add(modObject.GetInstanceID(), mi);
                }
                var modInfo = modInfoList[modObject.GetInstanceID()];
                if (Selection.activeGameObject != null && Selection.activeGameObject == modObject)
                    GUI.backgroundColor = new Color(0.5f, 1f, 0.5f);
                else
                    GUI.backgroundColor = new Color(0.9f, 0.9f, 0.9f);
                EditorGUILayout.BeginVertical(GUI.skin.box);
                modInfo.isEnabled = EditorGUILayout.BeginToggleGroup(modObject.name, modInfo.isEnabled);
                if (!IsHumanoid(modObject))
                    GUI.enabled = false;
                modInfo.isHumanoid = EditorGUILayout.Toggle(englishMode ? "Is humanoid" : "関節を接続", modInfo.isHumanoid);
                GUI.enabled = true;
                if (!modInfo.isHumanoid)
                {
                    EditorGUILayout.LabelField("");
                    EditorGUILayout.LabelField(englishMode ? "Object joint settings" : "オブジェクト接続設定", new GUIStyle() { fontStyle = FontStyle.Bold });
                    modInfo.autoSelectSource = EditorGUILayout.Toggle(englishMode ? "Auto select source" : "ソース自動選択", modInfo.autoSelectSource);
                    if (modInfo.autoSelectSource)
                    {
                        GUI.enabled = false;
                        var bones = GetAllChildren(GetArmatureObject(avatarObject));
                        GameObject nearestBone = bones.OrderByDescending(b => Vector3.Distance(b.transform.position, modObject.transform.position)).LastOrDefault();
                        if (nearestBone != null)
                            modInfo.genericSourceObjectID = nearestBone.GetInstanceID();
                    }
                    var sourceObject = modInfo.genericSourceObjectID == null ? null : EditorUtility.InstanceIDToObject((int)modInfo.genericSourceObjectID);
                    var inputSourceObject = EditorGUILayout.ObjectField(englishMode ? "Source" : "ソース", sourceObject, typeof(GameObject), true);
                    if (inputSourceObject == null)
                        modInfo.genericSourceObjectID = null;
                    else
                        modInfo.genericSourceObjectID = inputSourceObject.GetInstanceID();
                    GUI.enabled = true;
                }
                EditorGUILayout.EndToggleGroup();
                EditorGUILayout.EndVertical();
            }
            GUI.backgroundColor = defaultColor;
            EditorGUILayout.EndVertical();
            if (GUILayout.Button(englishMode ? "Assembly" : "組み込み実行"))
                Activate();
        }

        private static void Init()
        {
            if (!IsInstalled())
            {
                EditorGUIUtility.PingObject(GetModsObject());
            }
        }

        private static bool IsInstalled()
        {
            return avatarTransform.Find("Mods") != null;
        }

        private static bool IsHumanoid(GameObject go)
        {
            var animator = go.GetComponent<Animator>();
            if (animator == null)
                return false;
            var bones = GetBones(animator.avatar);
            if (bones == null)
                return false;
            return true;
        }

        private static Dictionary<string,string> GetBones(Avatar avatar)
        {
            var guids = AssetDatabase.FindAssets("t:Model", null);
            var dic = new Dictionary<string, string>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (obj.GetComponent<Animator>() == null)
                    continue;
                if (obj.GetComponent<Animator>().avatar == avatar)
                {
                    var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                    var human = importer.humanDescription.human;
                    foreach (var bone in human)
                    {
                        dic.Add(bone.humanName, bone.boneName);
                    }
                    break;
                }
            }
            return dic;
        }

        private static void Activate()
        {
            SavedAvatarMod.Set(avatarName, allModInfoList[avatarName]);
            foreach (Transform child in GetModsObject().transform)
            {
                var modObject = child.gameObject;
                ActivateMod(modObject);
            }
        }

        private static void ActivateMod(GameObject go)
        {
            ModInfo mi = SavedAvatarMod.Get(avatarName)[go.GetInstanceID()];
            if (!mi.isEnabled)
                return;
            if (mi.isHumanoid)
            {
                var armature = GetArmatureObject(go);
                var bones = GetAllChildren(armature);
                bones.Add(armature);
                foreach (var bone in bones)
                {
                    var constraint = bone.AddComponent<ParentConstraint>();
                    if (constraint == null)
                        constraint = bone.GetComponent<ParentConstraint>();
                    var rigName = GetRigName(go, bone.name);
                    if (rigName == null)
                        continue;
                    var parentObject = GetBoneObject(avatarObject, rigName);
                    constraint.constraintActive = true;
                    ConstraintSource cs = default(ConstraintSource);
                    cs.weight = 1.0f;
                    cs.sourceTransform = parentObject.transform;
                    for(int i = 0; i < constraint.sourceCount; i++)
                    {
                        ConstraintSource cs2 = constraint.GetSource(i);
                        if (cs2.sourceTransform == cs.sourceTransform && cs2.weight == cs.weight)
                        {
                            constraint.RemoveSource(i);
                            break;
                        }
                    }
                    constraint.AddSource(cs);
                }
            }
            else
            {
                var constraint = go.AddComponent<ParentConstraint>();
                if (constraint == null)
                    constraint = go.GetComponent<ParentConstraint>();
                if (mi.genericSourceObjectID != null)
                {
                    var parentObject = (GameObject)EditorUtility.InstanceIDToObject((int)mi.genericSourceObjectID);
                    constraint.constraintActive = true;
                    ConstraintSource cs = default(ConstraintSource);
                    cs.weight = 1.0f;
                    cs.sourceTransform = parentObject.transform;
                    for (int i = 0; i < constraint.sourceCount; i++)
                    {
                        ConstraintSource cs2 = constraint.GetSource(i);
                        if (cs2.sourceTransform == cs.sourceTransform && cs2.weight == cs.weight)
                        {
                            constraint.RemoveSource(i);
                            break;
                        }
                    }
                    constraint.AddSource(cs);
                }
            }

        }

        private static GameObject GetBoneObject(GameObject avatarObject, string rigName)
        {
            var boneNames = GetBones(avatarObject.GetComponent<Animator>().avatar);
            var armatureGameObject = GetArmatureObject(avatarObject);
            if (boneNames[rigName] == "Armature")
                return armatureGameObject;
            return GetAllChildren(armatureGameObject).First(x => x.name == boneNames[rigName]);
        }

        private static string GetRigName(GameObject avatarObject, string boneName)
        {
            var boneNames = GetBones(avatarObject.GetComponent<Animator>().avatar);
            var str = "";
            try
            {
                str = boneNames.First(x => x.Value == boneName).Key;
            }
            catch
            {
                return null;
            }
            return str;
        }

        private static GameObject GetArmatureObject(GameObject avatarObject)
        {
            foreach (Transform child in avatarObject.transform)
            {
                if (child.gameObject.name == "Armature")
                    return child.gameObject;
            }
            return null;
        }

        private static GameObject GetModsObject()
        {
            foreach (Transform child in avatarObject.transform)
            {
                if (child.gameObject.name == "Mods")
                    return child.gameObject;
            }
            var go = new GameObject("Mods");
            go.transform.parent = avatarObject.transform;
            return go;
        }

        private static List<GameObject> GetAllChildren(GameObject obj)
        {
            List<GameObject> allChildren = new List<GameObject>();
            GetChildren(obj, ref allChildren);
            return allChildren;
        }

        private static void GetChildren(GameObject obj, ref List<GameObject> allChildren)
        {
            Transform children = obj.GetComponentInChildren<Transform>();
            if (children.childCount == 0)
            {
                return;
            }
            foreach (Transform ob in children)
            {
                allChildren.Add(ob.gameObject);
                GetChildren(ob.gameObject, ref allChildren);
            }
        }
    }

}