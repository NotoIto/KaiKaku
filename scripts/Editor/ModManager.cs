using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Animations;
using System.Linq;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

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
        private static Dictionary<string, Dictionary<int, ModInfo>> allModInfoListViewOld = new Dictionary<string, Dictionary<int, ModInfo>>();
        private static Dictionary<string, Dictionary<int, ModInfo>> allModInfoListView = new Dictionary<string, Dictionary<int, ModInfo>>();
        private static bool englishMode = false;
        private static Dictionary<string, string> changedObjectPathList = new Dictionary<string, string>();

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
            if (Selection.activeGameObject == null || !Selection.activeGameObject.activeInHierarchy)
            {
                avatarSelected = false;
                return;
            }
            avatarTransform = Selection.activeGameObject.transform.root;
            avatarObject = avatarTransform.gameObject;
            if (IsHumanoid(avatarObject))
            {
                avatarName = avatarObject.GetComponent<Animator>().avatar.name;
                if (!allModInfoListView.ContainsKey(avatarName))
                {
                    if (SavedAvatarMod.Get(avatarName) != null)
                    {
                        allModInfoListView.Add(avatarName, SavedAvatarMod.Get(avatarName));
                    }
                    else
                    {
                        allModInfoListView.Add(avatarName, new Dictionary<int, ModInfo>());
                    }
                }
                if (!allModInfoListViewOld.ContainsKey(avatarName))
                {
                    if (SavedAvatarMod.Get(avatarName) != null)
                    {
                        allModInfoListViewOld.Add(avatarName, SavedAvatarMod.Get(avatarName));
                    }
                    else
                    {
                        allModInfoListViewOld.Add(avatarName, new Dictionary<int, ModInfo>());
                    }
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
            EditorGUILayout.LabelField("MODELS", new GUIStyle() { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
            EditorGUILayout.BeginVertical(GUI.skin.box);
            foreach (Transform child in GetModsObject().transform)
            {
                var modObject = child.gameObject;
                var modInfoList = allModInfoListView[avatarName];

                if (!modInfoList.ContainsKey(modObject.GetInstanceID()))
                {
                    var mi = new ModInfo() { isEnabled = true, autoSelectSource = true, isHumanoid = false };
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
                        var bones = GetAllChildren(GetHipsObject(avatarObject));
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
            return avatarTransform.Find("Models") != null;
        }

        private static bool IsHumanoid(GameObject go)
        {
            var bones = GetBones(go);
            if (bones == null || bones.Count == 0)
                return false;
            return true;
        }

        private static Dictionary<string, string> GetBones(GameObject humanoidObject)
        {
            var dic = new Dictionary<string, string>();
            var animator = humanoidObject.GetComponent<Animator>();
            if (animator == null)
                return dic;
            var avatar = animator.avatar;
            var guids = AssetDatabase.FindAssets("t:Model", null);
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
                    if (human.Length == 0)
                    {
                        Debug.Log("Reimported");
                        AssetDatabase.ImportAsset(path, ImportAssetOptions.ImportRecursive);
                        importer = AssetImporter.GetAtPath(path) as ModelImporter;
                        human = importer.humanDescription.human;
                        if (allModInfoListView.ContainsKey(avatar.name))
                            allModInfoListView[avatarName][humanoidObject.GetInstanceID()].isHumanoid = true;
                    }
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
            SavedAvatarMod.Set(avatarName, allModInfoListView[avatarName]);
            foreach (Transform child in GetModsObject().transform)
            {
                var modObject = child.gameObject;
                ActivateMod(modObject);
            }
            allModInfoListViewOld[avatarName] = DeepClone(allModInfoListView[avatarName]);
        }

        private static void ActivateMod(GameObject go)
        {
            ModInfo mi = SavedAvatarMod.Get(avatarName)[go.GetInstanceID()];
            ModInfo miOld = null;
            if (allModInfoListViewOld.ContainsKey(avatarName) && allModInfoListViewOld[avatarName].ContainsKey(go.GetInstanceID()))
                miOld = allModInfoListViewOld[avatarName][go.GetInstanceID()];
            if (!mi.isEnabled)
                return;
            if (mi.isHumanoid)
            {
                var armature = GetHipsObject(go);
                var bones = GetAllChildren(armature);
                bones.Add(armature);
                var changedObjectKeys = bones.Select(b => GetHierarchyPath(b.transform, go.transform.parent)).ToArray();
                foreach (var bone in bones)
                {
                    var rigName = GetRigName(go, bone.name);
                    if (rigName == null || rigName == "")
                        continue;
                    dynamic constraint;
                    if (rigName == "Hips")
                    {
                        constraint = bone.GetComponent<ParentConstraint>();
                        if (constraint == null)
                            constraint = bone.AddComponent<ParentConstraint>();
                    }
                    else
                    {
                        constraint = bone.GetComponent<RotationConstraint>();
                        if (constraint == null)
                            constraint = bone.AddComponent<RotationConstraint>();
                    }
                    var parentObject = GetBoneObject(avatarObject, rigName);
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
                    bone.name = "_" + bone.name;
                }
                for (int i = 0; i < bones.Count; i++)
                {
                    changedObjectPathList.Add(changedObjectKeys[i], GetHierarchyPath(bones[i].transform, go.transform.parent));
                }
                ConvertAvatarAnimationClips(go);
            }
            else
            {
                var constraint = go.GetComponent<ParentConstraint>();
                if (constraint == null)
                    constraint = go.AddComponent<ParentConstraint>();
                if (mi.genericSourceObjectID != null)
                {
                    var parentObject = (GameObject)EditorUtility.InstanceIDToObject((int)mi.genericSourceObjectID);
                    GameObject parentObjectOld = null;
                    if (miOld != null)
                        parentObjectOld = (GameObject)EditorUtility.InstanceIDToObject((int)miOld.genericSourceObjectID);
                    constraint.constraintActive = true;
                    ConstraintSource cs = default(ConstraintSource);
                    cs.weight = 1.0f;
                    cs.sourceTransform = parentObject.transform;
                    if (parentObjectOld != null)
                    {
                        ConstraintSource cs1 = default(ConstraintSource);
                        cs1.weight = 1.0f;
                        cs1.sourceTransform = parentObjectOld.transform;
                        for (int i = 0; i < constraint.sourceCount; i++)
                        {
                            ConstraintSource cs2 = constraint.GetSource(i);
                            if (cs2.sourceTransform == cs1.sourceTransform && cs2.weight == cs1.weight)
                            {
                                constraint.RemoveSource(i);
                                break;
                            }
                        }
                    }
                    constraint.AddSource(cs);
                }
            }

        }

        private static void ConvertAvatarAnimationClips(GameObject go)
        {
            AssetDatabase.Refresh();
            var avatarDescriptor = go.GetComponent<VRCSDK2.VRC_AvatarDescriptor>();
            if (avatarDescriptor == null)
                return;
            var animationOverrideStanding = avatarDescriptor.CustomStandingAnims;
            var animationOverrideSitting = avatarDescriptor.CustomSittingAnims;
            AnimatorOverrideController newAnimationOverrideStanding = new AnimatorOverrideController(animationOverrideStanding.runtimeAnimatorController);
            AnimatorOverrideController newAnimationOverrideSitting = new AnimatorOverrideController(animationOverrideSitting.runtimeAnimatorController);

            var standingClips = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            animationOverrideStanding.GetOverrides(standingClips);
            var sittingClips = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            animationOverrideSitting.GetOverrides(sittingClips);
            var newStandingClips = new List<AnimationClipPair>();
            var newSittingClips = new List<AnimationClipPair>();
            foreach (var clip in standingClips)
            {
                var newClip = ConvertClip(go, clip.Value);
                if (newClip != null)
                    newStandingClips.Add(new AnimationClipPair() { overrideClip = newClip, originalClip = clip.Key });
            }
            foreach (var clip in sittingClips)
            {
                var newClip = ConvertClip(go, clip.Value);
                if (newClip != null)
                    newSittingClips.Add(new AnimationClipPair() { overrideClip = newClip, originalClip = clip.Key });
            }
            newAnimationOverrideStanding.clips = newStandingClips.ToArray();
            newAnimationOverrideSitting.clips = newSittingClips.ToArray();
            AssetDatabase.CreateAsset(
                newAnimationOverrideStanding,
                AssetDatabase.GenerateUniqueAssetPath("Assets/NotoIto/KaiKaku/saved/KK_" + animationOverrideStanding.name + ".overrideController")
                );
            AssetDatabase.CreateAsset(
                newAnimationOverrideSitting,
                AssetDatabase.GenerateUniqueAssetPath("Assets/NotoIto/KaiKaku/saved/KK_" + animationOverrideSitting.name + ".overrideController")
                );
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static AnimationClip ConvertClip(GameObject go, AnimationClip clip)
        {
            if (clip == null)
            {
                return null;
            }
            else if(AssetDatabase.GetMainAssetTypeAtPath("Assets/NotoIto/KaiKaku/saved/KK_" + clip.name + ".anim") != null)
            {
                return (AnimationClip)AssetDatabase.LoadAssetAtPath("Assets/NotoIto/KaiKaku/saved/KK_" + clip.name + ".anim", typeof(AnimationClip));
            }
            var newClip = new AnimationClip();
            var curveBindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var curveBinding in curveBindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, curveBinding);
                var curveBindingPath = changedObjectPathList.ContainsKey(go.name + "/" + curveBinding.path) ?
                    "Models/" + changedObjectPathList[go.name + "/" + curveBinding.path] :
                    "Models/" + go.name + "/" + curveBinding.path;
                EditorCurveBinding newCurveBinding = new EditorCurveBinding();
                newCurveBinding.path = curveBindingPath;
                newCurveBinding.type = curveBinding.type;
                newCurveBinding.propertyName = curveBinding.propertyName;
                AnimationUtility.SetEditorCurve(newClip, newCurveBinding, curve);
            }
            newClip.name = "KK_" + clip.name;
            AssetDatabase.CreateAsset(
                newClip,
                AssetDatabase.GenerateUniqueAssetPath("Assets/NotoIto/KaiKaku/saved/" + newClip.name + ".anim")
                );
            return newClip;
        }

        private static GameObject GetBoneObject(GameObject avatarObject, string rigName)
        {
            var boneNames = GetBones(avatarObject);
            return GetAllChildren(avatarObject).First(x => x.name == boneNames[rigName]);
        }

        private static string GetRigName(GameObject avatarObject, string boneName)
        {
            var boneNames = GetBones(avatarObject);
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

        private static GameObject GetHipsObject(GameObject avatarObject)
        {
            return GetBoneObject(avatarObject, "Hips");
        }

        private static GameObject GetModsObject()
        {
            foreach (Transform child in avatarObject.transform)
            {
                if (child.gameObject.name == "Models")
                    return child.gameObject;
            }
            var go = new GameObject("Models");
            go.transform.parent = avatarObject.transform;
            return go;
        }

        private static List<GameObject> GetAllChildren(GameObject obj)
        {
            List<GameObject> allChildren = new List<GameObject>();
            if (obj != null)
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
                if (ob == GetModsObject().transform)
                    continue;
                allChildren.Add(ob.gameObject);
                GetChildren(ob.gameObject, ref allChildren);
            }
        }

        public static T DeepClone<T>(T obj)
        {
            using (var ms = new MemoryStream())
            {
                var formatter = new BinaryFormatter();
                formatter.Serialize(ms, obj);
                ms.Position = 0;

                return (T)formatter.Deserialize(ms);
            }
        }

        private static string GetHierarchyPath(Transform self, Transform root)
        {
            string path = self.gameObject.name;
            Transform parent = self.parent;
            while (parent != null && parent != root)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }
    }

}