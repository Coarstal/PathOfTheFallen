using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Jotunn.Managers;

namespace CoarstalsRPClasses
{
    public static class RPClassSelectionUI
    {
        private class CardWidgets
        {
            public RPClassType ClassType;
            public Image OuterBorder;
            public Image CardBg;
            public Text TitleText;
            public Text RoleText;
            public Text StatsText;
            public GameObject CurrentBadge;
        }

        private static GameObject s_canvasRootObj;
        private static Canvas s_dedicatedCanvas;
        private static CanvasScaler s_canvasScaler;
        private static GameObject s_rootOverlay;
        private static RectTransform s_mainPanelRt;
        private static Text s_headerTitle;
        private static Text s_headerSubtitle;
        private static Button s_confirmButton;
        private static Text s_confirmButtonText;
        private static Button s_cancelButton;
        private static Text s_cancelButtonText;

        private static readonly List<CardWidgets> s_cards = new List<CardWidgets>();

        private static bool s_isOpen;
        private static bool s_isRespecFromScroll;
        private static bool s_allowCancel;
        private static RPClassType s_selectedClass = RPClassType.Warrior;

        public static bool IsOpen
        {
            get { return s_isOpen && s_rootOverlay != null && s_rootOverlay.activeSelf; }
        }

        public static bool IsMandatoryInitialChoiceActive
        {
            get { return IsOpen && !s_allowCancel; }
        }

