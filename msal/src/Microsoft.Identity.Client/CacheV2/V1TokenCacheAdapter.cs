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
using Microsoft.Identity.Client.CacheV2.Impl.Utils;
using Microsoft.Identity.Client.Internal.Requests;
using Microsoft.Identity.Core;
using Microsoft.Identity.Core.Cache;
using Microsoft.Identity.Core.Instance;
using Microsoft.Identity.Core.OAuth2;
using Microsoft.Identity.Core.Telemetry;

namespace Microsoft.Identity.Client.CacheV2
{
    internal class V1TokenCacheAdapter : ITokenCacheAdapter
    {
        private readonly IAadInstanceDiscovery _aadInstanceDiscovery;
        private readonly IValidatedAuthoritiesCache _validatedAuthoritiesCache;
        private readonly string _clientId;
        private readonly object _lockObj = new object();
        private readonly ITelemetryManager _telemetryManager;
        private TokenCache _tokenCache;

        public V1TokenCacheAdapter(
            ITelemetryManager telemetryManager,
            IAadInstanceDiscovery aadInstanceDiscovery,
            IValidatedAuthoritiesCache validatedAuthoritiesCache,
            string clientId)
        {
            _telemetryManager = telemetryManager;
            _aadInstanceDiscovery = aadInstanceDiscovery;
            _validatedAuthoritiesCache = validatedAuthoritiesCache;
            _clientId = clientId;
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
                    if (value != null && !(value is TokenCache))
                    {
                        throw new InvalidOperationException(
                            "V1TokenCacheAdapter interacts directly with TokenCache instance types only");
                    }

                    _tokenCache = (TokenCache)value;
                    if (_tokenCache != null)
                    {
                        _tokenCache.ClientId = _clientId;
                        _tokenCache.TelemetryManager = _telemetryManager;
                        _tokenCache.AadInstanceDiscovery = _aadInstanceDiscovery;
                    }
                }
            }
        }

        /// <inheritdoc />
        public IEnumerable<IAccount> GetAccounts(string authority, bool validateAuthority, RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetAccounts(authority, validateAuthority, requestContext);
            }
        }

        /// <inheritdoc />
        public void RemoveAccount(IAccount account, RequestContext requestContext)
        {
            lock (_lockObj)
            {
                _tokenCache?.RemoveAccount(account, requestContext);
            }
        }

        /// <inheritdoc />
        public bool TryReadCache(
            AuthenticationRequestParameters authenticationRequestParameters,
            out MsalTokenResponse msalTokenResponse,
            out IAccount account)
        {
            msalTokenResponse = null;
            account = null;

            MsalAccessTokenCacheItem atItem = FindAccessTokenAsync(authenticationRequestParameters).ConfigureAwait(false).GetAwaiter().GetResult();
            MsalRefreshTokenCacheItem rtItem = FindRefreshTokenAsync(authenticationRequestParameters).ConfigureAwait(false).GetAwaiter().GetResult();
            
            if (atItem == null && rtItem == null)
            {
                return false;
            }

            var tokenResponse = new MsalTokenResponse();
            if (atItem != null)
            {
                tokenResponse.AccessToken = atItem.Secret;
                tokenResponse.ExpiresIn = Convert.ToInt64(DateTime.UtcNow.Subtract(atItem.ExpiresOn).TotalSeconds);
                tokenResponse.ExtendedExpiresIn = Convert.ToInt64(DateTime.UtcNow.Subtract(atItem.ExtendedExpiresOn).TotalSeconds);
                tokenResponse.Scope = ScopeUtils.JoinScopes(atItem.ScopeSet);

                var msalIdTokenCacheItem = GetIdTokenCacheItem(new MsalIdTokenCacheKey(atItem.Environment, atItem.TenantId, atItem.HomeAccountId, atItem.ClientId), null);
                tokenResponse.IdToken = msalIdTokenCacheItem.Secret;

                account = new Account(msalIdTokenCacheItem.HomeAccountId, msalIdTokenCacheItem.IdToken.PreferredUsername, msalIdTokenCacheItem.Environment);
            }

            if (rtItem != null)
            {
                tokenResponse.RefreshToken = rtItem.Secret;
            }

            msalTokenResponse = tokenResponse;
            return true;
        }

        public AuthenticationResult SaveAccessAndRefreshToken(
            AuthenticationRequestParameters authenticationRequestParameters,
            MsalTokenResponse msalTokenResponse)
        {
            lock (_lockObj)
            {
                if (TokenCache == null)
                {
                    return new AuthenticationResult(authenticationRequestParameters, msalTokenResponse);
                }
                else
                {
                    authenticationRequestParameters.RequestContext.Logger.Info("Saving Token Response to cache..");

                    var tuple = _tokenCache.SaveAccessAndRefreshToken(
                        _validatedAuthoritiesCache,
                        _aadInstanceDiscovery,
                        authenticationRequestParameters,
                        msalTokenResponse);

                    return new AuthenticationResult(tuple.Item1, tuple.Item2);
                }
            }
        }

        /// <inheritdoc />
        public Task<MsalAccessTokenCacheItem> FindAccessTokenAsync(AuthenticationRequestParameters authenticationRequestParameters)
        {
            lock (_lockObj)
            {
                if (_tokenCache == null)
                {
                    return Task.FromResult<MsalAccessTokenCacheItem>(null);
                }

                return _tokenCache.FindAccessTokenAsync(authenticationRequestParameters);
            }
        }

        /// <inheritdoc />
        public MsalIdTokenCacheItem GetIdTokenCacheItem(MsalIdTokenCacheKey msalIdTokenCacheKey, RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetIdTokenCacheItem(msalIdTokenCacheKey, requestContext);
            }
        }

        /// <inheritdoc />
        public Task<MsalRefreshTokenCacheItem> FindRefreshTokenAsync(AuthenticationRequestParameters authenticationRequestParameters)
        {
            lock (_lockObj)
            {
                if (_tokenCache == null)
                {
                    return Task.FromResult<MsalRefreshTokenCacheItem>(null);
                }
                return _tokenCache.FindRefreshTokenAsync(authenticationRequestParameters);
            }
        }

        public void SetKeychainSecurityGroup(string keychainSecurityGroup)
        {
#if iOS
            lock (_lockObj)
            {
                _tokenCache.TokenCacheAccessor.SetKeychainSecurityGroup(keychainSecurityGroup);
                _tokenCache.LegacyCachePersistence.SetKeychainSecurityGroup(keychainSecurityGroup);
            }
#endif
        }

        /// <inheritdoc />
        public ICollection<string> GetAllAccessTokenCacheItems(RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetAllAccessTokenCacheItems(requestContext);
            }
        }

        /// <inheritdoc />
        public ICollection<MsalAccessTokenCacheItem> GetAllAccessTokensForClient(RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetAllAccessTokensForClient(requestContext);
            }
        }

        /// <inheritdoc />
        public ICollection<MsalAccountCacheItem> GetAllAccounts(RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetAllAccounts(requestContext);
            }
        }

        /// <inheritdoc />
        public ICollection<string> GetAllAccountCacheItems(RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetAllAccountCacheItems(requestContext);
            }
        }

        /// <inheritdoc />
        public ICollection<MsalIdTokenCacheItem> GetAllIdTokensForClient(RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetAllIdTokensForClient(requestContext);
            }
        }

        /// <inheritdoc />
        public ICollection<string> GetAllIdTokenCacheItems(RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetAllIdTokenCacheItems(requestContext);
            }
        }

        /// <inheritdoc />
        public ICollection<MsalRefreshTokenCacheItem> GetAllRefreshTokensForClient(RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetAllRefreshTokensForClient(requestContext);
            }
        }

        /// <inheritdoc />
        public ICollection<string> GetAllRefreshTokenCacheItems(RequestContext requestContext)
        {
            lock (_lockObj)
            {
                return _tokenCache?.GetAllRefreshTokenCacheItems(requestContext);
            }
        }

        /// <inheritdoc />
        public void RemoveMsalAccount(IAccount account, RequestContext requestContext)
        {
            lock (_lockObj)
            {
                _tokenCache?.RemoveMsalAccount(account, requestContext);
            }
        }
    }
}