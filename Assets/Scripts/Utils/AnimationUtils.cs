
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class AnimationUtils
{
    public const string CREATE_ANIMATION_OVERRIDES = "Assets/Animation/Create Animation Overrides";

    [MenuItem(CREATE_ANIMATION_OVERRIDES)]
    private static void CreateOverrides()
    {
        AnimatorOverrideController obj = Selection.activeObject as AnimatorOverrideController;
        CreateOverrides(obj);
    }

    [MenuItem("CONTEXT/AnimatorOverrideController/Create Animation Overrides")]
    private static void ContextCreateOverrides(MenuCommand cmd)
    {
        var x = cmd.context;
        CreateOverrides(x as AnimatorOverrideController);
    }

    public static void CreateOverrides(AnimatorOverrideController obj)
    {
        if (obj.runtimeAnimatorController == null)
        {
            Debug.LogError("Assign a base controller to override from!");
            return;
        }

        string current = GetCurrentSelectionFolder();
        List<string> paths = new List<string>();
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        obj.GetOverrides(overrides);
        foreach (var clip in overrides)
        {
            string path = Path.Combine(current, clip.Key.name + ".anim");
            paths.Add(path);
        }

        foreach (var path in paths)
        {
            bool exists = File.Exists(Path.Combine(Application.dataPath.Replace("Assets", ""), path));
            if (exists)
            {
                Debug.LogWarning(path + " already exists, new clip will not be created!");
            }
        }
        paths.RemoveAll(x => File.Exists(Path.Combine(Application.dataPath.Replace("Assets", ""), x)));

        Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();

        int created = 0;
        int total = paths.Count;
        foreach (var path in paths)
        {
            string name = Path.GetFileNameWithoutExtension(path);
            EditorUtility.DisplayProgressBar("Creating clips...", $"Creating '{name}', {created + 1}/{total}", (float)created / total);

            var clip = new AnimationClip();
            clip.name = name;

            var def = AnimationUtility.GetAnimationClipSettings(clip);
            def.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, def);

            AssetDatabase.CreateAsset(clip, path);

            clips.Add(clip.name, clip);
            created++;
        }
        EditorUtility.ClearProgressBar();
        Debug.Log($"Created {created} new animation clips...");

        int added = 0;
        int replaced = 0;
        int maintained = 0;

        List<KeyValuePair<AnimationClip, AnimationClip>> newOverrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        newOverrides.AddRange(overrides);

        foreach (var clip in overrides)
        {
            if (clip.Value != null)
            {
                if (clips.ContainsKey(clip.Key.name))
                {
                    string oldPath = AssetDatabase.GetAssetPath(clip.Value);
                    Debug.LogWarning($"There is already an override specified for '{clip.Key.name}', it will be replaced by the newly created clip. Old override was '{oldPath}'");

                    // Use same key but new value.
                    newOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip.Key, clips[clip.Key.name]));
                    replaced++;
                }
                else
                {
                    // There is an override specified, and we couldn't create a new clip, presumably because it already existed.
                    // Use the old one...
                    newOverrides.Add(clip);
                    maintained++;
                }
            }
            else
            {
                if (clips.ContainsKey(clip.Key.name))
                {
                    // Use same key but new value.
                    newOverrides.Add(new KeyValuePair<AnimationClip, AnimationClip>(clip.Key, clips[clip.Key.name]));
                    added++;
                }
                else
                {
                    // There is no override specified and we couldn't create a new clip, presumably because it already existed.
                    // Use the old one... I could possibly dig around the asset database for the existing one, but I won't.
                    newOverrides.Add(clip);
                    maintained++;
                }
            }
        }

        // Now apply.
        obj.ApplyOverrides(newOverrides);

        Debug.Log($"Assigned {added}, replaced {replaced} and maintained {maintained} overrides");
    }

    [MenuItem(CREATE_ANIMATION_OVERRIDES, validate = true)]
    public static bool ValidateCreateOverrides()
    {
        return Selection.activeObject is AnimatorOverrideController;
    }

    public static string GetCurrentSelectionFolder()
    {
        string path = AssetDatabase.GetAssetPath(Selection.activeObject);
        if (path == "")
        {
            return "Assets";
        }
        else if (Path.GetExtension(path) != "")
        {
            return path.Replace(Path.GetFileName(AssetDatabase.GetAssetPath(Selection.activeObject)), "");
        }
        else
        {
            return null;
        }
    }
}