        public static void Open(bool isRespecFromScroll, bool forceAllowCancel)
        {
            if (GUIManager.IsHeadless() || GUIManager.Instance == null || GUIManager.CustomGUIFront == null)
            {
                return;
            }

            EnsureBuilt();
            if (s_rootOverlay == null) return;

            s_isRespecFromScroll = isRespecFromScroll;
            s_allowCancel = isRespecFromScroll || forceAllowCancel;

            Player local = Player.m_localPlayer;
            RPClassType current = local != null ? RPPlayerClassManager.GetPlayerClass(local) : RPClassType.None;

            // Default selection: first available class different from current (for respec), or Warrior initially
            if (current == RPClassType.None)
            {
                s_selectedClass = RPClassType.Warrior;
            }
            else if (isRespecFromScroll)
            {
                s_selectedClass = (current == RPClassType.Warrior) ? RPClassType.Knight : RPClassType.Warrior;
            }
            else
            {
                s_selectedClass = current;
            }

            RefreshLocalizedTexts(current);
            UpdateSelectionVisuals(current);

            // Hide inventory if opened by right-clicking scroll so UI is clean
            if (InventoryGui.IsVisible() && InventoryGui.instance != null)
            {
                InventoryGui.instance.Hide();
            }

            s_canvasRootObj.SetActive(true);
            s_rootOverlay.SetActive(true);
            s_isOpen = true;
            Canvas.ForceUpdateCanvases();

            GUIManager.BlockInput(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public static void Close()
        {
            if (s_rootOverlay != null)
            {
                s_rootOverlay.SetActive(false);
            }
            if (s_canvasRootObj != null)
            {
                s_canvasRootObj.SetActive(false);
            }
            if (s_isOpen)
            {
                s_isOpen = false;
                GUIManager.BlockInput(false);
            }
        }

        public static void DestroyUI()
        {
            if (s_isOpen)
            {
                GUIManager.BlockInput(false);
                s_isOpen = false;
            }
            if (s_canvasRootObj != null)
            {
                UnityEngine.Object.Destroy(s_canvasRootObj);
                s_canvasRootObj = null;
                s_dedicatedCanvas = null;
                s_canvasScaler = null;
                s_rootOverlay = null;
                s_mainPanelRt = null;
            }
            s_cards.Clear();
        }

        public static void UpdateTick()
        {
            if (!IsOpen) return;

            // Keep cursor unlocked while choosing class
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Allow ESC to cancel ONLY when cancel is permitted (e.g. Hildir Scroll or Admin UI)
            if (s_allowCancel && Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
            }
        }

        private static string Loc(string token)
        {
            if (Localization.instance != null) return Localization.instance.Localize(token);
            return token;
        }

        private static void EnsureBuilt()
        {
            if (s_rootOverlay != null) return;

            // Create our OWN top-level ScreenSpaceOverlay Canvas with CanvasScaler.ScaleMode.ScaleWithScreenSize (1920x1080)!
            // Unlike Transform.localScale (which blurs fonts by upscaling low-DPI glyph quads),
            // CanvasScaler sets Canvas.scaleFactor so Unity rasterizes EVERY font glyph & 9-slice border at true native 4K/2K DPI!
            s_canvasRootObj = new GameObject("Coarstal_RPClassNativeCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            UnityEngine.Object.DontDestroyOnLoad(s_canvasRootObj);

            // Match UI layer of Jotunn CustomGUIFront so Valheim camera/raycaster rules work identically
            if (GUIManager.CustomGUIFront != null)
            {
                s_canvasRootObj.layer = GUIManager.CustomGUIFront.layer;
            }

            s_dedicatedCanvas = s_canvasRootObj.GetComponent<Canvas>();
            s_dedicatedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            s_dedicatedCanvas.pixelPerfect = true;
            s_dedicatedCanvas.sortingOrder = 2500;

            s_canvasScaler = s_canvasRootObj.GetComponent<CanvasScaler>();
            s_canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s_canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
            s_canvasScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Shrink;

            // Full-screen dim backdrop
            s_rootOverlay = new GameObject("Coarstal_RPClassSelectionOverlay", typeof(RectTransform), typeof(Image));
            s_rootOverlay.transform.SetParent(s_canvasRootObj.transform, false);
            RectTransform overlayRt = s_rootOverlay.GetComponent<RectTransform>();
            overlayRt.anchorMin = Vector2.zero;
            overlayRt.anchorMax = Vector2.one;
            overlayRt.offsetMin = Vector2.zero;
            overlayRt.offsetMax = Vector2.zero;
            Image dimImg = s_rootOverlay.GetComponent<Image>();
            dimImg.color = new Color(0.03f, 0.02f, 0.02f, 0.84f);

            // Main Valheim Woodpanel (1300 x 964 at 1080p reference, dynamically scaled for 2K/4K!)
            GameObject mainPanel = GUIManager.Instance.CreateWoodpanel(
                s_rootOverlay.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                1300f,
                964f,
                false);
            s_mainPanelRt = mainPanel.GetComponent<RectTransform>();

            Font norseBold = GUIManager.Instance.NorseBold != null ? GUIManager.Instance.NorseBold : GUIManager.Instance.AveriaSerifBold;
            Font averia = GUIManager.Instance.AveriaSerif != null ? GUIManager.Instance.AveriaSerif : Resources.GetBuiltinResource<Font>("Arial.ttf");
            Font averiaBold = GUIManager.Instance.AveriaSerifBold != null ? GUIManager.Instance.AveriaSerifBold : averia;

            // Header Title
            GameObject titleObj = GUIManager.Instance.CreateText(
                "",
                mainPanel.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -38f),
                norseBold,
                34,
                new Color(1.0f, 0.82f, 0.36f, 1f),
                true,
                Color.black,
                1080f,
                46f,
                false);
            s_headerTitle = titleObj.GetComponent<Text>();
            s_headerTitle.alignment = TextAnchor.MiddleCenter;

            // Subtitle
            GameObject subObj = GUIManager.Instance.CreateText(
                "",
                mainPanel.transform,
                new Vector2(0.5f, 1f),
                new Vector2(0.5f, 1f),
                new Vector2(0f, -74f),
                averia,
                16,
                new Color(0.88f, 0.84f, 0.76f, 1f),
                true,
                Color.black,
                1080f,
                30f,
                false);
            s_headerSubtitle = subObj.GetComponent<Text>();
            s_headerSubtitle.alignment = TextAnchor.MiddleCenter;

            // 6 Class Cards in a 3x2 Grid
            s_cards.Clear();
            RPClassType[] classes = new RPClassType[]
            {
                RPClassType.Warrior,
                RPClassType.Knight,
                RPClassType.Thief,
                RPClassType.Hunter,
                RPClassType.Sorcerer,
                RPClassType.Beggar
            };

            float cardW = 402f;
            float cardH = 386f;
            float gapX = 16f;
            float gapY = 14f;

            for (int i = 0; i < classes.Length; i++)
            {
                int col = i % 3;
                int row = i / 3;

                float posX = (col - 1) * (cardW + gapX);
                float posY = (row == 0) ? (cardH * 0.5f + gapY * 0.5f - 8f) : (-cardH * 0.5f - gapY * 0.5f - 8f);

                CardWidgets cw = CreateClassCard(mainPanel.transform, classes[i], new Vector2(posX, posY), cardW, cardH, averia, averiaBold);
                s_cards.Add(cw);
            }

            // Confirm Button
            GameObject confirmObj = GUIManager.Instance.CreateButton(
                "Confirm",
                mainPanel.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(0f, 38f),
                280f,
                46f);
            s_confirmButton = confirmObj.GetComponent<Button>();
            s_confirmButtonText = confirmObj.GetComponentInChildren<Text>();
            s_confirmButton.onClick.AddListener(OnConfirmClicked);

            // Cancel Button (only visible during Hildir Scroll / Admin open)
            GameObject cancelObj = GUIManager.Instance.CreateButton(
                "Cancel",
                mainPanel.transform,
                new Vector2(0.5f, 0f),
                new Vector2(0.5f, 0f),
                new Vector2(180f, 38f),
                180f,
                46f);
            s_cancelButton = cancelObj.GetComponent<Button>();
            s_cancelButtonText = cancelObj.GetComponentInChildren<Text>();
            s_cancelButton.onClick.AddListener(Close);

            s_rootOverlay.SetActive(false);
            s_canvasRootObj.SetActive(false);
        }

        private static CardWidgets CreateClassCard(
            Transform parent,
            RPClassType cls,
            Vector2 centerOffset,
            float width,
            float height,
            Font bodyFont,
            Font boldFont)
        {
            CardWidgets w = new CardWidgets();
            w.ClassType = cls;

            // Outer Gold/Accent Selection Frame
            GameObject outer = new GameObject("Card_" + cls, typeof(RectTransform), typeof(Image), typeof(Button));
            outer.transform.SetParent(parent, false);
            RectTransform outerRt = outer.GetComponent<RectTransform>();
            outerRt.anchorMin = new Vector2(0.5f, 0.5f);
            outerRt.anchorMax = new Vector2(0.5f, 0.5f);
            outerRt.anchoredPosition = centerOffset;
            outerRt.sizeDelta = new Vector2(width, height);
            w.OuterBorder = outer.GetComponent<Image>();

            // Inner Dark Card Body
            GameObject inner = new GameObject("InnerBg", typeof(RectTransform), typeof(Image));
            inner.transform.SetParent(outer.transform, false);
            RectTransform innerRt = inner.GetComponent<RectTransform>();
            innerRt.anchorMin = Vector2.zero;
            innerRt.anchorMax = Vector2.one;
            innerRt.offsetMin = new Vector2(3f, 3f);
            innerRt.offsetMax = new Vector2(-3f, -3f);
            w.CardBg = inner.GetComponent<Image>();

            // Class Emblem Icon (top-left)
            GameObject iconObj = new GameObject("Emblem", typeof(RectTransform), typeof(Image));
            iconObj.transform.SetParent(inner.transform, false);
            RectTransform iconRt = iconObj.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0f, 1f);
            iconRt.anchorMax = new Vector2(0f, 1f);
            iconRt.pivot = new Vector2(0f, 1f);
            iconRt.anchoredPosition = new Vector2(12f, -10f);
            iconRt.sizeDelta = new Vector2(52f, 52f);
            Image iconImg = iconObj.GetComponent<Image>();
            iconImg.sprite = RPIcons.GetClassIcon(cls);
            iconImg.preserveAspect = true;

            // Class Name
            GameObject nameObj = CreateRawText(inner.transform, boldFont, 21, RPClassConfig.GetClassColor(cls), TextAnchor.UpperLeft);
            RectTransform nameRt = nameObj.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0f, 1f);
            nameRt.offsetMin = new Vector2(72f, -38f);
            nameRt.offsetMax = new Vector2(-10f, -10f);
            w.TitleText = nameObj.GetComponent<Text>();

            // Role Subtitle
            GameObject roleObj = CreateRawText(inner.transform, bodyFont, 13, new Color(0.80f, 0.76f, 0.68f, 1f), TextAnchor.UpperLeft);
            RectTransform roleRt = roleObj.GetComponent<RectTransform>();
            roleRt.anchorMin = new Vector2(0f, 1f);
            roleRt.anchorMax = new Vector2(1f, 1f);
            roleRt.pivot = new Vector2(0f, 1f);
            roleRt.offsetMin = new Vector2(72f, -60f);
            roleRt.offsetMax = new Vector2(-10f, -38f);
            w.RoleText = roleObj.GetComponent<Text>();

            // "Current" Badge (top-right)
            GameObject badgeObj = new GameObject("CurrentBadge", typeof(RectTransform), typeof(Image));
            badgeObj.transform.SetParent(inner.transform, false);
            RectTransform badgeRt = badgeObj.GetComponent<RectTransform>();
            badgeRt.anchorMin = new Vector2(1f, 1f);
            badgeRt.anchorMax = new Vector2(1f, 1f);
            badgeRt.pivot = new Vector2(1f, 1f);
            badgeRt.anchoredPosition = new Vector2(-10f, -10f);
            badgeRt.sizeDelta = new Vector2(78f, 22f);
            badgeObj.GetComponent<Image>().color = new Color(0.22f, 0.44f, 0.22f, 0.92f);
            GameObject badgeTxtObj = CreateRawText(badgeObj.transform, boldFont, 12, new Color(0.75f, 1f, 0.75f, 1f), TextAnchor.MiddleCenter);
            RectTransform btRt = badgeTxtObj.GetComponent<RectTransform>();
            btRt.anchorMin = Vector2.zero;
            btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero;
            btRt.offsetMax = Vector2.zero;
            w.CurrentBadge = badgeObj;

            // Divider line
            GameObject div = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            div.transform.SetParent(inner.transform, false);
            RectTransform divRt = div.GetComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0f, 1f);
            divRt.anchorMax = new Vector2(1f, 1f);
            divRt.pivot = new Vector2(0.5f, 1f);
            divRt.offsetMin = new Vector2(12f, -68f);
            divRt.offsetMax = new Vector2(-12f, -67f);
            div.GetComponent<Image>().color = new Color(0.65f, 0.52f, 0.28f, 0.45f);

