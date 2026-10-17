using System;
using System.Collections.Generic;

namespace BazaarCasino
{
    // No Unity dependencies: the same rules are exercised by the console verification suite.
    public static class Games
    {
        public static readonly int[] Wheel = {0,32,15,19,4,21,2,25,17,34,6,27,13,36,11,30,8,23,10,5,24,16,33,1,20,14,31,9,22,18,29,7,28,12,35,3,26};
        public static readonly string[] Symbols = { "CHERRY", "LEMON", "BELL", "STAR", "SEVEN", "MOON" };
        private static readonly int[] SlotWeights = {35,25,18,12,7,3};
        private static readonly int[] SlotMultipliers = {3,5,8,12,25,60};
        public static bool IsRed(int n) => n > 0 && ((n <= 10 || n >= 19 && n <= 28) ? n % 2 == 1 : n % 2 == 0);

        // Choice: red, black, even, odd, 1-18, 19-36, straight number.
        public static int RouletteMultiplier(int number, int choice, int straight)
        {
            if (number < 0 || number > 36 || choice < 0 || choice > 12) return 0;
            if (choice == 6) return number == straight ? 36 : 0;
            if (number == 0) return 0;
            if (choice >= 7 && choice <= 9) return (number - 1) / 12 == choice - 7 ? 3 : 0;
            if (choice >= 10) return (number - 1) % 3 == choice - 10 ? 3 : 0;
            bool wins = choice == 0 ? IsRed(number) : choice == 1 ? !IsRed(number) :
                choice == 2 ? number % 2 == 0 : choice == 3 ? number % 2 == 1 :
                choice == 4 ? number <= 18 : number >= 19;
            return wins ? 2 : 0;
        }
        public static int SlotSymbol(Random rng)
        {
            int n = rng.Next(100);
            for (int i = 0; i < SlotWeights.Length; i++) { n -= SlotWeights[i]; if (n < 0) return i; }
            return 0;
        }
        public static int SlotMultiplier(int a, int b, int c)
        {
            if (a < 0 || b < 0 || c < 0 || a >= 6 || b >= 6 || c >= 6) return 0;
            if (a == b && b == c) return SlotMultipliers[a];
            return a == b || b == c || a == c ? 1 : 0;
        }
        public static int Value(IEnumerable<int> cards)
        {
            int value = 0, aces = 0;
            foreach (int card in cards)
            {
                int rank = card % 13;
                value += rank == 0 ? 11 : Math.Min(rank + 1, 10);
                if (rank == 0) aces++;
            }
            while (value > 21 && aces-- > 0) value -= 10;
            return value;
        }
        public static bool Natural(List<int> cards) => cards.Count == 2 && Value(cards) == 21;
        public static string CardText(int card)
        {
            string[] ranks = {"A","2","3","4","5","6","7","8","9","10","J","Q","K"};
            string[] suits = {"S","H","D","C"};
            return ranks[card % 13] + suits[card / 13 % 4];
        }
    }

    public sealed class BlackjackRound
    {
        public readonly List<int> player = new List<int>();
        public readonly List<int> dealer = new List<int>();
        public readonly List<int> deck = new List<int>();
        public int wager;
        public bool active;
        public int payout;
        public string result;
        public BlackjackRound(Random rng, int bet)
        {
            wager = bet;
            for (int i = 0; i < 312; i++) deck.Add(i % 52);
            for (int i = deck.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); int c = deck[i]; deck[i] = deck[j]; deck[j] = c; }
            player.Add(Draw()); dealer.Add(Draw()); player.Add(Draw()); dealer.Add(Draw());
            active = true;
            if (Games.Natural(player) || Games.Natural(dealer)) Settle();
        }
        public int Draw() { int last = deck.Count - 1; int c = deck[last]; deck.RemoveAt(last); return c; }
        public void Hit()
        {
            if (!active) return;
            player.Add(Draw());
            if (Games.Value(player) >= 21) Stand();
        }
        public void Stand()
        {
            if (!active) return;
            if (Games.Value(player) <= 21)
                while (Games.Value(dealer) < 17) dealer.Add(Draw());
            Settle();
        }
        public void Double()
        {
            if (!active || player.Count != 2) return;
            wager *= 2; player.Add(Draw()); Stand();
        }
        private void Settle()
        {
            active = false;
            int p = Games.Value(player), d = Games.Value(dealer);
            if (p > 21) { payout = 0; result = "Bust"; }
            else if (Games.Natural(dealer)) { payout = Games.Natural(player) ? wager : 0; result = payout > 0 ? "Push" : "Dealer blackjack"; }
            else if (Games.Natural(player)) { payout = wager + wager * 3 / 2; result = "Blackjack!"; }
            else if (d > 21 || p > d) { payout = wager * 2; result = d > 21 ? "Dealer bust — you win" : "You win"; }
            else if (p == d) { payout = wager; result = "Push"; }
            else { payout = 0; result = "Dealer wins"; }
        }
    }
}
