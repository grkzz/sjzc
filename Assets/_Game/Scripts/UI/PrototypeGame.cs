using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ShenYi.Prototype
{
    public sealed partial class PrototypeGame : MonoBehaviour
    {
        const float Width = 1080, Height = 1920;
        static readonly Color Ink = new Color(.07f, .08f, .15f, .95f);
        static readonly Color Lime = new Color(.82f, .95f, .26f);
        static readonly Color Violet = new Color(.68f, .52f, 1f);
        static readonly Color Muted = new Color(.68f, .71f, .81f);
        RectTransform root, overlay, handRoot;
        Font font;
        Texture2D boardArt, arenaArt, eggArt, monsterArt;
        Texture2D eggDualFace, monsterDualFace, monsterGrassFace;
        PerspectiveBoard perspective;
        Text status, opponentLabel, playerLabel, questLabel, hint, currency, turnLabel, detail;
        Button endButton, questButton, skillButton;
        readonly Button[,] monsterButtons = new Button[2, 5], eggButtons = new Button[2, 5];
        readonly Text[,] monsterTexts = new Text[2, 5], eggTexts = new Text[2, 5];
        readonly Text[,] monsterNames = new Text[2, 5];
        readonly RawImage[,] monsterImages = new RawImage[2, 5], eggImages = new RawImage[2, 5];
        readonly Dictionary<int, Button> handButtons = new Dictionary<int, Button>();
        PrototypeData data;
        public BattleModel Battle { get; private set; }
        int selectedCard = -1, selectedEgg = -1, selectedMonster = -1, inspected = -1;
        bool questTarget, busy, rewarded;
        string actionHint = "将手牌中的蛋向上拖到己方空蛋位，或点选后点击空位。";
        Coroutine ai;
        int coins, wins;
        StandaloneInputModule testPointerModule;
        PrototypePointerTestInput testPointerInput;
        public bool AutoSmokeTest;
        public string ScreenshotPath;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Application.runInBackground = true;
            Screen.orientation = ScreenOrientation.Portrait;
            font = Resources.Load<Font>("Prototype/PrototypeChinese");
            if (font == null) font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei", "Arial Unicode MS", "Arial" }, 24);
            boardArt = Resources.Load<Texture2D>("Prototype/Board");
            arenaArt = Resources.Load<Texture2D>("Prototype/ArenaForeground");
            eggArt = Resources.Load<Texture2D>("Prototype/CardFaces/EggFire");
            eggDualFace = Resources.Load<Texture2D>("Prototype/CardFaces/EggDual");
            monsterArt = Resources.Load<Texture2D>("Prototype/CardFaces/MonsterFire");
            monsterDualFace = Resources.Load<Texture2D>("Prototype/CardFaces/MonsterDual");
            monsterGrassFace = Resources.Load<Texture2D>("Prototype/CardFaces/MonsterGrass");
            var config = Resources.Load<TextAsset>("Prototype/PrototypeData");
            data = JsonUtility.FromJson<PrototypeData>(config.text);
            data.Validate();
            coins = PlayerPrefs.GetInt("ShenYi.Prototype.Coins", 0); wins = PlayerPrefs.GetInt("ShenYi.Prototype.Wins", 0);
            BuildUI();
            NewBattle(20261106);
            ShowWelcome();
        }
        void Start()
        {
            if (AutoSmokeTest) { StartCoroutine(SmokeTest()); return; }
            var args = Environment.GetCommandLineArgs();
            int smoke = Array.IndexOf(args, "-shenyiSmoke");
            if (smoke >= 0)
            {
                AutoSmokeTest = true;
                ScreenshotPath = smoke + 1 < args.Length ? args[smoke + 1] : Application.persistentDataPath + "/prototype-smoke.png";
                StartCoroutine(SmokeTest());
            }
        }
        void Update()
        {
            UpdateTargetHover();
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (overlay.gameObject.activeSelf) overlay.gameObject.SetActive(false); else { ClearSelection(); Refresh(); }
            }
        }
        void BuildUI()
        {
            var canvasObject = new GameObject("PortraitCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(Width, Height); scaler.matchWidthOrHeight = .5f;
            // Aspect fitter provides a fixed portrait playfield on wide PC screens without stretching the board.
            var screenBg = Panel(canvasObject.transform, "Letterbox", Vector2.zero, Vector2.zero, new Color(.025f, .03f, .065f));
            Stretch(screenBg);
            root = Panel(canvasObject.transform, "SafePortraitArea", Vector2.zero, new Vector2(Width, Height), Color.clear);
            root.anchorMin = root.anchorMax = root.pivot = new Vector2(.5f, .5f); root.anchoredPosition = Vector2.zero;
            root.gameObject.AddComponent<PortraitSafeArea>();
            var boardView = Art(root, "PerspectiveBoardComposite", null, 0, 0, Width, Height);
            perspective = boardView.gameObject.AddComponent<PerspectiveBoard>();
            perspective.Initialize(boardArt);
            Art(root, "ArenaForeground", arenaArt, 0, 0, Width, Height);
            Panel(root, "TopShade", new Vector2(0, 0), new Vector2(Width, 215), Ink);
            Label(root, "神奕战场", 42, 30, 480, 65, 43, Color.white, TextAnchor.MiddleLeft);
            Label(root, "夕黎学院  /  遗迹试炼", 45, 99, 590, 34, 24, Muted, TextAnchor.MiddleLeft);
            currency = Label(root, "", 620, 35, 405, 40, 24, Lime, TextAnchor.MiddleRight);
            MakeButton(root, "规则", 838, 98, 175, 62, ShowRules, new Color(.24f, .2f, .38f), 25);
            turnLabel = Label(root, "", 45, 154, 400, 40, 27, Lime, TextAnchor.MiddleLeft);
            status = Label(root, "", 440, 154, 573, 40, 25, Color.white, TextAnchor.MiddleRight);

            Panel(root, "OpponentHUD", new Vector2(195,299), new Vector2(690,51), new Color(.07f,.06f,.13f,.87f));
            opponentLabel = Label(root, "", 200, 299, 680, 50, 25, Violet, TextAnchor.MiddleCenter);
            playerLabel = Label(root, "", 80, 1580, 920, 47, 27, Lime, TextAnchor.MiddleCenter);
            float[] centers = { .166f, .337f, .5f, .663f, .83f };
            // Battle owner 0 is the player: near/lower lime half. Owner 1 is the far purple half.
            float[] eggYs = { .883f, .111f }, monsterYs = { .594f, .399f };
            for (int p = 0; p < 2; p++) for (int i = 0; i < 5; i++)
            {
                int owner = p, slot = i;
                float x = centers[i] * 1080 - 64;
                float ey = eggYs[p] * 1773 - 87;
                float my = monsterYs[p] * 1773 - 87;
                eggButtons[p, i] = MakeButton(perspective.CardSurface, "", x, ey, 128, 174, () => OnEgg(owner, slot), new Color(.07f, .1f, .16f, .25f), 18);
                eggImages[p, i] = Art(eggButtons[p, i].transform, "EggArt", eggArt, 1, 1, 126, 168);
                AddSelectionBorder(eggImages[p, i], false);
                var eggBadge = Panel(eggButtons[p, i].transform, "LiveStatus", new Vector2(0,174), new Vector2(128,30), Ink);
                eggTexts[p, i] = Label(eggBadge, "", 1, 0, 126, 30, 21, Color.white, TextAnchor.MiddleCenter);
                eggFeedback[p,i]=eggButtons[p,i].gameObject.AddComponent<CardSlotFeedback>(); eggFeedback[p,i].Initialize(font);
                perspective.Register(eggButtons[p, i]);
                monsterButtons[p, i] = MakeButton(perspective.CardSurface, "", x, my, 128, 174, () => OnMonster(owner, slot), new Color(.07f, .1f, .16f, .25f), 18);
                monsterImages[p, i] = Art(monsterButtons[p, i].transform, "MonsterArt", monsterArt, 1, 1, 126, 168);
                AddSelectionBorder(monsterImages[p, i], false);
                monsterNames[p, i] = Label(monsterButtons[p, i].transform, "", 0, 0, 128, 28, 20, Color.white, TextAnchor.MiddleCenter);
                monsterNames[p, i].gameObject.SetActive(false);
                var monsterBadge = Panel(monsterButtons[p, i].transform, "LiveStatus", new Vector2(0,174), new Vector2(128,30), Ink);
                monsterTexts[p, i] = Label(monsterBadge, "", 1, 0, 126, 30, 21, Color.white, TextAnchor.MiddleCenter);
                monsterFeedback[p,i]=monsterButtons[p,i].gameObject.AddComponent<CardSlotFeedback>(); monsterFeedback[p,i].Initialize(font);
                perspective.Register(monsterButtons[p, i]);
            }
            // Screen-space controls stay level and readable; only cards on the table follow perspective.
            Panel(root, "QuestHUD", new Vector2(45,224), new Vector2(590,66), new Color(.07f,.06f,.13f,.84f));
            questLabel = Label(root, "", 60, 224, 570, 66, 23, Color.white, TextAnchor.MiddleLeft);
            questButton = MakeButton(root, "召唤霸主", 700, 231, 300, 62, BeginQuest, new Color(.24f, .18f, .36f, .94f), 25);
            MakeButton(root, "攻击决斗者", 355, 352, 370, 57, AttackFace, Ink, 25);
            endButton = MakeButton(root, "结束回合", 795, 1630, 235, 74, EndTurn, new Color(.32f, .39f, .1f), 28);
            skillButton = MakeButton(root, "主动技能", 50, 1630, 230, 74, UseSkill, Ink, 25);
            MakeButton(root, "战报", 605, 1630, 170, 74, ShowLog, Ink, 25);
            MakeButton(root, "卡牌详情", 300, 1630, 285, 74, ShowDetail, Ink, 25);

            Panel(root, "HandShade", new Vector2(25, 1710), new Vector2(1030, 198), Ink);
            hint = Label(root, "", 48, 1707, 984, 52, 23, Lime, TextAnchor.MiddleCenter);
            handRoot = Panel(root, "HandContent", new Vector2(38, 1748), new Vector2(1004, 150), Color.clear);
            // ScrollRect supports both a mouse wheel and a horizontal touch drag.
            var scroll = handRoot.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = true; scroll.vertical = false;
            handRoot.gameObject.AddComponent<RectMask2D>();
            var content = Panel(handRoot, "Cards", Vector2.zero, new Vector2(1004, 150), Color.clear);
            scroll.viewport = handRoot; scroll.content = content; scroll.movementType = ScrollRect.MovementType.Clamped;
            handRoot = content;
            Label(root, "向上拖牌放置 / 点选后点目标  ·  横向拖动浏览手牌", 80, 1893, 920, 24, 18, Muted, TextAnchor.MiddleCenter);
            overlay = Panel(root, "Modal", Vector2.zero, new Vector2(Width, Height), new Color(.015f, .02f, .05f, .93f));
            overlay.gameObject.SetActive(false);
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
        public void NewBattle(int seed)
        {
            if (ai != null) StopCoroutine(ai);
            busy = false; rewarded = false; Battle = new BattleModel(data, seed);
            ClearSelection(); if (overlay != null) overlay.gameObject.SetActive(false); Refresh();
        }
        void ClearSelection()
        {
            selectedCard = selectedEgg = selectedMonster = -1; questTarget = false;
            actionHint = "将手牌中的蛋向上拖到己方空蛋位，或点选后点击空位。";
        }
        bool Interactive() { return Battle != null && !Battle.Over && Battle.Active == 0 && !busy; }
        Card SelectedCard() { return Battle.Players[0].Hand.Find(c => c.Id == selectedCard); }
        void OnCard(int id)
        {
            if (!Interactive()) return;
            ClearSelection(); selectedCard = id; var card = SelectedCard();
            if (!card.IsEgg && card.Key == "draw") { Resolve(Battle.PlayTool(0, id, 0)); return; }
            actionHint = card.IsEgg ? "已选蛋卡：点击己方空蛋位放置。" : BattleModel.ToolDescription(card.Key);
            Refresh();
        }
        void DropHandCard(int id, Vector2 screenPosition)
        {
            if(!Interactive() || overlay.gameObject.activeSelf) return;
            var card=Battle.Players[0].Hand.Find(x=>x.Id==id);
            if(card==null) return;
            Button target;
            if(!perspective.TryGetSlotAtScreen(screenPosition,null,out target))
            { actionHint="未放置：将蛋拖到己方最下排的空蛋位；道具需拖到对应目标。"; Refresh(); return; }
            for(int owner=0;owner<2;owner++) for(int slot=0;slot<5;slot++)
            {
                if(target!=eggButtons[owner,slot] && target!=monsterButtons[owner,slot]) continue;
                int previous=draggingCard; draggingCard=id;
                string reason; bool valid=TargetAllowed(owner,slot,target==eggButtons[owner,slot],out reason);
                draggingCard=previous;
                if(!valid) { actionHint="未使用："+reason; Refresh(); return; }
            }
            for(int i=0;i<Battle.OpenSlots(0);i++)
            {
                if(target==eggButtons[0,i] && (card.IsEgg || card.Key=="watergun" || card.Key=="fold"))
                { OnCard(id); OnEgg(0,i); return; }
                if(target==monsterButtons[0,i] && !card.IsEgg && card.Key!="watergun" && card.Key!="fold")
                { OnCard(id); if(card.Key!="draw") OnMonster(0,i); return; }
            }
            actionHint=card.IsEgg?"未放置：蛋卡请拖到己方最下排已开放的空蛋位。":"未使用：请将道具拖到对应的己方目标位置。";
            Refresh();
        }
        void OnEgg(int owner, int slot)
        {
            if (owner == 1) { ShowEggDetail(owner, slot); return; }
            if (!Interactive()) return;
            var card = SelectedCard();
            if (card != null)
            {
                if (!card.IsEgg && card.Key != "watergun" && card.Key != "fold") { actionHint = "这张道具需要选择己方怪兽位。"; Refresh(); return; }
                Resolve(card.IsEgg ? Battle.PlayEgg(0, card.Id, slot) : Battle.PlayTool(0, card.Id, slot)); return;
            }
            var egg = Battle.Players[0].Eggs[slot];
            if (egg == null) { actionHint = "先选择一张蛋卡。"; Refresh(); return; }
            if (egg.Remaining > 0) { ShowEggDetail(owner, slot); return; }
            ClearSelection(); selectedEgg = slot; actionHint = "蛋已就绪：点击己方空怪兽位孵化，或继续等待蕴灵。"; Refresh();
        }
        void OnMonster(int owner, int slot)
        {
            inspected = owner * 5 + slot;
            if (!Interactive()) { ShowDetail(); return; }
            if (owner == 1)
            {
                if (selectedMonster >= 0) Resolve(Battle.Attack(0, selectedMonster, slot)); else ShowDetail();
                return;
            }
            if (questTarget) { Resolve(Battle.ClaimQuest(0, slot)); return; }
            if (selectedEgg >= 0) { Resolve(Battle.Hatch(0, selectedEgg, slot)); return; }
            var c = SelectedCard();
            if (c != null)
            {
                if (c.IsEgg || c.Key == "watergun" || c.Key == "fold") { actionHint = "请点击己方孵化区中的蛋位。"; Refresh(); return; }
                Resolve(Battle.PlayTool(0, c.Id, slot)); return;
            }
            var m = Battle.Players[0].Monsters[slot];
            if (m == null) { actionHint = "先选择一枚就绪的蛋。"; Refresh(); return; }
            ClearSelection(); selectedMonster = slot;
            actionHint = m.Definition.name + "：点击敌兽攻击；无敌兽时可攻击决斗者。"; Refresh();
        }
        void AttackFace()
        {
            if (!Interactive()) return;
            if (selectedMonster < 0) { actionHint = "先选择己方可攻击的怪兽。"; Refresh(); return; }
            Resolve(Battle.Attack(0, selectedMonster, -1));
        }
        void UseSkill()
        {
            if (!Interactive()) return;
            if (selectedMonster < 0) { actionHint = "先选择花洒鼠或吸铁蟹等有主动技能的怪兽。"; Refresh(); return; }
            Resolve(Battle.UseSkill(0, selectedMonster));
        }
        void BeginQuest()
        {
            if (!Interactive()) return;
            if (Battle.Players[0].QuestClaimed || Battle.Players[0].Hatched < data.questGoal)
            { actionHint = "累计孵化 " + data.questGoal + " 只怪兽后，可以选择空位召唤霸主。"; Refresh(); return; }
            ClearSelection(); questTarget = true; actionHint = "任务已完成！点击一个己方空怪兽位召唤霸主。"; Refresh();
        }
        void Resolve(bool success)
        {
            if (success) ClearSelection(); else actionHint = Battle.Error ?? "当前操作不可用。";
            Refresh(); CheckResult();
        }
        void EndTurn()
        {
            if (!Interactive()) return;
            if (!Battle.EndTurn(0)) return;
            ClearSelection(); Refresh(); CheckResult();
            if (!Battle.Over) ai = StartCoroutine(OpponentTurn());
        }
        IEnumerator OpponentTurn()
        {
            busy = true; Refresh(); yield return new WaitForSeconds(.6f);
            int guard = 0;
            while (!Battle.Over && Battle.Active == 1 && guard++ < 160)
            {
                if (!Battle.StepAI(1)) { Debug.LogError("AI could not perform a legal action."); Battle.EndTurn(1); break; }
                Refresh(); yield return new WaitForSeconds(.36f);
            }
            busy = false; ai = null; Refresh(); CheckResult();
        }
        void Refresh()
        {
            if (Battle == null) return;
            var player = Battle.Players[0]; var enemy = Battle.Players[1];
            currency.text = "金币 " + coins + "  ·  胜场 " + wins;
            turnLabel.text = "第 " + Math.Max(1, player.PersonalTurn) + " 回合  /  " + (Battle.Active == 0 ? "你的行动" : "对手行动");
            status.text = Battle.Over ? "决斗结束" : busy || Battle.Active == 1 ? "司欧斯正在思考…" : "抽牌 → 孵化 → 战斗 → 繁殖";
            opponentLabel.text = "司欧斯  ♥ " + Math.Max(0, enemy.Health) + " / " + data.startingHealth + "   手牌 " + enemy.Hand.Count + "   牌库 " + enemy.Deck.Count;
            playerLabel.text = "你  ♥ " + Math.Max(0, player.Health) + " / " + data.startingHealth + "   手牌 " + player.Hand.Count + "   牌库 " + player.Deck.Count;
            questLabel.text = "遗迹共鸣 · 孵化 " + Math.Min(player.Hatched, data.questGoal) + " / " + data.questGoal + (player.QuestClaimed ? "\n霸主已召唤" : "\n达成后手动召唤霸主");
            hint.text = busy || Battle.Active == 1 ? "对手行动中  ·  点击敌方卡牌可查看详情" : actionHint;
            endButton.interactable = Interactive(); skillButton.interactable = Interactive();
            questButton.interactable = Interactive() && !player.QuestClaimed && player.Hatched >= data.questGoal;
            for (int p = 0; p < 2; p++) for (int i = 0; i < 5; i++)
            {
                var d = Battle.Players[p]; bool locked = i >= Battle.OpenSlots(p);
                var e = d.Eggs[i]; var m = d.Monsters[i];
                eggImages[p, i].gameObject.SetActive(e != null); monsterImages[p, i].gameObject.SetActive(m != null);
                if (e != null)
                {
                    var definition = data.eggs.FirstOrDefault(x => x.core == e.Core);
                    ShowCard(eggImages[p,i], "egg_" + (definition != null ? definition.id : e.Core), EggPrefab(e.Elements), EggFace(e.Elements), p == 0 && selectedEgg == i);
                }
                if (m != null) ShowCard(monsterImages[p,i], "monster_" + m.Definition.id, MonsterPrefab(m.Elements), MonsterFace(m.Elements), p == 0 && selectedMonster == i);
                eggTexts[p, i].text = locked ? "第4回合开放" : e == null ? "" :
                    (e.Remaining == 0 ? "就绪" : "剩" + e.Remaining) + " · " + e.Tier + "阶 · 灵" + e.Spirit;
                monsterTexts[p, i].text = locked ? "第4回合开放" : m == null ? "" :
                    "攻" + m.Attack + " ♥" + Math.Max(0, m.Health) + " " + (m.Male ? "公" : "母");
                eggTexts[p, i].transform.parent.gameObject.SetActive(false);
                monsterTexts[p, i].transform.parent.gameObject.SetActive(false);
                SetButtonColor(eggButtons[p, i], locked ? new Color(.08f, .08f, .14f, .8f) : p == 0 && selectedEgg == i ? new Color(.26f, .39f, .12f, .95f)
                    : e != null ? new Color(.12f, .15f, .22f, .98f) : new Color(.06f, .07f, .12f, .34f));
                SetButtonColor(monsterButtons[p, i], locked ? new Color(.08f, .08f, .14f, .8f) : p == 0 && selectedMonster == i ? new Color(.24f, .35f, .1f, .98f)
                    : m != null ? new Color(.12f, .11f, .21f, .98f) : new Color(.06f, .07f, .12f, .34f));
                monsterTexts[p, i].color = m != null && m.IsBoss ? Lime : Color.white;
            }
            foreach (Transform child in handRoot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            handButtons.Clear();
            handRoot.sizeDelta = new Vector2(Math.Max(1004, player.Hand.Count * 157), 150);
            for (int i = 0; i < player.Hand.Count; i++)
            {
                var card = player.Hand[i]; int id = card.Id;
                var button = MakeButton(handRoot, "", i * 157, 0, 145, 150, () => OnCard(id), selectedCard == id ? new Color(.27f, .35f, .13f) : new Color(.15f, .14f, .24f), 20);
                handButtons[id]=button;
                if (card.IsEgg)
                {
                    var def = data.Egg(card.Key);
                    var face = Art(button.transform, "FullEggCard", EggFace(def.elements), 17, 2, 108, 144);
                    AddSelectionBorder(face, false);
                    ShowCard(face, "egg_" + def.id, EggPrefab(def.elements), EggFace(def.elements), selectedCard == id);
                }
                else Label(button.transform, BattleModel.ToolName(card.Key) + "\n─────\n" + ToolShort(card.Key), 7, 5, 131, 138, 20, new Color(.87f, .82f, 1f), TextAnchor.MiddleCenter);
                var drag=button.gameObject.AddComponent<HandCardDrag>();
                drag.DragRoot=root; drag.CanDrag=()=>Interactive() && !overlay.gameObject.activeSelf;
                drag.Drop=position=>DropHandCard(id,position);
                drag.DragStarted=position=>BeginTargetDrag(id,position);
                drag.DragMoved=position=>MoveTargetDrag(position);
                drag.DragFinished=EndTargetDrag;
            }
            RefreshSlotFeedback();
        }
        static void ShowCard(RawImage face, string id, string fallback, Texture image, bool selected)
        {
            var host = face.GetComponent<CardPrefabHost>();
            if (host == null) host = face.gameObject.AddComponent<CardPrefabHost>();
            host.Show(id, fallback, image, selected);
        }
        static string EggPrefab(string[] elements) { return elements != null && elements.Length > 1 ? "egg_dual" : "egg_fire"; }
        static string MonsterPrefab(string[] elements)
        {
            if (elements != null && elements.Length > 1) return "monster_dual";
            return elements != null && elements.Contains("草") ? "monster_grass" : "monster_fire";
        }
        Texture2D EggFace(string[] elements) { return elements != null && elements.Length > 1 ? eggDualFace : eggArt; }
        static void AddSelectionBorder(RawImage face, bool selected)
        {
            var outline = face.gameObject.AddComponent<Outline>();
            outline.effectColor = Lime; outline.effectDistance = new Vector2(3,3); outline.enabled = selected;
        }
        Texture2D MonsterFace(string[] elements)
        {
            if (elements != null && elements.Length > 1) return monsterDualFace;
            if (elements != null && elements.Contains("草")) return monsterGrassFace;
            return monsterArt;
        }
        static string ShortName(string value) { return value.Length > 5 ? value.Substring(0, 5) : value; }
        static string ToolShort(string key)
        {
            switch (key) { case "watergun": return "蛋加速 / 蕴灵"; case "craft": return "怪兽 +3/+3"; case "draw": return "立即抽 1 张";
                case "fold": return "蛋就绪\n孵化生命为 1"; case "revive": return "复活上一阵亡"; default: return ""; }
        }
        void CheckResult()
        {
            if (!Battle.Over || rewarded) return;
            rewarded = true;
            int reward = Battle.Winner == 0 ? 100 : Battle.Winner < 0 ? 35 : 20;
            if (!AutoSmokeTest)
            {
                coins += reward; if (Battle.Winner == 0) wins++;
                PlayerPrefs.SetInt("ShenYi.Prototype.Coins", coins); PlayerPrefs.SetInt("ShenYi.Prototype.Wins", wins); PlayerPrefs.Save();
            }
            Refresh();
            Modal(Battle.Winner == 0 ? "决斗胜利" : Battle.Winner < 0 ? "势均力敌" : "本次挑战结束",
                "司欧斯：「这份力量，竟然来自这些旧日的玩具……」\n\n" + "本场奖励：" + reward + " 金币\n累计胜场：" + wins
                + "\n\n你已经完成了放蛋、孵化、攻击和繁殖的基础循环。\n可以再次挑战，尝试不同站位与孵化时机。", "再战一场", () => NewBattle(unchecked(Environment.TickCount)));
        }
        void ShowWelcome()
        {
            Modal("夕黎学院 · 第一场决斗", "在地下遗迹中，你与灵族怪兽一起遇见了灵君司欧斯。\n\n「让我看看，你与这些旧物之间的羁绊能带来什么。」\n\n① 选择手牌中的蛋，点击下方空蛋位放置。\n② 等待己方回合计时归零，再选择空怪兽位孵化。\n③ 点击己方怪兽，再点敌兽进行攻击。\n④ 结束回合，相邻异性怪兽会自动产蛋。\n\n首次教学固定先手，首回合不能攻击；无费用限制。", "开始决斗", CloseModal);
        }
        void ShowRules()
        {
            Modal("原型规则", "双方 60 生命；起手 3 枚蛋 + 2 道具。先手首回合不抽牌、不攻击。每个己方回合抽 1 张。\n\n前 3 个己方回合开放 3 格，第 4 回合开放 5 格。蛋在己方回合开始时计时 -1；就绪后继续等待会积攒蕴灵。\n\n攻击怪兽同时受到反击。有敌兽时必须先攻击敌兽；没有敌兽时可攻击决斗者。合格蛋自动抵挡一次直接攻击，孵化 +1。\n\n结束回合时，相邻异性怪兽繁殖；普通阶级最高为 3，后代蛋池逐渐集中。满蛋位时放弃多余后代，同性时自动为一只怪兽重抽性别。\n\n累计孵化 5 只怪兽可手动召唤霸主。牌库耗尽后逐回合受到递增疲劳伤害。\n\n本版只实现灵族和有限效果；跨种族杂交、完整构筑与剧情待后续扩展。", "继续对局", CloseModal);
        }
        void ShowLog()
        {
            Modal("最近战报", string.Join("\n", Battle.History.Skip(Math.Max(0, Battle.History.Count - 18)).ToArray()), "返回战场", CloseModal);
        }
        void ShowDetail()
        {
            if (inspected < 0) { Modal("卡牌详情", "点击场上的怪兽查看数值和效果。\n\n先选择己方攻击者，再点敌方怪兽，会直接发起攻击。", "返回", CloseModal); return; }
            int p = inspected / 5, slot = inspected % 5; var m = Battle.Players[p].Monsters[slot];
            if (m == null) { actionHint = "该怪兽位当前为空。"; Refresh(); return; }
            Modal(m.Definition.name, m.Definition.race + " · " + string.Join("/", m.Elements) + " · " + (m.Male ? "公" : "母")
                + "\n\n" + m.Tier + " 阶 / 第 " + (m.Generation + 1) + " 代\n攻击 " + m.Attack + "  生命 " + m.Health + "/" + m.MaxHealth
                + "\n可消耗蕴灵 " + m.Spirit + "  总蕴灵 " + Battle.TotalSpirit(p, m) + "\n剩余攻击次数 " + m.Attacks
                + "\n\n" + m.Definition.description + "\n\n当前使用现有卡面模拟；以这里的实战数据为准。", "返回战场", CloseModal, MonsterFace(m.Elements));
        }
        void ShowEggDetail(int p, int slot)
        {
            var e = Battle.Players[p].Eggs[slot]; if (e == null) return;
            var lines = e.Pool.Select((id, i) => data.Monster(id).name + "  " + e.Weights[i] + "%" + (i == e.Pool.Length - 1 ? "  ★隐藏" : ""));
            Modal(e.Name, e.Tier + " 阶 / 第 " + (e.Generation + 1) + " 代\n剩余 " + e.Remaining + " / 初始 " + e.Initial + " 回合\n蕴灵 " + e.Spirit
                + "\n拦截：" + (e.BlockUsed ? "已使用" : "未使用") + "\n\n孵化候选\n" + string.Join("\n", lines.ToArray())
                + "\n\n就绪时，点击己方蛋，再点击空怪兽位即可孵化。", "返回战场", CloseModal, EggFace(e.Elements));
        }
        void CloseModal() { overlay.gameObject.SetActive(false); RefreshSlotFeedback(); }
        void Modal(string title, string body, string buttonText, Action action, Texture2D face = null)
        {
            foreach (Transform child in overlay) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            overlay.gameObject.SetActive(true); overlay.SetAsLastSibling();
            RefreshSlotFeedback();
            if(face!=null) { CardDetailModal(title,body,buttonText,action,face); return; }
            Panel(overlay, "Card", new Vector2(80, 310), new Vector2(920, 1240), new Color(.105f, .1f, .18f));
            Label(overlay, title, 130, 365, 820, 90, 40, Lime, TextAnchor.MiddleCenter);
            detail = Label(overlay, body, 145, 490, 790, 860, 29, Color.white, TextAnchor.UpperLeft);
            detail.resizeTextForBestFit = true; detail.resizeTextMinSize = 20; detail.resizeTextMaxSize = 29;
            MakeButton(overlay, buttonText, 300, 1410, 480, 90, action, new Color(.37f, .43f, .12f), 31);
        }
        void CardDetailModal(string title,string body,string buttonText,Action action,Texture2D face)
        {
            Panel(overlay,"Card",new Vector2(40,125),new Vector2(1000,1670),new Color(.105f,.1f,.18f));
            var heading=Label(overlay,title,85,155,910,100,48,Lime,TextAnchor.MiddleCenter);
            heading.resizeTextForBestFit=true; heading.resizeTextMinSize=36; heading.resizeTextMaxSize=48;
            var viewport=Panel(overlay,"DetailViewport",new Vector2(90,285),new Vector2(900,1285),new Color(.105f,.1f,.18f));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>(); scroll.horizontal=false; scroll.vertical=true;
            scroll.viewport=viewport; scroll.movementType=ScrollRect.MovementType.Clamped;
            var content=Panel(viewport,"DetailContent",Vector2.zero,new Vector2(900,1285),Color.clear);
            scroll.content=content;
            // Keep the whole source image visible and make the text independent of image width.
            // Source card art is 3:4; imported NPOT textures may report square GPU dimensions.
            float imageWidth=540,imageHeight=720;
            Art(content,"FullCardPreview",face,(900-imageWidth)*.5f,0,imageWidth,imageHeight);
            Label(content,"素材卡面预览 · 实战数据见下方",0,imageHeight+16,900,45,28,Muted,TextAnchor.MiddleCenter);
            float textY=imageHeight+95;
            detail=Label(content,body,20,textY,860,100,40,Color.white,TextAnchor.UpperLeft);
            detail.lineSpacing=1.15f; detail.resizeTextForBestFit=false;
            float textHeight=Mathf.Ceil(detail.preferredHeight)+20;
            ((RectTransform)detail.transform).sizeDelta=new Vector2(860,textHeight);
            content.sizeDelta=new Vector2(900,Mathf.Max(1285,textY+textHeight+30));
            scroll.verticalNormalizedPosition=1;
            Label(overlay,"上下拖动或滚动鼠标查看完整详情",90,1585,900,40,28,Muted,TextAnchor.MiddleCenter);
            MakeButton(overlay,buttonText,210,1650,660,105,action,new Color(.37f,.43f,.12f),38);
        }
        IEnumerator SmokeTest()
        {
            CloseModal(); AutoSmokeTest = true;
            // Standalone startup/splash may still own the backbuffer on the first frames.
            yield return new WaitForSeconds(4);
            yield return new WaitForEndOfFrame();
            if (!string.IsNullOrEmpty(ScreenshotPath)) ScreenCapture.CaptureScreenshot(ScreenshotPath + ".opening.png");
            yield return new WaitForSeconds(.3f);
            testPointerModule=EventSystem.current.GetComponent<StandaloneInputModule>();
            testPointerModule.enabled=false;
            testPointerInput=EventSystem.current.gameObject.AddComponent<PrototypePointerTestInput>();
            testPointerModule.inputOverride=testPointerInput;
            var lower=perspective.ScreenPointForSlot(eggButtons[0,0]);
            var upper=perspective.ScreenPointForSlot(eggButtons[1,0]);
            Vector2 lowerDesign,upperDesign;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,lower,null,out lowerDesign);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root,upper,null,out upperDesign);
            if(root.rect.yMax-lowerDesign.y<=1200 || root.rect.yMax-upperDesign.y>=650 || lower.y>=upper.y)
                throw new InvalidOperationException("Player must own lower lime board; opponent must own upper purple board.");
            // Exercise hand hit testing, drag handlers and screen-to-perspective drop routing.
            var firstEgg=Battle.Players[0].Hand.First(x=>x.IsEgg);
            int originalHand=Battle.Players[0].Hand.Count;
            SimulatePointerClick(HandScreenPoint(firstEgg.Id));
            MoveTargetDrag(perspective.ScreenPointForSlot(eggButtons[0,0]));
            if(!eggFeedback[0,0].Legal || eggFeedback[1,0].Legal || eggFeedback[0,3].Legal || monsterFeedback[0,0].Legal)
                throw new InvalidOperationException("Placement feedback incorrectly allows enemy, locked or monster slots.");
            yield return new WaitForEndOfFrame();
            if(!string.IsNullOrEmpty(ScreenshotPath)) ScreenCapture.CaptureScreenshot(ScreenshotPath+".targets.png");
            yield return new WaitForSeconds(.3f);
            SimulateHandDrag(firstEgg.Id,perspective.ScreenPointForSlot(eggButtons[1,0]),false);
            if(Battle.Players[0].Hand.Count!=originalHand) throw new InvalidOperationException("Invalid drop consumed a card.");
            yield return null;
            SimulateHandDrag(firstEgg.Id,perspective.ScreenPointForSlot(eggButtons[0,0]),true);
            if(Battle.Players[0].Hand.Count!=originalHand) throw new InvalidOperationException("Hand browsing placed a card.");
            yield return null;
            int placed=0;
            foreach (var card in Battle.Players[0].Hand.Where(x => x.IsEgg).ToArray())
            {
                int slot=Battle.FirstEmptyEgg(0);
                if(placed++==0)
                {
                    SimulatePointerClick(HandScreenPoint(card.Id));
                    if(selectedCard!=card.Id) throw new InvalidOperationException("Real pointer click did not select hand card.");
                    SimulatePointerClick(perspective.ScreenPointForSlot(eggButtons[0,slot]));
                }
                else SimulateHandDrag(card.Id,perspective.ScreenPointForSlot(eggButtons[0,slot]),false);
                if(Battle.Players[0].Eggs[slot]==null || Battle.Players[0].Hand.Any(x=>x.Id==card.Id))
                    throw new InvalidOperationException("Hand drag failed to place egg in selected slot.");
                yield return null;
            }
            if(root.Find("DraggedCard")!=null) throw new InvalidOperationException("Drag ghost remained after release.");
            Debug.Log("SHENYI_POINTER_CHECKS lower player/upper AI; 1 real click placement + 2 real drags; invalid drop retained; horizontal browsing retained; ghost cleaned");
            string targetReason;
            var tool=Battle.Players[0].Hand.FirstOrDefault(x=>!x.IsEgg && x.Key=="craft");
            if(tool!=null)
            {
                BeginTargetDrag(tool.Id,perspective.ScreenPointForSlot(eggButtons[0,0]));
                if(TargetAllowed(0,0,true,out targetReason) || TargetAllowed(0,0,false,out targetReason))
                    throw new InvalidOperationException("Craft must reject eggs and empty monster slots.");
                EndTargetDrag();
            }
            yield return new WaitForEndOfFrame();
            if(!string.IsNullOrEmpty(ScreenshotPath)) ScreenCapture.CaptureScreenshot(ScreenshotPath+".placed.png");
            yield return new WaitForSeconds(.3f);
            int[] playerEggIds=Battle.Players[0].Eggs.Select(x=>x==null?0:x.Id).ToArray();
            ShowEggDetail(0,0);
            yield return new WaitForEndOfFrame();
            if(!string.IsNullOrEmpty(ScreenshotPath)) ScreenCapture.CaptureScreenshot(ScreenshotPath+".detail.png");
            yield return new WaitForSeconds(.3f);
            var detailScroll=overlay.GetComponentInChildren<ScrollRect>();
            detailScroll.verticalNormalizedPosition=0;
            yield return new WaitForEndOfFrame();
            if(!string.IsNullOrEmpty(ScreenshotPath)) ScreenCapture.CaptureScreenshot(ScreenshotPath+".detail-bottom.png");
            yield return new WaitForSeconds(.3f);
            CloseModal();
            EndTurn();
            if(eggFeedback[0,0].Legal || eggFeedback[0,0].Ready || monsterFeedback[0,0].Legal)
                throw new InvalidOperationException("Player action cues remained during AI turn.");
            while (busy) yield return null;
            if(!playerEggIds.SequenceEqual(Battle.Players[0].Eggs.Select(x=>x==null?0:x.Id)))
                throw new InvalidOperationException("AI changed player eggs during opponent turn.");
            if(!eggFeedback[0,0].Ready) throw new InvalidOperationException("Ready egg has no hatch cue.");
            yield return new WaitForEndOfFrame();
            if(!string.IsNullOrEmpty(ScreenshotPath)) ScreenCapture.CaptureScreenshot(ScreenshotPath+".ready.png");
            yield return new WaitForSeconds(.3f);
            SimulatePointerClick(perspective.ScreenPointForSlot(eggButtons[0,0]));
            if(!monsterFeedback[0,0].Legal || monsterFeedback[1,0].Legal)
                throw new InvalidOperationException("Hatch feedback must target only own empty monster slots.");
            yield return new WaitForEndOfFrame();
            if(!string.IsNullOrEmpty(ScreenshotPath)) ScreenCapture.CaptureScreenshot(ScreenshotPath+".hatch-targets.png");
            yield return new WaitForSeconds(.3f);
            Debug.Log("SHENYI_FEEDBACK_CHECKS legal/enemy/locked/wrong row, ready egg cue and hatch target checks passed");
            for (int slot = 0; slot < Battle.OpenSlots(0); slot++)
                if (Battle.Players[0].Eggs[slot] != null && Battle.Players[0].Eggs[slot].Remaining == 0)
                { perspective.ClickSlot(eggButtons[0, slot]); perspective.ClickSlot(monsterButtons[0, Battle.FirstEmptyMonster(0)]); yield return null; }
            if (Battle.Players[0].Monsters.All(x => x == null)) throw new InvalidOperationException("UI smoke could not hatch a monster.");
            Refresh();
            Debug.Log("SHENYI_RAYCAST_CHECKS " + perspective.RaycastChecks);
            yield return new WaitForEndOfFrame();
            if (!string.IsNullOrEmpty(ScreenshotPath)) ScreenCapture.CaptureScreenshot(ScreenshotPath);
            yield return new WaitForSeconds(1);
            int actions = 0;
            while (!Battle.Over && actions++ < 16000) Battle.StepAI(Battle.Active);
            CheckResult();
            yield return new WaitForEndOfFrame();
            if (!string.IsNullOrEmpty(ScreenshotPath)) ScreenCapture.CaptureScreenshot(ScreenshotPath + ".result.png");
            yield return new WaitForSeconds(.5f);
            var result = "{\"completed\":" + (Battle.Over ? "true" : "false") + ",\"winner\":" + Battle.Winner + ",\"actions\":" + actions + ",\"turns\":" + Battle.Turn + "}";
            if (!string.IsNullOrEmpty(ScreenshotPath)) System.IO.File.WriteAllText(ScreenshotPath + ".json", result);
            Debug.Log("SHENYI_SMOKE " + result);
            if (!Application.isEditor) Application.Quit(Battle.Over ? 0 : 1);
            testPointerModule.inputOverride=null; testPointerModule.enabled=true; Destroy(testPointerInput);
        }
        Vector2 HandScreenPoint(int cardId)
        {
            Canvas.ForceUpdateCanvases(); var rect=(RectTransform)handButtons[cardId].transform;
            return RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        }
        void SimulatePointerClick(Vector2 point)
        {
            testPointerInput.Frame(testPointerModule,point,false);
            testPointerInput.Frame(testPointerModule,point,true);
            testPointerInput.Frame(testPointerModule,point,false);
        }
        void SimulateHandDrag(int cardId, Vector2 destination, bool horizontal)
        {
            Canvas.ForceUpdateCanvases();
            var button=handButtons[cardId]; var rect=(RectTransform)button.transform;
            Vector2 start=HandScreenPoint(cardId);
            var e=new PointerEventData(EventSystem.current) { position=start, pressPosition=start, button=PointerEventData.InputButton.Left, pointerDrag=button.gameObject };
            var hits=new List<RaycastResult>(); EventSystem.current.RaycastAll(e,hits);
            if(hits.Count==0 || ExecuteEvents.GetEventHandler<IBeginDragHandler>(hits[0].gameObject)!=button.gameObject)
                throw new InvalidOperationException("Hand card is blocked from pointer input.");
            testPointerInput.Frame(testPointerModule,start,false);
            testPointerInput.Frame(testPointerModule,start,true);
            testPointerInput.Frame(testPointerModule,start+(horizontal?Vector2.right:Vector2.up)*40,true);
            var end=horizontal?start+Vector2.right*60:destination;
            testPointerInput.Frame(testPointerModule,end,true);
            testPointerInput.Frame(testPointerModule,end,false);
        }
        RectTransform Panel(Transform parent, string name, Vector2 pos, Vector2 size, Color color)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image)); obj.transform.SetParent(parent, false);
            var rect = obj.GetComponent<RectTransform>(); Place(rect, pos.x, pos.y, size.x, size.y);
            var img = obj.GetComponent<Image>(); img.color = color; img.raycastTarget = color.a > 0;
            return rect;
        }
        Text Label(Transform parent, string value, float x, float y, float w, float h, int size, Color color, TextAnchor alignment)
        {
            var obj = new GameObject("Text", typeof(RectTransform), typeof(Text)); obj.transform.SetParent(parent, false);
            Place(obj.GetComponent<RectTransform>(), x, y, w, h); var text = obj.GetComponent<Text>();
            text.font = font; text.text = value; text.fontSize = size; text.color = color; text.alignment = alignment;
            text.raycastTarget = false; text.supportRichText = false; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }
        RawImage Art(Transform parent, string name, Texture texture, float x, float y, float w, float h)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(RawImage)); obj.transform.SetParent(parent, false);
            Place(obj.GetComponent<RectTransform>(), x, y, w, h); var art = obj.GetComponent<RawImage>(); art.texture = texture; art.raycastTarget = false; return art;
        }
        Button MakeButton(Transform parent, string text, float x, float y, float w, float h, Action action, Color color, int size)
        {
            var rect = Panel(parent, "Button_" + text, new Vector2(x, y), new Vector2(w, h), color);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = rect.GetComponent<Image>();
            var colors = button.colors; colors.highlightedColor = new Color(1.15f, 1.15f, 1.15f); colors.pressedColor = new Color(.7f, .85f, .45f); button.colors = colors;
            button.onClick.AddListener(() => action());
            if (!string.IsNullOrEmpty(text)) Label(rect, text, 5, 2, w - 10, h - 4, size, Color.white, TextAnchor.MiddleCenter);
            return button;
        }
        static void Place(RectTransform rect, float x, float y, float w, float h)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(w, h);
        }
        static void Stretch(RectTransform rect) { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        static void SetButtonColor(Button button, Color color) { button.GetComponent<Image>().color = color; }
    }

    // Retains a 9:16 board inside the device safe area or a desktop window of any aspect ratio.
    public sealed class PortraitSafeArea : MonoBehaviour
    {
        RectTransform rect;
        void LateUpdate()
        {
            if (rect == null) rect = (RectTransform)transform;
            var parent = (RectTransform)rect.parent;
            Rect safe = Screen.safeArea;
            float width = parent.rect.width * safe.width / Math.Max(1, Screen.width);
            float height = parent.rect.height * safe.height / Math.Max(1, Screen.height);
            float scale = Mathf.Min(width / 1080f, height / 1920f);
            rect.localScale = new Vector3(scale, scale, 1);
            rect.anchoredPosition = new Vector2((safe.center.x / Math.Max(1, Screen.width) - .5f) * parent.rect.width,
                (safe.center.y / Math.Max(1, Screen.height) - .5f) * parent.rect.height);
        }
    }
}
