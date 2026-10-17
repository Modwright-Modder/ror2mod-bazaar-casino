using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;
namespace BazaarCasino
{
    [BepInPlugin("local.gakon.bazaarcasino", "Bazaar Casino", "1.10.6")]
    public sealed partial class CasinoPlugin : BaseUnityPlugin
    {
        internal static CasinoPlugin Instance;
        internal static bool InBazaar => Run.instance && SceneInfo.instance && SceneInfo.instance.sceneDef && SceneInfo.instance.sceneDef.baseSceneName == "bazaar";
        internal static CharacterMaster LocalMaster => LocalUserManager.GetFirstLocalUser()?.cachedMaster;
        private sealed class Session
        {
            internal BlackjackRound hand;
            internal float nextAction;
            internal uint lastRequest;
            internal uint caseToken;
            internal int caseItem=-1,caseSeed;
            internal float caseStarted,caseReadyAt;
            internal bool casePending,caseCommand;
            internal int caseCost;
            internal int caseUnitPrice;
            internal int[] caseRewards=Array.Empty<int>();
        }
        private readonly Dictionary<CharacterMaster, Session> sessions = new Dictionary<CharacterMaster, Session>();
        private readonly HashSet<NetworkClient> clients = new HashSet<NetworkClient>();
        private readonly System.Random rng = new System.Random(Guid.NewGuid().GetHashCode());
        private ConfigEntry<int> minBet, maxBet;
        private ConfigEntry<float> loungeBrightness;
        internal float LoungeBrightness=>loungeBrightness.Value;
        private ConfigEntry<KeyCode> openKey;
        private ConfigEntry<bool> scaledPrices;
        private ConfigEntry<int> requestMessageId;
        private readonly Dictionary<ItemTier, ConfigEntry<int>> salePrices = new Dictionary<ItemTier, ConfigEntry<int>>();
        private CasinoUI ui;
        private bool serverRegistered;
        private Run previousRun;
        private bool wasBazaar;
        private uint requestCounter;
        private int serverMinimum,serverMaximum;
        internal CasinoState State;
        internal KeyCode OpenKey => openKey.Value;
        internal bool Waiting;
        private float requestStarted;
        private GameObject dealer;
        private float nextDealerAttempt;
        private ConfigEntry<bool> spawnDealer;
        private void Awake()
        {
            Instance = this;
            SceneDirector.onPrePopulateSceneServer += PrepareSceneDirector;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += PrepareLoadedScene;
            spawnDealer = Config.Bind("Dealer", "Enabled", true, "Spawn an item broker inside the Casino Lounge.");
            On.RoR2.Interactor.AttemptInteraction += InteractWithDealer;
            On.RoR2.PlayerCharacterMasterController.Update += SuppressSeatUpdate;
            On.RoR2.PlayerCharacterMasterController.FixedUpdate += SuppressSeatFixedUpdate;
            On.RoR2.UI.MPEventSystem.Update += CasinoEventUpdate;
            On.RoR2.UI.ChatBox.SubmitChat += SubmitCasinoChat;
            minBet = Config.Bind("Economy", "MinimumBet", 10, new ConfigDescription("Floor for the live minimum wager. The minimum follows the current scaled Lunar Cache price, capped by MaximumBet.", new AcceptableValueRange<int>(1, 10000)));
            maxBet = Config.Bind("Economy", "MaximumBet", 100000, new ConfigDescription("Maximum initial wager. Shared blackjack can reserve up to four times this amount with split and doubles.", new AcceptableValueRange<int>(10, 1000000)));
            scaledPrices = Config.Bind("Selling", "ScaleWithDifficulty", true, "Scale sale prices with Run.GetDifficultyScaledCost, like gold shop costs.");
            ConfigureGacha();
            ConfigureRace();
            ConfigureSoundtrack();
            loungeBrightness=Config.Bind("Lighting","Brightness",1.3f,new ConfigDescription("Local lounge ambient/fill brightness; no shadow cost. Applied only to lounge camera renders.",new AcceptableValueRange<float>(.3f,3f)));
            openKey = Config.Bind("Controls", "OpenCasino", KeyCode.F6, "Close the active casino interface. Open tables, slot machines and the broker with E.");
            requestMessageId = Config.Bind("Network", "MessageId", 28061, new ConfigDescription("Reserved request message ID; replies use ID+1. Same value required on all players. Change only if another mod conflicts.", new AcceptableValueRange<int>(1000, 32000)));
            Price(ItemTier.Tier1, 8); Price(ItemTier.Tier2, 20); Price(ItemTier.Tier3, 100);
            Price(ItemTier.Boss, 50); Price(ItemTier.Lunar, 30); Price(ItemTier.VoidTier1, 10);
            Price(ItemTier.VoidTier2, 25); Price(ItemTier.VoidTier3, 120); Price(ItemTier.VoidBoss, 60);
            Price(ItemTier.Cursed, 30); Price(ItemTier.Hex, 40); Price(ItemTier.FoodTier, 4); Price(ItemTier.Page, 20);
            ui = gameObject.AddComponent<CasinoUI>();
            Logger.LogInfo("Bazaar Casino loaded. Use E on casino tables, slot machines and the broker; F6 closes the current interface. All transactions are server-authoritative.");
        }
        private void Price(ItemTier tier, int gold) => salePrices[tier] = Config.Bind("Selling", tier.ToString(), gold, new ConfigDescription("Base sale value for one permanent item; 0 disables selling this tier.", new AcceptableValueRange<int>(0, 1000000)));
        private void Update()
        {
            if (InBazaar && lounge && spawnDealer.Value && !dealer && Time.unscaledTime >= nextDealerAttempt)
            {
                nextDealerAttempt = Time.unscaledTime + 2f;
                dealer = DealerNPC.Spawn(lounge);
            }
            if ((!InBazaar||!lounge||!spawnDealer.Value) && dealer) { Destroy(dealer); dealer = null; }
            if (NetworkServer.active && !serverRegistered)
            {
                NetworkServer.RegisterHandler((short)requestMessageId.Value, ReceiveRequest);
                serverRegistered = true;
            }
            if (!NetworkServer.active) serverRegistered = false;
            foreach (var client in NetworkClient.allClients)
                if (client != null && clients.Add(client))
                {
                    client.RegisterHandler((short)(requestMessageId.Value + 1), ReceiveReply);
                    client.RegisterHandler((short)(requestMessageId.Value + 2), ReceiveTableReply);
                }
            clients.RemoveWhere(c => !NetworkClient.allClients.Contains(c));
            if (previousRun != Run.instance || wasBazaar && !InBazaar)
            {
                ClearTables(previousRun==Run.instance && wasBazaar);
                sessions.Clear();
            }
            UpdateTableExit();UpdateTables();UpdateCaseDrops();UpdateRace();
            previousRun = Run.instance; wasBazaar = InBazaar;
            if (!InBazaar) { ui.Close(); State = null; Waiting = false;serverMinimum=serverMaximum=0; }
            else if (Input.GetKeyDown(openKey.Value)) ui.Toggle();
            if (Waiting && Time.unscaledTime - requestStarted > 6f)
            {
                Waiting = false;
                if(loungeTravel.Pending){loungeTravel.Cancel();ShowLoungeTravelError("Host did not confirm lounge travel. Check that every player uses Bazaar Casino 1.10.6 and the same network MessageId.");}
                ui.SharedRequestTimedOut();
                ui.SlotTimedOut();
                ui.CaseTimedOut();
                ui.RaceTimedOut();
                ui.Notify("Host did not respond. Install Bazaar Casino on the host and every player; check the network MessageId matches.");
            }
        }
        internal void Send(ActionKind action, int bet = 0, int choice = 0, int number = 0, RouletteBet[] bets = null, TableKind table = 0)
        {
            var master = LocalMaster;
            if (!InBazaar || !master || Waiting) return;
            var identity = master.GetComponent<NetworkIdentity>();
            if (!identity) return;
            var request = new CasinoRequest { requestId = ++requestCounter, masterId = identity.netId.Value, action = action, wager = bet, choice = choice, number = number, bets = bets, table=(byte)table };
            BeginLoungeTravel(request,master);
            Waiting = true; requestStarted = Time.unscaledTime;
            if (NetworkServer.active) Deliver(Process(master, request));
            else
            {
                var client = NetworkClient.allClients.FirstOrDefault(c => c.isConnected);
                if (client == null) { Waiting = false; if(loungeTravel.Pending){loungeTravel.Cancel();ShowLoungeTravelError("No server connection for lounge travel.");}else ui.Notify("No server connection."); return; }
                client.Send((short)requestMessageId.Value, request);
            }
        }
        private void InteractWithDealer(On.RoR2.Interactor.orig_AttemptInteraction orig, Interactor self, GameObject target)
        {
            if(ui&&self&&LocalMaster&&self.gameObject==LocalMaster.GetBodyObject()&&(ui.IsAnyCasinoOpen||ui.InteractionBlocked)&&target&&
                (target.GetComponent<WorldSlotMachine>()||target.GetComponent<WorldCaseMachine>()||target.GetComponent<WorldCrabRace>()||target.GetComponent<WorldTable>()||target.GetComponent<DealerNPC>()||target.GetComponent<CasinoLoungeDoor>()))return;
            var machine=target?target.GetComponent<WorldSlotMachine>():null;
            var cache=target?target.GetComponent<WorldCaseMachine>():null;
            var race=target?target.GetComponent<WorldCrabRace>():null;
            if(race){OpenRace(self,race);return;}
            if(cache){OpenCaseMachine(self,cache);return;}
            if(machine){OpenSlotMachine(self,machine);return;}
            var door=target?target.GetComponent<CasinoLoungeDoor>():null;
            if(door){UseLoungeDoor(self,door);return;}
            var table=target ? target.GetComponent<WorldTable>() : null;
            if(table){OpenTable(self,table);return;}
            if (target && target.GetComponent<DealerNPC>())
            {
                // The dealer is a local visual; open locally before UNet tries to
                // serialize its object ID. Gambling requests still go to the server.
                OpenDealer(self, target);
                return;
            }
            orig(self, target);
        }
        internal void OpenDealer(Interactor activator, GameObject target)
        {
            var body = LocalMaster ? LocalMaster.GetBody() : null;
            if (body && activator && activator.gameObject == body.gameObject && InBazaar && Vector3.Distance(body.corePosition, target.transform.position) < 8f && ui.CanOpenInteraction)
                ui.OpenBroker();
        }
        internal void OpenSlotMachine(Interactor actor,WorldSlotMachine machine)
        {
            var body=LocalMaster?LocalMaster.GetBody():null;
            if(body&&actor&&actor.gameObject==body.gameObject&&InBazaar&&ui.CanOpenInteraction&&Vector3.Distance(body.corePosition,machine.transform.position)<8)ui.OpenSlots(machine);
        }
        private void ReceiveRequest(NetworkMessage message)
        {
            try
            {
                var request = message.ReadMessage<CasinoRequest>();
                // Resolve the actor from the actual sender connection, never trust a client-supplied master.
                var user = NetworkUser.readOnlyInstancesList.FirstOrDefault(u => u && u.connectionToClient == message.conn && u.master && u.master.GetComponent<NetworkIdentity>().netId.Value == request.masterId);
                if (!user) return;
                var state = Process(user.master, request);
                message.conn.Send((short)(requestMessageId.Value + 1), new CasinoReply { json = JsonUtility.ToJson(state) });
            }
            catch (Exception e) { Logger.LogWarning("Rejected invalid casino request: " + e.Message); }
        }
        private void ReceiveReply(NetworkMessage message)
        {
            try { Deliver(JsonUtility.FromJson<CasinoState>(message.ReadMessage<CasinoReply>().json)); }
            catch (Exception e) { Waiting = false; if(loungeTravel.Pending){loungeTravel.Cancel();ShowLoungeTravelError("Invalid host response for lounge travel. Check matching casino versions.");} Logger.LogWarning("Invalid casino response: " + e.Message); }
        }
        private void Deliver(CasinoState state)
        {
            Waiting=false;
            if(state.minBet>0&&state.maxBet>=state.minBet)
            {serverMinimum=state.minBet;serverMaximum=state.maxBet;ui.SetWagerLimits(state.minBet,state.maxBet);}
            if(state.loungeReply){DeliverLoungeTravel(state);return;}
            if(loungeTravel.Matches(state.requestId))
            {loungeTravel.Cancel();ShowLoungeTravelError("The host uses an older lounge travel protocol. Update Bazaar Casino to 1.10.6 on every player.");return;}
            if(state.raceReply){if(state.raceState!=null&&lounge&&lounge.Race)lounge.Race.Receive(state.raceState);ui.RaceResponse(state.message);return;}
            if(state.tableReply){if(state.tableState!=null)ApplyTableSnapshot(state.tableState);ui.TableResponse(state.message);return;}
            State = state; ui.Receive(state);
        }
        internal int SalePrice(ItemDef def)
        {
            if (!def || def.hidden || !def.canRemove || !salePrices.TryGetValue(def.tier, out var entry) || entry.Value <= 0) return 0;
            return scaledPrices.Value && Run.instance ? Math.Min(100000000, Run.instance.GetDifficultyScaledCost(entry.Value)) : entry.Value;
        }
        private SaleItem[] SaleList(CharacterMaster master)
        {
            if (!master.inventory) return Array.Empty<SaleItem>();
            return ItemCatalog.allItemDefs.Where(d => d && master.inventory.GetItemCountPermanent(d.itemIndex) > 0 && SalePrice(d) > 0)
                .Select(d => new SaleItem { index = (int)d.itemIndex, count = master.inventory.GetItemCountPermanent(d.itemIndex), price = SalePrice(d) }).ToArray();
        }
        private static void Credit(CharacterMaster master, int amount)
        {
            // Direct wallet update avoids payout-induced item effects or item duplication.
            if (amount > 0) master.money = (uint)Math.Min(uint.MaxValue, (ulong)master.money + (ulong)amount);
        }
        private CasinoState Process(CharacterMaster master, CasinoRequest request)
        {
            var state = new CasinoState { requestId = request.requestId, minBet = MinimumBet, maxBet = MaximumBet, loungeReply=IsLoungeAction(request.action), tableReply=request.action>=ActionKind.TableJoin&&request.action<=ActionKind.TableSplit&&!IsLoungeAction(request.action), raceReply=request.action==ActionKind.RaceBet };
            if (!NetworkServer.active || !InBazaar || !master || !master.GetBody() || !master.GetBody().healthComponent.alive || SceneExitController.isRunning)
            { state.message = "Casino is only available while alive inside the Bazaar."; return state; }
            if (!sessions.TryGetValue(master, out var session)) sessions[master] = session = new Session();
            string error = null;
            if (request.requestId <= session.lastRequest) error = "Duplicate request ignored.";
            else
            {
                session.lastRequest = request.requestId;
                if (request.action != ActionKind.State && Time.unscaledTime < session.nextAction) error = "Please wait for the current action.";
                else if (request.action < ActionKind.State || request.action > ActionKind.RaceBet) error = "Unknown action.";
                else if(request.action==ActionKind.RaceBet)
                {
                    state.raceReply=true;error=ProcessRace(master,request);state.raceState=MakeRaceSnapshot();state.message=error??"Race wager updated.";
                    session.nextAction=Time.unscaledTime+.15f;
                }
                else if(request.action==ActionKind.LoungeEnter||request.action==ActionKind.LoungeExit)
                {
                    error=ProcessLoungeDoor(master,request,state);
                    state.message=error??(request.action==ActionKind.LoungeEnter?"Welcome to the Casino Lounge.":"Returned to Bazaar.");
                    session.nextAction=Time.unscaledTime+.4f;
                }
                else if (request.action>=ActionKind.TableJoin&&request.action<=ActionKind.TableSplit)
                {
                    error=ProcessTable(master,request);
                    state.message=error ?? "Table updated.";
                    if(worldTables.TryGetValue((TableKind)request.table,out var responseTable))state.tableState=MakeSnapshot((TableKind)request.table,responseTable);
                    session.nextAction=Time.unscaledTime+.12f;
                }
                else if(request.action!=ActionKind.State && request.action!=ActionKind.Sell && HasTableStake(request.masterId)) error="Finish or cancel your shared table wager first.";
                else if(request.action==ActionKind.CaseRedeem)
                {
                    error=RedeemCase(master,session,state,request);session.nextAction=Time.unscaledTime+.15f;
                }
                else if(request.action==ActionKind.Slots&&ValidateSlotMachine(master.GetBody(),request.number) is string machineError)error=machineError;
                else if(request.action==ActionKind.Deal||request.action==ActionKind.Hit||request.action==ActionKind.Stand||request.action==ActionKind.Double||request.action==ActionKind.Roulette)error="Blackjack and roulette are played at the shared tables. Press E at a table.";
                else if(request.action==ActionKind.Sell&&(!dealer||Vector3.Distance(master.GetBody().corePosition,dealer.transform.position)>8))error="Talk to the Lunar Broker inside the lounge to sell items.";
                else if (request.action == ActionKind.Sell)
                {
                    var def = ItemCatalog.GetItemDef((ItemIndex)request.choice);
                    int price = SalePrice(def);
                    if (!def || !master.inventory || price <= 0 || master.inventory.GetItemCountPermanent(def.itemIndex) < 1) error = "This item cannot be sold.";
                    else if ((ulong)master.money + (ulong)price > uint.MaxValue) error = "Gold wallet is full.";
                    else
                    {
                        master.inventory.GiveItemPermanent(def.itemIndex, -1);
                        state.game = 4;
                        Credit(master, price); state.message = "Sold 1 " + Language.GetString(def.nameToken) + " for " + price + " gold.";
                        session.nextAction = Time.unscaledTime + 0.2f;
                    }
                }
                else if (request.action == ActionKind.Hit || request.action == ActionKind.Stand || request.action == ActionKind.Double)
                {
                    if (session.hand == null || !session.hand.active) error = "No active blackjack hand.";
                    else if (request.action == ActionKind.Double && (session.hand.player.Count != 2 || master.money < session.hand.wager)) error = "Double requires two cards and enough gold for the extra wager.";
                    else
                    {
                        if (request.action == ActionKind.Double) { master.money -= (uint)session.hand.wager; session.hand.Double(); }
                        else if (request.action == ActionKind.Hit) session.hand.Hit(); else session.hand.Stand();
                        state.game = 1; state.wager = session.hand.wager;
                        if (!session.hand.active) { Credit(master, session.hand.payout); state.payout = session.hand.payout; }
                        state.message = session.hand.active ? "Hit, Stand, or Double." : session.hand.result;
                        session.nextAction = Time.unscaledTime + 0.2f;
                    }
                }
                else if (request.action != ActionKind.State)
                {
                    int bet = request.wager;
                    bool validTable = request.action != ActionKind.Roulette || RouletteBets.Validate(request.bets, state.minBet, state.maxBet, master.money, out bet);
                    if (session.hand != null && session.hand.active) error = "Finish your blackjack hand first.";
                    else if (bet < state.minBet || bet > state.maxBet || (uint)bet > master.money) error = "Wager outside the allowed range or insufficient gold.";
                    else if (!validTable) error = "Invalid roulette chips, total wager, or insufficient gold.";
                    else
                    {
                        master.money -= (uint)bet; state.wager = bet;
                        if (request.action == ActionKind.Deal)
                        {
                            session.hand = new BlackjackRound(rng, bet); state.game = 1;
                            if (!session.hand.active) { state.payout = session.hand.payout; Credit(master, state.payout); }
                            state.message = session.hand.active ? "Hit, Stand, or Double." : session.hand.result;
                            session.nextAction = Time.unscaledTime + 0.2f;
                        }
                        else if (request.action == ActionKind.Roulette)
                        {
                            state.game = 2; state.spinSeed = rng.Next();
                            var spin = new RouletteSpin(state.spinSeed);
                            state.wheelNumber = spin.number;
                            state.payout = RouletteBets.Payout(request.bets, spin.number);
                            Credit(master, state.payout); state.message = "Roulette: " + state.wheelNumber + " — " + (state.payout > 0 ? "win" : "no win");
                            session.nextAction = Time.unscaledTime + spin.duration;
                        }
                        else if (request.action == ActionKind.Slots)
                        {
                            state.game = 3; state.reels = new[] { Games.SlotSymbol(rng), Games.SlotSymbol(rng), Games.SlotSymbol(rng) };
                            state.payout = bet * Games.SlotMultiplier(state.reels[0], state.reels[1], state.reels[2]);
                            Credit(master, state.payout); state.message = state.payout > 0 ? "Slots payout!" : "No match";
                            BroadcastSlotResult(request.number,state.reels,state.payout-bet);
                            session.nextAction = Time.unscaledTime + SpinTiming.Reels;
                        }
                    }
                }
            }
            if (session.hand != null)
            {
                var hand = session.hand;
                state.handActive = hand.active; state.canDouble = hand.active && hand.player.Count == 2 && master.money >= hand.wager;
                state.handWager = hand.wager; state.player = hand.player.ToArray();
                state.dealer = hand.active ? new[] { hand.dealer[0], -1 } : hand.dealer.ToArray();
                state.playerTotal = Games.Value(hand.player); state.dealerTotal = hand.active ? 0 : Games.Value(hand.dealer);
            }
            state.message = error ?? state.message ?? "Welcome to the Bazaar Casino.";
            state.gold = master.money; if(!state.tableReply)state.items = SaleList(master);
            FillCaseState(state,session);
            return state;
        }
        private void OnDestroy()
        {
            SceneDirector.onPrePopulateSceneServer -= PrepareSceneDirector;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= PrepareLoadedScene;
            StopScenePreparation();
            On.RoR2.Interactor.AttemptInteraction -= InteractWithDealer;
            On.RoR2.PlayerCharacterMasterController.Update -= SuppressSeatUpdate;
            On.RoR2.PlayerCharacterMasterController.FixedUpdate -= SuppressSeatFixedUpdate;
            On.RoR2.UI.MPEventSystem.Update -= CasinoEventUpdate;
            On.RoR2.UI.ChatBox.SubmitChat -= SubmitCasinoChat;
            ClearTables(false);
            if (dealer) Destroy(dealer);
            if (NetworkServer.active && serverRegistered) NetworkServer.UnregisterHandler((short)requestMessageId.Value);
            foreach (var client in clients) if (client != null)
            {client.UnregisterHandler((short)(requestMessageId.Value + 1));client.UnregisterHandler((short)(requestMessageId.Value + 2));}
            if (ui) ui.Close(); CasinoArt.ReleaseShared(); Instance = null;
        }
    }
}
