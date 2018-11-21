﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Identity.Core.CacheV2.Impl;
using Microsoft.Identity.Core.CacheV2.Schema;

namespace Microsoft.Identity.Core.CacheV2
{
    internal class CacheManager : ICacheManager
    {
        private readonly AuthParameters _authParameters;
        private readonly IStorageManager _storageManager;

        public CacheManager(IStorageManager storageManager, AuthParameters authParameters)
        {
            _storageManager = storageManager;
            _authParameters = authParameters;
        }

        public bool TryReadCache(out TokenResponse tokens, out Account account)
        {
            tokens = null;
            account = null;

            string homeAccountId = _authParameters.AccountId;
            var authority = _authParameters.Authority;
            string environment = authority.GetEnvironment(); // todo: ?
            string realm = authority.GetRealm(); // todo: ?
            string clientId = _authParameters.ClientId;
            string target = string.Join(" ", _authParameters.RequestedScopes);

            if (string.IsNullOrWhiteSpace(homeAccountId) || string.IsNullOrWhiteSpace(environment) ||
                string.IsNullOrWhiteSpace(realm) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(target))
            {
                tokens = null;
                account = null;
                return false;
            }

            var credentialsResponse = _storageManager.ReadCredentials(
                string.Empty,
                homeAccountId,
                environment,
                realm,
                clientId,
                string.Empty,
                target,
                new HashSet<CredentialType>
                {
                    CredentialType.OAuth2AccessToken,
                    CredentialType.OAuth2RefreshToken,
                    CredentialType.OidcIdToken
                });

            if (credentialsResponse.Status.StatusType != OperationStatusType.Success)
            {
                // error reading credentials from the cache
                return false;
            }

            if (!credentialsResponse.Credentials.Any())
            {
                // no credentials found in the cache
                return false;
            }

            if (credentialsResponse.Credentials.ToList().Count > 3)
            {
                // expected to read up to 3 credentials from cache, somehow read more...
            }

            Credential accessToken = null;
            Credential refreshToken = null;
            Credential idToken = null;

            foreach (var credential in credentialsResponse.Credentials)
            {
                switch (credential.CredentialType)
                {
                case CredentialType.OAuth2AccessToken:
                    if (accessToken != null)
                    {
                        // warning, more than one access token read from cache
                    }

                    accessToken = credential;
                    break;
                case CredentialType.OAuth2RefreshToken:
                    if (refreshToken != null)
                    {
                        // warning, more than one refresh token read from cache
                    }

                    refreshToken = credential;
                    break;
                case CredentialType.OidcIdToken:
                    if (idToken != null)
                    {
                        // warning, more than one idtoken read from cache
                    }

                    idToken = credential;
                    break;
                default:
                    // warning unknown credential type
                    break;
                }
            }

            if (idToken == null)
            {
                // warning, no id token
            }

            if (accessToken == null)
            {
                // warning no access token
            }
            else if (!IsAccessTokenValid(accessToken))
            {
                DeleteCachedAccessToken(
                    homeAccountId,
                    environment,
                    realm,
                    clientId,
                    target);
                accessToken = null;
            }

            if (accessToken != null)
            {
                refreshToken = null; // there's no need to return a refresh token, just the access token
            }
            else if (refreshToken == null)
            {
                // warning, no valid access token and no refresh token found in cache
                return false;
            }

            Microsoft.Identity.Core.CacheV2.Impl.IdToken idTokenJwt = null;
            if (idToken != null)
            {
                idTokenJwt = new Microsoft.Identity.Core.CacheV2.Impl.IdToken(idToken.Secret);
            }

            if (accessToken != null)
            {
                var accountResponse = _storageManager.ReadAccount(string.Empty, homeAccountId, environment, realm);
                if (accountResponse.Status.StatusType != OperationStatusType.Success)
                {
                    // warning, error reading account from cache
                }
                else
                {
                    account = accountResponse.Account;
                }

                if (account == null)
                {
                    // warning, no account in cache, will still return token if found
                }
            }

            tokens = new TokenResponse(idTokenJwt, accessToken, refreshToken);
            return true;
        }

