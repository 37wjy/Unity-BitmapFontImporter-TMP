
using UnityEngine;
using UnityEditor;
using System.IO;



namespace Mx
{
    public class BFMenuTool
    {

        [MenuItem("Tools/Bitmap Font/Rebuild Bitmap Font")]
        public static void RebuildFont()
        {
            TextAsset selected = Selection.activeObject as TextAsset;
            BFImporter.DoImportBitmapFont(AssetDatabase.GetAssetPath(selected));
        }

        // [MenuItem("Tools/Bitmap Font/Rebuild All Bitmap Font", true)]
        // public static bool CheckRebuildAllFont()
        // {
        //     return !EditorApplication.isPlaying;
        // }

        [MenuItem("Tools/Bitmap Font/Rebuild All Bitmap Font TMP")]
        public static void RebuildAllFont()
        {
            string dataPath = Application.dataPath;
            string[] files = Directory.GetFiles(Application.dataPath, "*.fnt", SearchOption.AllDirectories);
            foreach (var path in files)
            {                
                var f = Path.GetRelativePath(dataPath, path);
                UnityEngine.Debug.Log($">>>>> {f}");
                BFImporter.DoImportBitmapFont(Path.Combine("Assets", f));
            }
        }
    }
}


