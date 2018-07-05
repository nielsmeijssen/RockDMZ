using CsvHelper;
using FluentValidation;
using MediatR;
using RockDMZ.Infrastructure;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace RockDMZ.Features.ApiDatatable
{
    public class EditCsvMetrics
    {
        public class Query : IRequest<Result>
        {
            public int? Id { get; set; }
            public string JsonSecret { get; set; }
        }

        public class QueryValidator : AbstractValidator<Query>
        {
            public QueryValidator()
            {
                RuleFor(m => m.Id).NotNull();
            }
        }

        public class Result
        {
            public int Id { get; set; }
            public string CsvMetricsDimensions { get; set; }
            public List<ApiColumn> ApiColumns { get; set; }

            public class ApiColumn
            {
                public ApiColumn()
                {
                    Kind = "standard";
                }
                public string Id { get; set; }
                public int ColumnIndex { get; set; }
                public string uiName { get; set; }
                //public string Description { get; set; }
                //public string Group { get; set; }
                //public string Type { get; set; }
                //public string Status { get; set; }
                //public bool AllowedInSegments { get; set; }
                //public string Calculation { get; set; }
                public bool Selected { get; set; }
                public string Kind { get; set; }
            }

            //public class CustomApiColumn : ApiColumn
            //{
            //    public CustomApiColumn()
            //    {
            //        Kind = "customdimension";
            //    }

            //    public bool? Active { get; set; }
            //    public DateTime? Created { get; set; }
            //    public DateTime? Updated { get; set; }
            //    public string Scope { get; set; }
            //}
        }

        public class QueryHandler : IAsyncRequestHandler<Query, Result>
        {
            private readonly ToolsContext _db;

            public QueryHandler(ToolsContext db)
            {
                _db = db;
            }

            public async Task<Result> Handle(Query message)
            {
                var dt = _db.ApiDatatables.Include("ServiceAccount").SingleOrDefault(c => c.Id == message.Id);
                var sa = dt.ServiceAccount;
                // var jsonSecret = message.JsonSecret;

                var result = new Result();
                result.Id = dt.Id;
                result.CsvMetricsDimensions = dt.ApiQuery;
                var bccLocation = sa.ServiceLocation;
                result.ApiColumns = GetPublicCsvApiColumns(bccLocation, ',', dt.ApiQuery);
                

                return result;
            }

            private List<Result.ApiColumn> GetPublicCsvApiColumns(string fileLocation, char separator, string apiQuery)
            {
                var apiColumns = new List<Result.ApiColumn>();
                var metricIds = apiQuery?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                // get file
                WebClient client = new WebClient();
                Stream stream = client.OpenRead(fileLocation);
                StreamReader reader = new StreamReader(stream);
                // read file in csv
                using (var csv = new CsvReader(reader)) { 
                    // get column names in first line
                    // csv.ReadHeader(); // not necessary
                    var headers = csv.Parser.Read();
                    // for each column header
                    var counter = 0;
                    foreach (var c in headers)
                    {
                        var a = new Result.ApiColumn();
                        a.uiName = c;
                        a.Id = c;
                        a.ColumnIndex = counter;
                        a.Selected = (metricIds != null && metricIds.Contains(a.Id));
                        apiColumns.Add(a);
                        counter++;
                    }
                }
                return apiColumns;
            }
        }

        public class Command : IRequest
        {
            public int Id { get; set; }
            public List<string> ColumnId { get; set; }
            [Display(Name = "Metrics and dimensions separated by comma's")]
            public string CsvMetricsDimensions { get; set; }
        }

        public class CommandValidator : AbstractValidator<Command>
        {
            public CommandValidator()
            {
                RuleFor(m => m.Id).NotNull();
                RuleFor(m => m.ColumnId).NotEmpty().Unless(m => !String.IsNullOrEmpty(m.CsvMetricsDimensions));
                // RuleFor(m => m.ColumnId).Must(m => m.Exists(x => x.StartsWith("m|"))).Unless(m => m.CsvMetricsDimensions != null && m.CsvMetricsDimensions.Contains("m|"));
            }
        }

        public class CommandHandler : IAsyncRequestHandler<Command>
        {
            private readonly ToolsContext _db;

            public CommandHandler(ToolsContext db)
            {
                _db = db;
            }

            public async Task Handle(Command message)
            {
                var apiDatatable = await _db.ApiDatatables.FindAsync(message.Id);

                var newColumns = message.ColumnId.Count == 0 ?
                    (message.CsvMetricsDimensions.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)).ToList() :
                    message.ColumnId;

                var columnsChanged = false;

                var currentColumns = apiDatatable.ApiQuery?.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

                if (currentColumns != null && currentColumns.Length == newColumns.Count)
                {
                    foreach (var v in currentColumns)
                    {
                        if (!newColumns.Contains(v))
                        {
                            columnsChanged = true; // what to do when columns change?
                            break;
                        }
                    }
                }
                else
                {
                    columnsChanged = true;
                    // apiDatatable.LastDateDownloaded = null; => what to do when columns change?
                }


                apiDatatable.ApiQuery = String.Join(",", newColumns);

                _db.SaveChanges();

                // if (columnsChanged && File.Exists(apiDatatable.LocalFilePath)) File.Delete(apiDatatable.LocalFilePath);
            }
        }
    }
}
