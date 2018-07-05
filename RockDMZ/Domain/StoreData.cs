using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using CsvHelper;

namespace RockDMZ.Domain
{
    public class StoreData
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string ClientName { get; set; }

        [MaxLength(50)]
        public string GmbStoreId { get; set; }

        [MaxLength(50)]
        public string StoreName { get; set; }

        [MaxLength(255)]
        public string GoogleLocationCanonicalName { get; set; }

        [MaxLength(50)]
        public string LocationName { get; set; }

        [MaxLength(20)]
        public string StoreName20 { get; set; }

        [MaxLength(25)]
        public string StoreName25 { get; set; }

        [MaxLength(30)]
        public string StoreName30 { get; set; }
    }

    public sealed class StoreDataCsvMap : CsvHelper.Configuration.ClassMap<StoreData>
    {
        public StoreDataCsvMap()
        {
            Map(m => m.Id).Ignore();

            Map(m => m.ClientName).Name("ClientName");

            Map(m => m.GmbStoreId).Name("GmbStoreId");

            Map(m => m.StoreName).Name("StoreName");

            Map(m => m.GoogleLocationCanonicalName).Name("GoogleLocationCanonicalName");

            Map(m => m.StoreName).Name("LocationName");

            Map(m => m.StoreName).Name("StoreName20");

            Map(m => m.StoreName).Name("StoreName25");

            Map(m => m.StoreName).Name("StoreName30");
        }
    }
}
