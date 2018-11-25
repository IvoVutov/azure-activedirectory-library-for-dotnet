// ------------------------------------------------------------------------------
// 
// Copyright (c) Microsoft Corporation.
// All rights reserved.
// 
// This code is licensed under the MIT License.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files(the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and / or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions :
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
// 
// ------------------------------------------------------------------------------

using Microsoft.Identity.Client.CacheV2.Impl;
using Microsoft.Identity.Client.CacheV2.Impl.InMemory;
using Microsoft.Identity.Core;
using Microsoft.Identity.Core.Instance;
using Microsoft.Identity.Core.Telemetry;

namespace Microsoft.Identity.Client.CacheV2
{
    internal static class TokenCacheAdapterFactory
    {
        private static bool UsingV1 { get; set; } = true;

        public static ITokenCacheAdapter CreateTokenCacheAdapter(
            ITelemetryManager telemetryManager,
            IAadInstanceDiscovery aadInstanceDiscovery,
            IValidatedAuthoritiesCache validatedAuthoritiesCache,
            IStorageManager storageManager,
            string clientId)
        {
            if (UsingV1)
            {
                return new V1TokenCacheAdapter(telemetryManager, aadInstanceDiscovery, validatedAuthoritiesCache, clientId);
            }
            else
            {
                return new V2TokenCacheAdapter(storageManager);
            }
        }

        public static ITokenCache CreateTokenCache()
        {
            if (UsingV1)
            {
                return new TokenCache();
            }
            else
            {
                return new TokenCacheV2();
            }
        }

        public static IStorageManager CreateStorageManagerForTests()
        {
            var storageManager = new StorageManager(
                new PathStorageWorker(new InMemoryCachePathStorage(), new FileSystemCredentialPathManager()),
                new AdalLegacyCacheManager(PlatformProxyFactory.GetPlatformProxy().CreateLegacyCachePersistence()));
            return storageManager;
        }
    }
}