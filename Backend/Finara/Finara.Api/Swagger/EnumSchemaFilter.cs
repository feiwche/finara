using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace Finara.Api.Swagger;

// Program.cs'te JsonStringEnumConverter yalnızca gerçek istek/yanıt serileştirmesini
// (Microsoft.AspNetCore.Http.Json.JsonOptions) etkiliyor — Swashbuckle'ın şema üretimi
// minimal API'lerde bunu otomatik okumuyor ve enum'ları ham int olarak gösteriyor. Bu
// filtre şema tarafını da string'e çeviriyor, ikisi tutarlı olsun diye.
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum)
            return;

        schema.Enum.Clear();
        schema.Type = "string";
        schema.Format = null;
        foreach (var name in Enum.GetNames(context.Type))
            schema.Enum.Add(new OpenApiString(name));
    }
}
