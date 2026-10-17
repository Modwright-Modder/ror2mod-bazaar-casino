using System;
using System.Globalization;

namespace BazaarCasino
{
    internal enum ManualCommand { None,BluePortal,Gold }
    internal static class ManualCommandText
    {
        // Parse only our two local shortcuts. Never forward arbitrary chat text
        // to the console, including text after separators or extra arguments.
        internal static bool TryParse(string text,out ManualCommand command,out int amount,out string error)
        {
            command=ManualCommand.None;amount=0;error=null;
            string input=(text??"").Trim();
            if(input.StartsWith("/",StringComparison.Ordinal))input=input.Substring(1);
            var parts=input.Split((char[])null,StringSplitOptions.RemoveEmptyEntries);
            if(parts.Length==0)return false;
            if(parts[0].Equals("modwright_blueportal",StringComparison.OrdinalIgnoreCase))
            {
                command=ManualCommand.BluePortal;
                if(parts.Length!=1)error="Usage: /modwright_blueportal";
                return true;
            }
            if(!parts[0].Equals("modwright_gold",StringComparison.OrdinalIgnoreCase))return false;
            command=ManualCommand.Gold;
            if(parts.Length!=2)error="Usage: /modwright_gold <amount>";
            else if(!int.TryParse(parts[1],NumberStyles.None,CultureInfo.InvariantCulture,out amount)||amount<1||amount>1000000)
                error="Choose an amount from 1 to 1,000,000 gold.";
            return true;
        }
    }
}
