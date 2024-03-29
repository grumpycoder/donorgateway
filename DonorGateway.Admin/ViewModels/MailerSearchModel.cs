﻿using System;

namespace DonorGateway.Admin.ViewModels
{
    public class MailerSearchModel : PagerModel<MailerViewModel>
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string ZipCode { get; set; }
        public string SourceCode { get; set; }
        public string FinderNumber { get; set; }
        public bool? Suppress { get; set; } = false;
        public int? CampaignId { get; set; }
        public int? ReasonId { get; set; }
    }
}