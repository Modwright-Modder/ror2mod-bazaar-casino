using System;

namespace BazaarCasino
{
    public static class WagerRules
    {
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
