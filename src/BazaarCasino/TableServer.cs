using System;
using System.Collections.Generic;
using System.Linq;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private SharedBlackjack blackjackTable=new SharedBlackjack();
        private SharedRoulette rouletteTable=new SharedRoulette();
        private readonly Dictionary<TableKind,WorldTable> worldTables=new Dictionary<TableKind,WorldTable>();
        private CasinoLounge lounge;
        private uint slotSpinCounter;
        private readonly float[] slotReadyAt=new float[3];
        private readonly float[] slotStartedAt=new float[3];
        private readonly TableSnapshot[] slotDisplays=new TableSnapshot[3];
        private string ValidateSlotMachine(CharacterBody body,int number)
        {
            if(number==0)return "Use E on a slot machine inside the Casino Lounge.";
            if(!lounge||number<1||number>lounge.Slots.Count)return "Slot machine is unavailable.";
            if(Vector3.Distance(body.corePosition,lounge.Slots[number-1].transform.position)>8)return "Move closer to the slot machine.";
            if(Time.unscaledTime<slotReadyAt[number-1])return "This machine is still spinning. Try again in a moment.";
            return null;
        }
        private void BroadcastSlotResult(int number,int[] reels,int net)
        {
            if(number<1||number>3)return;
            slotReadyAt[number-1]=Time.unscaledTime+SpinTiming.Reels;
            slotStartedAt[number-1]=Time.unscaledTime;
            var snapshot=new TableSnapshot{machine=number,slotSpin=++slotSpinCounter,slotReels=reels,slotNet=net,sceneId=Stage.instance?Stage.instance.netId.Value:0};
            slotDisplays[number-1]=snapshot;ApplyTableSnapshot(snapshot);
            NetworkServer.SendToAll((short)(requestMessageId.Value+2),new TableReply{json=JsonUtility.ToJson(snapshot)});
        }
        private readonly Dictionary<uint,TableKind> joinedTables=new Dictionary<uint,TableKind>();
        internal readonly Dictionary<TableKind,TableSnapshot> tableStates=new Dictionary<TableKind,TableSnapshot>();
        private int tableRevision;
        private float nextTableBroadcast,nextTableSpawn;
        private float nextPlacementWarning;
        private readonly Dictionary<TableKind,float> tableReceivedAt=new Dictionary<TableKind,float>();
        private TablePhase oldBlackjackPhase,oldRoulettePhase;
        internal int MinimumBet=>!NetworkServer.active&&serverMinimum>0?serverMinimum:WagerRules.ScaledMinimum(minBet.Value,CasePrice(),maxBet.Value);
        internal int MaximumBet=>!NetworkServer.active&&serverMaximum>0?serverMaximum:Math.Max(minBet.Value,maxBet.Value);
        internal WorldTable TableObject(TableKind kind)=>worldTables.TryGetValue(kind,out var table)?table:null;
        internal TableSnapshot Snapshot(TableKind kind)=>tableStates.TryGetValue(kind,out var state)?state:null;
        internal float TableTimeLeft(TableKind kind)=>tableStates.TryGetValue(kind,out var state)&&tableReceivedAt.TryGetValue(kind,out var at)?Mathf.Max(0,state.remaining-Time.unscaledTime+at):0;
        internal void UseLoungeDoor(Interactor actor,CasinoLoungeDoor door)
        {
            var body=LocalMaster?LocalMaster.GetBody():null;
            if(!body||!actor||actor.gameObject!=body.gameObject||!door||!InBazaar||Waiting||Vector3.Distance(body.corePosition,door.transform.position)>8)return;
            if(pendingTableExit!=0)return;
            ui.Close();Send(door.IsExit?ActionKind.LoungeExit:ActionKind.LoungeEnter);
        }
        private string ProcessLoungeDoor(CharacterMaster master,CasinoRequest request,CasinoState state)
        {
            if(!lounge)return "Lounge is not available yet.";
            if(joinedTables.ContainsKey(request.masterId))return "Stand up from your table first.";
            var door=request.action==ActionKind.LoungeExit?lounge.Exit:lounge.Entrance;
            var body=master.GetBody();
            if(Vector3.Distance(body.corePosition,door.transform.position)>8)return "Move closer to the lounge door.";
            var destination=door.IsExit?lounge.ReturnPosition():lounge.Arrival;
            var facing=door.IsExit?-door.transform.forward:lounge.transform.forward;
            state.loungeBodyId=body.netId.Value;state.loungeSceneId=Stage.instance?Stage.instance.netId.Value:0;
            state.loungeX=destination.x;state.loungeY=destination.y;state.loungeZ=destination.z;
            state.loungeYaw=Quaternion.LookRotation(facing).eulerAngles.y;
            TeleportHelper.TeleportBody(body,destination,false);
            if(body.characterMotor)body.characterMotor.velocity=Vector3.zero;
            if(body.characterDirection)body.characterDirection.forward=facing;
            state.loungeMoved=true;
            Logger.LogInfo("Authorized "+(door.IsExit?"lounge exit":"lounge entry")+" for "+PlayerName(master)+" (request "+request.requestId+").");
            return null;
        }
        internal void OpenTable(Interactor actor,WorldTable table)
        {
            var body=LocalMaster?LocalMaster.GetBody():null;
            if(!body||!actor||actor.gameObject!=body.gameObject||!InBazaar||Vector3.Distance(body.corePosition,table.transform.position)>12)return;
            if(pendingTableExit!=0){ui.Notify("Leaving the previous seat; try again in a moment.");return;}
            if(!ui.CanOpenInteraction)return;
            ui.OpenSharedTable(table.Kind);
        }
        private void UpdateTables()
        {
            if(!NetworkServer.active||!InBazaar)return;
            // Settle while masters still have their Bazaar wallets, before the
            // next scene starts and normal stage/economy hooks reset money.
            if(SceneExitController.isRunning){if(worldTables.Count>0||lounge)ClearTables(true);return;}
            if(worldTables.Count<2 && Time.unscaledTime>=nextTableSpawn)
            {
                nextTableSpawn=Time.unscaledTime+2;
                if(!lounge)
                {
                    try{lounge=CasinoLounge.TrySpawn();if(lounge)Logger.LogInfo("Casino lounge created: "+CasinoLounge.Diagnostic);}
                    catch(Exception error){if(Time.unscaledTime>=nextPlacementWarning)Logger.LogError("Could not create casino lounge: "+error);}
                    if(!lounge)
                    {
                        if(Time.unscaledTime>=nextPlacementWarning){nextPlacementWarning=Time.unscaledTime+20;Logger.LogWarning("Casino lounge placement: "+CasinoLounge.Diagnostic);}
                        return;
                    }
                }
                foreach(TableKind kind in new[]{TableKind.Blackjack,TableKind.Roulette})
                {
                    if(worldTables.ContainsKey(kind))continue;
                    try
                    {
                        var position=lounge.TablePosition(kind);
                        worldTables[kind]=WorldTable.Create(kind,position,lounge.TableRotation);
                        tableRevision++;nextTableBroadcast=0;
                        Logger.LogInfo("Shared "+kind+" table placed at "+position);
                    }
                    catch(Exception error)
                    {
                        if(Time.unscaledTime>=nextPlacementWarning)Logger.LogError("Could not create "+kind+" table: "+error);
                    }
                }
                if(worldTables.Count<2&&Time.unscaledTime>=nextPlacementWarning)
                {nextPlacementWarning=Time.unscaledTime+20;Logger.LogWarning("Retrying missing lounge tables; found "+worldTables.Count+" of 2.");}
            }
            double now=Time.unscaledTime;
            // Position drift never relinquishes an authorized seat. A
            // committed blackjack hand stands and still settles against the dealer.
            foreach(var pair in joinedTables.ToArray())
            {
                var master=FindMaster(pair.Key); var body=master?master.GetBody():null;
                if(!worldTables.TryGetValue(pair.Value,out var table))continue;
                if(!body||!body.healthComponent||!seatAnchors.TryGetValue(pair.Key,out var anchor)||!anchor.Keeps(body.netId.Value,true,body.healthComponent.alive,SceneExitController.isRunning))
                    LeaveSharedTable(pair.Key,pair.Value,now);
            }
            uint previousTurn=blackjackTable.turn;int previousHand=blackjackTable.Find(previousTurn)?.activeHand??0;
            blackjackTable.Tick(now,rng);rouletteTable.Tick(now,rng);
            PayTables();
            if(blackjackTable.phase!=oldBlackjackPhase||rouletteTable.phase!=oldRoulettePhase||previousTurn!=blackjackTable.turn||previousHand!=(blackjackTable.Find(blackjackTable.turn)?.activeHand??0))
            {
                tableRevision++;nextTableBroadcast=0;
                oldBlackjackPhase=blackjackTable.phase;oldRoulettePhase=rouletteTable.phase;
            }
            if(blackjackTable.phase==TablePhase.Betting)blackjackTable.seats.RemoveAll(s=>!joinedTables.TryGetValue(s.id,out var kind)||kind!=TableKind.Blackjack);
            if(rouletteTable.phase==TablePhase.Betting)rouletteTable.seats.RemoveAll(s=>!joinedTables.TryGetValue(s.id,out var kind)||kind!=TableKind.Roulette);
            if(Time.unscaledTime>=nextTableBroadcast){nextTableBroadcast=Time.unscaledTime+.5f;BroadcastTables();}
        }
        private static CharacterMaster FindMaster(uint id)=>NetworkUser.readOnlyInstancesList.FirstOrDefault(u=>u&&u.master&&u.master.GetComponent<NetworkIdentity>().netId.Value==id)?.master;
        private static string PlayerName(CharacterMaster master)
        {
            string name=NetworkUser.readOnlyInstancesList.FirstOrDefault(u=>u&&u.master==master)?.userName ?? "Survivor";
            name=name.Replace("<","").Replace(">","");return name.Length>24?name.Substring(0,24):name;
        }
        private bool HasTableStake(uint id)
        {
            return blackjackTable.Find(id)?.wager>0&&blackjackTable.phase!=TablePhase.Results || rouletteTable.Find(id)?.wager>0&&rouletteTable.phase!=TablePhase.Results;
        }
        private void LeaveSharedTable(uint id,TableKind kind,double now)
        {
            int refund=kind==TableKind.Blackjack?blackjackTable.Leave(id,now):rouletteTable.Leave(id);
            var master=FindMaster(id);if(master)Credit(master,refund);
            joinedTables.Remove(id);ReleaseSeating(id,true);standingPositions.Remove(id);seatAnchors.Remove(id);tableRevision++;nextTableBroadcast=0;PayTables();
        }
        private string ProcessTable(CharacterMaster master,CasinoRequest request)
        {
            var kind=(TableKind)request.table;uint id=request.masterId;double now=Time.unscaledTime;
            if(kind!=TableKind.Blackjack&&kind!=TableKind.Roulette||!worldTables.TryGetValue(kind,out var table))return "Table is not available yet.";
            if(request.action==ActionKind.TableLeave)
            {
                if(joinedTables.TryGetValue(id,out var own)&&own==kind)LeaveSharedTable(id,kind,now);
                BroadcastTables();return null;
            }
            if(!joinedTables.TryGetValue(id,out var atTable)||atTable!=kind)
                if(Vector3.Distance(master.GetBody().corePosition,table.transform.position)>14)return "Move closer to the table.";
            if(joinedTables.ContainsKey(id)&&seatAnchors.TryGetValue(id,out var currentAnchor)&&currentAnchor.BodyId!=master.GetBody().netId.Value)return "Your character changed. Join the table again.";
            if(sessions.TryGetValue(master,out var privateSession)&&privateSession.hand?.active==true)return "Finish your private blackjack hand first.";
            if(request.action==ActionKind.TableJoin)
            {
                if(sessions.TryGetValue(master,out var session)&&session.hand?.active==true)return "Finish your private blackjack hand first.";
                if(joinedTables.TryGetValue(id,out var previous)&&previous!=kind)
                {
                    if(HasTableStake(id))return "Finish or cancel your wager at the other table first.";
                    LeaveSharedTable(id,previous,now);
                }
                bool success=kind==TableKind.Blackjack?blackjackTable.Join(id,PlayerName(master)):rouletteTable.Join(id,PlayerName(master));
                if(!success)return "Table is full or a round is in progress. Watch, then join the next round.";
                if(!joinedTables.TryGetValue(id,out var current)||current!=kind)
                {
                    // Capture the root, not native footPosition: non-capsule
                    // survivors use their centre as footPosition. The owning
                    // client applies its seat only after receiving this snapshot.
                    standingPositions[id]=((Component)master.GetBody()).transform.position;
                    var body=master.GetBody();var ownSeat=kind==TableKind.Blackjack?blackjackTable.Find(id):rouletteTable.Find(id);
                    var position=table.ChairFoot(ownSeat.slot)+Vector3.up*CasinoSeat.Clearance(body);
                    seatAnchors[id]=new SeatAnchor(body.netId.Value,ownSeat.slot,position.x,position.y,position.z,table.ChairFacing(ownSeat.slot).eulerAngles.y);
                }
                joinedTables[id]=kind;
            }
            else
            {
                if(!joinedTables.TryGetValue(id,out var joined)||joined!=kind)return "Join this table first (E).";
                if(request.action==ActionKind.TableBet)
                {
                    if(kind==TableKind.Blackjack)
                    {
                        if(!blackjackTable.CanBet(id))return "Betting is closed.";
                        int amount=request.wager,previous=blackjackTable.Find(id).wager;
                        if(!WagerRules.Valid(amount,MinimumBet,MaximumBet,(ulong)master.money+(uint)previous,request.allIn,true))return "Invalid wager or insufficient gold.";
                        AdjustReservedGold(master,previous,amount);blackjackTable.Bet(id,amount,now);
                    }
                    else
                    {
                        if(!rouletteTable.CanBet(id))return "Betting is closed.";
                        int previous=rouletteTable.Find(id).wager,total=0;
                        var bets=request.bets??Array.Empty<RouletteBet>();
                        uint budget=(uint)Math.Min(uint.MaxValue,(ulong)master.money+(uint)previous);
                        if(request.allIn&&bets.Length==0)return "All In requires a positive wager.";
                        if(bets.Length>0&&!RouletteBets.Validate(bets,MinimumBet,MaximumBet,budget,out total,request.allIn))return "Invalid chips, total wager, or insufficient gold.";
                        AdjustReservedGold(master,previous,total);rouletteTable.Bet(id,bets,total,now);
                    }
                }
                else if(request.action==ActionKind.TableStart)
                {
                    var seats=kind==TableKind.Blackjack?blackjackTable.seats:rouletteTable.seats;
                    bool betting=kind==TableKind.Blackjack?blackjackTable.CanStart:rouletteTable.CanStart;
                    if(!betting||(kind==TableKind.Blackjack&&seats.Any(s=>s.wager==0))||(kind==TableKind.Roulette&&rouletteTable.Find(id).wager==0))return "Place a wager first. Manual start waits for every seated player to bet; automatic start takes 5 seconds.";
                    if(kind==TableKind.Blackjack)blackjackTable.Start(rng,now);else rouletteTable.Start(rng.Next(),now);
                }
                else
                {
                    if(kind!=TableKind.Blackjack||!blackjackTable.CanAct(id))return "Wait for your blackjack turn.";
                    if(request.action==ActionKind.TableHit)blackjackTable.Hit(id,now);
                    else if(request.action==ActionKind.TableStand)blackjackTable.Stand(id,now);
                    else if(request.action==ActionKind.TableDouble)
                    {
                        var seat=blackjackTable.Find(id);
                        if(!blackjackTable.CanDouble(id)||master.money<(uint)seat.ActiveStake)return "Double requires two cards and enough gold.";
                        master.money-=(uint)seat.ActiveStake;blackjackTable.Double(id,now);
                    }
                    else if(request.action==ActionKind.TableSplit)
                    {
                        var seat=blackjackTable.Find(id);
                        if(!blackjackTable.CanSplit(id)||master.money<(uint)seat.ActiveStake)return "Split needs a matching pair and enough gold for a second hand.";
                        master.money-=(uint)seat.ActiveStake;blackjackTable.Split(id,now);
                    }
                    else return "Invalid table action.";
                }
            }
            tableRevision++;PayTables();BroadcastTables();return null;
        }
        private static void AdjustReservedGold(CharacterMaster master,int previous,int current)
        {if(current>previous)master.money-=(uint)(current-previous);else Credit(master,previous-current);}
        private void PayTables()
        {
            foreach(var seat in blackjackTable.TakePayments()){var master=FindMaster(seat.id);if(master)Credit(master,seat.payout);}
            foreach(var seat in rouletteTable.TakePayments()){var master=FindMaster(seat.id);if(master)Credit(master,seat.payout);}
        }
        private TableSnapshot MakeSnapshot(TableKind kind,WorldTable table)
        {
            double now=Time.unscaledTime;bool bj=kind==TableKind.Blackjack;
            var phase=bj?blackjackTable.phase:rouletteTable.phase;
            var position=table.transform.position;
            var snapshot=bj?TableSnapshots.Blackjack(blackjackTable):TableSnapshots.Roulette(rouletteTable);
            foreach(var seat in snapshot.seats)
            {
                seat.seated=joinedTables.TryGetValue(seat.id,out var joined)&&joined==kind;
                var master=FindMaster(seat.id);var body=master?master.GetBody():null;
                seat.bodyId=body?body.netId.Value:0;seat.returnRoot=true;
                if(seat.seated&&seatAnchors.TryGetValue(seat.id,out var anchor))
                {
                    seat.bodyId=anchor.BodyId;seat.slot=anchor.Slot;seat.hasAnchor=true;
                    seat.anchorX=anchor.X;seat.anchorY=anchor.Y;seat.anchorZ=anchor.Z;seat.anchorYaw=anchor.Yaw;
                }
                if(seat.seated&&standingPositions.TryGetValue(seat.id,out var standing))
                {seat.returnX=standing.x;seat.returnY=standing.y;seat.returnZ=standing.z;}
            }
            snapshot.revision=tableRevision;
            snapshot.sceneId=Stage.instance?Stage.instance.netId.Value:0;
            snapshot.remaining=(float)Math.Max(0,(bj?blackjackTable.deadline:rouletteTable.deadline)-now);
            snapshot.elapsed=bj||phase==TablePhase.Betting?0:(float)(now-rouletteTable.started);
            snapshot.x=position.x;snapshot.y=position.y;snapshot.z=position.z;snapshot.yaw=table.transform.eulerAngles.y;
            if(lounge)
            {
                snapshot.lounge=true;var room=lounge.transform.position;var door=lounge.Entrance.transform.position;
                snapshot.roomX=room.x;snapshot.roomY=room.y;snapshot.roomZ=room.z;snapshot.roomYaw=lounge.transform.eulerAngles.y;
                snapshot.doorX=door.x;snapshot.doorY=door.y;snapshot.doorZ=door.z;snapshot.doorYaw=lounge.Entrance.transform.eulerAngles.y;
            }
            return snapshot;
        }
        private void BroadcastTables()
        {
            foreach(var pair in worldTables.ToArray())
            {
                var snapshot=MakeSnapshot(pair.Key,pair.Value);ApplyTableSnapshot(snapshot);
                var json=JsonUtility.ToJson(snapshot);
                if(System.Text.Encoding.UTF8.GetByteCount(json)>30000){Logger.LogError("Table snapshot exceeds message limit.");continue;}
                NetworkServer.SendToAll((short)(requestMessageId.Value+2),new TableReply{json=json});
            }
            // Persist each cabinet's final display for spectators and late joins.
            for(int i=0;i<slotDisplays.Length;i++)if(slotDisplays[i]!=null)
            {
                slotDisplays[i].elapsed=Time.unscaledTime-slotStartedAt[i];
                NetworkServer.SendToAll((short)(requestMessageId.Value+2),new TableReply{json=JsonUtility.ToJson(slotDisplays[i])});
            }
        }
        private void ReceiveTableReply(NetworkMessage message)
        {
            try{ApplyTableSnapshot(JsonUtility.FromJson<TableSnapshot>(message.ReadMessage<TableReply>().json));}
            catch(Exception error){Logger.LogWarning("Invalid table snapshot: "+error.Message);}
        }
        private void ApplyTableSnapshot(TableSnapshot snapshot)
        {
            if(!InBazaar||snapshot==null)return;
            if(Stage.instance&&snapshot.sceneId!=0&&Stage.instance.netId.Value!=snapshot.sceneId)return;
            var messageKind=SnapshotRoutes.Classify(snapshot.kind,snapshot.machine,snapshot.race!=null,snapshot.race?.minimum??0,snapshot.race?.maximum??0);
            if(messageKind==SnapshotKind.Ignore)return;
            if(messageKind==SnapshotKind.Race){if(lounge&&lounge.Race)lounge.Race.Receive(snapshot.race);return;}
            if(messageKind==SnapshotKind.Slot)
            {
                if(lounge&&snapshot.machine<=lounge.Slots.Count)lounge.Slots[snapshot.machine-1].Reveal(snapshot.slotSpin,snapshot.slotReels,snapshot.slotNet,snapshot.elapsed);
                return;
            }
            if(snapshot.kind<1||snapshot.kind>2)return;
            var kind=(TableKind)snapshot.kind;
            if(tableStates.TryGetValue(kind,out var previous)&&snapshot.revision<previous.revision)return;
            tableStates[kind]=snapshot;
            tableReceivedAt[kind]=Time.unscaledTime;
            if(snapshot.lounge&&!lounge)lounge=CasinoLounge.Create(new Vector3(snapshot.roomX,snapshot.roomY,snapshot.roomZ),Quaternion.Euler(0,snapshot.roomYaw,0),new Vector3(snapshot.doorX,snapshot.doorY,snapshot.doorZ),Quaternion.Euler(0,snapshot.doorYaw,0));
            if(!worldTables.TryGetValue(kind,out var table)||!table)worldTables[kind]=table=WorldTable.Create(kind,new Vector3(snapshot.x,snapshot.y,snapshot.z),Quaternion.Euler(0,snapshot.yaw,0));
            if(pendingTableExit==kind&&!snapshot.seats.Any(s=>s.id==LocalMasterId&&s.seated))pendingTableExit=0;
            table.Receive(snapshot);ApplySeating(snapshot,table);ui.ReceiveTable(snapshot);
            // Clients prepare geometry from host-authoritative snapshots at startup.
            if(!NetworkServer.active)
            {
                if(lounge&&spawnDealer.Value&&!dealer)dealer=DealerNPC.Spawn(lounge);
                WarmSceneVisuals();
            }
        }
        private void ClearTables(bool settle)
        {
            loungeTravel.Cancel();
            ClearRace(settle);
            RefundPendingCases(settle);
            StopScenePreparation();sceneVisualWarmed=false;
            if(settle&&NetworkServer.active)
            {
                double now=Time.unscaledTime;
                foreach(var seat in blackjackTable.seats)if(blackjackTable.phase==TablePhase.Betting){var master=FindMaster(seat.id);if(master)Credit(master,seat.wager);}
                foreach(var seat in rouletteTable.seats)if(rouletteTable.phase==TablePhase.Betting){var master=FindMaster(seat.id);if(master)Credit(master,seat.wager);}
                blackjackTable.Finish(now);rouletteTable.Finish(now);PayTables();
            }
            ClearSeating(settle&&InBazaar);pendingTableExit=0;
            if(lounge)
            {
                if(settle&&InBazaar&&NetworkServer.active)
                    foreach(var user in NetworkUser.readOnlyInstancesList)
                    {
                        var body=user&&user.master?user.master.GetBody():null;
                        if(body&&body.healthComponent.alive&&lounge.Contains(body.footPosition))TeleportHelper.TeleportBody(body,lounge.ReturnPosition(),false);
                    }
                Destroy(lounge.gameObject);lounge=null;
            }
            foreach(var table in worldTables.Values)if(table)Destroy(table.gameObject);
            worldTables.Clear();tableStates.Clear();tableReceivedAt.Clear();joinedTables.Clear();blackjackTable=new SharedBlackjack();rouletteTable=new SharedRoulette();
            Array.Clear(slotReadyAt,0,slotReadyAt.Length);Array.Clear(slotStartedAt,0,slotStartedAt.Length);Array.Clear(slotDisplays,0,slotDisplays.Length);slotSpinCounter=0;
            tableRevision=0;nextTableSpawn=0;nextTableBroadcast=0;oldBlackjackPhase=oldRoulettePhase=TablePhase.Betting;
            if(ui)ui.CloseSharedTable(false);
        }
    }
}
