using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Joyn.DokRouter
{
    public class JGTimelogDomainTable
    {
        //MainDokRouterEngine 51 for first octet
        public const uint _51_MainDokRouterEngine = 0x33000000; //51.0.0.0
        public const uint _51_Pipeline = 0x33010000; //51.1.0.0
        public const uint _51_Activity = 0x330B0000; //51.11.0.0
    }
}
