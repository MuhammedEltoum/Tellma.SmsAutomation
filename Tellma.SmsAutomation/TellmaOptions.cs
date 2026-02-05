using System;
using System.Collections.Generic;
using System.Text;

namespace Tellma.SmsAutomation
{
    public class TellmaOptions
    {
        public string? ClientId { get; set; }
        public string? ClientSecret { get; set; }
        public string? CenterCodes { get; set; }
        public string? TenantIds { get; set; }
        public string? SmsProvider { get; set; }
        //This should be retrieved from tellma.
        public string? TenantSmsProvider { get; set; }
        public decimal VatRate { get; set; } = 0.15m;
    }
}
