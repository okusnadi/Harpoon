﻿using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Harpoon.Registrations.EFStorage
{
    public class WebHookRegistrationStore<TContext> : IWebHookRegistrationStore, IWebHookStore
        where TContext : DbContext, IRegistrationsContext
    {
        private readonly TContext _context;
        private readonly IPrincipalIdGetter _idGetter;
        private readonly IDataProtector _dataProtector;
        private readonly ILogger<WebHookRegistrationStore<TContext>> _logger;

        public WebHookRegistrationStore(TContext context, IPrincipalIdGetter idGetter, IDataProtectionProvider dataProtectionProvider, ILogger<WebHookRegistrationStore<TContext>> logger)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _idGetter = idGetter ?? throw new ArgumentNullException(nameof(idGetter));
            _dataProtector = dataProtectionProvider?.CreateProtector(DataProtection.Purpose) ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<IReadOnlyList<IWebHook>> GetAllWebHooksAsync(string trigger)
        {
            var webHooks = await _context.WebHooks
                .Where(w => !w.IsPaused && w.Filters.Any(f => f.TriggerId == trigger))
                .Include(w => w.Filters)
                .AsNoTracking()
                .ToListAsync();

            foreach (var webHook in webHooks)
            {
                Prepare(webHook);
            }

            return webHooks;
        }

        public async Task<IWebHook> GetWebHookAsync(IPrincipal user, Guid id)
        {
            var key = await _idGetter.GetPrincipalIdAsync(user);
            var webHook = await _context.WebHooks
                .Where(w => w.PrincipalId == key && w.Id == id)
                .Include(w => w.Filters)
                .AsNoTracking()
                .FirstOrDefaultAsync();

            if (webHook == null)
            {
                return null;
            }

            Prepare(webHook);

            return webHook;
        }

        public async Task<IReadOnlyList<IWebHook>> GetWebHooksAsync(IPrincipal user)
        {
            var key = await _idGetter.GetPrincipalIdAsync(user);
            var webHooks = await _context.WebHooks
                .Where(w => w.PrincipalId == key)
                .Include(w => w.Filters)
                .AsNoTracking()
                .ToListAsync();

            foreach (var webHook in webHooks)
            {
                Prepare(webHook);
            }

            return webHooks;
        }

        private void Prepare(WebHook webHook)
        {
            if (webHook == null)
            {
                return;
            }

            webHook.Secret = Unprotect(webHook.ProtectedSecret);
            webHook.Callback = new Uri(Unprotect(webHook.ProtectedCallback));
        }

        private string Unprotect(string input)
        {
            try
            {
                return _dataProtector.Unprotect(input);
            }
            catch
            {
                if (!(_dataProtector is IPersistedDataProtector persistedProtector))
                {
                    throw;
                }

                return Encoding.UTF8.GetString(persistedProtector.DangerousUnprotect(Encoding.UTF8.GetBytes(input), true, out var _, out var _));
            }
        }

        public async Task<WebHookRegistrationStoreResult> InsertWebHookAsync(IPrincipal user, IWebHook webHook)
        {
            if (webHook == null)
            {
                throw new ArgumentNullException(nameof(webHook));
            }

            if (webHook.Id == default)
            {
                throw new ArgumentException("WebHook id needs to be set by client.");
            }

            if (webHook.Callback == null)
            {
                throw new ArgumentException("WebHook callback needs to be set.");
            }

            if (webHook.Secret == null)
            {
                throw new ArgumentException("WebHook secret needs to be set.");
            }

            if (webHook.Filters == null)
            {
                throw new ArgumentException("WebHook filters needs to be set.");
            }

            var key = await _idGetter.GetPrincipalIdAsync(user);
            var dbWebHook = new WebHook
            {
                Id = webHook.Id,
                PrincipalId = key,
                ProtectedCallback = _dataProtector.Protect(webHook.Callback.ToString()),
                ProtectedSecret = _dataProtector.Protect(webHook.Secret),
                Filters = webHook.Filters.Select(f => new WebHookFilter
                {
                    TriggerId = f.TriggerId,
                    Parameters = f.Parameters == null ? null : new Dictionary<string, object>(f.Parameters)
                }).ToList()
            };

            try
            {
                _context.Add(dbWebHook);
                await _context.SaveChangesAsync();
                return WebHookRegistrationStoreResult.Success;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"WebHook {dbWebHook.Id} insertion failed : {e.Message}");
                return WebHookRegistrationStoreResult.InternalError;
            }
        }

        public async Task<WebHookRegistrationStoreResult> UpdateWebHookAsync(IPrincipal user, IWebHook webHook)
        {
            if (webHook == null)
            {
                throw new ArgumentNullException(nameof(webHook));
            }

            var key = await _idGetter.GetPrincipalIdAsync(user);
            var dbWebHook = await _context.WebHooks
                .Where(w => w.PrincipalId == key && w.Id == webHook.Id)
                .Include(w => w.Filters)
                .FirstOrDefaultAsync();

            if (dbWebHook == null)
            {
                return WebHookRegistrationStoreResult.NotFound;
            }

            dbWebHook.IsPaused = webHook.IsPaused;

            if (webHook.Callback != null)
            {
                dbWebHook.ProtectedCallback = _dataProtector.Protect(webHook.Callback.ToString());
            }

            if (!string.IsNullOrEmpty(webHook.Secret))
            {
                dbWebHook.ProtectedSecret = _dataProtector.Protect(webHook.Secret);
            }

            if (webHook.Filters != null)
            {
                _context.RemoveRange(dbWebHook.Filters);
                dbWebHook.Filters = webHook.Filters.Select(f => new WebHookFilter
                {
                    TriggerId = f.TriggerId,
                    Parameters = f.Parameters == null ? null : new Dictionary<string, object>(f.Parameters)
                }).ToList();
            }

            try
            {
                await _context.SaveChangesAsync();
                return WebHookRegistrationStoreResult.Success;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"WebHook {dbWebHook.Id} update failed : {e.Message}");
                return WebHookRegistrationStoreResult.InternalError;
            }
        }

        public async Task<WebHookRegistrationStoreResult> DeleteWebHookAsync(IPrincipal user, Guid id)
        {
            var key = await _idGetter.GetPrincipalIdAsync(user);
            var webHook = await _context.WebHooks.Where(w => w.PrincipalId == key && w.Id == id).FirstOrDefaultAsync();

            if (webHook == null)
            {
                return WebHookRegistrationStoreResult.NotFound;
            }

            _context.Remove(webHook);
            try
            {
                await _context.SaveChangesAsync();
                return WebHookRegistrationStoreResult.Success;
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"WebHook {id} deletion failed : {e.Message}");
                return WebHookRegistrationStoreResult.InternalError;
            }
        }

        public async Task DeleteWebHooksAsync(IPrincipal user)
        {
            var key = await _idGetter.GetPrincipalIdAsync(user);
            var webHooks = await _context.WebHooks.Where(r => r.PrincipalId == key).ToListAsync();

            if (webHooks == null || webHooks.Count == 0)
            {
                return;
            }

            try
            {
                _context.RemoveRange(webHooks);
                await _context.SaveChangesAsync();
            }
            catch (Exception e)
            {
                _logger.LogError(e, $"WebHooks deletion from user {key} failed : {e.Message}");
                throw;
            }
        }
    }
}