        public Account CacheTokenResponse(TokenResponse tokenResponse)
        {
            string homeAccountId = GetHomeAccountId(tokenResponse);
            var authority = _authParameters.Authority;
            string environment = authority.GetEnvironment();
            string realm = authority.GetRealm();
            string clientId = _authParameters.ClientId;
            string target = ScopeUtils.JoinScopes(tokenResponse.GrantedScopes);

            if (string.IsNullOrWhiteSpace(homeAccountId) || string.IsNullOrWhiteSpace(environment) ||
                string.IsNullOrWhiteSpace(realm) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(target))
            {
                // skipping writing to the cache, PK is empty
                return null;
            }

            var credentialsToWrite = new List<Credential>();
            long cachedAt = DateTime.UtcNow.Ticks; // todo: this is probably wrong

            if (tokenResponse.HasRefreshToken)
            {
                credentialsToWrite.Add(
                    Credential.CreateRefreshToken(
                        homeAccountId,
                        environment,
                        clientId,
                        cachedAt,
                        tokenResponse.RefreshToken,
                        string.Empty));
            }

            if (tokenResponse.HasAccessToken)
            {
                long expiresOn = tokenResponse.ExpiresOn.Ticks; // todo: this is probably wrong
                long extendedExpiresOn = tokenResponse.ExtendedExpiresOn.Ticks; // todo: this is probably wrong

                var accessToken = Credential.CreateAccessToken(
                    homeAccountId,
                    environment,
                    realm,
                    clientId,
                    target,
                    cachedAt,
                    expiresOn,
                    extendedExpiresOn,
                    tokenResponse.AccessToken,
                    string.Empty);
                if (IsAccessTokenValid(accessToken))
                {
                    credentialsToWrite.Add(accessToken);
                }
            }

            var idTokenJwt = tokenResponse.IdToken;
            if (!idTokenJwt.IsEmpty)
            {
                credentialsToWrite.Add(
                    Credential.CreateIdToken(
                        homeAccountId,
                        environment,
                        realm,
                        clientId,
                        cachedAt,
                        idTokenJwt.Raw,
                        string.Empty));
            }

            var status = _storageManager.WriteCredentials(string.Empty, credentialsToWrite);
            if (status.StatusType != OperationStatusType.Success)
            {
                // warning error writing to cache
            }

            // if id token jwt is empty, return null

            string localAccountId = GetLocalAccountId(idTokenJwt);
            var authorityType = GetAuthorityType();

            var account = Account.Create(
                homeAccountId,
                environment,
                realm,
                localAccountId,
                authorityType,
                idTokenJwt.PreferredUsername,
                idTokenJwt.GivenName,
                idTokenJwt.FamilyName,
                idTokenJwt.MiddleName,
                idTokenJwt.Name,
                idTokenJwt.AlternativeId,
                tokenResponse.RawClientInfo,
                string.Empty);

            status = _storageManager.WriteAccount(string.Empty, account);

            if (status.StatusType != OperationStatusType.Success)
            {
                // warning error writing account to cache
            }

            return account;
        }

        public void DeleteCachedRefreshToken()
        {
            string homeAccountId = _authParameters.AccountId;
            string environment = _authParameters.Authority.GetEnvironment();
            string clientId = _authParameters.ClientId;

            if (string.IsNullOrWhiteSpace(homeAccountId) || string.IsNullOrWhiteSpace(environment) ||
                string.IsNullOrWhiteSpace(clientId))
            {
                // warning failed to delete refresh token from cache, pk is empty
                return;
            }

            var status = _storageManager.DeleteCredentials(
                string.Empty,
                homeAccountId,
                environment,
                string.Empty,
                clientId,
                string.Empty,
                string.Empty,
                new HashSet<CredentialType>
                {
                    CredentialType.OAuth2RefreshToken
                });
            if (status.StatusType != OperationStatusType.Success)
            {
                // warning, error deleting invalid refresh token from cache
            }
        }

        private void DeleteCachedAccessToken(
            string homeAccountId,
            string environment,
            string realm,
            string clientId,
            string target)
        {
            var status = _storageManager.DeleteCredentials(
                string.Empty,
                homeAccountId,
                environment,
                realm,
                clientId,
                string.Empty,
                target,
                new HashSet<CredentialType>
                {
                    CredentialType.OAuth2AccessToken
                });
            if (status.StatusType != OperationStatusType.Success)
            {
                // warning, failure deleting access token
            }
        }

        public static string GetLocalAccountId(Microsoft.Identity.Core.CacheV2.Impl.IdToken idTokenJwt)
        {
            string localAccountId = idTokenJwt.Oid;
            if (string.IsNullOrWhiteSpace(localAccountId))
            {
                localAccountId = idTokenJwt.Subject;
            }

            return localAccountId;
        }

        public AuthorityType GetAuthorityType()
        {
            string[] pathSegments = _authParameters.Authority.GetPath().Split('/');
            if (pathSegments.Count() < 2)
            {
                return AuthorityType.MsSts;
            }

            return string.Compare(pathSegments[1], "adfs", StringComparison.OrdinalIgnoreCase) == 0
                       ? AuthorityType.Adfs
                       : AuthorityType.MsSts;
        }

        public static string GetHomeAccountId(TokenResponse tokenResponse)
        {
            if (!string.IsNullOrWhiteSpace(tokenResponse.Uid) && !string.IsNullOrWhiteSpace(tokenResponse.Utid))
            {
                return $"{tokenResponse.Uid}.{tokenResponse.Utid}";
            }

            var idToken = tokenResponse.IdToken;
            string homeAccountId = idToken.Upn;
            if (!string.IsNullOrWhiteSpace(homeAccountId))
            {
                return homeAccountId;
            }

            homeAccountId = idToken.Email;
            if (!string.IsNullOrWhiteSpace(homeAccountId))
            {
                return homeAccountId;
            }

            return idToken.Subject;
        }

        public static bool IsAccessTokenValid(Credential accessToken)
        {
            long now = TimeUtils.GetSecondsFromEpochNow();

            if (accessToken.ExpiresOn <= now + 300)
            {
                // access token is expired
                return false;
            }

            // living in the future
            if (accessToken.CachedAt > now)
            {
                return false;
            }

            return true;
        }
    }

}
