using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;
using UnityEditor.Build;

public class IOSAppIconGenerator : EditorWindow
{
    private Texture2D sourceTexture;
    private string outputPath = "Assets/Art/Icons/iOS/Generated";
    private bool overwriteMarketingIcon = true;

    [MenuItem("Tools/GreyboxProtoType/Generate iOS Icons...")]
    public static void ShowWindow()
    {
        GetWindow<IOSAppIconGenerator>("iOS Icon Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Source Settings", EditorStyles.boldLabel);
        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source Texture", sourceTexture, typeof(Texture2D), false);

        if (GUILayout.Button("Pick Master PNG from Disk"))
        {
            string path = EditorUtility.OpenFilePanel("Select Master PNG", "", "png");
            if (!string.IsNullOrEmpty(path))
            {
                byte[] fileData = File.ReadAllBytes(path);
                sourceTexture = new Texture2D(2, 2);
                sourceTexture.LoadImage(fileData);
            }
        }

        EditorGUILayout.Space();
        GUILayout.Label("Output Settings", EditorStyles.boldLabel);
        outputPath = EditorGUILayout.TextField("Output Folder", outputPath);
        overwriteMarketingIcon = EditorGUILayout.Toggle("Overwrite 1024 Marketing Icon", overwriteMarketingIcon);

        EditorGUILayout.Space();

        if (GUILayout.Button("Generate and Assign Icons", GUILayout.Height(40)))
        {
            ExecuteGeneration();
        }
    }

    private void ExecuteGeneration()
    {
        if (sourceTexture == null)
        {
            EditorUtility.DisplayDialog("Error", "Please select a source texture first.", "OK");
            return;
        }

        if (sourceTexture.width < 512 || sourceTexture.height < 512 || sourceTexture.width != sourceTexture.height)
        {
            EditorUtility.DisplayDialog("Error", "Source must be square and at least 512x512.", "OK");
            return;
        }

        if (!Directory.Exists(outputPath))
        {
            Directory.CreateDirectory(outputPath);
        }

        // Fetch all icon kinds supported by the iOS platform dynamically
        PlatformIconKind[] kinds = PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.iOS);
        NamedBuildTarget target = NamedBuildTarget.iOS;

        try
        {
            AssetDatabase.StartAssetEditing();

            foreach (var kind in kinds)
            {
                PlatformIcon[] icons = PlayerSettings.GetPlatformIcons(target, kind);
                if (icons == null || icons.Length == 0) continue;

                // Version-agnostic way to identify the Marketing icon:
                // Check if the kind name string contains "Marketing" OR if any icon in the slot is 1024x1024
                string kindString = kind.ToString();
                bool isMarketing = kindString.Contains("Marketing") || (icons.Length > 0 && icons[0].width == 1024);

                if (isMarketing && !overwriteMarketingIcon) continue;

                for (int i = 0; i < icons.Length; i++)
                {
                    int w = icons[i].width;
                    int h = icons[i].height;
                    
                    // Use kindString to ensure unique filenames
                    string fileName = $"ios_icon_{w}x{h}_{kindString}_{i}.png";
                    string fullPath = Path.Combine(outputPath, fileName);

                    // Resize and Save
                    Texture2D resized = ResizeTexture(sourceTexture, w, h);
                    byte[] bytes = resized.EncodeToPNG();
                    File.WriteAllBytes(fullPath, bytes);
                    DestroyImmediate(resized);

                    // Import to AssetDB
                    AssetDatabase.ImportAsset(fullPath);
                    
                    // Set Import Settings
                    TextureImporter importer = AssetImporter.GetAtPath(fullPath) as TextureImporter;
                    if (importer != null)
                    {
                        importer.textureType = TextureImporterType.Default;
                        
                        // Apple Requirement: 1024px icon cannot have alpha
                        if (isMarketing)
                        {
                            importer.alphaSource = TextureImporterAlphaSource.None;
                            importer.alphaIsTransparency = false;
                        }
                        else
                        {
                            importer.alphaSource = TextureImporterAlphaSource.FromInput;
                            importer.alphaIsTransparency = true; 
                        }

                        importer.mipmapEnabled = false;
                        importer.SaveAndReimport();
                    }

                    // Load and Assign
                    Texture2D asset = AssetDatabase.LoadAssetAtPath<Texture2D>(fullPath);
                    icons[i].SetTexture(asset);
                }

                // Apply back to PlayerSettings
                PlayerSettings.SetPlatformIcons(target, kind, icons);
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("Success", "iOS Icons generated and assigned to Player Settings.", "OK");
    }

    private Texture2D ResizeTexture(Texture2D source, int width, int height)
    {
        RenderTexture rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        rt.filterMode = FilterMode.Trilinear;

        RenderTexture.active = rt;
        Graphics.Blit(source, rt);

        Texture2D result = new Texture2D(width, height, TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(0, 0, width, height), 0, 0);
        result.Apply();

        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}