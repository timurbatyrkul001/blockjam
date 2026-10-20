using System;
using UnityEngine;
using UnityEngine.UI;

namespace FruitMerge
{
    // Ana menü: BÖLÜMLER (harita + 30 bölüm) ve KLASİK (rekor modu). Yerleşim modes_design/main_menu_layout.png.
    public partial class MergeHud
    {
        public event Action LevelsPressed, ClassicPressed;

        GameObject menuPanel;
        Text menuLevelsInfo, menuClassicInfo;

        public bool MenuVisible => menuPanel != null && menuPanel.activeSelf;

        public void ShowMenu(int classicBest)
        {
            menuLevelsInfo.text = $"{LevelLibrary.Count} bölüm • {LevelProgress.TotalStars} yıldız";
            menuClassicInfo.text = classicBest > 0 ? $"Rekor: {Fmt(classicBest)}" : "Sınırsız hamle • Rekorunu kır";
            ApplyAudioSettings();
            menuPanel.SetActive(true);
            menuPanel.transform.SetAsLastSibling();
        }

        public void HideMenu() => menuPanel.SetActive(false);

        void BuildMenu()
        {
            var panel = MakeImage("Menu", transform, FruitArt.Hex("#CFEFD8"));
            Stretch(panel.rectTransform);
            menuPanel = panel.gameObject;
            var bg = FruitArt.Load("Art/background");
            if (bg != null) { panel.sprite = bg; panel.color = Color.white; }

            // oyundaki bahçe ve ağaç (aynı parçalar ve ölçüler); kartlar önünde
            BuildBackScenery(panel.transform);
            var treeSprite = FruitArt.Load("Art/tree");
            if (treeSprite != null)
            {
                var tree = MakeImage("Tree", panel.transform, Color.white);
                tree.sprite = treeSprite;
                tree.raycastTarget = false;
                float aspect = treeSprite.rect.width / treeSprite.rect.height;
                Place(tree.rectTransform, new Vector2(0.5f, 0), new Vector2(0, TreeBottom), new Vector2(TreeWidth, TreeWidth / aspect), new Vector2(0.5f, 0));
            }
            var front = BuildFrontScenery(panel.transform);
            Decor(front, "Art/basket", 775, 365, 290, 225, 1f);

            var safe = MakeRect("MenuSafe", panel.transform);
            Stretch(safe);
            safe.gameObject.AddComponent<SafeArea>();

            var titleCard = MakePanel("Title", safe, panelSprite);
            Place(titleCard.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -330), new Vector2(800, 300), new Vector2(0.5f, 1));
            var title = MakeLabel("Text", titleCard.transform, 96, TextAnchor.MiddleCenter, false, Weight.Bold);
            title.text = "FRUIT MERGE";
            title.color = FruitArt.DarkText;
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 34), new Vector2(760, 120));
            var tagline = MakeLabel("Tagline", titleCard.transform, 40, TextAnchor.MiddleCenter, false, Weight.SemiBold);
            tagline.text = "Bahçende birleştir!";
            tagline.color = FruitArt.Hex("#8A5A2B");
            Place(tagline.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -66), new Vector2(760, 56));

            menuLevelsInfo = MenuCard(safe, "Levels", "BÖLÜMLER", 1, -820, () => LevelsPressed?.Invoke());
            menuClassicInfo = MenuCard(safe, "Classic", "KLASİK", FruitChain.MaxTier, -1180, () => ClassicPressed?.Invoke());

            // menüde de ayrı müzik/ses düğmeleri (oyundakilerle aynı ayarlar)
            var music = MakeButton("Music", safe, roundButton, FruitArt.Hex("#8BC34A"), ToggleMusic);
            Place(music, new Vector2(1, 0), new Vector2(-170, 66), new Vector2(110, 110), new Vector2(1, 0));
            AddAudioButton(music, "Art/icon_music", "M", isMusic: true);
            var sound = MakeButton("Sound", safe, roundButton, FruitArt.Hex("#8BC34A"), ToggleSound);
            Place(sound, new Vector2(1, 0), new Vector2(-40, 66), new Vector2(110, 110), new Vector2(1, 0));
            AddAudioButton(sound, "Art/icon_sound", "S", isMusic: false);

            menuPanel.SetActive(false);
        }

        // Büyük seçenek kartı: meyve ikonu, başlık, alt bilgi; kartın tamamı dokunulabilir.
        Text MenuCard(RectTransform parent, string name, string title, int tier, float y, Action onClick)
        {
            var card = MakeButton(name, parent, panelSprite, FruitArt.Cream, onClick);
            Place(card, new Vector2(0.5f, 1), new Vector2(0, y), new Vector2(820, 300), new Vector2(0.5f, 1));
            var icon = MakeImage("Fruit", card, Color.white);
            icon.raycastTarget = false;
            Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(150, 0), new Vector2(170, 170));
            SetFruitImage(icon, tier);
            var label = MakeLabel("Title", card, 72, TextAnchor.MiddleLeft, false, Weight.Bold);
            label.text = title;
            label.color = FruitArt.DarkText;
            Place(label.rectTransform, new Vector2(0, 0.5f), new Vector2(530, 38), new Vector2(520, 90));
            var info = MakeLabel("Info", card, 36, TextAnchor.MiddleLeft, false, Weight.SemiBold);
            info.color = FruitArt.Hex("#8A5A2B");
            Place(info.rectTransform, new Vector2(0, 0.5f), new Vector2(530, -48), new Vector2(520, 50));
            return info;
        }
    }
}
