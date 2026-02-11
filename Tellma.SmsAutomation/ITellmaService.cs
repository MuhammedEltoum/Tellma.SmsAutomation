using System;
using System.Collections.Generic;
using System.Text;
using Tellma.SmsAutomation.Contract;

namespace Tellma.SmsAutomation
{
    public interface ITellmaService
    {
        public Task<IEnumerable<SmsRequest>> PreReminder(int tenantId, CancellationToken token);
        public Task<IEnumerable<SmsRequest>> TodayReminder(int tenantId, CancellationToken token);
        public Task<IEnumerable<SmsRequest>> OverdueReminder(int tenantId, CancellationToken token);
        public Task<IEnumerable<SmsRequest>> PenaltyNotice(int tenantId, CancellationToken token);
        public Task<IEnumerable<SmsRequest>> AdvancePaymentConfirmation(int tenantId, CancellationToken token);
        public Task<IEnumerable<SmsRequest>> ContractExpiryNotice(int tenantId, CancellationToken token);
    }
}
