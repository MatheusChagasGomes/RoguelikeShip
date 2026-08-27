using System;
using System.Reflection;
using UnityEditor;

/// <summary>
/// Adds portrait Game View presets so Fit to Width can be checked on 16:9, 18:9, 19.5:9 and 20:9.
/// </summary>
[InitializeOnLoad]
static class PortraitGameViewSizes
{
    static readonly GameViewSizeGroupType[] Groups =
    {
        GameViewSizeGroupType.Standalone,
        GameViewSizeGroupType.Android,
        GameViewSizeGroupType.iOS,
    };

    static PortraitGameViewSizes()
    {
        foreach (GameViewSizeGroupType group in Groups)
        {
            AddAspect(group, "16:9 Portrait", 9, 16);
            AddAspect(group, "18:9 Portrait", 9, 18);
            AddAspect(group, "19.5:9 Portrait", 6, 13);
            AddAspect(group, "20:9 Portrait", 9, 20);
        }
    }

    static void AddAspect(GameViewSizeGroupType groupType, string name, int width, int height)
    {
        try
        {
            Assembly editorAssembly = typeof(Editor).Assembly;
            Type sizesType = editorAssembly.GetType("UnityEditor.GameViewSizes");
            Type sizeType = editorAssembly.GetType("UnityEditor.GameViewSize");
            Type sizeTypeEnum = editorAssembly.GetType("UnityEditor.GameViewSizeType");
            if (sizesType == null || sizeType == null || sizeTypeEnum == null)
            {
                return;
            }

            Type singletonType = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object instance = singletonType.GetProperty("instance")?.GetValue(null, null);
            MethodInfo getGroup = sizesType.GetMethod("GetGroup");
            object group = getGroup?.Invoke(instance, new object[] { (int)groupType });
            if (group == null)
            {
                return;
            }

            Type groupClass = group.GetType();
            MethodInfo getDisplayTexts = groupClass.GetMethod("GetDisplayTexts");
            if (getDisplayTexts?.Invoke(group, null) is string[] texts)
            {
                foreach (string text in texts)
                {
                    if (!string.IsNullOrEmpty(text) && text.StartsWith(name, StringComparison.Ordinal))
                    {
                        return;
                    }
                }
            }

            ConstructorInfo constructor = sizeType.GetConstructor(new[]
            {
                sizeTypeEnum, typeof(int), typeof(int), typeof(string)
            });
            if (constructor == null)
            {
                return;
            }

            object aspectType = Enum.Parse(sizeTypeEnum, "AspectRatio");
            object newSize = constructor.Invoke(new[] { aspectType, width, height, name });
            groupClass.GetMethod("AddCustomSize")?.Invoke(group, new[] { newSize });
        }
        catch (Exception)
        {
            // Game View internals can change between Unity versions.
        }
    }
}
