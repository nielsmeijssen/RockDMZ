using FluentValidation;
using MediatR;
using RockDMZ.Infrastructure;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using RockDMZ.Domain;
using System;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;
using System.Collections.Generic;
using System.IO;
using CsvHelper;

namespace RockDMZ.Features.AdWordsLocation
{
    public class EditLaunch
    {
        public class Query : IRequest<Command>
        {
            public int? Id { get; set; }
        }

        public class QueryValidator : AbstractValidator<Query>
        {
            public QueryValidator()
            {
                RuleFor(m => m.Id).NotNull();
            }
        }

        public class QueryHandler : IAsyncRequestHandler<Query, Command>
        {
            private readonly ToolsContext _db;

            public QueryHandler(ToolsContext db)
            {
                _db = db;
            }

            public async Task<Command> Handle(Query message)
            {
                var rtn = await _db.AdWordsLocationProjects.Where(c => c.Id == message.Id).ProjectToSingleOrDefaultAsync<Command>();

                _db.Database.ExecuteSqlCommand("DELETE FROM PointOfInterestAdWordsLocation WHERE AdWordsLocationProjectId = " + rtn.Id);

                var jsArrayList = new List<string>();
                var poiAdwordsLocations = new List<PointOfInterestAdWordsLocation>();
                // get all GLIDs in NL
                var glids = _db.AdWordsLocations.Where(c => c.CountryCode == "NL").Where(c => c.Latitude != null).ToList();
                // get all POIs 
                var pois = _db.PointsOfInterest.Where(c => c.Latitude != null).ToList();
                // foreach GLID
                foreach(var glid in glids)
                {
                    // get the glid longitude and latitude as decimal
                    var glidLat = Convert.ToDouble(glid.Latitude, CultureInfo.InvariantCulture);
                    var glidLng = Convert.ToDouble(glid.Longitude, CultureInfo.InvariantCulture);
                    var glidCoordinate = new Coordinates(glidLat, glidLng);
                    
                    // select the POIs within KmRange
                    var selectedPois = new SortedDictionary<double, PointOfInterest>();
                    foreach (var poi in pois)
                    {
                        var poiLat = Convert.ToDouble(poi.Latitude, CultureInfo.InvariantCulture);
                        var poiLng = Convert.ToDouble(poi.Longitude, CultureInfo.InvariantCulture);
                        var poiCoordinate = new Coordinates(poiLat, poiLng);

                        var distance = glidCoordinate.DistanceTo(poiCoordinate, UnitOfLength.Kilometers);

                        if (distance <= rtn.KmRange)
                        {
                            if (!selectedPois.Keys.Contains(distance)) selectedPois.Add(distance, poi);
                        }
                    }

                    // add array [0] = GLID , [1] = POI to rtn.JsArray
                    if (selectedPois.Count > 0)
                    {
                        if (rtn.MultipleResults && selectedPois.Count > 1)
                        {
                            jsArrayList.Add("[" + glid.Id + ",[" + string.Join(",", selectedPois.Select(x => x.Value)) + "]]");
                            foreach(var poi in selectedPois)
                            {
                                var poiAw = new PointOfInterestAdWordsLocation();
                                poiAw.AdWordsLocationProjectId = rtn.Id;
                                poiAw.AdWordsLocationId = glid.Id;
                                poiAw.PoiId = poi.Value.StoreCode.ToString();
                                poiAdwordsLocations.Add(poiAw);
                            }
                        }
                        else
                        {
                            jsArrayList.Add("[" + glid.Id + "," + selectedPois.First().Value.StoreCode + "]");

                            var poiAw = new PointOfInterestAdWordsLocation();
                            poiAw.AdWordsLocationProjectId = rtn.Id;
                            poiAw.AdWordsLocationId = glid.Id;
                            poiAw.PoiId = selectedPois.First().Value.StoreCode.ToString();
                            poiAdwordsLocations.Add(poiAw);
                        }
                    }                    
                }
                rtn.JsArray = string.Join(",", jsArrayList);

                _db.PointOfInterestAdWordsLocations.AddRange(poiAdwordsLocations);

                // store as businessdatafeed
                GenerateAdCustomizerFeed(rtn.Id, new FileStorage().Datatables + "_LocationGeneric_" + rtn.Id + ".csv");

                return rtn;
            }

