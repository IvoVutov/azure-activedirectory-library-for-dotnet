﻿// ------------------------------------------------------------------------------
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

using System;
using System.Collections.Generic;
using Microsoft.Identity.Core.CacheV2;
using Microsoft.Identity.Core.CacheV2.Impl;
using Microsoft.Identity.Core.CacheV2.Impl.InMemory;
using Microsoft.Identity.Core.CacheV2.Impl.Utils;
using Microsoft.Identity.Core.CacheV2.Schema;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.MSAL.NET.Unit.net45.CacheV2Tests
{
    [TestClass]
    public class TokenCacheV2Tests
    {
        private InMemoryCachePathStorage _cachePathStorage;
        private FileSystemCredentialPathManager _credentialPathManager;
        private StorageManager _storageManager;
        private PathStorageWorker _storageWorker;
        private TokenCacheV2 _tokenCache;

        private const string TheSecret = "the_secret";

        [TestInitialize]
        public void TestInitialize()
        {
            _tokenCache = new TokenCacheV2();

            _cachePathStorage = new InMemoryCachePathStorage();
            _credentialPathManager = new FileSystemCredentialPathManager();
            _storageWorker = new PathStorageWorker(_cachePathStorage, _credentialPathManager);
            _storageManager = new StorageManager(_storageWorker);

            _tokenCache.BindToStorageManager(_storageManager);
        }

        [TestCleanup]
        public void TestCleanup()
        {
        }

        [TestMethod]
        [TestCategory("TokenCacheTests")]
        public void GetExactScopesMatchedAccessTokenTest()
        {
            var atItem = Credential.CreateAccessToken(
                MsalTestConstants.HomeAccountId,
                MsalTestConstants.ProductionPrefNetworkEnvironment,
                new Uri(MsalTestConstants.AuthorityTestTenant).GetRealm(),
                MsalTestConstants.ClientId,
                ScopeUtils.JoinScopes(MsalTestConstants.Scope),
                TimeUtils.GetSecondsFromEpochNow(),
                TimeUtils.GetSecondsFromEpochNow() + MsalCacheV2TestConstants.ValidExpiresIn,
                TimeUtils.GetSecondsFromEpochNow() + MsalCacheV2TestConstants.ValidExtendedExpiresIn,
                TheSecret,
                string.Empty);

            _storageWorker.WriteCredentials(
                new List<Credential>
                {
                    atItem
                });

            var cacheManager = new CacheManager(
                _storageManager,
                new AuthParameters
                {
                    AccountId = MsalTestConstants.HomeAccountId,
                    Authority = new Uri(MsalTestConstants.AuthorityTestTenant),
                    ClientId = MsalTestConstants.ClientId,
                    RequestedScopes = MsalCacheV2TestConstants.Scope
                });

            Assert.IsTrue(cacheManager.TryReadCache(out var tokenResponse, out var accountResponse));
            Assert.IsNotNull(tokenResponse);
            Assert.IsNull(accountResponse);

            Assert.AreEqual(TheSecret, tokenResponse.AccessToken);
        }
    }
}