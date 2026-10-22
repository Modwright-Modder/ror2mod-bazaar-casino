using System;
using System.Collections;
using System.Linq;
using RoR2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BazaarCasino
{
    internal sealed partial class CasinoUI
    {
        private GameObject wagerDialog;
        private bool WagerDialogOpen=>wagerDialog&&wagerDialog.activeSelf;
        private ulong TableBudget()
        {
            var mine=Plugin.Snapshot(selectedTable)?.seats.FirstOrDefault(s=>s.id==MyId);
            return (ulong)(CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.money:0)+(uint)Math.Max(0,mine?.wager??0);
        }
        private ulong RaceBudget()
        {
            var ticket=activeRace?.State?.tickets?.FirstOrDefault(t=>t.id==MyId);
            int reserved=activeRace?.State?.phase==0?ticket?.wager??0:0;
            return (ulong)(CasinoPlugin.LocalMaster?CasinoPlugin.LocalMaster.money:0)+(uint)Math.Max(0,reserved);
        }
        private static bool TryAllIn(ulong available,int maximum,out int amount,out string error)
        {
            amount=0;error=null;
            if(available==0||available>(ulong)maximum){error="All In exceeds the table limit or your wallet is empty.";return false;}
            amount=(int)available;return true;
        }
        private void SetSlotAllIn()
        {
            if(Plugin.Waiting||activeSlotMachine?.IsSpinning==true)return;
            slotAllIn=TryAllIn(CasinoPlugin.LocalMaster.money,Plugin.MaximumBet,out int amount,out slotResponse);
            if(slotAllIn){bet=amount;slotResponse="All In selected. Pull the lever to wager your entire gold.";}
        }
        private void OpenTableAmount()
        {
            if(Plugin.Waiting||betInFlight||draftDirty)return;
            OpenWagerDialog(selectedTable==TableKind.Blackjack?"Total blackjack wager":"Roulette chip amount",Plugin.MinimumBet,Plugin.MaximumBet,TableBudget,
                (amount,all)=>
                {
                    if(all){ChooseTableAllIn();return;}
                    tableAllIn=false;tableBet=amount;
                    if(selectedTable==TableKind.Blackjack)TableAction(ActionKind.TableBet,amount,"Placing custom wager…");
                    DrawTableControls();
                },tableBet);
        }
        private void ChooseTableAllIn()
        {
            if(Plugin.Waiting||draftDirty||betInFlight)return;
            if(!TryAllIn(TableBudget(),Plugin.MaximumBet,out int amount,out tableMessage)){DrawTableControls();return;}
            if(selectedTable==TableKind.Blackjack)
            {
                tableMessage="Placing All In wager…";
                Plugin.Send(ActionKind.TableBet,amount,table:selectedTable,allIn:true);
            }
            else
            {
                tableAllIn=true;tableMessage="All In selected. Click one spot to replace your bets with your entire gold.";
            }
            DrawTableControls();
        }
        private void CloseWagerDialog()
        {
            if(!wagerDialog)return;
            ReleaseCasinoFocus(wagerDialog);wagerDialog.SetActive(false);Destroy(wagerDialog);wagerDialog=null;
        }
        private void OpenWagerDialog(string title,int minimum,int maximum,Func<ulong> budget,Action<int,bool> apply,int current)
        {
            if(Plugin.Waiting||activeSlotMachine?.IsSpinning==true)return;
            CloseWagerDialog();
            wagerDialog=Rect(canvasRoot.transform,"CustomWagerDialog").gameObject;Stretch((RectTransform)wagerDialog.transform);
            // A full-screen raycast shield also blocks cabinet/world clicks while typing.
            wagerDialog.AddComponent<Image>().color=new Color(0,0,0,.55f);
            var panel=Rect(wagerDialog.transform,"WagerPanel");Anchors(panel,.32f,.31f,.68f,.69f);
            panel.gameObject.AddComponent<Image>().color=new Color(.055f,.09f,.12f,.99f);
            var heading=Label(panel,title,27,Gold);Anchors(heading.rectTransform,.05f,.80f,.95f,.96f);
            var limits=Label(panel,"Minimum: "+minimum.ToString("N0")+" · Maximum: "+maximum.ToString("N0")+"\nAvailable: "+budget().ToString("N0"),19,Color.white);
            Anchors(limits.rectTransform,.05f,.59f,.95f,.79f);limits.enableWordWrapping=true;
            var fieldRect=Rect(panel,"Amount");Anchors(fieldRect,.08f,.39f,.92f,.56f);
            fieldRect.gameObject.SetActive(false);
            var background=fieldRect.gameObject.AddComponent<Image>();background.color=new Color(.16f,.21f,.25f);
            var viewport=Rect(fieldRect,"Viewport");Stretch(viewport);viewport.gameObject.AddComponent<RectMask2D>();
            var value=Label(viewport,"",25,Color.white);Anchors(value.rectTransform,.03f,.02f,.97f,.98f);
            value.GetComponent<CasinoLocalizedLabel>().enabled=false;
            var field=fieldRect.gameObject.AddComponent<TMP_InputField>();field.targetGraphic=background;
            field.textViewport=viewport;field.textComponent=value;field.contentType=TMP_InputField.ContentType.IntegerNumber;
            field.characterLimit=10;field.lineType=TMP_InputField.LineType.SingleLine;field.text=Math.Max(minimum,current).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var feedback=Label(panel,"All In may go below the minimum. The maximum still applies.",16,Muted);
            Anchors(feedback.rectTransform,.05f,.21f,.95f,.37f);feedback.enableWordWrapping=true;
            Action<bool> confirm=all=>
            {
                if(Plugin.Waiting){feedback.text="Please wait for the current action.";return;}
                int amount;
                if(all)
                {
                    if(!TryAllIn(budget(),maximum,out amount,out string error)){feedback.text=error;return;}
                }
                else if(!WagerRules.TryAmount(field.text,out amount)||!WagerRules.Valid(amount,minimum,maximum,budget(),false))
                {feedback.text="Enter a whole number within the limits and your available gold.";return;}
                CloseWagerDialog();apply(amount,all);
            };
            var cancel=Button(panel,"Cancel",CloseWagerDialog);Anchors((RectTransform)cancel.transform,.05f,.04f,.32f,.19f);
            var allIn=Button(panel,"All In",()=>confirm(true));Anchors((RectTransform)allIn.transform,.35f,.04f,.62f,.19f);
            var accept=Button(panel,"Apply",()=>confirm(false));Anchors((RectTransform)accept.transform,.65f,.04f,.95f,.19f);
            field.onSubmit.AddListener(_=>confirm(false));field.onFocusSelectAll=true;
            fieldRect.gameObject.SetActive(true);StartCoroutine(FocusAmountField(field));
        }
        private IEnumerator FocusAmountField(TMP_InputField field)
        {yield return null;if(field&&WagerDialogOpen){field.Select();field.ActivateInputField();}}
    }
}
