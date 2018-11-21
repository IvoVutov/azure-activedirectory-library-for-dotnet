using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Identity.Core.CacheV2.Schema;

namespace Microsoft.Identity.Core.CacheV2.Impl
{
    internal interface IStorageManager
    {
        //event EventHandler<TokenCacheNotificationArgs> BeforeAccess;
        //event EventHandler<TokenCacheNotificationArgs> AfterAccess;
        //event EventHandler<TokenCacheNotificationArgs> BeforeWrite;
        byte[] Serialize();
        void Deserialize(byte[] serializedBytes);

        ReadCredentialsResponse ReadCredentials(
            string correlationId,
            string homeAccountId,
            string environment,
            string realm,
            string clientId,
            string familyId,
            string target,
            ISet<CredentialType> types);

        OperationStatus WriteCredentials(string correlationId, IEnumerable<Credential> credentials);

        OperationStatus DeleteCredentials(
            string correlationId,
            string homeAccountId,
            string environment,
            string realm,
            string clientId,
            string familyId,
            string target,
            ISet<CredentialType> types);

        ReadAccountsResponse ReadAllAccounts(string correlationId);

        ReadAccountResponse ReadAccount(
            string correlationId,
            string homeAccountId,
            string environment,
            string realm);

        OperationStatus WriteAccount(string correlationId, Account account);

        OperationStatus DeleteAccount(
            string correlationId,
            string homeAccountId,
            string environment,
            string realm);

        OperationStatus DeleteAccounts(string correlationId, string homeAccountId, string environment);
        AppMetadata ReadAppMetadata(string environment, string clientId);
        void WriteAppMetadata(AppMetadata appMetadata);
    }
}
