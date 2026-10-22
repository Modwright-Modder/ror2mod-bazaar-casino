using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using RoR2.UI;
using RoR2.UI.SkinControllers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BazaarCasino
{
    internal sealed partial class CasinoUI : MonoBehaviour
    {
        private static readonly Color Gold = new Color(0.95f, 0.77f, 0.32f);
        private static readonly Color Muted = new Color(0.65f, 0.69f, 0.76f);
        private RectTransform modalFrame,wagerBar;
        private TextMeshProUGUI modalTitle,modalRules;
        private GameObject canvasRoot, modal, buttonPrefab;
        private RectTransform content;
        private TextMeshProUGUI wallet, status, wagerText;
        private TMP_FontAsset font;
        private int pageIndex=3, bet = 10;
        private WorldSlotMachine activeSlotMachine;
        private int selectedItem = -1;
        private bool animating;
        private bool ready;
        private float retryInitAt;
        private string notice = "Gold wagers • gold payouts";
        private int lastWheel = -1;
        private int[] lastReels;
        private Texture2D wheelTexture;
        private Coroutine animation;
        private readonly List<AsyncOperationHandle<Sprite>> iconHandles = new List<AsyncOperationHandle<Sprite>>();
        internal bool IsOpen => modal && modal.activeSelf || IsWorldMachineFocused;
        private CasinoPlugin Plugin => CasinoPlugin.Instance;
        private CasinoState State => Plugin?.State;

        internal void Toggle() { if (IsOpen || tablePanel&&tablePanel.activeSelf) Close(); }
        internal bool Prepare()=>Init();
        private bool Init()
        {
            if (canvasRoot && ready) return true;
            if (Time.unscaledTime < retryInitAt) return false;
            retryInitAt = Time.unscaledTime + 1f;
            try { return BuildUI(); }
            catch (Exception error)
            {
                ready = false;
                if (canvasRoot) { canvasRoot.SetActive(false); Destroy(canvasRoot); }
                canvasRoot = null; modal = null; content = null;
                Debug.LogError("[BazaarCasino] UI initialization failed; discarded incomplete panel: " + error);
                return false;
            }
        }
        private bool BuildUI()
        {
            var cube = RoR2.Artifacts.CommandArtifactManager.commandCubePrefab;
            var panel = cube ? cube.GetComponent<PickupPickerController>()?.panelPrefab : null;
            var button = panel ? panel.GetComponentsInChildren<HGButton>(true).FirstOrDefault(b => b.name == "CancelButton") : null;
            if (!button) return false;
            buttonPrefab = button.gameObject;
            font = button.GetComponentInChildren<TextMeshProUGUI>(true)?.font ?? TMP_Settings.defaultFontAsset;
            canvasRoot = new GameObject("BazaarCasinoCanvas", typeof(RectTransform));
            canvasRoot.SetActive(false);
            var canvas = canvasRoot.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 90;
            var scaler = canvasRoot.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 0.5f;
            canvasRoot.AddComponent<GraphicRaycaster>();
            DontDestroyOnLoad(canvasRoot);
            modal = Rect(canvasRoot.transform, "CasinoModal").gameObject;
            Stretch((RectTransform)modal.transform);
            modal.SetActive(false);
            modal.AddComponent<Image>().color = new Color(0, 0, 0, 0.82f);
            var provider = modal.AddComponent<MPEventSystemProvider>();
            provider.fallBackToMainEventSystem = true;
            // Awake is deferred on this inactive object. Its property setter calls
            // RebuildLinks before the event-system locator exists, aborting UI creation.
            modal.AddComponent<CursorOpener>();
            var frame = modalFrame = Rect(modal.transform, "Frame");
            Anchors(frame, 0.075f, 0.07f, 0.925f, 0.93f);
            frame.gameObject.AddComponent<Image>().color = new Color(0.045f, 0.055f, 0.08f, 1);
            var title = modalTitle = Label(frame, "LUNAR BROKER", 32, Gold, TextAlignmentOptions.MidlineLeft);
            Anchors(title.rectTransform, 0.035f, 0.89f, 0.53f, 0.97f);
            wallet = Label(frame, "Gold: —", 24, Gold, TextAlignmentOptions.MidlineRight);
            Anchors(wallet.rectTransform, 0.55f, 0.90f, 0.85f, 0.96f);
            var close = Button(frame, "Close", Close); Anchors(close.transform as RectTransform, 0.87f, 0.90f, 0.97f, 0.96f);
            content = Rect(frame, "Content"); Anchors(content, 0.035f, 0.20f, 0.965f, 0.77f);
            wagerBar=Rect(frame,"SlotWagerControls");Anchors(wagerBar,.035f,.105f,.505f,.17f);
            var minus = Button(wagerBar, "−", () => ChangeBet(-10)); Anchors(minus.transform as RectTransform, 0, 0, .12f, 1);
            wagerText = Label(wagerBar, "Wager: 10", 22, Color.white); Anchors(wagerText.rectTransform, .13f, 0, .45f, 1);
            var plus = Button(wagerBar, "+", () => ChangeBet(10)); Anchors(plus.transform as RectTransform, .46f, 0, .58f, 1);
            var x10 = Button(wagerBar, "×10", () => SetBet((long)bet * 10)); Anchors(x10.transform as RectTransform, .60f, 0, .78f, 1);
            var div10 = Button(wagerBar, "÷10", () => SetBet(bet / 10)); Anchors(div10.transform as RectTransform, .80f, 0, 1, 1);
            var rules = modalRules = Label(frame, "Payouts include your wager. ESC / F6 to close.", 16, Muted, TextAlignmentOptions.MidlineRight);
            Anchors(rules.rectTransform, 0.52f, 0.10f, 0.965f, 0.18f);
            status = Label(frame, notice, 19, Color.white, TextAlignmentOptions.MidlineLeft); Anchors(status.rectTransform, 0.035f, 0.02f, 0.965f, 0.085f);
            canvasRoot.SetActive(true);
            BuildSlotOverlay();
            BuildCaseOverlay();
            ready = true;
            return true;
        }
        private void OpenFocusedPanel()
        {
            if(!CasinoPlugin.InBazaar||!CasinoPlugin.LocalMaster||!Init())return;
            Close();pageIndex=3;selectedItem=-1;
            modalTitle.text="LUNAR BROKER";
            wagerBar.gameObject.SetActive(false);modalRules.gameObject.SetActive(false);
            Anchors(modalFrame,.16f,.10f,.84f,.90f);
            Anchors(content,.035f,.09f,.965f,.86f);
            notice="Select an item to sell for gold.";
            modal.SetActive(true);Notify(notice);DrawPage();Plugin.Send(ActionKind.State);
        }
        internal void OpenBroker()=>OpenFocusedPanel();
        internal void OpenSlots(WorldSlotMachine machine)=>OpenSlotOverlay(machine);
        internal void Close()
        {
            if(IsAnyCasinoOpen)interactionGate.Close(Time.unscaledTime);
            CloseWagerDialog();
            CloseSharedTable();
            CloseSlotOverlay();
            CloseCaseOverlay();
            CloseRaceOverlay();
            activeSlotMachine=null;
            if (!modal) return;
            if (animation != null) { StopCoroutine(animation); animation = null; }
            RememberCards(State);
            if(State?.reels!=null) lastReels=State.reels.ToArray();
            if(State?.wheelNumber>=0) lastWheel=State.wheelNumber;
            animating = false; ReleaseCasinoFocus(modal); modal.SetActive(false);
        }
        private void Update()
        {
            ObserveInteractionRelease();
            if (CasinoPlugin.InBazaar && !canvasRoot) Init();
            if (canvasRoot) canvasRoot.SetActive(CasinoPlugin.InBazaar);
            UpdateSlotOverlay();
            UpdateCaseOverlay();
            UpdateRaceOverlay();
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) CloseFromEscape();
            if (wallet && !animating && !Plugin.Waiting) wallet.text = "Gold: " + (CasinoPlugin.LocalMaster ? CasinoPlugin.LocalMaster.money.ToString("N0") : "—");
        }
        internal void Notify(string message) { notice = message; if (status) status.text = message; }
        internal void Receive(CasinoState state)
        {
            if (state == null) return;
            SetWagerLimits(state.minBet,state.maxBet);
            if(IsSlotFocused){SlotResponse(state);return;}
            if(IsCaseOpen){CaseResponse(state);return;}
            {
                if (state.message != "Welcome to the Bazaar Casino." || notice == "Gold wagers • gold payouts") Notify(ResultText(state));
                RememberCards(state);
                if (IsOpen) DrawPage();
            }
        }
        internal void SetWagerLimits(int minimum,int maximum)
        {
            if(minimum<=0||maximum<minimum)return;
            if(!slotAllIn)bet=Mathf.Clamp(bet,minimum,maximum);
            int oldChip=tableBet;tableBet=Mathf.Clamp(tableBet,minimum,maximum);
            if(oldChip!=tableBet&&tablePanel&&tablePanel.activeSelf)DrawTableControls();
        }
        private string ResultText(CasinoState state)
        {
            if (state.game >= 1 && state.game <= 3 && !state.handActive)
                return state.message + "  |  Returned: " + state.payout.ToString("N0") + " gold  |  Net: " + (state.payout - state.wager).ToString("+0;-0;0");
            return state.message;
        }
        private void ChangeBet(int delta) => SetBet((long)bet + delta);
        private void SetBet(long amount)
        {
            if (animating || Plugin.Waiting || State?.handActive == true) return;
            slotAllIn=false;
            bet = (int)Math.Max(Plugin.MinimumBet, Math.Min(Plugin.MaximumBet, amount));
            if (wagerText) wagerText.text = "Wager: " + bet.ToString("N0");
            if(modal&&modal.activeSelf) DrawPage();
        }
        private void Act(ActionKind action, int choice = 0, int number = 0)
        {
            if (animating || Plugin.Waiting || action!=ActionKind.Slots&&action!=ActionKind.Sell) return;
            if(action==ActionKind.Slots&&activeSlotMachine)number=activeSlotMachine.Index+1;
            lastAction = action; Notify("Processing…"); Plugin.Send(action, bet, choice, number);
        }
        private void ClearPage()
        {
            foreach (var handle in iconHandles) if (handle.IsValid()) Addressables.Release(handle);
            iconHandles.Clear();
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
        }
        private void DrawPage()
        {
            if (!content) return;
            ClearPage();
            if (wagerText) wagerText.text = "Wager: " + bet.ToString("N0");
            DrawSales();
        }
        private void DrawSales()
        {
            var caption = Label(content, "Sell one permanent item at a time. Select an item, then confirm the sale.", 19, Muted);
            Anchors(caption.rectTransform, 0, 0.88f, 1, 1);
            var scrollRoot = Rect(content, "SellScroll"); Anchors(scrollRoot, 0, 0.18f, 1, 0.85f);
            var scroll = scrollRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false;
            scrollRoot.gameObject.AddComponent<Image>().color = new Color(0.03f, 0.035f, 0.05f, 0.4f);
            scrollRoot.gameObject.AddComponent<RectMask2D>();
            var list = Rect(scrollRoot, "Items"); list.anchorMin = new Vector2(0, 1); list.anchorMax = new Vector2(1, 1); list.pivot = new Vector2(0.5f, 1); list.sizeDelta = Vector2.zero;
            var layout = list.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 6; layout.childForceExpandHeight = false; layout.childControlHeight = true;
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = list; scroll.viewport = scrollRoot;
            var items = State?.items;
            if (items == null || items.Length == 0)
            {
                var empty = Label(list, "No sellable permanent items in your inventory.", 22, Muted); empty.gameObject.AddComponent<LayoutElement>().preferredHeight = 60;
            }
            else foreach (var item in items)
            {
                var def = ItemCatalog.GetItemDef((ItemIndex)item.index); if (!def) continue;
                int index = item.index;
                var button = Button(list, "", () => { selectedItem = index; DrawPage(); });
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 54;
                if (selectedItem == index) button.GetComponent<TableButtonStyle>().SetPrimary(true);
                var iconRect = Rect(button.transform, "ItemIcon"); Anchors(iconRect, 0.012f, 0.1f, 0.062f, 0.9f);
                var image = iconRect.gameObject.AddComponent<Image>(); image.raycastTarget = false;
                #pragma warning disable CS0618 // Modded items may still provide a direct sprite.
                image.sprite = def.pickupIconSprite; image.enabled = image.sprite;
                #pragma warning restore CS0618
                if (!image.sprite && def.pickupIconSpriteReference != null && def.pickupIconSpriteReference.RuntimeKeyIsValid())
                {
                    var handle = Addressables.LoadAssetAsync<Sprite>(def.pickupIconSpriteReference.RuntimeKey); iconHandles.Add(handle);
                    handle.Completed += h => { if (image && h.Status == AsyncOperationStatus.Succeeded) { image.sprite = h.Result; image.enabled = true; } };
                }
                var name = Label(button.transform, Language.GetString(def.nameToken) + "  ×" + item.count, 20, Color.white, TextAlignmentOptions.MidlineLeft);
                Anchors(name.rectTransform, 0.08f, 0, 0.76f, 1);
                var price = Label(button.transform, "+" + item.price.ToString("N0") + " gold", 20, Gold, TextAlignmentOptions.MidlineRight);
                Anchors(price.rectTransform, 0.77f, 0, 0.98f, 1);
                var tooltip = button.gameObject.AddComponent<TooltipProvider>(); tooltip.titleToken = def.nameToken; tooltip.bodyToken = def.descriptionToken;
            }
            var selected = items?.FirstOrDefault(i => i.index == selectedItem);
            var sell = PlaceButton(selected == null ? "Select an item" : "Sell 1 for " + selected.price.ToString("N0") + " gold", 0.23f, 0, 0.77f, 0.15f,
                () => { if (selectedItem >= 0) Act(ActionKind.Sell, selectedItem); });
            sell.interactable = selected != null;
        }
        private Texture2D MakeWheel()
        {
            var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            var pixels = new Color[256 * 256];
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float dx = x - 127.5f, dy = y - 127.5f, radius = Mathf.Sqrt(dx * dx + dy * dy);
                float angle = Mathf.Atan2(dx, dy) + Mathf.PI / 37;
                if (angle < 0) angle += Mathf.PI * 2;
                int segment = Mathf.FloorToInt(angle * 37 / (Mathf.PI * 2)) % 37;
                int n = Games.Wheel[segment];
                Color color = n == 0 ? new Color(0.12f, 0.40f, 0.28f) : Games.IsRed(n) ? new Color(0.65f, 0.10f, 0.17f) : new Color(0.09f, 0.11f, 0.16f);
                pixels[y * 256 + x] = radius > 127 ? Color.clear : radius > 123 || radius > 74 && radius < 78 ? Gold : radius < 74 ? new Color(0.055f, 0.065f, 0.09f) : color;
            }
            texture.SetPixels(pixels); texture.Apply(); return texture;
        }
        private MPButton PlaceButton(string text, float x0, float y0, float x1, float y1, UnityEngine.Events.UnityAction action)
        {
            var button = Button(content, text, action); Anchors(button.transform as RectTransform, x0, y0, x1, y1);
            button.interactable = !animating && !Plugin.Waiting; return button;
        }
        private MPButton Button(Transform parent, string text, UnityEngine.Events.UnityAction action)
        {
            var rect=Rect(parent,"CasinoButton");
            var image=rect.gameObject.AddComponent<Image>();image.color=new Color(.065f,.105f,.14f,.94f);
            var button=rect.gameObject.AddComponent<MPButton>();button.targetGraphic=image;button.onClick.AddListener(action);
            var label=Label(rect,text,21,Color.white);Anchors(label.rectTransform,.055f,.06f,.945f,.94f);
            label.enableAutoSizing=true;label.fontSizeMin=14;label.fontSizeMax=22;
            TableButtonStyle.Apply(button,image,label,false);
            return button;
        }
        private RectTransform Rect(Transform parent, string name)
        {
            var obj = new GameObject(name, typeof(RectTransform)); obj.transform.SetParent(parent, false); return (RectTransform)obj.transform;
        }
        private TextMeshProUGUI Label(Transform parent, string text, float size, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center)
        {
            var label = Rect(parent, "Label").gameObject.AddComponent<TextMeshProUGUI>();
            CasinoText.Bind(label);label.font = font; label.text = text; label.fontSize = size; label.color = color; label.alignment = alignment;
            label.raycastTarget = false; label.enableWordWrapping = false; label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
        private static void Anchors(RectTransform rect, float x0, float y0, float x1, float y1)
        { rect.anchorMin = new Vector2(x0, y0); rect.anchorMax = new Vector2(x1, y1); rect.offsetMin = rect.offsetMax = Vector2.zero; }
        private static void Stretch(RectTransform rect) => Anchors(rect, 0, 0, 1, 1);
        private void OnDestroy()
        {
            art.Dispose(); Close(); CaseArtwork.Release(); foreach (var handle in iconHandles) if (handle.IsValid()) Addressables.Release(handle);
            if (canvasRoot) Destroy(canvasRoot); if (wheelTexture) Destroy(wheelTexture);
            TableButtonStyle.Release();
        }
    }
}
