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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Identity.Client.CacheV2.Impl;
using Microsoft.Identity.Client.CacheV2.Schema;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Core;
using Microsoft.Identity.Core.Cache;
using Microsoft.Identity.Core.OAuth2;

namespace Microsoft.Identity.Client.CacheV2
{
    internal class V2TokenCacheAdapter : ITokenCacheAdapter
    {
        private readonly object _lockObj = new object();
        private readonly IStorageManager _storageManager;
        private TokenCacheV2 _tokenCache;

        public V2TokenCacheAdapter(IStorageManager storageManager)
        {
            _storageManager = storageManager;
        }

        /// <inheritdoc />
        public ITokenCache TokenCache
        {
            get
            {
                lock (_lockObj)
                {
                    return _tokenCache;
                }
            }
            set
            {
                lock (_lockObj)
                {
                    if (value != null && !(value is TokenCacheV2))
                    {
                        throw new InvalidOperationException(
                            "V2TokenCacheAdapter interacts directly with TokenCacheV2 instance types only");
                    }

                    _tokenCache = (TokenCacheV2)value;
                    _tokenCache?.BindToStorageManager(_storageManager);
                }
            }
        }


        /// <inheritdoc />
        public IEnumerable<IAccount> GetAccounts(string authority, bool validateAuthority, RequestContext requestContext)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void RemoveAccount(IAccount account, RequestContext requestContext)
        {
            var operationStatus = _storageManager.DeleteAccount(
                requestContext.TelemetryRequestId,
                account.HomeAccountId.ToString(),
                "environment",  // todo: where to get this?
                "realm");       // todo: where to get this?
            if (operationStatus.StatusType == OperationStatusType.Failure)
            {
                // todo: throw exception!
            }
        }

        /// <inheritdoc />
        public AuthenticationResult SaveAccessAndRefreshToken(
            AuthenticationRequestParameters authenticationRequestParameters,
            MsalTokenResponse msalTokenResponse)
        {
            var cacheManager = new CacheManager(_storageManager, authenticationRequestParameters);
            var account = cacheManager.CacheTokenResponse(msalTokenResponse);
            return new AuthenticationResult(authenticationRequestParameters, msalTokenResponse, account);
        }

        public bool TryReadCache(AuthenticationRequestParameters authenticationRequestParameters, out MsalTokenResponse msalTokenResponse, out IAccount account)
        {
            var cacheManager = new CacheManager(_storageManager, authenticationRequestParameters);
            return cacheManager.TryReadCache(out msalTokenResponse, out account);
        }

        /// <inheritdoc />
        public Task<MsalAccessTokenCacheItem> FindAccessTokenAsync(AuthenticationRequestParameters authenticationRequestParameters)
        {
            throw new NotImplementedException();
            //if (TryReadCache(authenticationRequestParameters, out MsalTokenResponse tokenResponse, out IAccount account))
            //{
            //    return Task.FromResult(new MsalAccessTokenCacheItem(account.Environment, authenticationRequestParameters.ClientId, tokenResponse, account.HomeAccountId.TenantId));
            //}

            //return null;
        }

        /// <inheritdoc />
        public MsalIdTokenCacheItem GetIdTokenCacheItem(MsalIdTokenCacheKey msalIdTokenCacheKey, RequestContext requestContext)
        {
            throw new NotImplementedException();

            //_storageManager.ReadAccount(requestContext.TelemetryRequestId, msalIdTokenCacheKey.)
            //var cacheManager = new CacheManager(_storageManager, authenticationRequestParameters);
            //if (cacheManager.TryReadCache(out MsalTokenResponse tokenResponse, out IAccount account))
            //{
            //    return new MsalIdTokenCacheItem(
            //        account.Environment,
            //        requestContext.ClientId,
            //        tokenResponse,
            //        account.HomeAccountId.TenantId);
            //}
        }

        /// <inheritdoc />
        public Task<MsalRefreshTokenCacheItem> FindRefreshTokenAsync(AuthenticationRequestParameters authenticationRequestParameters)
        {
            throw new NotImplementedException();
            //var cacheManager = new CacheManager(_storageManager, authenticationRequestParameters);
            //if (cacheManager.TryReadCache(out MsalTokenResponse tokenResponse, out IAccount account))
            //{
            //    if (!string.IsNullOrWhiteSpace(tokenResponse.RefreshToken))
            //    {
            //        return Task.FromResult(new MsalRefreshTokenCacheItem(account.Environment, authenticationRequestParameters.ClientId, tokenResponse));
            //    }
            //}

            //return Task.FromResult((MsalRefreshTokenCacheItem)null);
        }

        /// <inheritdoc />
        public void SetKeychainSecurityGroup(string keychainSecurityGroup)
        {
#if iOS
            lock (_lockObj)
            {
                //_tokenCache.TokenCacheAccessor.SetKeychainSecurityGroup(keychainSecurityGroup);  // TODO(mzuber): need the CacheV2 equivalent behavior for this...
                //_tokenCache.LegacyCachePersistence.SetKeychainSecurityGroup(keychainSecurityGroup);
            }
#endif
        }

        /// <inheritdoc />
        public ICollection<string> GetAllAccessTokenCacheItems(RequestContext requestContext)
        {
            // note: this one is TEST ONLY...

            _storageManager.ReadCredentials(
                requestContext.TelemetryRequestId,
                string.Empty,
                string.Empty,
                string.Empty,
                requestContext.ClientId,
                string.Empty,
                string.Empty,
                new HashSet<CredentialType>
                {
                    CredentialType.OAuth2AccessToken
                });

            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public ICollection<MsalAccessTokenCacheItem> GetAllAccessTokensForClient(RequestContext requestContext)
        {
            // note: this one is TEST ONLY...
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public ICollection<MsalAccountCacheItem> GetAllAccounts(RequestContext requestContext)
        {
            _storageManager.ReadAllAccounts(requestContext.TelemetryRequestId);
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public ICollection<string> GetAllAccountCacheItems(RequestContext requestContext)
        {
            // note: this one is TEST ONLY...
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public ICollection<MsalIdTokenCacheItem> GetAllIdTokensForClient(RequestContext requestContext)
        {
            // note: this one is TEST ONLY...

            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public ICollection<string> GetAllIdTokenCacheItems(RequestContext requestContext)
        {
            // note: this one is TEST ONLY...
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public ICollection<MsalRefreshTokenCacheItem> GetAllRefreshTokensForClient(RequestContext requestContext)
        {
            // note: this one is TEST ONLY...
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public ICollection<string> GetAllRefreshTokenCacheItems(RequestContext requestContext)
        {
            // note: this one is TEST ONLY...
            throw new NotImplementedException();
        }

        /// <inheritdoc />
        public void RemoveMsalAccount(IAccount user, RequestContext requestContext)
        {
            var operationStatus = _storageManager.DeleteAccount(
                requestContext.TelemetryRequestId,
                user.HomeAccountId.ToString(),
                user.Environment,
                string.Empty);  // how do we get realm here?

            if (operationStatus.StatusType == OperationStatusType.Failure)
            {
                // todo: throw exception
            }
        }
    }
}