            // Bonuses & Debuffs Rich Text Body
            GameObject statsObj = CreateRawText(inner.transform, bodyFont, 12, new Color(0.92f, 0.90f, 0.86f, 1f), TextAnchor.UpperLeft);
            RectTransform statsRt = statsObj.GetComponent<RectTransform>();
            statsRt.anchorMin = Vector2.zero;
            statsRt.anchorMax = Vector2.one;
            statsRt.offsetMin = new Vector2(12f, 7f);
            statsRt.offsetMax = new Vector2(-10f, -70f);
            w.StatsText = statsObj.GetComponent<Text>();
            w.StatsText.supportRichText = true;
            w.StatsText.lineSpacing = 1.0f;
            w.StatsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            w.StatsText.verticalOverflow = VerticalWrapMode.Truncate;
            w.StatsText.resizeTextForBestFit = true;
            w.StatsText.resizeTextMinSize = 10;
            w.StatsText.resizeTextMaxSize = 12;

            RPClassType captured = cls;
            Button btn = outer.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(delegate
            {
                s_selectedClass = captured;
                Player p = Player.m_localPlayer;
                RPClassType cur = p != null ? RPPlayerClassManager.GetPlayerClass(p) : RPClassType.None;
                UpdateSelectionVisuals(cur);
            });

