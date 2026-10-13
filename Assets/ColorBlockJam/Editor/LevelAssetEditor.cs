using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ColorBlockJam.EditorTools
{
    // Level dosyasını seçince Inspector'da açılan görsel editör:
    // fırça seç → ızgarada hücrelere tıkla/sürükle → kapıları listeden ekle → "Çözülebilir mi?" → "▶ Oyna".
    [CustomEditor(typeof(LevelAsset))]
    public class LevelAssetEditor : Editor
    {
        const float Cell = 30f;
        static readonly Color EmptyColor = new(0.23f, 0.27f, 0.36f);
        static readonly Color WallColor = new(0.42f, 0.46f, 0.6f);
        static readonly Color BorderColor = new(0.14f, 0.16f, 0.22f);

        char brush = LevelDef.Empty;
        string solvedFor;
        SolveResult solve;

        LevelAsset Asset => (LevelAsset)target;
        LevelDef Def => Asset.level;

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var level = serializedObject.FindProperty("level");
            EditorGUILayout.PropertyField(level.FindPropertyRelative("timeLimit"), new GUIContent("Süre (saniye)"));
            EditorGUILayout.PropertyField(level.FindPropertyRelative("coinReward"), new GUIContent("Coin ödülü"));
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawSize();
            DrawBrushes();
            DrawGrid();

            EditorGUILayout.Space(8);
            serializedObject.Update();
            EditorGUILayout.PropertyField(level.FindPropertyRelative("blocks"), new GUIContent("Bloklar (harf → renk)"), true);
            EditorGUILayout.PropertyField(level.FindPropertyRelative("gates"), new GUIContent("Kapılar (kenar, başlangıç, uzunluk, renk)"), true);
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            DrawChecks();
        }

        // ---- boyut ----

        void DrawSize()
        {
            var rows = Def.Rows();
            int h = Mathf.Max(1, rows.Length), w = rows.Length > 0 ? rows[0].Length : 6;

            EditorGUILayout.BeginHorizontal();
            int newW = Mathf.Clamp(EditorGUILayout.IntField("Genişlik", w), 3, 12);
            int newH = Mathf.Clamp(EditorGUILayout.IntField("Yükseklik", h), 3, 14);
            EditorGUILayout.EndHorizontal();

            if (rows.Length == 0 || newW != w || newH != h) Resize(rows, newW, newH);
        }

        void Resize(string[] rows, int w, int h)
        {
            var result = new List<string>();
            for (int r = 0; r < h; r++)
            {
                // satırlar yukarıdan sayılır; yükseklik değişince alttaki satırlar korunsun
                int src = rows.Length - h + r;
                var row = src >= 0 && src < rows.Length ? rows[src] : "";
                row = row.Length >= w ? row.Substring(0, w) : row + new string(LevelDef.Empty, w - row.Length);
                result.Add(row);
            }
            SetLayout(result.ToArray(), "Level boyutu");
        }

        // ---- fırçalar ----

        void DrawBrushes()
        {
            EditorGUILayout.LabelField("Fırça", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            BrushButton(LevelDef.Empty, "Boş", EmptyColor);
            BrushButton(LevelDef.Wall, "Duvar", WallColor);
            foreach (var b in Def.blocks)
                if (!string.IsNullOrEmpty(b.id)) BrushButton(b.id[0], b.id, Palette.Of(b.color));
            if (GUILayout.Button("+ Yeni blok", GUILayout.Width(90))) AddBlock();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("Fırçayı seç, ızgaraya tıkla ya da sürükle. Aynı harf = aynı blok. " +
                                    "Kapıları aşağıdaki listeden ekle; ızgaranın çevresinde renkli görünürler.", MessageType.None);
        }

        void BrushButton(char id, string label, Color color)
        {
            var prev = GUI.backgroundColor;
            GUI.backgroundColor = brush == id ? Color.white : color;
            var style = new GUIStyle(GUI.skin.button) { fontStyle = brush == id ? FontStyle.Bold : FontStyle.Normal };
            if (GUILayout.Button(brush == id ? $"[{label}]" : label, style, GUILayout.MinWidth(40))) brush = id;
            GUI.backgroundColor = prev;
        }

        void AddBlock()
        {
            char id = 'a';
            while (Def.FindBlock(id) != null && id < 'z') id++;
            var used = new HashSet<BlockColor>();
            foreach (var b in Def.blocks) used.Add(b.color);
            var color = BlockColor.Red;
            foreach (BlockColor c in System.Enum.GetValues(typeof(BlockColor)))
                if (!used.Contains(c)) { color = c; break; }

            Undo.RecordObject(Asset, "Blok ekle");
            Def.blocks.Add(new BlockDef { id = id.ToString(), color = color });
            EditorUtility.SetDirty(Asset);
            brush = id;
        }

        // ---- ızgara ----

        void DrawGrid()
        {
            var rows = Def.Rows();
            if (rows.Length == 0) return;
            int h = rows.Length, w = rows[0].Length;

            var area = GUILayoutUtility.GetRect((w + 2) * Cell, (h + 2) * Cell, GUILayout.ExpandWidth(false));
            var e = Event.current;
            var labelStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };

            for (int gy = -1; gy <= h; gy++)
            for (int gx = -1; gx <= w; gx++)
            {
                var rect = new Rect(area.x + (gx + 1) * Cell, area.y + (h - gy) * Cell, Cell - 2, Cell - 2);
                bool border = gx < 0 || gy < 0 || gx >= w || gy >= h;
                if (border)
                {
                    var gate = GateAt(gx, gy, w, h);
                    EditorGUI.DrawRect(rect, gate != null ? Palette.Of(gate.color) : BorderColor);
                    continue;
                }

                char ch = gx < rows[h - 1 - gy].Length ? rows[h - 1 - gy][gx] : LevelDef.Empty;
                var block = ch is LevelDef.Empty or LevelDef.Wall ? null : Def.FindBlock(ch);
                var color = ch == LevelDef.Empty ? EmptyColor : ch == LevelDef.Wall ? WallColor
                    : block != null ? Palette.Of(block.color) : Color.magenta;
                EditorGUI.DrawRect(rect, color);
                if (block != null)
                    GUI.Label(rect, block.axis == MoveAxis.Both ? ch.ToString() : $"{ch}{(block.axis == MoveAxis.Horizontal ? "↔" : "↕")}", labelStyle);

                if ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && rect.Contains(e.mousePosition))
                {
                    if (ch != brush) Paint(rows, gx, h - 1 - gy);
                    e.Use();
                }
            }
        }

        void Paint(string[] rows, int x, int row)
        {
            var sb = new StringBuilder(rows[row]) { [x] = brush };
            rows[row] = sb.ToString();
            SetLayout(rows, "Level çiz");
        }

        GateData GateAt(int gx, int gy, int w, int h)
        {
            foreach (var g in Def.gates)
            {
                bool hit = g.side switch
                {
                    Side.Left => gx == -1 && gy >= g.start && gy <= g.End,
                    Side.Right => gx == w && gy >= g.start && gy <= g.End,
                    Side.Bottom => gy == -1 && gx >= g.start && gx <= g.End,
                    _ => gy == h && gx >= g.start && gx <= g.End,
                };
                if (hit) return g;
            }
            return null;
        }

        void SetLayout(string[] rows, string undoName)
        {
            Undo.RecordObject(Asset, undoName);
            Def.layout = string.Join("\n", rows);
            EditorUtility.SetDirty(Asset);
            Repaint();
        }

        // ---- kontrol ve test ----

        void DrawChecks()
        {
            var problems = Def.Validate();
            if (problems.Count > 0)
            {
                EditorGUILayout.HelpBox("Düzeltilmesi gerekenler:\n• " + string.Join("\n• ", problems), MessageType.Error);
                return;
            }

            var fingerprint = JsonUtility.ToJson(Def);
            if (solvedFor != fingerprint) solve = null;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Çözülebilir mi?", GUILayout.Height(28)))
            {
                solve = LevelSolver.Solve(Def.Build());
                solvedFor = fingerprint;
            }

            var catalog = CBJEditorTools.LoadCatalog();
            int index = catalog != null ? catalog.levels.IndexOf(Asset) : -1;
            if (index < 0)
            {
                if (catalog != null && GUILayout.Button("Oyuna ekle (kataloğun sonuna)", GUILayout.Height(28)))
                {
                    Undo.RecordObject(catalog, "Kataloğa level ekle");
                    catalog.levels.Add(Asset);
                    EditorUtility.SetDirty(catalog);
                    AssetDatabase.SaveAssets();
                }
            }
            else if (GUILayout.Button($"▶ Bu level'ı oyna (Level {index + 1})", GUILayout.Height(28)))
            {
                AssetDatabase.SaveAssets();
                PlayerPrefs.SetInt(LevelDatabase.TestLevelKey, index);
                PlayerPrefs.Save();
                EditorApplication.isPlaying = true;
            }
            EditorGUILayout.EndHorizontal();

            if (solve != null)
                EditorGUILayout.HelpBox(solve.Summary, solve.Solved ? MessageType.Info : MessageType.Warning);
        }
    }
}
