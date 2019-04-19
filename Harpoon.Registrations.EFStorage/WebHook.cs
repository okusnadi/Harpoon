﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Harpoon.Registrations.EFStorage
{
    public class WebHook : IWebHook
    {
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public Guid Id { get; set; }

        [NotMapped]
        public Uri Callback { get; set; }
        [Required]
        public string ProtectedCallback { get; set; }

        [NotMapped]
        public string Secret { get; set; }
        [Required]
        public string ProtectedSecret { get; set; }

        public bool IsPaused { get; set; }

        public List<WebHookFilter> Filters { get; set; }

        IReadOnlyCollection<IWebHookFilter> IWebHook.Filters => Filters;
    }
}
