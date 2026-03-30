using CartolaOptimization.Application.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace CartolaOptimization.Api.Swagger;

/// <summary>
/// Supplies a helpful default Swagger example for lineup optimization requests without changing runtime defaults.
/// </summary>
public sealed class PredictedLineupRequestSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type != typeof(PredictedLineupRequest))
        {
            return;
        }

        schema.Example = new OpenApiObject
        {
            ["budget"] = new OpenApiDouble(100d),
            ["formation"] = new OpenApiArray
            {
                new OpenApiObject
                {
                    ["positionCartolaId"] = new OpenApiInteger(1),
                    ["quantity"] = new OpenApiInteger(1),
                },
                new OpenApiObject
                {
                    ["positionCartolaId"] = new OpenApiInteger(2),
                    ["quantity"] = new OpenApiInteger(2),
                },
                new OpenApiObject
                {
                    ["positionCartolaId"] = new OpenApiInteger(3),
                    ["quantity"] = new OpenApiInteger(2),
                },
                new OpenApiObject
                {
                    ["positionCartolaId"] = new OpenApiInteger(4),
                    ["quantity"] = new OpenApiInteger(3),
                },
                new OpenApiObject
                {
                    ["positionCartolaId"] = new OpenApiInteger(5),
                    ["quantity"] = new OpenApiInteger(3),
                },
                new OpenApiObject
                {
                    ["positionCartolaId"] = new OpenApiInteger(6),
                    ["quantity"] = new OpenApiInteger(1),
                },
            },
        };
    }
}
