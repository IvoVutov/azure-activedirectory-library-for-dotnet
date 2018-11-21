using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Identity.Core.CacheV2.Impl;
using Microsoft.Identity.Core.CacheV2.Schema;

namespace Microsoft.Identity.Core.CacheV2
{
    internal interface ICacheManager
    {
        bool TryReadCache(out TokenResponse tokens, out Account account);
        Account CacheTokenResponse(TokenResponse tokenResponse);
        void DeleteCachedRefreshToken();
    }
}
