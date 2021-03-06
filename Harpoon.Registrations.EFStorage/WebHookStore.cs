﻿using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Harpoon.Registrations.EFStorage
{
    /// <summary>
    /// Default <see cref="IWebHookStore"/> implementation using EF
    /// </summary>
    /// <typeparam name="TContext"></typeparam>
    public class WebHookStore<TContext> : IWebHookStore
        where TContext : DbContext, IRegistrationsContext
    {
        private readonly TContext _context;
        private readonly ISecretProtector _secretProtector;
        private readonly IWebHookMatcher _webHookMatcher;

        /// <summary>Initializes a new instance of the <see cref="WebHookStore{TContext}"/> class.</summary>
        public WebHookStore(TContext context, ISecretProtector secretProtector, IWebHookMatcher webHookMatcher)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _secretProtector = secretProtector ?? throw new ArgumentNullException(nameof(secretProtector));
            _webHookMatcher = webHookMatcher ?? throw new ArgumentNullException(nameof(webHookMatcher));
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<IWebHook>> GetApplicableWebHooksAsync(IWebHookNotification notification, CancellationToken cancellationToken = default)
        {
            if (notification == null)
            {
                throw new ArgumentNullException(nameof(notification));
            }

            var webHooks = await FilterQuery(_context.WebHooks.AsNoTracking()
                .Where(w => !w.IsPaused)
                .Include(w => w.Filters), notification)
                .ToListAsync(cancellationToken);

            var result = new List<IWebHook>();
            foreach (var webHook in webHooks.Where(w => _webHookMatcher.Matches(w, notification)))
            {
                webHook.Secret = _secretProtector.Unprotect(webHook.ProtectedSecret);
                webHook.Callback = new Uri(_secretProtector.Unprotect(webHook.ProtectedCallback));
                result.Add(webHook);
            }
            return result;
        }

        /// <summary>
        /// Apply the SQL filter matching the current notification
        /// </summary>
        /// <param name="query"></param>
        /// <param name="notification"></param>
        /// <returns></returns>
        protected virtual IQueryable<WebHook> FilterQuery(IQueryable<WebHook> query, IWebHookNotification notification)
        {
            return query.Where(w => w.Filters == null || w.Filters.Count == 0 || w.Filters.Any(f => f.Trigger == notification.TriggerId));
        }
    }
}