using ScrypCommon.Enum;
using System;
using System.Collections.Generic;

namespace ScrypCommon.Model
{
    public class Offer
    {
        public string Id { get; set; }

        public string OfferName { get; set; }

        public string OfferedBy { get; set; }

        public string OfferedByEmail { get; set; }

        public bool IsConditional { get; set; }

        public string Condition { get; set; }

        public DateTime CreatedTimestamp { get; set; }

        public string CreatedBy { get; set; }

        public string ModifiedBy { get; set; }

        public DateTime ModifiedTimestamp { get; set; }

        public bool IsInactive { get; set; } = false;

        public DateTime ValidFrom { get; set; }

        public DateTime ValidTill { get; set; }

        public ICollection<string> ValidOnDays { get; set; }

        public string Description { get; set; }

        public string ItemOnOffer { get; set; }

        public decimal ItemPrice { get; set; }

        public decimal ScrypPrice { get; set; }

        public int Availability { get; set; }

        public string TimeStart { get; set; }

        public string TimeEnd { get; set; }

        public OfferResetType OfferResetType { get; set; }

        public string OfferQRCode { get; set; }
    }
}
