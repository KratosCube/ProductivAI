using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProductivAI.Core
{
    public delegate void StreamingResponseCallback(string token, bool isComplete);
}
