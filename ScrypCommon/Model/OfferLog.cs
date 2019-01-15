using ScrypCommon.Enum;
using System;
using System.Collections.Generic;
using System.Text;

namespace ScrypCommon.Model
{
    public class OfferLog
    {
        public string Id { get; set; }

        public string OfferId { get; set; }

        public string UserId { get; set; }

        public AvailStatus Status { get; set; }

        public decimal ScrypCurrency { get; set; }
    }
}
