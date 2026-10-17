using System;

namespace BazaarCasino
{
    public static class CaseLayout
    {
        public const float WindowPixels=720;
        public const float WindowBottom=1.35f;
        public const float PixelScale=.005f;
        public const float CabinetHeight=WindowBottom+WindowPixels*PixelScale+.8f;
        public static void RowBounds(int count,int row,out float bottom,out float top)
        {
            if(!CaseBatchRules.ValidCount(count)||row<0||row>=count)throw new ArgumentOutOfRangeException();
            float height=count==1?230:72;
            float start=(WindowPixels+count*height)*.5f;
            top=(start-row*height)/WindowPixels;bottom=(start-(row+1)*height)/WindowPixels;
        }
    }
}
