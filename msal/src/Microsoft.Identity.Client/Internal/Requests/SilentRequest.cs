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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Identity.Core;
using Microsoft.Identity.Core.Cache;
using Microsoft.Identity.Core.Http;
using Microsoft.Identity.Core.Instance;
using Microsoft.Identity.Core.OAuth2;
using Microsoft.Identity.Core.Telemetry;

namespace Microsoft.Identity.Client.Internal.Requests
{
    internal class SilentRequest : RequestBase
    {
        public SilentRequest(
            IServiceBundle serviceBundle,
            AuthenticationRequestParameters authenticationRequestParameters,
            ApiEvent.ApiIds apiId,
            bool forceRefresh)
            : base(serviceBundle, authenticationRequestParameters, apiId)
        {
            ForceRefresh = forceRefresh;
        }

        public bool ForceRefresh { get; }

        internal override async Task<AuthenticationResult> ExecuteAsync(CancellationToken cancellationToken)
        {
            if (TokenCacheAdapter.TokenCache == null)
            {
                throw new MsalUiRequiredException(
                    MsalUiRequiredException.TokenCacheNullError,
                    "Token cache is set to null. Silent requests cannot be executed.");
            }

            if (TokenCacheAdapter.TryReadCache(
                AuthenticationRequestParameters,
                out var msalTokenResponse,
                out var account))
            {
                if (!ForceRefresh && msalTokenResponse.HasAccessToken)
                {
                    return new AuthenticationResult(AuthenticationRequestParameters, msalTokenResponse, account);
                }

                if (msalTokenResponse.HasRefreshToken)
                {
                    AuthenticationRequestParameters.RequestContext.Logger.Verbose("Refreshing access token...");
                    await ResolveAuthorityEndpointsAsync().ConfigureAwait(false);
                    var msalTokenServerResponse = await SendTokenRequestAsync(GetBodyParameters(msalTokenResponse.RefreshToken), cancellationToken)
                                                .ConfigureAwait(false);

                    if (msalTokenServerResponse.RefreshToken == null)
                    {
                        msalTokenServerResponse.RefreshToken = msalTokenResponse.RefreshToken;
                        AuthenticationRequestParameters.RequestContext.Logger.Info(
                            "Refresh token was missing from the token refresh response, so the refresh token in the request is returned instead");
                    }

                    return CacheTokenResponseAndCreateAuthenticationResult(msalTokenServerResponse);
                }
            }

            AuthenticationRequestParameters.RequestContext.Logger.Verbose("No Refresh Token was found in the cache");
            throw new MsalUiRequiredException(MsalUiRequiredException.NoTokensFoundError, "No Refresh Token found in the cache");
        }

        private Dictionary<string, string> GetBodyParameters(string refreshTokenSecret)
        {
            var dict = new Dictionary<string, string>
            {
                [OAuth2Parameter.GrantType] = OAuth2GrantType.RefreshToken,
                [OAuth2Parameter.RefreshToken] = refreshTokenSecret
            };

            return dict;
        }
    }
}