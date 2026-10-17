using System;

namespace BazaarCasino
{
    // Rendering and pointer hit testing share the same table-local geometry.
    public static class RouletteLayout
    {
        public static SeatPoint Centre(int choice,int number)
        {
            if(choice==6)return number==0?new SeatPoint(-.67f,.38f):new SeatPoint(-.30f+(number-1)/3*.32f,.95f-(2-(number-1)%3)*.56f);
            if(choice<6){int[] columns={2,3,1,4,0,5};return new SeatPoint(-.12f+columns[choice]*.64f,-1.40f);}
            if(choice<=9)return new SeatPoint(.35f+(choice-7)*1.28f,-.88f);
            return new SeatPoint(.35f+(choice-10)*1.28f,1.50f);
        }
        public static SeatPoint Size(int choice,int number)=>choice==6?(number==0?new SeatPoint(.30f,1.66f):new SeatPoint(.31f,.53f)):new SeatPoint(choice<6?.61f:1.21f,.24f);
        public static bool Hit(float x,float z,out int choice,out int number)
        {
            choice=number=-1;
            if(float.IsNaN(x)||float.IsNaN(z)||float.IsInfinity(x)||float.IsInfinity(z))return false;
            for(int c=0;c<=12;c++)for(int n=0;n<(c==6?37:1);n++)
            {
                var p=Centre(c,n);var size=Size(c,n);
                if(Math.Abs(x-p.x)<=size.x*.5f&&Math.Abs(z-p.z)<=size.z*.5f){choice=c;number=n;return true;}
            }
            return false;
        }
        public static string Name(int choice,int number)
        {
            string[] names={"Red","Black","Even","Odd","1–18","19–36","","1st 12","2nd 12","3rd 12","Column 1","Column 2","Column 3"};
            return choice==6?number.ToString():names[choice];
        }
    }
}
