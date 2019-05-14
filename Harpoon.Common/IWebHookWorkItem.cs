﻿using System;

namespace Harpoon
{
    /// <summary>
    /// Represents a unique work item generated by a <see cref="IWebHookNotification"/>
    /// </summary>
    public interface IWebHookWorkItem
    {
        /// <summary>
        /// Gets the object unique id.
        /// </summary>
        Guid Id { get; }
        /// <summary>
        /// Gets the object time stamp.
        /// </summary>
        DateTime Timestamp { get; }
        /// <summary>
        /// Gets the <see cref="IWebHookNotification"/> that generated this <see cref="IWebHookWorkItem"/>.
        /// </summary>
        IWebHookNotification Notification { get; }
        /// <summary>
        /// Gets the registered <see cref="IWebHook"/> that matched the <see cref="IWebHookNotification"/>.
        /// </summary>
        IWebHook WebHook { get; }
    }
}