            return w;
        }

        private static GameObject CreateRawText(Transform parent, Font font, int fontSize, Color col, TextAnchor anchor)
        {
            GameObject go = new GameObject("Txt", typeof(RectTransform), typeof(Text), typeof(Outline));
            go.transform.SetParent(parent, false);
            Text txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = fontSize;
            txt.color = col;
            txt.alignment = anchor;
            txt.raycastTarget = false;
            Outline ol = go.GetComponent<Outline>();
            ol.effectColor = new Color(0f, 0f, 0f, 0.85f);
            ol.effectDistance = new Vector2(1f, -1f);
            return go;
        }

        private static void RefreshLocalizedTexts(RPClassType currentClass)
        {
            s_headerTitle.text = Loc(s_isRespecFromScroll ? "$rp_ui_title_respec" : "$rp_ui_title_initial");
            s_headerSubtitle.text = Loc(s_isRespecFromScroll ? "$rp_ui_subtitle_respec" : "$rp_ui_subtitle_initial");
            s_confirmButtonText.text = Loc("$rp_ui_confirm");
            s_cancelButtonText.text = Loc("$rp_ui_cancel");

            // Position Confirm/Cancel buttons cleanly
            RectTransform confirmRt = s_confirmButton.GetComponent<RectTransform>();
            if (s_allowCancel)
            {
                s_cancelButton.gameObject.SetActive(true);
                confirmRt.anchoredPosition = new Vector2(-105f, 36f);
                s_cancelButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(145f, 36f);
            }
            else
            {
                s_cancelButton.gameObject.SetActive(false);
                confirmRt.anchoredPosition = new Vector2(0f, 36f);
            }

            for (int i = 0; i < s_cards.Count; i++)
            {
                CardWidgets cw = s_cards[i];
                cw.TitleText.text = Loc(RPClassConfig.GetClassToken(cw.ClassType));
                cw.RoleText.text = Loc(RPClassConfig.GetClassRoleToken(cw.ClassType));
                cw.StatsText.text = RPClassConfig.BuildClassTooltip(cw.ClassType, includeLore: false);

                bool isCur = (currentClass != RPClassType.None && cw.ClassType == currentClass);
                cw.CurrentBadge.SetActive(isCur);
                if (isCur)
                {
                    Text badgeTxt = cw.CurrentBadge.GetComponentInChildren<Text>();
                    if (badgeTxt != null) badgeTxt.text = Loc("$rp_ui_current");
                }
            }
        }

