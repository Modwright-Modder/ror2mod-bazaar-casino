using System;

namespace BazaarCasino
{
    internal static class GachaRules
    {
        internal static bool NormalReward(string name,bool worldUnique,bool excludedFromDrops,bool scrap,bool excludedFromCommand)
        {
            if(worldUnique||excludedFromDrops||scrap||excludedFromCommand)return false;
            // Belmont's grant hook rejects these relics on all other survivors.
            // Do not sell a generic random case that can roll an unusable weapon.
            switch(name)
            {
                case "BelmontMorningStar":case "BelmontCrissaegrim":case "BelmontHolyCross":case "BelmontWingedBoots":case "BelmontBrotherSoul":return false;
                default:return true;
            }
        }
        internal static int ChooseTier(double roll, int[] weights, bool[] available)
        {
            if (roll < 0 || roll >= 1 || weights == null || available == null || weights.Length != 3 || available.Length != 3)
                throw new ArgumentException("Invalid case draw.");
            int total = 0;
            for (int i = 0; i < 3; i++) if (available[i]) total += Math.Max(0, weights[i]);
            if (total == 0) return -1;
            double cursor = roll * total;
            for (int i = 0; i < 3; i++)
            {
                int weight = available[i] ? Math.Max(0, weights[i]) : 0;
                if (cursor < weight) return i;
                cursor -= weight;
            }
            return -1;
        }
        internal static float RevealProgress(float elapsed)
        { float p = Math.Max(0, Math.Min(1, elapsed / SpinTiming.Case)); return 1 - (float)Math.Pow(1 - p, 4); }
        // Each row starts/stops slightly later; the final row still finishes
        // exactly when the host dispenses the paid batch at SpinTiming.Case.
        internal static float ReelElapsed(float elapsed,int row,int count)
        {
            if(!CaseBatchRules.ValidCount(count)||row<0||row>=count)throw new ArgumentOutOfRangeException();
            if(elapsed>=SpinTiming.Case)return SpinTiming.Case;
            const float delay=.06f;
            float duration=SpinTiming.Case-(count-1)*delay;
            return Math.Max(0,Math.Min(SpinTiming.Case,(elapsed-row*delay)*SpinTiming.Case/duration));
        }
        internal static int ScalePrice(int greenPrice, float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier) || multiplier <= 0) multiplier = 1;
            return (int)Math.Max(1, Math.Min(100000000, Math.Floor(greenPrice * (double)multiplier)));
        }
        internal static float CardOffset(float viewportWidth,int card,float jitter,float elapsed,float stride=180)
        {
            var from=viewportWidth*.5f-3.5f*stride;
            var to=viewportWidth*.5f-(card+.5f)*stride+jitter;
            return from+(to-from)*RevealProgress(elapsed);
        }
    }
}
