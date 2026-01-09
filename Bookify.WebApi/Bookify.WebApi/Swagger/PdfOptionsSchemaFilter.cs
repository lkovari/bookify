using Bookify.Core.Models;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Bookify.WebApi.Swagger;

public sealed class PdfOptionsSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (context.Type == typeof(BookRequest))
        {
            ApplyBookRequestDefaults(schema);
        }
        else if (context.Type == typeof(PdfOptions))
        {
            ApplyPdfOptionsDefaults(schema);
        }
        else if (context.Type == typeof(PdfMargins))
        {
            ApplyPdfMarginsDefaults(schema);
        }
    }

    private static void ApplyBookRequestDefaults(OpenApiSchema schema)
    {
        if (schema.Properties.TryGetValue("url", out var urlProperty))
        {
            urlProperty.Example = new OpenApiString("https://example.com/docs");
        }

        if (schema.Properties.TryGetValue("title", out var titleProperty))
        {
            titleProperty.Example = new OpenApiString("Documentation Book");
        }

        if (schema.Properties.TryGetValue("maximumDepth", out var maximumDepthProperty))
        {
            maximumDepthProperty.Default = new OpenApiInteger(0);
            maximumDepthProperty.Example = new OpenApiInteger(0);
        }

        if (schema.Properties.TryGetValue("processExternalUrls", out var processExternalUrlsProperty))
        {
            processExternalUrlsProperty.Default = new OpenApiBoolean(false);
            processExternalUrlsProperty.Example = new OpenApiBoolean(false);
        }
    }

    private static void ApplyPdfOptionsDefaults(OpenApiSchema schema)
    {
        if (schema.Properties.TryGetValue("format", out var formatProperty))
        {
            formatProperty.Default = new OpenApiString("A4");
            formatProperty.Example = new OpenApiString("A4");
        }

        if (schema.Properties.TryGetValue("printBackground", out var printBackgroundProperty))
        {
            printBackgroundProperty.Default = new OpenApiBoolean(true);
            printBackgroundProperty.Example = new OpenApiBoolean(true);
        }

        if (schema.Properties.TryGetValue("displayHeaderFooter", out var displayHeaderFooterProperty))
        {
            displayHeaderFooterProperty.Default = new OpenApiBoolean(false);
            displayHeaderFooterProperty.Example = new OpenApiBoolean(false);
        }

        if (schema.Properties.TryGetValue("margin", out var marginProperty))
        {
            marginProperty.Example = new OpenApiObject
            {
                ["top"] = new OpenApiString("1cm"),
                ["right"] = new OpenApiString("1cm"),
                ["bottom"] = new OpenApiString("1cm"),
                ["left"] = new OpenApiString("1cm")
            };
        }

        if (schema.Properties.TryGetValue("scale", out var scaleProperty))
        {
            scaleProperty.Example = new OpenApiString("1.0");
        }

        if (schema.Properties.TryGetValue("headerTemplate", out var headerTemplateProperty))
        {
            headerTemplateProperty.Example = new OpenApiString("");
        }

        if (schema.Properties.TryGetValue("footerTemplate", out var footerTemplateProperty))
        {
            footerTemplateProperty.Example = new OpenApiString("");
        }
    }

    private static void ApplyPdfMarginsDefaults(OpenApiSchema schema)
    {
        if (schema.Properties.TryGetValue("top", out var topProperty))
        {
            topProperty.Default = new OpenApiString("1cm");
            topProperty.Example = new OpenApiString("1cm");
        }

        if (schema.Properties.TryGetValue("right", out var rightProperty))
        {
            rightProperty.Default = new OpenApiString("1cm");
            rightProperty.Example = new OpenApiString("1cm");
        }

        if (schema.Properties.TryGetValue("bottom", out var bottomProperty))
        {
            bottomProperty.Default = new OpenApiString("1cm");
            bottomProperty.Example = new OpenApiString("1cm");
        }

        if (schema.Properties.TryGetValue("left", out var leftProperty))
        {
            leftProperty.Default = new OpenApiString("1cm");
            leftProperty.Example = new OpenApiString("1cm");
        }
    }
}