            private void GenerateAdCustomizerFeed(int projectId, string localFilePath)
            {
                var items = new List<List<string>>();
                var header = new List<string>();
                var hashtable = new HashSet<string>();
                // create the join
                using (var db = new ToolsContext())
                {
                    db.Configuration.AutoDetectChangesEnabled = false;
                    db.Configuration.ValidateOnSaveEnabled = false;
                    db.Database.CommandTimeout = 180;

                    //var input = (from i in db.PointOfInterestAdWordsLocations
                    //             join l in db.AdWordsLocations on i.AdWordsLocationId equals l.Id
                    //             join s in db.StoreDatas on i.PoiId equals s.GmbStoreId
                    //             where i.AdWordsLocationProjectId == projectId
                    //             select new
                    //             {
                    //                 i.AdWordsLocationId,
                    //                 l.CanonicalName,
                    //                 s.LocationName,
                    //                 s.StoreName20,
                    //                 s.StoreName25,
                    //                 s.StoreName30
                    //             });

                    var input = db.Database.SqlQuery<GenericLocation>(@"select a.CanonicalName as 'TargetLocation', s.LocationName, s.StoreName20, s.StoreName25, s.StoreName30
from PointOfInterestAdWordsLocation p join AdWordsLocation a on p.AdWordsLocationId = a.Id
join StoreData s on p.PoiId = s.GmbStoreId
where p.AdWordsLocationProjectId = " + projectId);


                    foreach (var r in input)
                    {
                        var lac = new GenericLocationBusinessDataItem();
                        // set header once
                        if (header.Count == 0) header = lac.Header;
                        // construct targetLocation
                        lac.TargetLocation = r.TargetLocation.Trim();
                        lac.TargetLocationRestriction = "";
                        lac.LocationName = r.LocationName;
                        lac.StoreName20 = r.StoreName20;
                        lac.StoreName25 = r.StoreName25;
                        lac.StoreName30 = r.StoreName30;

                        // avoid duplicate entries
                        if (!hashtable.Contains(lac.CustomId))
                        {
                            hashtable.Add(lac.CustomId);
                            items.Add(lac.Row);
                        }
                    }
                }
                // store feed
                AddToFile(items, localFilePath, header, false);
            }

            private void AddToFile(List<List<string>> data, string fileLocation, List<string> headers, bool append)
            {
                try
                {
                    if (headers == null || headers.Count == 0)
                    {
                        return;
                    }
                    if (append)
                    {
                        if (!File.Exists(fileLocation))
                        {
                            using (FileStream aFile = new FileStream(fileLocation, FileMode.Append, FileAccess.Write))
                            using (StreamWriter sw = new StreamWriter(aFile))
                            {
                                var csv = new CsvWriter(sw);
                                //csv.Configuration.QuoteAllFields = true;
                                foreach (var header in headers)
                                {
                                    csv.WriteField(header);
                                }
                                csv.NextRecord();
                                foreach (var row in data)
                                {
                                    foreach (var record in row)
                                    {
                                        csv.WriteField(record);
                                    }
                                    csv.NextRecord();
                                }
                                data = null;
                            }
                        }
                        else
                        {
                            using (FileStream aFile = new FileStream(fileLocation, FileMode.Append, FileAccess.Write))
                            using (StreamWriter sw = new StreamWriter(aFile))
                            {
                                var csv = new CsvWriter(sw);
                                //csv.Configuration.QuoteAllFields = true;
                                foreach (var row in data)
                                {
                                    foreach (var record in row)
                                    {
                                        csv.WriteField(record);
                                    }
                                    csv.NextRecord();
                                }
                                data = null;
                            }
                        }
                    }
                    else
                    {
                        using (FileStream aFile = new FileStream(fileLocation, FileMode.Create, FileAccess.Write))
                        using (StreamWriter sw = new StreamWriter(aFile))
                        {
                            var csv = new CsvWriter(sw);
                            //csv.Configuration.QuoteAllFields = true;
                            foreach (var header in headers)
                            {
                                csv.WriteField(header);
                            }
                            csv.NextRecord();
                            foreach (var row in data)
                            {
                                foreach (var record in row)
                                {
                                    csv.WriteField(record);
                                }
                                csv.NextRecord();
                            }
                            data = null;
                        }
                    }


                }
                catch (Exception ex)
                {
                    throw new Exception(ex.Message, ex.InnerException);
                }

            }
        }

        public class GenericLocation
        {
            public string TargetLocation { get; set; }
            public string LocationName { get; set; }
            public string StoreName20 { get; set; }
            public string StoreName25 { get; set; }
            public string StoreName30 { get; set; }
        }

        public class BoundingBox
        {
            private double _lat;
            private double _lng;
            private double _topLeftLat;
            private double _topLeftLng;
            private double _bottomRightLat;
            private double _bottomRightLng;
            private const double DegreesToRadians = Math.PI / 180.0;
            private const double RadiansToDegrees = 180.0 / Math.PI;
            private const double EarthRadius = 6378137.0;

            public BoundingBox(double lat, double lng, int kmRange)
            {
                _lat = lat;
                _lng = lng;
                CalculateDerivedPosition(kmRange * 1000, -45, out _topLeftLat, out _topLeftLng);
                CalculateDerivedPosition(kmRange * 1000, 135, out _topLeftLat, out _topLeftLng);
            }

            public double TopLeftLat
            {
                get
                {
                    return _topLeftLat;
                }
            }
            public double TopLeftLng { get { return _topLeftLng; } }
            public double BottomRightLat { get { return _bottomRightLat; } }
            public double BottomRightLng { get { return _bottomRightLng; } }

            public void CalculateDerivedPosition(double range, double bearing, out double latitude, out double longitude)
            {
                var latA = _lat * DegreesToRadians;
                var lonA = _lng * DegreesToRadians;
                var angularDistance = range / EarthRadius;
                var trueCourse = bearing * DegreesToRadians;

                var lat = Math.Asin(
                    Math.Sin(latA) * Math.Cos(angularDistance) +
                    Math.Cos(latA) * Math.Sin(angularDistance) * Math.Cos(trueCourse));

                var dlon = Math.Atan2(
                    Math.Sin(trueCourse) * Math.Sin(angularDistance) * Math.Cos(latA),
                    Math.Cos(angularDistance) - Math.Sin(latA) * Math.Sin(lat));

                var lon = ((lonA + dlon + Math.PI) % (Math.PI * 2)) - Math.PI;

                latitude = lat * RadiansToDegrees;
                longitude = lon * RadiansToDegrees;
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int KmRange { get; set; }
            public bool MultipleResults { get; set; }
            public OutputFormat OutputFormat { get; set; }
            public string JsArray { get; set; }
        }
    }
}
