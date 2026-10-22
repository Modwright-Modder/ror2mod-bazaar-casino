using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using RoR2;
using UnityEngine;
using UnityEngine.Networking;

namespace BazaarCasino
{
    public sealed partial class CasinoPlugin
    {
        private SharedCrabRace crabRace=new SharedCrabRace();
        private readonly Dictionary<uint,CharacterMaster> raceMasters=new Dictionary<uint,CharacterMaster>();
        private ConfigEntry<int> raceStakeMultiplier;
        private int raceRevision;
        private float nextRaceBroadcast;
        private void ConfigureRace()
        {
            raceStakeMultiplier=Config.Bind("CrabRace","MinimumCachePriceMultiplier",10,
                new ConfigDescription("High-stakes minimum: current difficulty-scaled Lunar Cache price times this value. Four equal-odds crabs; winner returns 4x stake including the wager.",new AcceptableValueRange<int>(1,1000)));
        }
        private int RaceMinimum()=>Math.Max(MinimumBet,(int)Math.Min(MaximumBet,(long)CasePrice()*raceStakeMultiplier.Value));
        private string ProcessRace(CharacterMaster master,CasinoRequest request)
        {
            if(!lounge||!lounge.Race||Vector3.Distance(master.GetBody().corePosition,lounge.Race.InteractionPosition)>8)return "Move closer to the Hermit Derby betting desk.";
            if(!lounge.Race.ModelsReady)return "Native Hermit Crabs are still loading; try again shortly.";
            if(!crabRace.CanBet)return "The race is underway. Betting opens after the result.";
            if(HasTableStake(request.masterId))return "Finish or cancel your table wager first.";
            int wager=request.wager;
            var previous=crabRace.Find(request.masterId);int old=previous?.wager??0;
            if(request.choice<0||request.choice>=CrabRun.Count||!WagerRules.Valid(wager,RaceMinimum(),MaximumBet,(ulong)master.money+(uint)old,request.allIn,true))return "Review the scaled minimum and maximum stake. All In must use your entire available gold.";
            if((ulong)master.money+(uint)old<(uint)wager)return "Not enough gold.";
            if((ulong)master.money+(uint)old-(uint)wager>uint.MaxValue)return "Gold wallet is full; spend gold before refunding.";
            if(!crabRace.SetBet(request.masterId,PlayerName(master),request.choice,wager,Time.unscaledTime))return "Betting is closed.";
            master.money=(uint)((ulong)master.money+(uint)old-(uint)wager);
            raceMasters[request.masterId]=master;raceRevision++;nextRaceBroadcast=0;
            return null;
        }
        private RaceSnapshot MakeRaceSnapshot()=>new RaceSnapshot
        {
            round=crabRace.round,phase=(int)crabRace.phase,seed=crabRace.seed,winner=crabRace.winner,
            elapsed=crabRace.phase==TablePhase.Betting?0:(float)Math.Max(0,Time.unscaledTime-crabRace.started),
            remaining=(float)Math.Max(0,crabRace.deadline-Time.unscaledTime),minimum=RaceMinimum(),maximum=MaximumBet,revision=raceRevision,
            // Clone for host rendering: never let clients/UI share mutable tickets.
            tickets=crabRace.tickets.Select(t=>new RaceTicket{id=t.id,name=t.name,lane=t.lane,wager=t.wager,payout=t.payout,paid=t.paid}).ToArray()
        };
        private void PayRace()
        {
            foreach(var ticket in crabRace.TakePayments())
                if(raceMasters.TryGetValue(ticket.id,out var master)&&master)Credit(master,ticket.payout);
        }
        private void UpdateRace()
        {
            if(!NetworkServer.active||!InBazaar||SceneExitController.isRunning||!lounge||!lounge.Race)return;
            var previous=crabRace.phase;
            // A disconnected bettor may reclaim an uncommitted wager. Once the
            // race starts, keep the ticket and settle against the same master.
            if(crabRace.CanBet)foreach(var ticket in crabRace.tickets.ToArray())
                if(!FindMaster(ticket.id))
                {
                    if(raceMasters.TryGetValue(ticket.id,out var master)&&master)Credit(master,ticket.wager);
                    crabRace.SetBet(ticket.id,ticket.name,ticket.lane,0,Time.unscaledTime);raceRevision++;
                }
            crabRace.Tick(Time.unscaledTime,crabRace.CanBet&&crabRace.deadline>0&&Time.unscaledTime>=crabRace.deadline?rng.Next():0);
            if(previous!=crabRace.phase){raceRevision++;nextRaceBroadcast=0;if(crabRace.CanBet)raceMasters.Clear();}
            PayRace();
            if(Time.unscaledTime<nextRaceBroadcast)return;
            nextRaceBroadcast=Time.unscaledTime+.25f;
            var snapshot=MakeRaceSnapshot();lounge.Race.Receive(snapshot);
            NetworkServer.SendToAll((short)(requestMessageId.Value+2),new TableReply{json=JsonUtility.ToJson(new TableSnapshot{kind=3,race=snapshot,sceneId=Stage.instance?Stage.instance.netId.Value:0})});
        }
        private void ClearRace(bool settle)
        {
            if(settle&&NetworkServer.active)
            {
                if(crabRace.CanBet)foreach(var ticket in crabRace.tickets)
                    if(raceMasters.TryGetValue(ticket.id,out var master)&&master)Credit(master,ticket.wager);
                crabRace.Finish(Time.unscaledTime);PayRace();
            }
            crabRace=new SharedCrabRace();raceMasters.Clear();raceRevision=0;nextRaceBroadcast=0;
        }
        internal void OpenRace(Interactor actor,WorldCrabRace race)
        {
            var body=LocalMaster?LocalMaster.GetBody():null;
            if(body&&actor&&actor.gameObject==body.gameObject&&InBazaar&&ui.CanOpenInteraction&&Vector3.Distance(body.corePosition,race.InteractionPosition)<8)ui.OpenRace(race);
        }
    }
}
