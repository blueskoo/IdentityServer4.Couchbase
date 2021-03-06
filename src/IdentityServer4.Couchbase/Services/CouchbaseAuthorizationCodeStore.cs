﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core;
using Couchbase.Linq;
using IdentityServer4.Couchbase.Wrappers;
using IdentityServer4.Models;
using IdentityServer4.Services;

namespace IdentityServer4.Couchbase.Services
{

    /// <summary>
    /// Couchbase authorization code store
    /// </summary>
    public class CouchbaseAuthorizationCodeStore : IAuthorizationCodeStore
    {
        readonly IBucket _bucket;
        readonly IBucketContext _context;

        public CouchbaseAuthorizationCodeStore(IBucket bucket, IBucketContext context)
        {
            _bucket = bucket;
            _context = context;            
        }
        
        /// <summary>
        /// Stores the data.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public Task StoreAsync(string key, AuthorizationCode value)
        {
            return _bucket.InsertAsync(AuthorizationCodeWrapper.AuthorizationCodeId(key), new AuthorizationCodeWrapper(key, value));
        }

        /// <summary>
        /// Retrieves the data.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public async Task<AuthorizationCode> GetAsync(string key)
        {
            var result = await _bucket.GetAsync<AuthorizationCodeWrapper>(AuthorizationCodeWrapper.AuthorizationCodeId(key));
            return result.Success ? result.Value.Model : null;
        }

        /// <summary>
        /// Removes the data.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns></returns>
        public Task RemoveAsync(string key)
        {            
            return _bucket.RemoveAsync(AuthorizationCodeWrapper.AuthorizationCodeId(key));
        }

        /// <summary>
        /// Retrieves all data for a subject identifier.
        /// </summary>
        /// <param name="subject">The subject identifier.</param>
        /// <returns>
        /// A list of token metadata
        /// </returns>
        public Task<IEnumerable<ITokenMetadata>> GetAllAsync(string subject)
        {
            var query =
                from item in _context.Query<AuthorizationCodeWrapper>()
                where item.Model.SubjectId == subject
                select item.Model;
            var list = query.ToArray();
            return Task.FromResult(list.Cast<ITokenMetadata>());
        }

        /// <summary>
        /// Revokes all data for a client and subject id combination.
        /// </summary>
        /// <param name="subject">The subject.</param>
        /// <param name="client">The client.</param>
        /// <returns></returns>
        public Task RevokeAsync(string subject, string client)
        {
            var query =
                from item in _context.Query<AuthorizationCodeWrapper>()
                where item.Model.Subject.GetSubjectId() == subject && item.Model.ClientId == client
                select item.Id;

            foreach (var key in query)
            {
                _bucket.RemoveAsync(key);
            }

            return Task.FromResult(0);
        }
    }
}