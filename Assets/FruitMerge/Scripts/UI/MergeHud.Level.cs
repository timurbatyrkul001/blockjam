using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FruitMerge
{
    // Bölüm modu: üstte bölüm + hamle kartları ve hedefler, ipucu şeridi, sıradaki meyve, kazanma/kaybetme kartı.
    // Yerleşim level_design mockup'ları + Codex'in yazı ölçüleri (1080x2340): krem panelde koyu kahve yazı,
    // renkli butonda krem yazı ve hafif gölge.
    public partial class MergeHud
    {
        public event Action NextPressed;

        const int MaxGoals = 3;
        const float ResultTop = 330f, ResultRow = 104f, ResultBottom = 340f;

        GameObject levelHud, instructionPanel, nextBagPanel, resultPanel, hintPanel;
        Text levelNumber, levelTitle, hintText, movesText, nextBagCount;
        RectTransform movesRect, goalsCard, resultCard;
        Image nextBagIcon;
        readonly Image[] goalIcons = new Image[MaxGoals];
        readonly Text[] goalCounts = new Text[MaxGoals];
        readonly Image[] goalChecks = new Image[MaxGoals];
        readonly RectTransform[] goalCells = new RectTransform[MaxGoals];

        // sonuç kartı
        Text resultTitle, resultSubtitle, resultPrimaryLabel, resultSecondaryLabel, resultFooter;
        Image resultIcon;
        RectTransform resultMap;
        readonly Image[] resultStars = new Image[3];
        readonly Image[] resultGoalIcons = new Image[MaxGoals];
        readonly Text[] resultGoalTexts = new Text[MaxGoals];
        readonly RectTransform[] resultGoalRows = new RectTransform[MaxGoals];
        RectTransform resultPrimary, resultSecondary;
        bool resultIsWin;

        public RectTransform GoalCell(int i) => goalCells[Mathf.Clamp(i, 0, MaxGoals - 1)];

        // ---- mod ----

        public void SetMode(bool level)
        {
            levelMode = level;
            pauseExitLabel.text = level ? "HARİTA" : "ANA MENÜ";
            foreach (var go in endlessOnly) go.SetActive(!level);
            levelHud.SetActive(level);
            Basket.gameObject.SetActive(true);
        }

        public void SetLevel(LevelDef d)
        {
            levelNumber.text = d.id.ToString();
            levelTitle.text = d.title;
            hintText.text = d.hint;
            hintPanel.SetActive(!string.IsNullOrEmpty(d.hint));
            int n = Mathf.Min(MaxGoals, d.goals.Length);
            // hedefler kartın içinde ortalanır
            for (int i = 0; i < MaxGoals; i++)
            {
                goalCells[i].gameObject.SetActive(i < n);
                if (i >= n) continue;
                SetFruitImage(goalIcons[i], d.goals[i].tier);
                goalCells[i].anchoredPosition = new Vector2((i - (n - 1) * 0.5f) * 185f, -22f);
            }
            bool tutorial = d.bag.Length == 0 && d.moves <= 0;
            instructionPanel.SetActive(tutorial);
            nextBagPanel.SetActive(d.bag.Length > 0);
            Basket.gameObject.SetActive(d.baskets > 0 || d.stones > 0);
        }

        public void SetGoals(LevelDef d, int[] have)
        {
            for (int i = 0; i < Mathf.Min(MaxGoals, d.goals.Length); i++)
            {
                int need = d.goals[i].count, got = Mathf.Min(have[i], need);
                goalCounts[i].text = $"{got}/{need}";
                goalChecks[i].gameObject.SetActive(got >= need);
            }
        }

        public void PunchGoal(int i) => StartCoroutine(Punch(goalCells[i], 0.4f, 0.3f));

        public void SetMoves(int left)
        {
            movesRect.gameObject.SetActive(left >= 0);
            if (left < 0) return;
            movesText.text = left.ToString();
            movesText.color = left <= 3 ? FruitArt.Hex("#D84315") : FruitArt.DarkText;
        }

        public void PunchMoves() => StartCoroutine(Punch(movesRect, 0.25f, 0.12f));

        public void SetNextBag(int tier, int left)
        {
            nextBagPanel.SetActive(left > 0);
            if (left <= 0) return;
            SetFruitImage(nextBagIcon, tier);
            nextBagCount.text = left > 1 ? $"+{left - 1}" : "";
        }

        // ---- sonuç kartı ----

        public void ShowWin(LevelDef d, int stars, int[] have, bool hasNext)
        {
            resultIsWin = true;
            resultTitle.text = stars >= 3 ? "HARİKA!" : stars == 2 ? "ÇOK İYİ!" : "TAMAM!";
            resultSubtitle.text = $"BÖLÜM {d.id} TAMAMLANDI";
            resultIcon.gameObject.SetActive(false);
            int n = Mathf.Min(MaxGoals, d.goals.Length);
            for (int i = 0; i < MaxGoals; i++)
            {
                bool on = i < n;
                resultGoalRows[i].gameObject.SetActive(on);
                if (!on) continue;
                SetFruitImage(resultGoalIcons[i], d.goals[i].tier);
                resultGoalTexts[i].text = $"{FruitNameTr(d.goals[i].tier)}   {Mathf.Min(have[i], d.goals[i].count)}/{d.goals[i].count}";
            }
            resultPrimaryLabel.text = hasNext ? "SONRAKİ BÖLÜM" : "HARİTAYA DÖN";
            resultSecondaryLabel.text = "TEKRAR OYNA";
            resultFooter.text = $"{stars} BAHÇE YILDIZI";
            OpenResult(n);
            StartCoroutine(StarsIn(stars));
        }

        public void ShowLose(LevelDef d, string reason)
        {
            resultIsWin = false;
            resultTitle.text = reason;
            resultSubtitle.text = $"BÖLÜM {d.id}";
            var sad = FruitArt.Load("Art/sad_fruit");
            resultIcon.gameObject.SetActive(true);
            if (sad != null) { resultIcon.sprite = sad; resultIcon.color = Color.white; resultIcon.preserveAspect = true; }
            else SetFruitImage(resultIcon, 1);
            foreach (var s in resultStars) s.gameObject.SetActive(false);
            foreach (var r in resultGoalRows) r.gameObject.SetActive(false);
            resultPrimaryLabel.text = "TEKRAR DENE";
            resultSecondaryLabel.text = "HARİTA";
            resultFooter.text = "ACELE YOK, YENİDEN DENE";
            OpenResult(0);
        }

        public void HideResult() => resultPanel.SetActive(false);

        // Kart yüksekliği içeriğe göre: üst (yıldız/ikon + başlık + açıklama) + hedef satırları + alt (yazı + butonlar).
        void OpenResult(int rows)
        {
            resultCard.sizeDelta = new Vector2(860, ResultTop + rows * ResultRow + ResultBottom + (rows > 0 ? 10 : 0));
            // kaybedince tek ikincil buton (HARİTA) ortada; kazanınca TEKRAR OYNA + HARİTA yan yana
            resultMap.gameObject.SetActive(resultIsWin);
            resultSecondary.anchorMin = resultSecondary.anchorMax = new Vector2(resultIsWin ? 0.3f : 0.5f, 0);
            resultPanel.SetActive(true);
            resultPanel.transform.SetAsLastSibling();
            StartCoroutine(PopIn(resultCard));
        }

        IEnumerator StarsIn(int stars)
        {
            var full = FruitArt.Load("Art/star_full");
            var empty = FruitArt.Load("Art/star_empty");
            for (int i = 0; i < 3; i++)
            {
                var s = resultStars[i];
                s.gameObject.SetActive(true);
                s.sprite = empty != null ? empty : FruitArt.Star();
                s.color = empty != null ? Color.white : FruitArt.Hex("#D9C9A8");
                s.rectTransform.localScale = Vector3.one;
            }
            yield return new WaitForSecondsRealtime(0.35f);
            for (int i = 0; i < stars; i++)
            {
                var s = resultStars[i];
                s.sprite = full != null ? full : FruitArt.Star();
                s.color = full != null ? Color.white : FruitArt.Gold;
                Sfx.Play("order", 0.7f, 0f, 0f, 1f + i * 0.12f);
                Haptics.Light();
                yield return Punch(s.rectTransform, 0.3f, 0.45f);
            }
        }

        static string FruitNameTr(int tier) => tier switch
        {
            0 => "KİRAZ", 1 => "ÇİLEK", 2 => "ÜZÜM", 3 => "PORTAKAL",
            4 => "ELMA", 5 => "ŞEFTALİ", 6 => "ARMUT", MergeTree.StoneTier => "TAŞ", _ => "KARPUZ",
        };

        // ---- kurulum ----

        // Krem panel üstünde koyu kahve yazı (gölgesiz).
        Text PanelText(string name, Transform parent, int size, Weight weight, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = MakeLabel(name, parent, size, align, shadow: false, weight);
            t.color = FruitArt.DarkText;
            return t;
        }

        void BuildLevelHud()
        {
            var root = MakeRect("LevelHud", Root);
            Stretch(root);
            levelHud = root.gameObject;

            // bölüm kartı
            var badge = MakePanel("LevelBadge", root, panelSprite);
            Place(badge.rectTransform, new Vector2(0, 1), new Vector2(36, -40), new Vector2(200, 170), new Vector2(0, 1));
            var cap = PanelText("Caption", badge.transform, 30, Weight.SemiBold);
            cap.text = "BÖLÜM";
            cap.color = FruitArt.Hex("#8A5A2B");
            Place(cap.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(180, 40));
            levelNumber = PanelText("Number", badge.transform, 76, Weight.Bold);
            Place(levelNumber.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(180, 90));

            // hamle kartı: bölüm kartıyla aynı genişlik ve sütunda, hemen altında
            var moves = MakePanel("Moves", root, panelSprite);
            movesRect = moves.rectTransform;
            Place(movesRect, new Vector2(0, 1), new Vector2(36, -222), new Vector2(200, 170), new Vector2(0, 1));
            var movesCap = PanelText("Caption", movesRect, 30, Weight.SemiBold);
            movesCap.text = "HAMLE";
            movesCap.color = FruitArt.Hex("#8A5A2B");
            Place(movesCap.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(180, 40));
            movesText = PanelText("Value", movesRect, 72, Weight.Bold);
            Place(movesText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(180, 90));

            // hedefler
            var goals = MakePanel("Goals", root, panelSprite);
            goalsCard = goals.rectTransform;
            Place(goalsCard, new Vector2(0, 1), new Vector2(256, -40), new Vector2(620, 170), new Vector2(0, 1));
            levelTitle = PanelText("Title", goalsCard, 34, Weight.SemiBold);
            levelTitle.color = FruitArt.Hex("#8A5A2B");
            Place(levelTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -44), new Vector2(580, 44));
            for (int i = 0; i < MaxGoals; i++)
            {
                var cell = MakeRect($"Goal{i}", goalsCard);
                Place(cell, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(175, 90));
                goalCells[i] = cell;
                goalIcons[i] = MakeImage("Icon", cell, Color.white);
                goalIcons[i].raycastTarget = false;
                Place(goalIcons[i].rectTransform, new Vector2(0, 0.5f), new Vector2(38, 0), new Vector2(72, 72));
                goalCounts[i] = PanelText("Count", cell, 44, Weight.Bold, TextAnchor.MiddleLeft);
                Place(goalCounts[i].rectTransform, new Vector2(0, 0.5f), new Vector2(132, 0), new Vector2(100, 56));
                goalChecks[i] = MakeCheck(goalIcons[i].rectTransform);
            }

            // ipucu: gökyüzünde kaybolmasın diye krem şerit üstünde koyu kahve
            var hint = MakePanel("Hint", root, panelSprite);
            hintPanel = hint.gameObject;
            Place(hint.rectTransform, new Vector2(0, 1), new Vector2(256, -222), new Vector2(788, 96), new Vector2(0, 1));
            hintText = PanelText("Text", hint.transform, 34, Weight.SemiBold);
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintText.resizeTextForBestFit = true;
            hintText.resizeTextMinSize = 24;
            hintText.resizeTextMaxSize = 34;
            Place(hintText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(740, 70));

            // alt: öğretici yazı (torbasız bölümler) ya da sıradaki meyve (torbalı bölümler)
            var instr = MakePanel("Instruction", root, panelSprite);
            instructionPanel = instr.gameObject;
            Place(instr.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(720, 150));
            var instrText = PanelText("Text", instr.transform, 34, Weight.SemiBold);
            instrText.text = "AYNI İKİ MEYVEYİ BİRLEŞTİR";
            Place(instrText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, 18), new Vector2(680, 48));
            var noTimer = PanelText("NoTimer", instr.transform, 26, Weight.Medium);
            noTimer.text = "BU BÖLÜMDE SÜRE SINIRI YOK";
            noTimer.color = FruitArt.Hex("#8A5A2B");
            Place(noTimer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0, -28), new Vector2(680, 36));

            var next = MakePanel("NextBag", root, panelSprite);
            nextBagPanel = next.gameObject;
            Place(next.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 250), new Vector2(460, 130));
            var nextCap = PanelText("Caption", next.transform, 34, Weight.SemiBold, TextAnchor.MiddleLeft);
            nextCap.text = "SIRADAKİ";
            Place(nextCap.rectTransform, new Vector2(0, 0.5f), new Vector2(140, 0), new Vector2(220, 48));
            nextBagIcon = MakeImage("Icon", next.transform, Color.white);
            nextBagIcon.raycastTarget = false;
            Place(nextBagIcon.rectTransform, new Vector2(1, 0.5f), new Vector2(-150, 0), new Vector2(90, 90));
            nextBagCount = PanelText("Count", next.transform, 34, Weight.Bold, TextAnchor.MiddleLeft);
            Place(nextBagCount.rectTransform, new Vector2(1, 0.5f), new Vector2(-50, 0), new Vector2(90, 48));

            levelHud.SetActive(false);
        }

        // Yeşil "tamam" işareti (Codex'in check_green.png'si; yoksa yeşil daire + OK).
        Image MakeCheck(RectTransform parent)
        {
            var sprite = FruitArt.Load("Art/check_green");
            var check = MakeImage("Check", parent, sprite != null ? Color.white : FruitArt.Hex("#43A047"));
            check.sprite = sprite != null ? sprite : FruitArt.Circle();
            check.raycastTarget = false;
            check.preserveAspect = true;
            Place(check.rectTransform, new Vector2(1, 0), new Vector2(4, 4), new Vector2(44, 44));
            if (sprite == null)
            {
                var ok = MakeLabel("Ok", check.transform, 20, TextAnchor.MiddleCenter);
                ok.text = "OK";
                Stretch(ok.rectTransform);
            }
            check.gameObject.SetActive(false);
            return check;
        }

        void BuildResult()
        {
            var dim = MakeImage("Result", transform, new Color(0, 0, 0, 0.45f));
            Stretch(dim.rectTransform);
            resultPanel = dim.gameObject;

            var card = MakePanel("Card", dim.transform, panelSprite);
            resultCard = card.rectTransform;
            Place(resultCard, new Vector2(0.5f, 0.5f), new Vector2(0, 20), new Vector2(860, 900));

            // üst: yıldızlar (kartın üst kenarına taşar) ya da üzgün meyve
            for (int i = 0; i < 3; i++)
            {
                var s = MakeImage($"Star{i}", resultCard, Color.white);
                s.raycastTarget = false;
                s.preserveAspect = true;
                bool mid = i == 1;
                Place(s.rectTransform, new Vector2(0.5f, 1), new Vector2((i - 1) * 200f, mid ? 50f : 15f), mid ? new Vector2(200, 200) : new Vector2(155, 155));
                resultStars[i] = s;
            }
            resultIcon = MakeImage("Icon", resultCard, Color.white);
            resultIcon.raycastTarget = false;
            Place(resultIcon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 60), new Vector2(200, 200));

            // başlık Codex'in kurdelesinin üstünde
            var ribbonSprite = FruitArt.Load("Art/win_ribbon");
            if (ribbonSprite != null)
            {
                var ribbon = MakeImage("Ribbon", resultCard, Color.white);
                ribbon.sprite = ribbonSprite;
                ribbon.raycastTarget = false;
                ribbon.preserveAspect = true;
                Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(940, 230));
            }
            resultTitle = MakeLabel("Title", resultCard, 64, TextAnchor.MiddleCenter, shadow: false, Weight.Bold);
            resultTitle.color = ribbonSprite != null ? FruitArt.CreamText : FruitArt.DarkText;
            if (ribbonSprite != null)
            {
                var outline = resultTitle.gameObject.AddComponent<Outline>();   // büyük başlıkta kahve kontur
                outline.effectColor = FruitArt.Outline;
                outline.effectDistance = new Vector2(3, -3);
            }
            resultTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            resultTitle.resizeTextForBestFit = true;
            resultTitle.resizeTextMinSize = 40;
            resultTitle.resizeTextMaxSize = 64;
            Place(resultTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(560, 90));
            resultSubtitle = PanelText("Subtitle", resultCard, 36, Weight.SemiBold);
            Place(resultSubtitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -268), new Vector2(800, 50));

            for (int i = 0; i < MaxGoals; i++)
            {
                var row = MakeImage($"Row{i}", resultCard, new Color(1f, 1f, 1f, 0.6f));
                row.sprite = FruitArt.Load("UI/panel");
                row.type = row.sprite != null && row.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
                row.raycastTarget = false;
                resultGoalRows[i] = row.rectTransform;
                Place(row.rectTransform, new Vector2(0.5f, 1), new Vector2(0, -ResultTop - i * ResultRow - 42), new Vector2(680, 90));
                resultGoalIcons[i] = MakeImage("Icon", row.transform, Color.white);
                resultGoalIcons[i].raycastTarget = false;
                Place(resultGoalIcons[i].rectTransform, new Vector2(0, 0.5f), new Vector2(62, 0), new Vector2(72, 72));
                resultGoalTexts[i] = PanelText("Text", row.transform, 36, Weight.SemiBold, TextAnchor.MiddleLeft);
                Place(resultGoalTexts[i].rectTransform, new Vector2(0, 0.5f), new Vector2(370, 0), new Vector2(460, 50));
                var check = MakeCheck(row.rectTransform);
                check.rectTransform.anchorMin = check.rectTransform.anchorMax = new Vector2(1, 0.5f);
                check.rectTransform.anchoredPosition = new Vector2(-50, 0);
                check.rectTransform.sizeDelta = new Vector2(56, 56);
                check.gameObject.SetActive(true);
            }

            // alt kısım karta göre aşağıdan yerleşir; kart boyu değişince boşluk oluşmaz
            resultFooter = PanelText("Footer", resultCard, 30, Weight.Medium);
            resultFooter.color = FruitArt.Hex("#8A5A2B");
            Place(resultFooter.rectTransform, new Vector2(0.5f, 0), new Vector2(0, 290), new Vector2(800, 44));

            resultPrimary = MakeButton("Primary", resultCard, greenButton, FruitArt.Hex("#35C46A"), () =>
            {
                if (resultIsWin) NextPressed?.Invoke();
                else RestartPressed?.Invoke();
            });
            Place(resultPrimary, new Vector2(0.5f, 0), new Vector2(0, 175), new Vector2(620, 150));
            resultPrimaryLabel = ButtonLabel(resultPrimary, "SONRAKİ BÖLÜM", 44);

            resultSecondary = MakeButton("Secondary", resultCard, null, Color.clear, () =>
            {
                if (resultIsWin) RestartPressed?.Invoke();
                else MapPressed?.Invoke();
            });
            resultSecondary.GetComponent<Image>().color = new Color(1, 1, 1, 0.001f);
            Place(resultSecondary, new Vector2(0.3f, 0), new Vector2(0, 58), new Vector2(340, 74));
            resultSecondaryLabel = ButtonLabel(resultSecondary, "TEKRAR OYNA", 34, FruitArt.DarkText);

            resultMap = MakeButton("Map", resultCard, null, Color.clear, () => MapPressed?.Invoke());
            resultMap.GetComponent<Image>().color = new Color(1, 1, 1, 0.001f);
            Place(resultMap, new Vector2(0.72f, 0), new Vector2(0, 58), new Vector2(300, 74));
            ButtonLabel(resultMap, "HARİTA", 34, FruitArt.DarkText);

            resultPanel.SetActive(false);
        }
    }
}
