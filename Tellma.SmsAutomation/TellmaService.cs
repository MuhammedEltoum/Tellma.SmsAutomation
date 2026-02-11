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
        private const decimal _penaltyRate = 0.05m;
        private readonly TellmaClient _client;
        private readonly ILogger<TellmaService> _logger;
        private readonly IEnumerable<string> _centerCodes;
        private readonly string _centerFilter;
        private readonly string _rentalInvoiceFilter;
        private readonly string _rentalAgreementFilter;
        private readonly string _rentalInvoiceSelect;
        private readonly string _rentalAgreementSelect;
        private readonly string _commonHaving;

        private readonly decimal _vatRate;

        public TellmaService(TellmaClient client, ILogger<TellmaService> logger, IOptions<TellmaOptions> options)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _centerCodes = (options?.Value.CenterCodes ?? " True ")
                .Split(",")
                .ToList();
            _centerFilter = string.Join(" OR ", _centerCodes.Select(code => $"Center.Code DescOf '{code}'"));
            _vatRate = options?.Value.VatRate ?? 0.15m;
            _logger = logger;
            _rentalInvoiceFilter = "Line.Definition.Code = 'CustomerPeriodInvoice' AND " +
                "EntryType.Concept DescOf 'InvoiceExtension' AND " +
                "Account.AccountType.Concept DescOf 'CustomerPerformanceObligationsOverAPeriodOfTimeControlExtension' AND " +
                "Line.State >= 0 AND " +
                $"({_centerFilter})";
            _rentalAgreementFilter = "Line.Definition.Code = 'CustomerPeriodOfTimeServiceInvoiceTemplate' AND " +
                "EntryType.Concept DescOf 'InvoiceExtension' AND " +
                "Account.AccountType.Concept DescOf 'CustomerPerformanceObligationsOverAPeriodOfTimeControlExtension' AND " +
                "Line.State >= 0 AND " +
                $"({_centerFilter})";
            _rentalInvoiceSelect = "NotedAgent.Name, " +
                "ISNULL(NotedAgent.ContactMobile, NotedAgent.Text1), " +
                "Resource.Name, " +
                "Unit.Name, " +
                "MAX(Time2), " + // Latest Invoice date for the property
                "Currency.Name, " +
                "Quantity, " +
                "0 - SUM(Direction * MonetaryValue)";
            _rentalAgreementSelect = "NotedAgent.Name, " +
                "ISNULL(NotedAgent.ContactMobile, NotedAgent.Text1), " +
                "Resource.Name, " +
                "Unit.Name, " +
                "Time2, " + 
                "Currency.Name, " +
                "Quantity, " +
                "0 - SUM(Direction * MonetaryValue), " +
                "MAX(Line.Document.PostingDate)"; // Latest Contract date for the property
            _commonHaving = "0 - SUM(Direction * MonetaryValue) <> 0";
        }

        public async Task<IEnumerable<SmsRequest>> PreReminder(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);
            string filter = _rentalInvoiceFilter + " AND " +
                "DiffDays(Today, Time2) <= 5 AND " + // Upcoming dues within the next 5 days
                "DiffDays(Today, Time2) > 4"; // Only those due in 5 days, not earlier

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetAggregate(new GetAggregateArguments
                {
                    Select = _rentalInvoiceSelect,
                    Filter = filter,
                    Having = _commonHaving
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de[0].ToString()!,
                TenantMobile = de[1].ToString()!,
                Property = de[2].ToString()!,
                Unit = de[3].ToString()!,
                DueDate = (DateTime)de[4]!,
                Currency = de[5].ToString()!,
                DueAmount = (Convert.ToDecimal(de[7])! + Convert.ToDecimal(de[7])! * _vatRate) / ((Decimal?)Convert.ToDecimal(de[6]) ?? 1.00m)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Dear {contract.TenantName}, Rent of {contract.DueAmount:N2} {contract.Currency} for {contract.Property}/{contract.Unit} due on {contract.DueDate:dd-MM-yyyy}. Kindly Pay on time. – Soreti Management."
                });
        }

        public async Task<IEnumerable<SmsRequest>> TodayReminder(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);

            string filter = _rentalInvoiceFilter + " AND " +
                "DiffDays(Today, Time2) = 0"; // Today's dues

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetAggregate(new GetAggregateArguments
                {
                    Select = _rentalInvoiceSelect,
                    Filter = filter,
                    Having = _commonHaving
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de[0].ToString()!,
                TenantMobile = de[1].ToString()!,
                Property = de[2].ToString()!,
                Unit = de[3].ToString()!,
                DueDate = (DateTime)de[4]!,
                Currency = de[5].ToString()!,
                DueAmount = (Convert.ToDecimal(de[7])! + Convert.ToDecimal(de[7])! * _vatRate) / ((Decimal?)Convert.ToDecimal(de[6]) ?? 1.00m)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Dear {contract.TenantName}, Rent due today for {contract.Property}/{contract.Unit}. Amount: {contract.DueAmount:N2} {contract.Currency}. Kindly Pay now to avoid late fees. – Soreti Management."
                });
        }

        public async Task<IEnumerable<SmsRequest>> OverdueReminder(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);
            string filter = _rentalInvoiceFilter + " AND " +
                "DiffDays(Now, Time2) <= -1 AND " + 
                "DiffDays(Now, Time2) >= -5"; // Overdue dues between 1 and 5 days late

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetAggregate(new GetAggregateArguments
                {
                    Select = _rentalInvoiceSelect,
                    Filter = filter,
                    Having = _commonHaving
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de[0].ToString()!,
                TenantMobile = de[1].ToString()!,
                Property = de[2].ToString()!,
                Unit = de[3].ToString()!,
                DueDate = (DateTime)de[4]!,
                Currency = de[5].ToString()!,
                DaysLate = Convert.ToInt32((DateTime.Today - (DateTime)de[4]!).TotalDays),
                DueAmount = (Convert.ToDecimal(de[7])! + Convert.ToDecimal(de[7])! * _vatRate) / ((Decimal?)Convert.ToDecimal(de[6]) ?? 1.00m)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Dear {contract.TenantName}, Rent for {contract.Property}/{contract.Unit} is {contract.DaysLate:N0} days late. Kindly Pay {contract.DueAmount:N2} {contract.Currency} immediately to avoid penalties. – Soreti Management."
                });
        }

        public async Task<IEnumerable<SmsRequest>> PenaltyNotice(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);
            string filter = _rentalInvoiceFilter + " AND " +
                "DiffDays(Now, Time2) < -5 AND " +
                "DiffDays(Now, Time2) >= -6"; // Dues that became overdue 6 days ago (5 full days late)

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetAggregate(new GetAggregateArguments
                {
                    Select = _rentalInvoiceSelect,
                    Filter = filter,
                    Having = _commonHaving
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de[0].ToString()!,
                TenantMobile = de[1].ToString()!,
                Property = de[2].ToString()!,
                Unit = de[3].ToString()!,
                DueDate = (DateTime)de[4]!,
                Currency = de[5].ToString()!,
                PenaltyAmount = Convert.ToDecimal(de[7])! * _penaltyRate / ((Decimal?)Convert.ToDecimal(de[6]) ?? 1),
                DueAmount = (Convert.ToDecimal(de[7])! + Convert.ToDecimal(de[7])! * _penaltyRate + Convert.ToDecimal(de[7])! * _vatRate) / ((Decimal?)Convert.ToDecimal(de[6]) ?? 1.00m)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Dear {contract.TenantName}, Late fee of {contract.PenaltyAmount:N2} {contract.Currency} applied. Total payable: {contract.DueAmount:N2} {contract.Currency} for {contract.Property}/{contract.Unit}. Urgent. – Soreti Management."
                });
        }

        public async Task<IEnumerable<SmsRequest>> AdvancePaymentConfirmation(int tenantId, CancellationToken token)
        {
            // Needs more business logic details to implement
            var tenantClient = _client.Application(tenantId);
            string filter = _rentalAgreementFilter + " AND " +
                "DiffDays(Today, Line.Document.PostingDate) >= 0 AND " +
                "DiffDays(Today, Time1) >= 0 AND " +
                "Line.Index = 0";

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetAggregate(new GetAggregateArguments
                {
                    Select = _rentalAgreementSelect,
                    Filter = filter,
                    Having = _commonHaving
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de[0].ToString()!,
                TenantMobile = de[1].ToString()!,
                Property = de[2].ToString()!,
                Unit = de[3].ToString()!,
                DueDate = Convert.ToDateTime(de[4])!.AddDays(1),
                Currency = de[5].ToString()!,
                AdvanceAmount = (Convert.ToDecimal(de[7])! + Convert.ToDecimal(de[7])! * _vatRate)
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Dear {contract.TenantName}, Advance of {contract.AdvanceAmount:N2} ETB for {contract.Property}/{contract.Unit} received. Next payment due {contract.DueDate:yyyy-MM-dd}. – Soreti Management."
                });
        }

        public async Task<IEnumerable<SmsRequest>> ContractExpiryNotice(int tenantId, CancellationToken token)
        {
            var tenantClient = _client.Application(tenantId);
            string filter = _rentalAgreementFilter + " AND " +
                "DiffDays(Now, Time2) < 30 AND " +
                "DiffDays(Now, Time2) >= 29"; // Contract expiring in 30 days

            var duesEntries = await tenantClient
                .DetailsEntries
                .GetAggregate(new GetAggregateArguments
                {
                    Select = _rentalAgreementSelect,
                    Filter = filter,
                    Having = _commonHaving
                }, token);

            return duesEntries.Data.Select(de => new TenantContractDues
            {
                TenantName = de[0].ToString()!,
                TenantMobile = de[1].ToString()!,
                Property = de[2].ToString()!,
                Unit = de[3].ToString()!,
                ContractExpiryDate = Convert.ToDateTime(de[4]),
            })
                .Select(contract => new SmsRequest
                {
                    PhoneNumber = contract.TenantMobile,
                    Message = $"Dear {contract.TenantName}, Lease for {contract.Property}/{contract.Unit} expires {contract.ContractExpiryDate:yyyy-MM-dd}. Contact Soreti Management to renew."
                });
        }
    }
}
