using UnityEngine;
using UnityEngine.Experimental.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Editor-time builder for Texture2DArray assets.
///
/// Usage:
///   1. Right-click in Project window -> Create -> Rendering -> Texture2DArray Builder.
///   2. Drop your textures into the list (all must share size, format and mip count).
///   3. It auto-rebuilds on change (or press "Rebuild Array").
///   4. The generated Texture2DArray is stored as a sub-asset of this builder —
///      expand the asset in the Project window and drag the array into your
///      material's Texture2DArray slot. References survive rebuilds.
///
/// Source texture requirements:
///   - Read/Write enabled in the importer (the builder can fix this for you).
///   - Same width/height/format/mips across all slices — use identical importer
///     settings (or a Preset) for all of them.
///   - Crunch compression must be OFF (crunched data can't be read back).
/// </summary>
[CreateAssetMenu(fileName = "TextureArrayBuilder", menuName = "Rendering/Texture2DArray Builder")]
public class TextureArrayBuilder : ScriptableObject
{
    [Tooltip("Slices of the array, in order. All must share size, format and mip count.")]
    public Texture2D[] textures;

    [Tooltip("Rebuild automatically whenever this asset is edited.")]
    public bool autoRebuild = true;

    [Tooltip("Automatically enable Read/Write on source textures that need it.")]
    public bool autoFixReadable = true;

    [Tooltip("The generated array (sub-asset). Drag this into your material.")]
    public Texture2DArray generatedArray;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!autoRebuild) return;
        // Defer: asset database operations aren't allowed inside OnValidate.
        EditorApplication.delayCall += () =>
        {
            if (this != null) Build(logErrors: false);
        };
    }

    public void Build(bool logErrors = true)
    {
        if (textures == null || textures.Length == 0) return;

        var first = textures[0];
        if (first == null) return;

        int width = first.width;
        int height = first.height;
        int mipCount = first.mipmapCount;
        GraphicsFormat format = first.graphicsFormat; // encodes sRGB vs linear too

        // Validate + make readable.
        for (int i = 0; i < textures.Length; i++)
        {
            var t = textures[i];
            if (t == null) { if (logErrors) Debug.LogError($"[{name}] Slice {i} is empty.", this); return; }

            if (t.width != width || t.height != height || t.graphicsFormat != format || t.mipmapCount != mipCount)
            {
                if (logErrors)
                    Debug.LogError(
                        $"[{name}] '{t.name}' ({t.width}x{t.height}, {t.graphicsFormat}, {t.mipmapCount} mips) " +
                        $"doesn't match '{first.name}' ({width}x{height}, {format}, {mipCount} mips). " +
                        "Give all source textures identical import settings.", this);
                return;
            }

            if (!t.isReadable && !TryMakeReadable(t, logErrors)) return;
        }

        // Build the array with the exact same GraphicsFormat as the sources,
        // so compressed formats (DXT/BC/ASTC...) are carried over untouched.
        var flags = mipCount > 1 ? TextureCreationFlags.MipChain : TextureCreationFlags.None;
        var array = new Texture2DArray(width, height, textures.Length, format, flags)
        {
            name = name + "_Array",
            wrapMode = first.wrapMode,
            filterMode = first.filterMode,
            anisoLevel = first.anisoLevel
        };

        // CPU-side copy so the data actually serializes into the asset.
        // (Graphics.CopyTexture is GPU-only and would save an empty asset.)
        for (int slice = 0; slice < textures.Length; slice++)
            for (int mip = 0; mip < mipCount; mip++)
                array.SetPixelData(textures[slice].GetPixelData<byte>(mip), mip, slice);

        // Keep readable (makeNoLongerReadable must stay false or the asset saves empty).
        array.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        // Persist as a sub-asset. On rebuild, copy over the existing sub-asset
        // in place so anything referencing it (materials!) keeps working.
        string path = AssetDatabase.GetAssetPath(this);
        if (generatedArray != null && AssetDatabase.GetAssetPath(generatedArray) == path)
        {
            EditorUtility.CopySerialized(array, generatedArray);
            DestroyImmediate(array);
        }
        else
        {
            AssetDatabase.AddObjectToAsset(array, this);
            generatedArray = array;
            EditorUtility.SetDirty(this);
        }

        AssetDatabase.SaveAssets();
    }

    bool TryMakeReadable(Texture2D t, bool logErrors)
    {
        if (!autoFixReadable)
        {
            if (logErrors) Debug.LogError($"[{name}] '{t.name}' needs Read/Write enabled in its import settings.", this);
            return false;
        }

        var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(t)) as TextureImporter;
        if (importer == null) return false;

        Debug.Log($"[{name}] Enabling Read/Write on '{t.name}'.", t);
        importer.isReadable = true;
        importer.SaveAndReimport();
        return true;
    }
#endif
}

#if UNITY_EDITOR
[CustomEditor(typeof(TextureArrayBuilder))]
class TextureArrayBuilderEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var builder = (TextureArrayBuilder)target;

        GUILayout.Space(8);
        if (GUILayout.Button("Rebuild Array", GUILayout.Height(28)))
            builder.Build();

        if (builder.generatedArray != null)
        {
            var a = builder.generatedArray;
            EditorGUILayout.HelpBox(
                $"{a.width}x{a.height}, {a.depth} slices, {a.graphicsFormat}, {a.mipmapCount} mips.\n" +
                "Drag 'generatedArray' (or the sub-asset in the Project window) into your material.",
                MessageType.Info);
        }
    }
}
#endif

//This script was written by Claude.