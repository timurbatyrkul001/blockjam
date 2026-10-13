using ColorBlockJam;
using UnityEngine;

namespace BentoBlast
{
    // Bento Blast'in 3D parçaları. Color Block'un ortak görsellerini kullanır:
    // Shapes3D (küp), Palette (renkler), FoodModels (meyveler).
    public static class BentoVisuals
    {
        public const float CellSize = 0.9f;
        const float FloorHeight = 0.1f;
        const float RimHeight = 0.34f;
        const float RimWidth = 0.09f;
        const float FruitSize = 0.58f;

        // Board'un merkezi (0,0,0); hücre (0,0) sol-alt köşe.
        public static Vector3 CellToWorld(Vector2Int c) =>
            new(c.x - (BentoBoard.Size - 1) * 0.5f, 0f, c.y - (BentoBoard.Size - 1) * 0.5f);

        public static Vector2Int WorldToCell(Vector3 p) =>
            new(Mathf.RoundToInt(p.x + (BentoBoard.Size - 1) * 0.5f), Mathf.RoundToInt(p.z + (BentoBoard.Size - 1) * 0.5f));

        // Tek hücrelik meyveli kap: krem zemin, 4 renkli çeper, ortada meyve.
        // withCollider: parmakla tutulabilsin diye zemine collider bırakılır.
        public static GameObject Tray(Transform parent, Vector3 localPos, BlockColor color, bool withCollider, string name = "Tray")
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = localPos;

            var rim = Palette.Of(color);
            var floor = Color.Lerp(Palette.TrayInner, rim, 0.15f);
            float half = CellSize * 0.5f;

            Shapes3D.Cube(root, Vector3.up * (FloorHeight * 0.5f), new Vector3(CellSize, FloorHeight, CellSize), floor,
                keepCollider: withCollider, smoothness: 0.3f, name: "Floor");
            Shapes3D.Cube(root, new Vector3(0, RimHeight * 0.5f, half - RimWidth * 0.5f), new Vector3(CellSize, RimHeight, RimWidth), rim, smoothness: 0.7f, name: "Rim");
            Shapes3D.Cube(root, new Vector3(0, RimHeight * 0.5f, -half + RimWidth * 0.5f), new Vector3(CellSize, RimHeight, RimWidth), rim, smoothness: 0.7f, name: "Rim");
            Shapes3D.Cube(root, new Vector3(half - RimWidth * 0.5f, RimHeight * 0.5f, 0), new Vector3(RimWidth, RimHeight, CellSize), rim, smoothness: 0.7f, name: "Rim");
            Shapes3D.Cube(root, new Vector3(-half + RimWidth * 0.5f, RimHeight * 0.5f, 0), new Vector3(RimWidth, RimHeight, CellSize), rim, smoothness: 0.7f, name: "Rim");

            float yaw = (localPos.x * 73f + localPos.z * 151f) % 360f;
            var fruit = FoodModels.Spawn(color, root, Vector3.up * FloorHeight, FruitSize, yaw);
            if (fruit != null) fruit.name = "Fruit";
            return root.gameObject;
        }

        // Kabın içindeki meyve (animasyonlar için); model yüklenemediyse null.
        public static Transform FruitOf(Transform tray) => tray != null ? tray.Find("Fruit") : null;

        // Parçanın görüntüsü: kök = parçanın (0,0) hücresi. Hücreler 1 birim aralıklı.
        public static GameObject PieceObject(Transform parent, Piece piece)
        {
            var root = new GameObject($"Piece_{piece.Color}").transform;
            root.SetParent(parent, false);
            foreach (var c in piece.Cells)
                Tray(root, new Vector3(c.x, 0f, c.y), piece.Color, withCollider: true, name: "Cell");
            return root.gameObject;
        }
    }
}
