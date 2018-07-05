using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace RockDMZ.Domain
{
    public class AdWordsLocationProject
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; }

        public int KmRange { get; set; }

        public bool MultipleResults { get; set; }

        public OutputFormat OutputFormat { get; set; }
    }

    public enum OutputFormat { Table, JavascriptArrayofArrays, JavascriptArrayOfObjects }

    public class AdWordsLocation
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(100)]
        public string CanonicalName { get; set; }

        public int ParentId { get; set; }

        [MaxLength(50)]
        public string CountryCode { get; set; }

        [MaxLength(50)]
        public string TargetType { get; set; }

        [MaxLength(50)]
        public string Status { get; set; }

        [MaxLength(100)]
        public string Latitude { get; set; }

        [MaxLength(100)]
        public string Longitude { get; set; }
    }

    public class PointOfInterest
    {
        [Key]
        public int StoreCode { get; set; }

        [MaxLength(100)]
        public string Address { get; set; }

        [MaxLength(50)]
        public string City { get; set; }

        [MaxLength(50)]
        public string Region { get; set; }

        [MaxLength(50)]
        public string Country { get; set; }

        [MaxLength(50)]
        public string PostalCode { get; set; }

        [MaxLength(50)]
        public string MainPhone { get; set; }

        [MaxLength(50)]
        public string Longitude { get; set; }

        [MaxLength(50)]
        public string Latitude { get; set; }
    }

    public class PointOfInterestAdWordsLocation
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string PoiId { get; set; }

        public int AdWordsLocationId { get; set; }

        public int AdWordsLocationProjectId { get; set; }

        // public virtual AdWordsLocationProject AdWordsLocationProject { get; set; }
    }
}
