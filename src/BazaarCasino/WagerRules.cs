using System;

namespace BazaarCasino
{
    public static class WagerRules
    {
        // The exception is valid only for an exact, positive full-bankroll wager.
        // Reservation belongs to this player and this game, not another table.
        public static bool Valid(int amount,int minimum,int maximum,ulong available,bool allIn,bool refund=false)
        {
            if(amount==0)return refund&&!allIn;
            return amount>0&&minimum>0&&maximum>=minimum&&amount<=maximum&&(ulong)amount<=available&&
                (allIn?(ulong)amount==available:amount>=minimum);
        }
        public static bool TryAmount(string text,out int amount)=>int.TryParse(text,System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,out amount)&&amount>0;
        public static int ScaledMinimum(int floor,int casePrice,int limit)
        {
            if(floor<1||casePrice<1||limit<1)throw new ArgumentOutOfRangeException();
            return Math.Min(Math.Max(floor,limit),Math.Max(floor,casePrice));
        }
        public static int[] Denominations(int minimum,int maximum)
        {
            if(minimum<1||maximum<minimum)throw new ArgumentOutOfRangeException();
            var values=new System.Collections.Generic.List<int>();
            foreach(int multiple in new[]{1,2,5,10})
            {int value=(int)Math.Min(maximum,(long)minimum*multiple);if(!values.Contains(value))values.Add(value);}
            return values.ToArray();
        }
        public static int Edit(int current,int chip,bool remove,int minimum,int maximum,uint wallet)
        {
            if(current<0||chip<minimum||minimum<1||maximum<minimum)throw new ArgumentOutOfRangeException();
            long next=remove?Math.Max(0,(long)current-chip):(long)current+chip;
            if(remove&&next<minimum)next=0;
            if(next>maximum||next-current>wallet)throw new InvalidOperationException("Insufficient gold or table limit reached.");
            return (int)next;
        }
    }
}
