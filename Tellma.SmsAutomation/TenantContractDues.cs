using System;
using System.Collections.Generic;
using System.Text;

namespace Tellma.SmsAutomation
{
    public class TenantContractDues
    {
        public string TenantName { get; set; } = String.Empty;
        public string TenantMobile { get; set; } = String.Empty;
        public string ContractCode { get; set; } = String.Empty;
        public DateTime ContractExpiryDate { get; set; }
        public string Property { get; set; } = String.Empty;
        public string Unit { get; set; } = String.Empty;
        public string Currency { get; internal set; } = String.Empty;
        public Decimal DueAmount { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysLate { get; set; }
        public Decimal PenaltyAmount { get; set; }
        public Decimal AdvanceAmount { get; internal set; }
    }
}
