using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace ColorBlockJam.EditorTools
{
    [InitializeOnLoad]
    static class CBJEditorTools
    {
        const string Root = "Assets/ColorBlockJam";
        const string ResourcesDir = Root + "/Resources";
        const string LevelsDir = Root + "/Levels";
        const string MaterialPath = ResourcesDir + "/CBJ_Base.mat";
        public const string CatalogPath = ResourcesDir + "/LevelCatalog.asset";

        static CBJEditorTools() => EditorApplication.delayCall += () =>
        {
            EnsureBaseMaterial();
            EnsureLevelCatalog();
        };

        // Projedeki render pipeline'a (URP veya Built-in) uygun temel materyali bir kere oluşturur.
        static void EnsureBaseMaterial()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) != null) return;

            var shader = GraphicsSettings.currentRenderPipeline != null
                ? Shader.Find("Universal Render Pipeline/Lit")
                : null;
            if (shader == null) shader = Shader.Find("Standard");

            EnsureFolder(ResourcesDir);
            AssetDatabase.CreateAsset(new Material(shader) { name = "CBJ_Base" }, MaterialPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ColorBlockJam] Temel materyal oluşturuldu: {MaterialPath} ({shader.name})");
        }

        // İlk açılışta koddaki 20 level'ı ayrı dosyalara çevirir ve kataloğa dizer.
        // Bundan sonra level'lar sadece bu dosyalardan düzenlenir.
        static void EnsureLevelCatalog()
        {
            if (AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath) != null) return;

            EnsureFolder(LevelsDir);
            EnsureFolder(ResourcesDir);
            var catalog = ScriptableObject.CreateInstance<LevelCatalog>();

            var defs = DefaultLevels.All();
            for (int i = 0; i < defs.Count; i++)
            {
                var asset = ScriptableObject.CreateInstance<LevelAsset>();
                asset.level = defs[i];
                AssetDatabase.CreateAsset(asset, $"{LevelsDir}/Level_{i + 1:00}.asset");
                catalog.levels.Add(asset);
            }

            AssetDatabase.CreateAsset(catalog, CatalogPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[ColorBlockJam] {defs.Count} level dosyası oluşturuldu: {LevelsDir}");
        }

        public static LevelCatalog LoadCatalog() => AssetDatabase.LoadAssetAtPath<LevelCatalog>(CatalogPath);

        [MenuItem("Color Block Jam/Yeni level oluştur", priority = 0)]
        static void CreateLevel()
        {
            EnsureFolder(LevelsDir);
            var catalog = LoadCatalog();
            int number = catalog != null ? catalog.levels.Count + 1 : 1;

            var asset = ScriptableObject.CreateInstance<LevelAsset>();
            asset.level = LevelDef.Create("......", "......", "......", "......", "......", "......").Time(90).Coins(20);
            var path = AssetDatabase.GenerateUniqueAssetPath($"{LevelsDir}/Level_{number:00}.asset");
            AssetDatabase.CreateAsset(asset, path);

            if (catalog != null)
            {
                Undo.RecordObject(catalog, "Kataloğa level ekle");
                catalog.levels.Add(asset);
                EditorUtility.SetDirty(catalog);
            }
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
        }

        [MenuItem("Color Block Jam/Level sırasını göster (katalog)", priority = 1)]
        static void ShowCatalog() => Selection.activeObject = LoadCatalog();

        [MenuItem("Color Block Jam/İlerlemeyi sıfırla (Level 1, 0 coin)", priority = 20)]
        static void ResetProgress()
        {
            PuzzleKit.Progress.Reset();
            Debug.Log("[ColorBlockJam] İlerleme sıfırlandı.");
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}