        private static void UpdateSelectionVisuals(RPClassType currentClass)
        {
            for (int i = 0; i < s_cards.Count; i++)
            {
                CardWidgets cw = s_cards[i];
                bool selected = (cw.ClassType == s_selectedClass);
                Color clsCol = RPClassConfig.GetClassColor(cw.ClassType);

                if (selected)
                {
                    cw.OuterBorder.color = Color.Lerp(clsCol, new Color(1f, 0.88f, 0.42f, 1f), 0.45f);
                    cw.CardBg.color = new Color(0.18f, 0.14f, 0.11f, 0.97f);
                }
                else
                {
                    cw.OuterBorder.color = new Color(0.36f, 0.29f, 0.20f, 0.72f);
                    cw.CardBg.color = new Color(0.09f, 0.07f, 0.06f, 0.94f);
                }
            }

            // Prevent accidentally burning a 1000-gold Rebirth Scroll on the exact same class already active!
            bool sameAsCurrentOnScroll = s_isRespecFromScroll && (currentClass == s_selectedClass);
            s_confirmButton.interactable = !sameAsCurrentOnScroll;
        }

        private static void OnConfirmClicked()
        {
            Player player = Player.m_localPlayer;
            if (player == null || s_selectedClass == RPClassType.None)
            {
                return;
            }

            if (s_isRespecFromScroll)
            {
                // Verify and consume 1 Scroll of Rebirth from player's inventory ONLY on confirmation!
                Inventory inv = player.GetInventory();
                if (inv == null)
                {
                    Close();
                    return;
                }

                ItemDrop.ItemData scrollItem = FindRebirthScrollInInventory(inv);
                if (scrollItem == null)
                {
                    player.Message(MessageHud.MessageType.Center, "Свиток Перерождения не найден в инвентаре!");
                    Close();
                    return;
                }

                inv.RemoveOneItem(scrollItem);
                RPPlayerClassManager.SetPlayerClass(player, s_selectedClass, refillVitals: true, showBanner: false);

                string className = Loc(RPClassConfig.GetClassToken(s_selectedClass));
                string msg = Loc("$msg_rp_rebirth_used") + "\n" + string.Format(Loc("$msg_rp_class_chosen"), className);
                player.Message(MessageHud.MessageType.Center, msg, 0, RPIcons.GetClassIcon(s_selectedClass));
            }
            else
            {
                RPPlayerClassManager.SetPlayerClass(player, s_selectedClass, refillVitals: true, showBanner: true);
            }

            Close();
        }

        private static ItemDrop.ItemData FindRebirthScrollInInventory(Inventory inv)
        {
            List<ItemDrop.ItemData> all = inv.GetAllItems();
            for (int i = 0; i < all.Count; i++)
            {
                ItemDrop.ItemData it = all[i];
                if (it != null && it.m_shared != null && it.m_shared.m_name == "$item_rp_rebirth_scroll")
                {
                    return it;
                }
            }
            return null;
        }
    }
}
