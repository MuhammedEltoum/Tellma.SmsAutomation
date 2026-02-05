using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using Tellma.Api.Dto;
using Tellma.Client;
using Tellma.SmsAutomation.Contract;

namespace Tellma.SmsAutomation
{
    public class TellmaService : ITellmaService
    {
        private readonly TellmaClient _client;
        private readonly ILogger<TellmaService> _logger;
        private readonly IEnumerable<string> _centerCodes;
        private readonly string _centerFilter;
        private readonly decimal _vatRate;

        public TellmaService(TellmaClient client, ILogger<TellmaService> logger, IOptions<TellmaOptions> options)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _centerCodes = (options?.Value.CenterCodes ?? "")
                .Split(",")
                .ToList();
            _centerFilter = string.Join(" OR ", _centerCodes.Select(code => $"Center.Code = '{code}'"));
            _vatRate = options?.Value.VatRate ?? 0.15m;
            _logger = logger;
        }

        public async Task<IEnumerable<SmsRequest>> PreReminder(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);
            string filter = "Line.Definition.Code = 'CustomerPeriodInvoice' AND " +
                "Line.State = 4 AND " +
                "DiffDays(Now, Time2) < 5 AND " +
                "DiffDays(Now, Time2) >= 4 AND " +
                "Time2 <> NULL AND " +
                $"{_centerFilter}";

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetEntities(new GetArguments
                {
                    Expand = "NotedAgent, Resource, Unit, Currency",
                    Filter = filter
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de.NotedAgent.Name,
                TenantMobile = de.NotedAgent.ContactMobile ?? de.NotedAgent.Text1,
                Property = de.Resource.Name,
                Unit = de.Unit.Name,
                DueDate = (DateTime)de.Time2!,
                Currency = de.Currency.Name,
                DueAmount = (Convert.ToDecimal(de.MonetaryValue)! + Convert.ToDecimal(de.Value)! * _vatRate) / (de.Quantity ?? 1)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Rent of {contract.DueAmount:N2} {contract.Currency} for {contract.Property}/{contract.Unit} due on {contract.DueDate:dd-MM-yyyy}. Kindly Pay on time. – Soreti Management."
                });
        }

        public async Task<IEnumerable<SmsRequest>> todayReminder(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);
            string filter = "Line.Definition.Code = 'CustomerPeriodInvoice' AND " +
                "Line.State = 4 AND " +
                "DiffDays(Now, Time2) < 1 AND " +
                "DiffDays(Now, Time2) > -1 AND " +
                "Time2 <> NULL AND " +
                $"{_centerFilter}";

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetEntities(new GetArguments
                {
                    Expand = "NotedAgent, Resource, Unit, Currency",
                    Filter = filter
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de.NotedAgent.Name,
                TenantMobile = de.NotedAgent.ContactMobile ?? de.NotedAgent.Text1,
                Property = de.Resource.Name,
                Unit = de.Unit.Name,
                DueDate = (DateTime)de.Time2!,
                Currency = de.Currency.Name,
                DueAmount = (Convert.ToDecimal(de.MonetaryValue)! + Convert.ToDecimal(de.MonetaryValue)! * _vatRate) / (de.Quantity ?? 1)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Rent due today for {contract.Property}/{contract.Unit}. Amount: {contract.DueAmount:N2} {contract.Currency}. Kindly Pay now to avoid late fees."
                });
        }

        public async Task<IEnumerable<SmsRequest>> OverdueReminder(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);
            string filter = "Line.Definition.Code = 'CustomerPeriodInvoice' AND " +
                "Line.State = 4 AND " +
                "DiffDays(Now, Time2) <= -1 AND " +
                "DiffDays(Now, Time2) >= -3 AND " +
                "Time2 <> NULL AND " +
                $"{_centerFilter}";

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetEntities(new GetArguments
                {
                    Expand = "NotedAgent, Resource, Unit, Currency",
                    Filter = filter
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de.NotedAgent.Name,
                TenantMobile = de.NotedAgent.ContactMobile ?? de.NotedAgent.Text1,
                Property = de.Resource.Name,
                Unit = de.Unit.Name,
                DueDate = (DateTime)de.Time2!,
                Currency = de.Currency.Name,
                DueAmount = (Convert.ToDecimal(de.MonetaryValue)! + Convert.ToDecimal(de.MonetaryValue)! * _vatRate) / (de.Quantity ?? 1)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Rent for {contract.Property}/{contract.Unit} is {contract.DaysLate:N0} days late. Kindly Pay {contract.DueAmount:N2} {contract.Currency} immediately to avoid penalties. – Soreti Management."
                });
        }

        public async Task<IEnumerable<SmsRequest>> PenaltyNotice(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);
            string filter = "Line.Definition.Code = 'CustomerPeriodInvoice' AND " +
                "Line.State = 4 AND " +
                "DiffDays(Now, Time2) < -3 AND " +
                "DiffDays(Now, Time2) >= -4 AND " +
                "Time2 <> NULL AND " +
                $"{_centerFilter}";

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetEntities(new GetArguments
                {
                    Expand = "NotedAgent, Resource, Unit, Currency",
                    Filter = filter
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de.NotedAgent.Name,
                TenantMobile = de.NotedAgent.ContactMobile ?? de.NotedAgent.Text1,
                Property = de.Resource.Name,
                Unit = de.Unit.Name,
                DueDate = (DateTime)de.Time2!,
                Currency = de.Currency.Name,
                PenaltyAmount = Convert.ToDecimal(de.MonetaryValue)! * 0.05m / (de.Quantity ?? 1),
                DueAmount = (Convert.ToDecimal(de.MonetaryValue)! + Convert.ToDecimal(de.MonetaryValue * _vatRate)! + Convert.ToDecimal(de.MonetaryValue)! * 0.05m) / (de.Quantity ?? 1)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Late fee of {contract.PenaltyAmount:N2} {contract.Currency} applied. Total payable: {contract.DueAmount:N2} {contract.Currency} for {contract.Property}/{contract.Unit}. Urgent."
                });
        }

        public async Task<IEnumerable<SmsRequest>> AdvancePaymentConfirmation(int tenantId, CancellationToken token)
        {
            // Needs more business logic details to implement
            var tenantClient = _client.Application(tenantId);
            string filter = "Line.Definition.Code = 'CustomerPeriodInvoice' AND " +
                "Line.State = 4 AND " +
                "DiffDays(Now, Time1) > -1 AND " +
                "DiffDays(Now, Time1) < 1 AND " +
                "Time1 <> NULL AND " +
                $"{_centerFilter}";

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetEntities(new GetArguments
                {
                    Expand = "NotedAgent, Resource, Unit, Currency",
                    Filter = filter
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de.NotedAgent.Name,
                TenantMobile = de.NotedAgent.ContactMobile ?? de.NotedAgent.Text1,
                Property = de.Resource.Name,
                Unit = de.Unit.Name,
                DueDate = (DateTime)de.Time1!.Value.AddMonths(Convert.ToInt32(de.Quantity)),
                Currency = de.Currency.Name,
                AdvanceAmount = (Convert.ToDecimal(de.MonetaryValue)! + Convert.ToDecimal(de.Value)! * _vatRate) / (de.Quantity ?? 1)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Advance of {contract.AdvanceAmount} ETB for {contract.Property}/{contract.Unit} received. Next payment due {contract.DueDate}. – Soreti Management."
                });
        }

        public async Task<IEnumerable<SmsRequest>> ContractExpiryNotice(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);
            string filter = "Line.Definition.Code = 'CustomerPeriodOfTimeServiceInvoiceTemplate' AND " +
                "Line.State = 2 AND " +
                "DiffDays(Now, Time2) <= 29 AND " +
                "DiffDays(Now, Time2) > 28 AND " +
                $" {_centerFilter}";

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetEntities(new GetArguments
                {
                    Expand = "NotedAgent, Resource, Unit, Currency",
                    Filter = filter
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de.NotedAgent.Name,
                TenantMobile = de.NotedAgent.ContactMobile ?? de.NotedAgent.Text1,
                Property = de.Resource.Name,
                Unit = de.Unit.Name,
                ContractExpiryDate = (DateTime)de.Time2!
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Lease for {contract.Property}/{contract.Unit} expires {contract.ContractExpiryDate:yyyy-MM-dd}. Contact Soreti Management to renew."
                });
        }
    }
}
