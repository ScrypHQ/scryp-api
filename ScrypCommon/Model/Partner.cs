using ScrypCommon.Enum;
using System;

namespace ScrypCommon.Model
{
    public class Partner
    {
        public string Id { get; set; }

        public string PartnerName { get; set; }

        public string PartnerEmail { get; set; }

        public string PartnerContact { get; set; }

        public decimal Latitude { get; set; }

        public decimal Longitude { get; set; }

        public string Address { get; set; }

        public string Pincode { get; set; }

        public string City { get; set; }

        public string DisplayPicture { set; get; }

        public DateTime CreatedTimestamp { get; set; }

        public DateTime ModifiedTimestamp { get; set; }

        public string CreatedBy { get; set; }

        public string ModifiedBy { get; set; }

        public State State { get; set; } = State.Inactive;

        public string PartnerRemarks { get; set; }
    }
}
