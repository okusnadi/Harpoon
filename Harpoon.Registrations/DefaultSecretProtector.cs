﻿using Microsoft.AspNetCore.DataProtection;
using System;
using System.Text;

namespace Harpoon.Registrations.EFStorage
{
    /// <inheritdoc />
    public class DefaultSecretProtector : ISecretProtector
    {
        /// <summary>
        /// Gets the default <see cref="IDataProtector"/> purpose
        /// </summary>
        public const string Purpose = "WebHookStorage";

        private readonly IDataProtector _dataProtector;

        /// <summary>Initializes a new instance of the <see cref="DefaultSecretProtector"/> class.</summary>
        /// <param name="dataProtectionProvider"></param>
        public DefaultSecretProtector(IDataProtectionProvider dataProtectionProvider)
        {
            _dataProtector = dataProtectionProvider?.CreateProtector(Purpose) ?? throw new ArgumentNullException(nameof(dataProtectionProvider));
        }

        /// <inheritdoc />
        public string Protect(string plaintext)
        {
            return _dataProtector.Protect(plaintext);
        }

        /// <inheritdoc />
        public string Unprotect(string protectedData)
        {
            try
            {
                return _dataProtector.Unprotect(protectedData);
            }
            catch
            {
                if (!(_dataProtector is IPersistedDataProtector persistedProtector))
                {
                    throw;
                }

                return Encoding.UTF8.GetString(persistedProtector.DangerousUnprotect(Encoding.UTF8.GetBytes(protectedData), true, out var _, out var _));
            }
        }
    }
}